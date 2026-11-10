using System;

namespace EntitasSourceGen
{
    /// <summary>
    /// Unique key: generates <c>context.GetEntityWithX(key)</c>. Adding a second entity with the same key throws.
    /// With a name, members that share it (in one or several components) form one composite key:
    /// <c>context.GetEntityWithName(a, b)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PrimaryEntityIndexAttribute : Attribute
    {
        public PrimaryEntityIndexAttribute() { }

        /// <param name="name">Index name shared by all members of a composite key.</param>
        public PrimaryEntityIndexAttribute(string name) { Name = name; }

        public string Name { get; }
    }

    /// <summary>
    /// Non-unique key: generates <c>context.GetEntitiesWithX(key)</c>.
    /// With a name, members that share it (in one or several components) form one composite key:
    /// <c>context.GetEntitiesWithName(a, b)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EntityIndexAttribute : Attribute
    {
        public EntityIndexAttribute() { }

        /// <param name="name">Index name shared by all members of a composite key.</param>
        public EntityIndexAttribute(string name) { Name = name; }

        public string Name { get; }
    }
}
