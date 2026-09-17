using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Semantic prompt graphics available to every power-up recipe. The trigger
/// entry deliberately describes the action instead of a physical LT/RT side,
/// so its assigned Sprite can follow the project's current input binding.
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
public struct PowerupHudPromptSlotLayout
{
    [Tooltip("Absolute pixel offset from the selected viewport anchor.")]
    [SerializeField] private Vector2 positionPixels;

    [Tooltip("Rendered width and height for whichever prompt uses this slot.")]
    [SerializeField] private Vector2 sizePixels;

    public Vector2 PositionPixels => positionPixels;
    public Vector2 SizePixels => sizePixels;

    public PowerupHudPromptSlotLayout(
        Vector2 positionPixels,
        Vector2 sizePixels)
    {
        this.positionPixels = positionPixels;
        this.sizePixels = sizePixels;
    }

    public void ClampSize()
    {
        sizePixels = new Vector2(
            Mathf.Max(1f, sizePixels.x),
            Mathf.Max(1f, sizePixels.y)
        );
    }
}

/// <summary>
/// Designer-authored icon assets and per-viewport layout for the held power-up
/// HUD. Gameplay inventory remains entirely in PlayerPowerupController.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/HUD Profile",
    fileName = "PowerupHudProfile"
)]
public class PowerupHudProfile : ScriptableObject
{
    public const int PromptSlotCount = 3;

    [Header("Power-up Icons")]
    [SerializeField] private Sprite tomatoIcon;
    [SerializeField] private Sprite iceCubeIcon;
    [SerializeField] private Sprite colaMentosIcon;
    [SerializeField] private Sprite strongFanIcon;

    [Header("Input Prompt Sprites")]
    [SerializeField] private Sprite rightJoystickPromptIcon;

    [Tooltip(
        "Prompt for the Activate Power-up action. Assign the LT or RT Sprite " +
        "that matches the project's current binding."
    )]
    [SerializeField] private Sprite activateTriggerPromptIcon;

    [SerializeField] private Sprite plusPromptIcon;

    [Header("Power-up Prompt Recipes")]
    [Tooltip("Slot order is 1, 2, 3. None leaves that slot hidden.")]
    [SerializeField]
    private PowerupHudPromptRecipe tomatoPromptRecipe =
        new PowerupHudPromptRecipe(
            PowerupHudPromptIcon.RightJoystick,
            PowerupHudPromptIcon.Plus,
            PowerupHudPromptIcon.ActivateTrigger
        );

    [SerializeField]
    private PowerupHudPromptRecipe iceCubePromptRecipe =
        new PowerupHudPromptRecipe(
            PowerupHudPromptIcon.RightJoystick,
            PowerupHudPromptIcon.Plus,
            PowerupHudPromptIcon.ActivateTrigger
        );

    [SerializeField]
    private PowerupHudPromptRecipe colaMentosPromptRecipe =
        new PowerupHudPromptRecipe(
            PowerupHudPromptIcon.None,
            PowerupHudPromptIcon.ActivateTrigger,
            PowerupHudPromptIcon.None
        );

    [SerializeField]
    private PowerupHudPromptRecipe strongFanPromptRecipe =
        new PowerupHudPromptRecipe(
            PowerupHudPromptIcon.ActivateTrigger,
            PowerupHudPromptIcon.Plus,
            PowerupHudPromptIcon.RightJoystick
        );

    [Header("Empty Slot")]
    [Tooltip(
        "Keep the slot frame visible while no item is stored. Disable this to " +
        "hide the complete widget when inventory is empty."
    )]
    [SerializeField] private bool showSlotWhenEmpty;

    [Tooltip(
        "Optional icon shown inside the frame when inventory is empty. The " +
        "frame can still be shown when this is left empty."
    )]
    [SerializeField] private Sprite emptySlotIcon;

    [Header("Player Backgrounds")]
    [Tooltip("Optional slot/frame Sprite drawn for Player 1.")]
    [SerializeField] private Sprite player1BackgroundSprite;
    [Tooltip("Optional slot/frame Sprite drawn for Player 2.")]
    [SerializeField] private Sprite player2BackgroundSprite;
    [Tooltip("Optional slot/frame Sprite drawn for Player 3.")]
    [SerializeField] private Sprite player3BackgroundSprite;
    [Tooltip("Optional slot/frame Sprite drawn for Player 4.")]
    [SerializeField] private Sprite player4BackgroundSprite;

