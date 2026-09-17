using UnityEngine;
using UnityEngine.Serialization;

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
    [Header("Power-up Icons")]
    [SerializeField] private Sprite tomatoIcon;
    [SerializeField] private Sprite iceCubeIcon;
    [SerializeField] private Sprite colaMentosIcon;
    [SerializeField] private Sprite strongFanIcon;

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
