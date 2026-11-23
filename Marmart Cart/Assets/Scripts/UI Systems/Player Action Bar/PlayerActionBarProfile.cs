using System;
using UnityEngine;

/// <summary>
/// Semantic prompt graphics available to every item. The trigger entry
/// describes the action instead of a physical LT/RT side, so its assigned
/// Sprite can follow the project's current input binding.
/// </summary>
public enum PowerupHudPromptIcon
{
    None = 0,
    RightJoystick = 1,
    ActivateTrigger = 2,
    Plus = 3
}

[Serializable]
public struct PowerupHudPromptRecipe
{
    [SerializeField] private PowerupHudPromptIcon slot1;
    [SerializeField] private PowerupHudPromptIcon slot2;
    [SerializeField] private PowerupHudPromptIcon slot3;

    public PowerupHudPromptRecipe(
        PowerupHudPromptIcon slot1,
        PowerupHudPromptIcon slot2,
        PowerupHudPromptIcon slot3)
    {
        this.slot1 = slot1;
        this.slot2 = slot2;
        this.slot3 = slot3;
    }

    public PowerupHudPromptIcon GetSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => slot1,
            1 => slot2,
            2 => slot3,
            _ => PowerupHudPromptIcon.None
        };
    }
}

[Serializable]
public struct ItemHudEntry
{
    [SerializeField] private ItemDefinition item;
    [SerializeField] private Sprite icon;

    [Tooltip("Slot order is 1, 2, 3. None leaves that slot hidden.")]
    [SerializeField] private PowerupHudPromptRecipe prompts;

    public ItemDefinition Item => item;
    public Sprite Icon => icon;
    public PowerupHudPromptRecipe Prompts => prompts;
}

/// <summary>
/// The one place to set up the Player Action Bar data: which bar prefab each
/// player gets, the icon and input prompts of every item (map powerups and
/// Ability items alike), and where the bar sits in 2P and 4P. The look and
/// placement inside the bar live in its prefabs: Player Action Bar Base and one
/// variant per player. Style only; nothing writes here at runtime.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/UI/Player Action Bar Profile",
    fileName = "PlayerActionBarProfile"
)]
public class PlayerActionBarProfile : ScriptableObject
{
    public const int PromptSlotCount = 3;

    [Header("Bar Prefabs")]
    [Tooltip("Player 1's variant of Player Action Bar Base.")]
    [SerializeField] private PlayerActionBarView player1BarPrefab;
    [Tooltip("Player 2's variant of Player Action Bar Base.")]
    [SerializeField] private PlayerActionBarView player2BarPrefab;
    [Tooltip("Player 3's variant of Player Action Bar Base.")]
    [SerializeField] private PlayerActionBarView player3BarPrefab;
    [Tooltip("Player 4's variant of Player Action Bar Base.")]
    [SerializeField] private PlayerActionBarView player4BarPrefab;

    [Header("Items")]
    [Tooltip("Icon and input prompts for each item the bar can show, from the map or an Ability.")]
    [SerializeField] private ItemHudEntry[] itemEntries = Array.Empty<ItemHudEntry>();

    [Header("Input Prompt Sprites")]
    [SerializeField] private Sprite rightJoystickPromptIcon;

    [Tooltip(
        "Prompt for the Activate Power-up action. Assign the LT or RT Sprite " +
        "that matches the project's current binding."
    )]
    [SerializeField] private Sprite activateTriggerPromptIcon;

    [SerializeField] private Sprite plusPromptIcon;

    [Header("2 Player Layout")]
    [Tooltip("Normalized anchor inside each player's own viewport. The bar's bottom centre sits here.")]
    [SerializeField] private Vector2 twoPlayerAnchor = new Vector2(0.5f, 0.08f);

    [Tooltip("Offset of the bar's bottom centre from the anchor, in 1920x1080 pixels.")]
    [SerializeField] private Vector2 twoPlayerOffsetPixels = Vector2.zero;

    [Tooltip("Scale of the whole bar as authored in the prefab.")]
    [Min(0.1f)]
    [SerializeField] private float twoPlayerScale = 1f;

    [Header("4 Player Layout")]
    [Tooltip("Normalized anchor inside each player's own viewport. The bar's bottom centre sits here.")]
    [SerializeField] private Vector2 fourPlayerAnchor = new Vector2(0.5f, 0.08f);

    [Tooltip("Offset of the bar's bottom centre from the anchor, in 1920x1080 pixels.")]
    [SerializeField] private Vector2 fourPlayerOffsetPixels = Vector2.zero;

    [Tooltip("Scale of the whole bar as authored in the prefab.")]
    [Min(0.1f)]
    [SerializeField] private float fourPlayerScale = 0.7f;

    public PlayerActionBarView GetBarPrefab(int playerIndex)
    {
        return playerIndex switch
        {
            1 => player1BarPrefab,
            2 => player2BarPrefab,
            3 => player3BarPrefab,
            4 => player4BarPrefab,
            _ => null
        };
    }

    public Sprite GetIcon(ItemDefinition item)
    {
        return TryGetItemEntry(item, out ItemHudEntry entry) ? entry.Icon : null;
    }

    public Sprite GetPromptSprite(ItemDefinition item, int promptSlot)
    {
        if (!TryGetItemEntry(item, out ItemHudEntry entry)) return null;

        return entry.Prompts.GetSlot(promptSlot) switch
        {
            PowerupHudPromptIcon.RightJoystick => rightJoystickPromptIcon,
            PowerupHudPromptIcon.ActivateTrigger => activateTriggerPromptIcon,
            PowerupHudPromptIcon.Plus => plusPromptIcon,
            _ => null
        };
    }

    public void GetLayout(
        int activePlayerCount,
        out Vector2 anchor,
        out Vector2 offsetPixels,
        out float scale)
    {
        bool fourPlayerLayout = activePlayerCount > 2;

        anchor = fourPlayerLayout ? fourPlayerAnchor : twoPlayerAnchor;
        offsetPixels = fourPlayerLayout ? fourPlayerOffsetPixels : twoPlayerOffsetPixels;
        scale = fourPlayerLayout ? fourPlayerScale : twoPlayerScale;
    }

    private bool TryGetItemEntry(ItemDefinition item, out ItemHudEntry entry)
    {
        if (item != null && itemEntries != null)
        {
            for (int i = 0; i < itemEntries.Length; i++)
            {
                if (itemEntries[i].Item != item) continue;

                entry = itemEntries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    private void OnValidate()
    {
        twoPlayerAnchor = Clamp01(twoPlayerAnchor);
        fourPlayerAnchor = Clamp01(fourPlayerAnchor);
    }

    private static Vector2 Clamp01(Vector2 value)
    {
        return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }
}
