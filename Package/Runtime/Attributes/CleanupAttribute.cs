using System;

namespace EntitasSourceGen
{
    public enum CleanupMode
    {
        /// <summary>Removes the component from every entity that has it.</summary>
        RemoveComponent,

        /// <summary>Destroys every entity that has the component.</summary>
        DestroyEntity,
    }

    /// <summary>
    /// Generates <c>RemoveX{Context}System</c> / <c>DestroyX{Context}System</c> and adds it to
    /// <c>{Context}CleanupSystems : Feature</c> (run via <c>Systems.Cleanup()</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CleanupAttribute : Attribute
    {
        public CleanupAttribute(CleanupMode cleanupMode)
        {
            CleanupMode = cleanupMode;
        }

        public CleanupMode CleanupMode { get; }
    }
}
