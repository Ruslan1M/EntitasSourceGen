using System;

namespace EntitasSourceGen
{
    /// <summary>At most one entity in the context has this component; API is generated on the context.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UniqueAttribute : Attribute
    {
    }
}
