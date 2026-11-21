using UnityEngine;

/// <summary>
/// How an item is used: thrown along an aimed arc, used at once, and so on.
/// Definition only, never runtime state.
/// </summary>
public abstract class ItemUseDefinition : ScriptableObject
{
    /// <summary>Whether the right stick aims this item before LT uses it.</summary>
    public abstract bool RequiresAim { get; }
}
