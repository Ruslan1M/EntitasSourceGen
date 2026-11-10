using System;

namespace EntitasSourceGen
{
    public enum EventTarget
    {
        /// <summary>Listeners on any entity are notified (<c>IAnyXListener</c>).</summary>
        Any,

        /// <summary>Only listeners on the changed entity itself are notified (<c>IXListener</c>).</summary>
        Self,
    }

    public enum EventType
    {
        Added,
        Removed,
    }

    /// <summary>
    /// Generates a listener interface, a listener component, an event system and
    /// <c>{Context}EventSystems : Feature</c>. Can be applied several times (e.g. Added and Removed).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EventAttribute : Attribute
    {
        public EventAttribute(EventTarget eventTarget, EventType eventType = EventType.Added, int priority = 0)
        {
            EventTarget = eventTarget;
            EventType = eventType;
            Priority = priority;
        }

        public EventTarget EventTarget { get; }
        public EventType EventType { get; }

        /// <summary>Lower runs first inside <c>{Context}EventSystems</c>.</summary>
        public int Priority { get; }
    }
}
