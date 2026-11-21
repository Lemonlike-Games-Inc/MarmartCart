using System;
using UnityEngine;

/// <summary>
/// Every item in the game, in one asset. The projectile pool prewarms every
/// thrown item listed here.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Item Library",
    fileName = "Item Library"
)]
public sealed class ItemLibrary : ScriptableObject
{
    [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

    public int ItemCount => items != null ? items.Length : 0;

    public ItemDefinition GetItem(int index)
    {
        return items != null && index >= 0 && index < items.Length
            ? items[index]
            : null;
    }

    private void OnValidate()
    {
        if (items == null) items = Array.Empty<ItemDefinition>();
    }
}
