using Microsoft.CodeAnalysis;

namespace EntitasSourceGen.Generator;

internal static class Diagnostics
{
    private const string Category = "EntitasSourceGen";

    public static readonly DiagnosticDescriptor ComponentWithoutContext = new(
        "ECG001", "Component has no context",
        "Component '{0}' implements IComponent but has no context attribute; no code is generated for it",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateComponentName = new(
        "ECG002", "Duplicate component name in context",
        "Components '{0}' and '{1}' both map to name '{2}' in context '{3}'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedComponentShape = new(
        "ECG003", "Unsupported component declaration",
        "Component '{0}' must be a public, non-generic, top-level class whose fields use public types",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UniqueInMultipleContexts = new(
        "ECG004", "Unique component in multiple contexts",
        "Component '{0}' is [Unique] but belongs to several contexts ({1}); use a single context",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ComponentWithoutMarker = new(
        "ECG005", "Component in assembly without marker",
        "Component '{0}' has a context attribute, but this assembly has no [assembly: EntitasSourceGen] marker; no code is generated",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GeneratedNameConflict = new(
        "ECG006", "Generated name conflict",
        "Generated name '{0}' for component '{1}' conflicts with {2}; it is not generated",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoContexts = new(
        "ECG007", "No contexts",
        "Assembly has the [assembly: EntitasSourceGen] marker but declares no context; derive an attribute from EntitasSourceGen.ContextAttribute",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousIndex = new(
        "ECG008", "Conflicting index attributes",
        "Member '{1}' of component '{0}' has both [PrimaryEntityIndex] and [EntityIndex]; [PrimaryEntityIndex] is used",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IgnoredIndex = new(
        "ECG009", "Index attribute ignored",
        "Member '{1}' of component '{0}' has an index attribute but is not a public instance field or get/set property; no index is generated",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IgnoredAttribute = new(
        "ECG010", "Attribute ignored",
        "{1} on component '{0}' is ignored: {2}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}