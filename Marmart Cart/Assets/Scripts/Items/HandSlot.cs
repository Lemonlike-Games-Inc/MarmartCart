using System;
using UnityEngine;

/// <summary>
/// One item slot of a PlayerItemHand. Runtime state only.
/// A source (a map Powerup pickup) loads it; the hand spends it.
/// </summary>
[Serializable]
public sealed class HandSlot
{
    [SerializeField] private bool loaded;
    [SerializeField] private ItemDefinition item;

    [Tooltip("Game time when the current item was loaded.")]
    [SerializeField] private float loadedAtTime;

    public bool IsLoaded => loaded && item != null;
    public ItemDefinition Item => item;
    public float LoadedAtTime => loadedAtTime;

    internal void Load(ItemDefinition newItem, float time)
    {
        item = newItem;
        loaded = true;
        loadedAtTime = time;
    }

    internal void Clear()
    {
        loaded = false;
        item = null;
        loadedAtTime = 0f;
    }
}
