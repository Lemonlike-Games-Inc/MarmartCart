using UnityEngine;

/// <summary>
/// What an item does (blind on hit, freeze on hit, speed boost...).
/// Definition only, never runtime state. Each scene effect system handles the
/// items that carry its effect type, so items can share and combine effects.
/// </summary>
public abstract class ItemEffectDefinition : ScriptableObject
{
}
