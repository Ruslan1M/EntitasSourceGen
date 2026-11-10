using System;

namespace EntitasSourceGen
{
    /// <summary>
    /// Enables Entitas code generation in the assembly that declares it.
    /// Assemblies without this marker get no generated code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class EntitasSourceGenAttribute : Attribute
    {
        /// <summary>Namespace for generated types. Null or empty = global namespace (Jenny-compatible).</summary>
        public string Namespace { get; set; }

        /// <summary>Generate <c>Contexts.sharedInstance</c>. Off by default.</summary>
        public bool Singleton { get; set; }
    }
}
