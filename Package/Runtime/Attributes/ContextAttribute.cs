using System;

namespace EntitasSourceGen
{
    /// <summary>
    /// Base for context attributes. Declare one per context in the consumer assembly:
    /// <c>public sealed class GameAttribute : ContextAttribute { }</c>.
    /// The context name is the class name without the <c>Attribute</c> suffix.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public abstract class ContextAttribute : Attribute
    {
    }
}
