using System;

namespace EntitasSourceGen
{
    /// <summary>
    /// Unique key: generates <c>context.GetEntityWithX(key)</c>. Adding a second entity with the same key throws.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PrimaryEntityIndexAttribute : Attribute
    {
    }

    /// <summary>Non-unique key: generates <c>context.GetEntitiesWithX(key)</c>.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EntityIndexAttribute : Attribute
    {
    }
}
