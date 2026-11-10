using System;

namespace EntitasSourceGen
{
    /// <summary>Replaces the <c>is</c> prefix of a flag component: <c>[FlagPrefix("flag")]</c> → <c>entity.flagX</c>.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FlagPrefixAttribute : Attribute
    {
        public FlagPrefixAttribute(string prefix)
        {
            Prefix = prefix;
        }

        public string Prefix { get; }
    }

    /// <summary>
    /// No entity / context API for this component. With <paramref name="generateIndex"/> = true (default) it still
    /// gets a component index and a matcher; with false it is ignored completely.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DontGenerateAttribute : Attribute
    {
        public DontGenerateAttribute(bool generateIndex = true)
        {
            GenerateIndex = generateIndex;
        }

        public bool GenerateIndex { get; }
    }
}