    [Header("Shared Appearance")]
    [SerializeField] private Color slotBackgroundTint = Color.white;
    [SerializeField] private Color iconTint = Color.white;
    [SerializeField] private bool preserveIconAspect = true;

    [Header("2 Player Layout")]
    [Tooltip("Normalized anchor inside each player's own viewport.")]
    [SerializeField]
    private Vector2 twoPlayerAnchor =
        new Vector2(0.5f, 0.08f);
    [FormerlySerializedAs("twoPlayerPositionPixels")]
    [Tooltip("Background position relative to the viewport anchor.")]
    [SerializeField]
    private Vector2 twoPlayerBackgroundPositionPixels =
        new Vector2(0f, 48f);
    [Tooltip("Power-up icon position relative to the viewport anchor.")]
    [SerializeField]
    private Vector2 twoPlayerIconPositionPixels =
        new Vector2(0f, 48f);
    [FormerlySerializedAs("twoPlayerSlotSizePixels")]
    [SerializeField]
    private Vector2 twoPlayerBackgroundSizePixels =
        new Vector2(112f, 112f);
    [SerializeField]
    private Vector2 twoPlayerIconSizePixels =
        new Vector2(88f, 88f);

    [Space]
    [SerializeField]
    private PowerupHudPromptSlotLayout twoPlayerPromptSlot1 =
        new PowerupHudPromptSlotLayout(
            new Vector2(-58f, -18f),
            new Vector2(32f, 32f)
        );
    [SerializeField]
    private PowerupHudPromptSlotLayout twoPlayerPromptSlot2 =
        new PowerupHudPromptSlotLayout(
            new Vector2(0f, -18f),
            new Vector2(32f, 32f)
        );
    [SerializeField]
    private PowerupHudPromptSlotLayout twoPlayerPromptSlot3 =
        new PowerupHudPromptSlotLayout(
            new Vector2(58f, -18f),
            new Vector2(32f, 32f)
        );

    [Header("4 Player Layout")]
    [Tooltip("Normalized anchor inside each player's own viewport.")]
    [SerializeField]
    private Vector2 fourPlayerAnchor =
        new Vector2(0.5f, 0.08f);
    [FormerlySerializedAs("fourPlayerPositionPixels")]
    [Tooltip("Background position relative to the viewport anchor.")]
    [SerializeField]
    private Vector2 fourPlayerBackgroundPositionPixels =
        new Vector2(0f, 32f);
    [Tooltip("Power-up icon position relative to the viewport anchor.")]
    [SerializeField]
    private Vector2 fourPlayerIconPositionPixels =
        new Vector2(0f, 32f);
    [FormerlySerializedAs("fourPlayerSlotSizePixels")]
    [SerializeField]
    private Vector2 fourPlayerBackgroundSizePixels =
        new Vector2(82f, 82f);
    [SerializeField]
    private Vector2 fourPlayerIconSizePixels =
        new Vector2(64f, 64f);

    [Space]
    [SerializeField]
    private PowerupHudPromptSlotLayout fourPlayerPromptSlot1 =
        new PowerupHudPromptSlotLayout(
            new Vector2(-42f, -14f),
            new Vector2(24f, 24f)
        );
    [SerializeField]
    private PowerupHudPromptSlotLayout fourPlayerPromptSlot2 =
        new PowerupHudPromptSlotLayout(
            new Vector2(0f, -14f),
            new Vector2(24f, 24f)
        );
    [SerializeField]
    private PowerupHudPromptSlotLayout fourPlayerPromptSlot3 =
        new PowerupHudPromptSlotLayout(
            new Vector2(42f, -14f),
            new Vector2(24f, 24f)
        );

    public bool ShowSlotWhenEmpty => showSlotWhenEmpty;
    public Sprite EmptySlotIcon => emptySlotIcon;
    public Color SlotBackgroundTint => slotBackgroundTint;
    public Color IconTint => iconTint;
    public bool PreserveIconAspect => preserveIconAspect;

    public Sprite GetBackgroundSprite(int playerIndex)
    {
        return playerIndex switch
        {
            1 => player1BackgroundSprite,
            2 => player2BackgroundSprite,
            3 => player3BackgroundSprite,
            4 => player4BackgroundSprite,
            _ => null
        };
    }

    public Sprite GetIcon(PowerupId powerupId)
    {
        return powerupId switch
        {
            PowerupId.Tomato => tomatoIcon,
            PowerupId.IceCube => iceCubeIcon,
            PowerupId.ColaMentos => colaMentosIcon,
            PowerupId.StrongFan => strongFanIcon,
            _ => null
        };
    }

