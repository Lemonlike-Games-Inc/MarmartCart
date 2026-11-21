using System;
using UnityEngine;

/// <summary>
/// An item a player can hold and use. Map Powerups and Ability Samples both
/// give items. Built from one use part (how it is used) and effect parts (what
/// it does). A variant is its own asset that points to the same parts as its
/// base item and adds or swaps one. Definition only, never runtime state.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Item",
    fileName = "Item"
)]
public sealed class ItemDefinition : ScriptableObject
{
    [Tooltip("Player-facing name. Empty uses the asset name.")]
    [SerializeField] private string displayName;

    [SerializeField] private ItemUseDefinition use;

    [SerializeField] private ItemEffectDefinition[] effects = Array.Empty<ItemEffectDefinition>();

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public ItemUseDefinition Use => use;
    public bool RequiresAim => use != null && use.RequiresAim;
    public int EffectCount => effects != null ? effects.Length : 0;

    public ItemEffectDefinition GetEffect(int index)
    {
        return effects != null && index >= 0 && index < effects.Length
            ? effects[index]
            : null;
    }

    public bool TryGetUse<T>(out T typedUse) where T : ItemUseDefinition
    {
        typedUse = use as T;
        return typedUse != null;
    }

    /// <summary>The first effect part of type T, if this item has one.</summary>
    public bool TryGetEffect<T>(out T effect) where T : ItemEffectDefinition
    {
        if (effects != null)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] is T match)
                {
                    effect = match;
                    return true;
                }
            }
        }

        effect = null;
        return false;
    }

    public bool HasEffect<T>() where T : ItemEffectDefinition
    {
        return TryGetEffect(out T _);
    }

    private void OnValidate()
    {
        if (effects == null) effects = Array.Empty<ItemEffectDefinition>();
    }
}
