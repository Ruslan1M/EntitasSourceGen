using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace EntitasSourceGen.Generator.Model;

internal enum IndexKind
{
    None,
    Entity,
    Primary,
}

/// <summary>
/// Public instance field or settable property of a component. Primitives only (cache-friendly).
/// <see cref="IndexName"/> is set for named (composite) indices: <c>[EntityIndex("Name")]</c>.
/// </summary>
internal sealed record FieldModel(string Name, string Type, IndexKind Index = IndexKind.None, string? IndexName = null);

internal enum CleanupKind
{
    None,
    RemoveComponent,
    DestroyEntity,
}

/// <summary>One <c>[Event(...)]</c> on a component.</summary>
internal sealed record EventModel(bool IsAny, bool IsRemoved, int Priority);

/// <summary>
/// Everything the emitters need about one component. Strings, bools and EquatableArray only:
/// no ISymbol / SyntaxNode / Location, otherwise incremental caching breaks.
/// </summary>
internal sealed record ComponentModel(
    string FullTypeName,
    string Name,
    EquatableArray<FieldModel> Fields,
    EquatableArray<string> Contexts,
    bool IsUnique,
    EquatableArray<EventModel> Events = default,
    bool IsGenerated = false,
    string FlagPrefix = "is",
    CleanupKind Cleanup = CleanupKind.None,
    bool GenerateMethods = true)
{
    public bool IsFlag => Fields.IsEmpty;

    /// <summary><c>isX</c>, or <c>{FlagPrefix}X</c> with [FlagPrefix].</summary>
    public string FlagName => FlagPrefix + Name;

    /// <summary><see cref="FlagName"/> escaped for use as an identifier (prefix + name may form a keyword).</summary>
    public string FlagId => Names.Escape(FlagName);

    /// <summary>Name usable as a standalone identifier (escaped if it is a keyword).</summary>
    public string Id => Names.Escape(Name);

    /// <summary>
    /// Single member named <c>Value</c>: <c>entity.Speed</c> returns <c>entity.speed.Value</c>.
    /// Needs a PascalCase name (otherwise it would collide with the component property) that is not a built-in
    /// member of Entity / Context (<c>Destroy</c>, <c>Reset</c>…): then the shortcut is just left out.
    /// </summary>
    public bool HasValueProperty => !IsGenerated && Fields.Length == 1 && Fields[0].Name == "Value" && char.IsUpper(Name[0])
                                    && !Analysis.EntityReserved.Contains(Name) && !Analysis.ContextReserved.Contains(Name);

    /// <summary>Generated <c>I{Name}Entity</c> interface for components shared by several contexts.</summary>
    public string InterfaceName => "I" + Name + "Entity";
}

/// <summary>Equatable location data, turned back into a Location only when reporting.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);

    public static LocationInfo From(Location location)
        => new(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
}

/// <summary>
/// Parse result of one class declaration. The emit pipeline only sees <see cref="Model"/>,
/// so moving code around (location change) never re-runs emission.
/// </summary>
internal sealed record ComponentCandidate(
    ComponentModel Model,
    bool IsUnsupported,
    LocationInfo Location,
    EquatableArray<string> IgnoredIndexMembers = default,
    EquatableArray<string> AmbiguousIndexMembers = default,
    EquatableArray<string> IgnoredAttributes = default);

/// <summary>A component bound to one context with its stable index.</summary>
internal sealed record ContextComponent(ComponentModel Component, int Index);

/// <summary>
/// One generated event in a context. <see cref="Listener"/> is the generated listener component
/// (also present in <see cref="ContextModel.Components"/>).
/// </summary>
internal sealed record EventInfo(
    ComponentModel Source,
    ComponentModel Listener,
    string BaseName,
    string InterfaceRef,
    bool IsAny,
    bool IsRemoved,
    int Priority)
{
    public string InterfaceName => "I" + Listener.Name;

    /// <summary>Jenny: no context prefix in the method (overloads by entity type instead).</summary>
    public string MethodName => "On" + (IsAny ? "Any" : string.Empty) + Source.Name + (IsRemoved ? "Removed" : string.Empty);
    public string SystemName => BaseName + "EventSystem";
}

/// <summary>One member of an entity index key.</summary>
internal sealed record IndexPart(ComponentModel Component, FieldModel Field);

/// <summary>
/// Entity index in a context: one part (classic <c>[EntityIndex]</c>) or several parts sharing a name
/// (composite key, a <c>ValueTuple</c>).
/// </summary>
internal sealed record IndexModel(string Name, bool IsPrimary, EquatableArray<IndexPart> Parts)
{
    public bool IsComposite => Parts.Length > 1;
}

/// <summary>One context with its components sorted by name (ordinal).</summary>
internal sealed record ContextModel(
    string Name,
    EquatableArray<ContextComponent> Components,
    EquatableArray<EventInfo> Events = default,
    EquatableArray<ComponentModel> Cleanups = default,
    EquatableArray<IndexModel> Indices = default,
    EquatableArray<string> Interfaces = default);
