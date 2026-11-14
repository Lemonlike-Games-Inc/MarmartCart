using System;

namespace LightSide
{
    /// <summary>
    /// Marks a state property as continuously drivable, so a motion system may move it between two values. An
    /// optional method name replaces the generated <c>&lt;Member&gt;To</c>; the declaring assembly must generate
    /// state accessors for the member to be reachable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class AnimatableAttribute : Attribute
    {
        /// <summary>Name of the generated extension method, or null for the default.</summary>
        public string MethodName { get; }

        /// <summary>One noun phrase saying what the member is; the member's display name when absent.</summary>
        public string Description { get; set; }

        public AnimatableAttribute(string methodName = null) => MethodName = methodName;
    }
}
