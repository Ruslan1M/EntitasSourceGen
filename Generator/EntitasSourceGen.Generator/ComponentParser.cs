using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EntitasSourceGen.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntitasSourceGen.Generator;

/// <summary>Semantic transforms: syntax → cache-friendly models.</summary>
internal static class ComponentParser
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
        => node is ClassDeclarationSyntax { BaseList: not null } or RecordDeclarationSyntax { BaseList: not null };

    /// <summary>Returns a candidate if the class implements <c>Entitas.IComponent</c>, otherwise null.</summary>
    public static ComponentCandidate? ParseComponent(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var syntax = (TypeDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(syntax, ct) is not INamedTypeSymbol { TypeKind: TypeKind.Class } type) return null;
        if (type.IsAbstract || type.IsStatic) return null;
        if (!ImplementsComponent(type)) return null;

        var contexts = new List<string>();
        var events = new List<EventModel>();
        var isUnique = false;
        var flagPrefix = "is";
        var cleanup = CleanupKind.None;
        var generateMethods = true;
        var ignoredAttributes = new List<string>();
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null) continue;
            var attrName = attrClass.ToDisplayString();
            var args = attribute.ConstructorArguments;
            if (attrName == Names.UniqueAttribute) isUnique = true;
            else if (attrName == Names.EventAttribute) events.Add(ParseEvent(attribute));
            else if (attrName == Names.CleanupAttribute)
                cleanup = args.Length > 0 && args[0].Value is 1 ? CleanupKind.DestroyEntity : CleanupKind.RemoveComponent;
            else if (attrName == Names.FlagPrefixAttribute)
            {
                if (args.Length > 0 && args[0].Value is string p && p.Length > 0 && SyntaxFacts.IsValidIdentifier(p)) flagPrefix = p;
                else ignoredAttributes.Add("[FlagPrefix]|the prefix must be a valid C# identifier; 'is' is used");
            }
            else if (attrName == Names.DontGenerateAttribute)
            {
                // generateIndex = false: the component does not exist for the generator.
                if (args.Length > 0 && args[0].Value is false) return null;
                generateMethods = false;
            }
            else if (IsContextAttribute(attrClass)) contexts.Add(Names.ContextName(attrClass.Name));
        }

        var members = CollectMembers(type, out var membersSupported, out var ignored, out var ambiguous, ignoredAttributes);

        if (!generateMethods)
        {
            const string reason = "[DontGenerate] components get no API";
            if (isUnique) ignoredAttributes.Add("[Unique]|" + reason);
            if (events.Count > 0) ignoredAttributes.Add("[Event]|" + reason);
            if (cleanup != CleanupKind.None) ignoredAttributes.Add("[Cleanup]|" + reason);
            foreach (var m in members.Where(m => m.Index != IndexKind.None).Select(m => m.Name).Concat(ambiguous).Distinct())
                ignoredAttributes.Add($"Index attribute on '{m}'|" + reason);
            ambiguous.Clear();
            ignored.Clear();
        }

        var unsupported = !membersSupported
                          || type.ContainingType is not null
                          || type.IsGenericType
                          || type.DeclaredAccessibility != Accessibility.Public;

        // Same target + type twice: keep the lowest priority, Jenny-style.
        var distinctEvents = events
            .GroupBy(e => (e.IsAny, e.IsRemoved))
            .Select(g => g.OrderBy(e => e.Priority).First())
            .OrderBy(e => e.IsAny).ThenBy(e => e.IsRemoved)
            .ToEquatableArray();

        var model = new ComponentModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Names.ComponentName(type.Name),
            members,
            contexts.Distinct().OrderBy(c => c, System.StringComparer.Ordinal).ToEquatableArray(),
            isUnique,
            generateMethods ? distinctEvents : EquatableArray<EventModel>.Empty,
            FlagPrefix: flagPrefix,
            Cleanup: generateMethods ? cleanup : CleanupKind.None,
            GenerateMethods: generateMethods);

        return new ComponentCandidate(model, unsupported, LocationInfo.From(syntax.Identifier.GetLocation()),
            ignored.ToEquatableArray(), ambiguous.ToEquatableArray(), ignoredAttributes.ToEquatableArray());
    }

    private static EventModel ParseEvent(AttributeData attribute)
    {
        var args = attribute.ConstructorArguments;
        // EventTarget { Any = 0, Self = 1 }, EventType { Added = 0, Removed = 1 }.
        var target = args.Length > 0 && args[0].Value is int t ? t : 0;
        var eventType = args.Length > 1 && args[1].Value is int et ? et : 0;
        var priority = args.Length > 2 && args[2].Value is int p ? p : 0;
        return new EventModel(target == 0, eventType == 1, priority);
    }

    /// <summary>
    /// Public instance fields and get/set properties, base class first (Jenny 1.14 parity).
    /// Readonly fields and init-only / get-only properties are skipped: they cannot be assigned.
    /// </summary>
    private static EquatableArray<FieldModel> CollectMembers(
        INamedTypeSymbol type, out bool supported, out List<string> ignoredIndex, out List<string> ambiguousIndex,
        List<string> ignoredAttributes)
    {
        supported = true;
        ignoredIndex = new List<string>();
        ambiguousIndex = new List<string>();
        var chain = new List<INamedTypeSymbol>();
        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
            chain.Add(t);
        chain.Reverse();

        var result = new List<FieldModel>();
        var seen = new HashSet<string>();
        foreach (var t in chain)
        {
            foreach (var member in t.GetMembers())
            {
                if (member.IsImplicitlyDeclared || member is not (IFieldSymbol or IPropertySymbol)) continue;

                var index = IndexOf(member, out var both, out var indexName);
                if (both) ambiguousIndex.Add(member.Name);
                if (indexName is not null && !IsIdentifier(indexName))
                {
                    ignoredAttributes.Add($"Index name \"{indexName}\" on '{member.Name}'|it must be a valid C# identifier; the default name is used");
                    indexName = null;
                }

                ITypeSymbol? memberType = member.IsStatic || member.DeclaredAccessibility != Accessibility.Public
                    ? null
                    : member switch
                    {
                        IFieldSymbol { IsConst: false, IsReadOnly: false } f => f.Type,
                        IPropertySymbol { IsIndexer: false, GetMethod: { DeclaredAccessibility: Accessibility.Public }, SetMethod: { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false } } p => p.Type,
                        _ => null,
                    };
                if (memberType is null)
                {
                    if (index != IndexKind.None) ignoredIndex.Add(member.Name);
                    continue;
                }

                // A derived member hides / overrides the base one: keep the most derived declaration.
                if (!seen.Add(member.Name)) result.RemoveAll(m => m.Name == member.Name);

                if (!IsEffectivelyPublic(memberType)) supported = false;
                result.Add(new FieldModel(member.Name, memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), index,
                    index == IndexKind.None ? null : indexName));
            }
        }

        return result.ToEquatableArray();
    }

    /// <summary>Index attribute on the member or on any property it overrides (Roslyn does not inherit them).</summary>
    private static IndexKind IndexOf(ISymbol member, out bool both, out string? indexName)
    {
        var primary = false;
        var entity = false;
        string? primaryName = null;
        string? entityName = null;
        for (var m = member; m is not null; m = (m as IPropertySymbol)?.OverriddenProperty)
        {
            foreach (var attribute in m.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();
                var arg = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
                if (name == Names.PrimaryEntityIndexAttribute && !primary)
                {
                    primary = true;
                    primaryName = arg;
                }
                else if (name == Names.EntityIndexAttribute && !entity)
                {
                    entity = true;
                    entityName = arg;
                }
            }
        }

        both = primary && entity;
        indexName = primary ? primaryName : entityName;
        return primary ? IndexKind.Primary : entity ? IndexKind.Entity : IndexKind.None;
    }

    private static bool IsIdentifier(string name)
        => SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    private static bool IsEffectivelyPublic(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsEffectivelyPublic(array.ElementType);
            case IPointerTypeSymbol:
            case ITypeParameterSymbol:
                return false;
            case INamedTypeSymbol named:
                for (var t = named; t is not null; t = t.ContainingType)
                {
                    if (t.DeclaredAccessibility != Accessibility.Public) return false;
                }
                return named.TypeArguments.All(IsEffectivelyPublic);
            default:
                return true;
        }
    }

    /// <summary>Returns the context name if the class derives from <c>EntitasSourceGen.ContextAttribute</c>.</summary>
    public static string? ParseContextAttribute(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol type) return null;
        if (type.IsAbstract) return null;
        return IsContextAttribute(type) ? Names.ContextName(type.Name) : null;
    }

    public static bool ImplementsComponent(INamedTypeSymbol type)
        => type.AllInterfaces.Any(i => i.ToDisplayString() == Names.ComponentInterface);

    public static bool IsContextAttribute(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.ToDisplayString() == Names.ContextAttributeBase) return true;
        }
        return false;
    }
}