    public Sprite GetPromptSprite(PowerupHudPromptIcon promptIcon)
    {
        return promptIcon switch
        {
            PowerupHudPromptIcon.RightJoystick => rightJoystickPromptIcon,
            PowerupHudPromptIcon.ActivateTrigger =>
                activateTriggerPromptIcon,
            PowerupHudPromptIcon.Plus => plusPromptIcon,
            _ => null
        };
    }

    public PowerupHudPromptIcon GetPromptForSlot(
        PowerupId powerupId,
        int slotIndex)
    {
        PowerupHudPromptRecipe recipe = powerupId switch
        {
            PowerupId.Tomato => tomatoPromptRecipe,
            PowerupId.IceCube => iceCubePromptRecipe,
            PowerupId.ColaMentos => colaMentosPromptRecipe,
            PowerupId.StrongFan => strongFanPromptRecipe,
            _ => default
        };

        return recipe.GetSlot(slotIndex);
    }

    public void GetLayout(
        int activePlayerCount,
        out Vector2 anchor,
        out Vector2 backgroundPositionPixels,
        out Vector2 iconPositionPixels,
        out Vector2 backgroundSizePixels,
        out Vector2 iconSizePixels)
    {
        bool fourPlayerLayout = activePlayerCount > 2;

        anchor = fourPlayerLayout
            ? fourPlayerAnchor
            : twoPlayerAnchor;
        backgroundPositionPixels = fourPlayerLayout
            ? fourPlayerBackgroundPositionPixels
            : twoPlayerBackgroundPositionPixels;
        iconPositionPixels = fourPlayerLayout
            ? fourPlayerIconPositionPixels
            : twoPlayerIconPositionPixels;
        backgroundSizePixels = fourPlayerLayout
            ? fourPlayerBackgroundSizePixels
            : twoPlayerBackgroundSizePixels;
        iconSizePixels = fourPlayerLayout
            ? fourPlayerIconSizePixels
            : twoPlayerIconSizePixels;
    }

    public void GetPromptSlotLayout(
        int activePlayerCount,
        int slotIndex,
        out Vector2 positionPixels,
        out Vector2 sizePixels)
    {
        bool fourPlayerLayout = activePlayerCount > 2;
        PowerupHudPromptSlotLayout layout = fourPlayerLayout
            ? GetFourPlayerPromptSlot(slotIndex)
            : GetTwoPlayerPromptSlot(slotIndex);

        positionPixels = layout.PositionPixels;
        sizePixels = layout.SizePixels;
    }

    private void OnValidate()
    {
        twoPlayerAnchor = Clamp01(twoPlayerAnchor);
        fourPlayerAnchor = Clamp01(fourPlayerAnchor);
        twoPlayerBackgroundSizePixels =
            ClampSize(twoPlayerBackgroundSizePixels);
        twoPlayerIconSizePixels = ClampSize(twoPlayerIconSizePixels);
        fourPlayerBackgroundSizePixels =
            ClampSize(fourPlayerBackgroundSizePixels);
        fourPlayerIconSizePixels = ClampSize(fourPlayerIconSizePixels);

        twoPlayerPromptSlot1.ClampSize();
        twoPlayerPromptSlot2.ClampSize();
        twoPlayerPromptSlot3.ClampSize();
        fourPlayerPromptSlot1.ClampSize();
        fourPlayerPromptSlot2.ClampSize();
        fourPlayerPromptSlot3.ClampSize();
    }

    private PowerupHudPromptSlotLayout GetTwoPlayerPromptSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => twoPlayerPromptSlot1,
            1 => twoPlayerPromptSlot2,
            2 => twoPlayerPromptSlot3,
            _ => default
        };
    }

    private PowerupHudPromptSlotLayout GetFourPlayerPromptSlot(int slotIndex)
    {
        return slotIndex switch
        {
            0 => fourPlayerPromptSlot1,
            1 => fourPlayerPromptSlot2,
            2 => fourPlayerPromptSlot3,
            _ => default
        };
    }

    private static Vector2 Clamp01(Vector2 value)
    {
        return new Vector2(
            Mathf.Clamp01(value.x),
            Mathf.Clamp01(value.y)
        );
    }

    private static Vector2 ClampSize(Vector2 value)
    {
        return new Vector2(
            Mathf.Max(1f, value.x),
            Mathf.Max(1f, value.y)
        );
    }
}
