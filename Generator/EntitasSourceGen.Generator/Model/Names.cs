using Microsoft.CodeAnalysis.CSharp;

namespace EntitasSourceGen.Generator.Model;

internal static class Names
{
    public const string ComponentInterface = "Entitas.IComponent";
    public const string ContextAttributeBase = "EntitasSourceGen.ContextAttribute";
    public const string UniqueAttribute = "EntitasSourceGen.UniqueAttribute";
    public const string PrimaryEntityIndexAttribute = "EntitasSourceGen.PrimaryEntityIndexAttribute";
    public const string EntityIndexAttribute = "EntitasSourceGen.EntityIndexAttribute";
    public const string EventAttribute = "EntitasSourceGen.EventAttribute";
    public const string CleanupAttribute = "EntitasSourceGen.CleanupAttribute";
    public const string FlagPrefixAttribute = "EntitasSourceGen.FlagPrefixAttribute";
    public const string DontGenerateAttribute = "EntitasSourceGen.DontGenerateAttribute";

    public static string StripSuffix(string name, string suffix)
        => name.Length > suffix.Length && name.EndsWith(suffix, System.StringComparison.Ordinal)
            ? name.Substring(0, name.Length - suffix.Length)
            : name;

    public static string ComponentName(string typeName) => StripSuffix(typeName, "Component");
    public static string ContextName(string attributeTypeName) => StripSuffix(attributeTypeName, "Attribute");

    /// <summary>camelCase identifier, escaped with @ when it collides with a C# keyword.</summary>
    public static string LowerFirst(string name)
    {
        if (name.Length == 0) return name;
        var lower = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return Escape(lower);
    }

    public static string UpperFirst(string name)
        => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    public static string Escape(string identifier)
        => SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;

    /// <summary>Parameter name for a field: <c>Value</c> → <c>newValue</c>.</summary>
    public static string NewParam(string fieldName) => "new" + UpperFirst(fieldName.TrimStart('@'));
}
