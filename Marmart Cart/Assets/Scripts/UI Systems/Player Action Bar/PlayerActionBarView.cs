using UnityEngine;

/// <summary>What one player's action bar shows. Filled by PlayerActionBarPresenter.</summary>
public struct ActionBarDisplay
{
    public bool HasPowerup;
    public bool HasAbility;
    public bool AbilityLoaded;
    public bool AbilityCharging;
    public AbilitySampleTier AbilityTier;
    public HandSlotId Equipped;
}

/// <summary>
/// One player's action bar at the bottom of their view: the Powerup card, the
/// Ability card, the swap hint between them, and the Emergency card reserved
/// for later. Lives on the root of Player Action Bar Base and its per-player
/// variants, which hold the look and placement. It only displays what
/// PlayerActionBarPresenter gives it.
///
/// Pick a Preview State in Prefab Mode to see the bar in that state, with the
/// icons and prompts authored on the cards. The presenter sets everything again
/// at runtime.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PlayerActionBarView : MonoBehaviour
{
    public enum PreviewState
    {
        PowerupEquippedAbilityCharging,
        BothLoadedAbilityEquipped,
        BothLoadedPowerupEquipped,
        AbilityChargingNoPowerup,
        PowerupOnlyNoAbility
    }

    [Header("Parts")]
    [Tooltip("Holds the cards. Shifted sideways so a lone Powerup card sits in the middle when the player has no Ability.")]
    [SerializeField] private RectTransform cardRow;

    [SerializeField] private ActionBarCardView powerupCard;
    [SerializeField] private ActionBarCardView abilityCard;

    [Tooltip("Reserved for Emergency Cart Delivery. Stays hidden until that feature exists.")]
    [SerializeField] private ActionBarCardView emergencyCard;

    [Tooltip("LB and arrow between the cards. Shown only when both are loaded, the only time a swap does anything.")]
    [SerializeField] private GameObject swapHint;

    [Header("Equipped Card")]
    [Tooltip("Scale of the equipped card. Only the equipped card shows its prompts.")]
    [Min(0.1f)]
    [SerializeField] private float equippedScale = 1f;

    [Tooltip("Scale of every card that is not equipped.")]
    [Min(0.1f)]
    [SerializeField] private float otherScale = 0.7f;

    [Tooltip("Seconds to grow or shrink when the equipped card changes.")]
    [Min(0f)]
    [SerializeField] private float resizeSeconds = 0.12f;

    [Header("Ability Ready Pop")]
    [Min(1f)]
    [SerializeField] private float readyPopScale = 1.25f;

    [Min(0.01f)]
    [SerializeField] private float readyPopSeconds = 0.3f;

    [Header("Frame Colors")]
    [SerializeField] private Color powerupFrameColor = Color.white;
    [SerializeField] private Color silverTierColor = new Color(0.72f, 0.76f, 0.82f);
    [SerializeField] private Color goldTierColor = new Color(1f, 0.78f, 0.2f);
    [SerializeField] private Color prismaticTierColor = new Color(0.66f, 0.5f, 1f);
    [SerializeField] private Color uniqueTierColor = new Color(1f, 0.38f, 0.45f);

    [Header("Editor Preview (Prefab Mode only)")]
    [SerializeField] private PreviewState previewState = PreviewState.PowerupEquippedAbilityCharging;
    [SerializeField] private AbilitySampleTier previewAbilityTier = AbilitySampleTier.Gold;

    [Range(0f, 1f)]
    [SerializeField] private float previewChargeProgress = 0.6f;

    [Min(0f)]
    [SerializeField] private float previewSecondsLeft = 12f;

    private RectTransform rectTransform;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            return rectTransform;
        }
    }

    public void SetIcons(Sprite powerupIcon, Sprite abilityIcon)
    {
        if (powerupCard != null) powerupCard.SetIcon(powerupIcon);
        if (abilityCard != null) abilityCard.SetIcon(abilityIcon);
    }

    /// <summary>Prompt sprite for the equipped item. Only the equipped card shows it.</summary>
    public void SetPrompt(int index, Sprite sprite)
    {
        if (powerupCard != null) powerupCard.SetPrompt(index, sprite);
        if (abilityCard != null) abilityCard.SetPrompt(index, sprite);
    }

    public void Display(in ActionBarDisplay display, bool animate)
    {
        bool powerupEquipped = display.HasPowerup && display.Equipped == HandSlotId.Map;
        bool abilityEquipped = display.AbilityLoaded && display.Equipped == HandSlotId.Ability;

        if (powerupCard != null)
        {
            powerupCard.SetVisible(true);
            powerupCard.Display(powerupFrameColor, !display.HasPowerup, false, powerupEquipped);
            powerupCard.SetTargetScale(powerupEquipped ? equippedScale : otherScale, resizeSeconds, animate);
        }

        if (abilityCard != null)
        {
            abilityCard.SetVisible(display.HasAbility);

            if (display.HasAbility)
            {
                abilityCard.Display(GetTierColor(display.AbilityTier), false, display.AbilityCharging, abilityEquipped);
                abilityCard.SetTargetScale(abilityEquipped ? equippedScale : otherScale, resizeSeconds, animate);
            }
        }

        if (emergencyCard != null) emergencyCard.SetVisible(false);

        if (swapHint != null)
        {
            bool showSwap = display.HasPowerup && display.AbilityLoaded;
            if (swapHint.activeSelf != showSwap) swapHint.SetActive(showSwap);
        }

        if (cardRow != null)
        {
            float shift = !display.HasAbility && powerupCard != null
                ? -((RectTransform)powerupCard.transform).anchoredPosition.x
                : 0f;

            cardRow.anchoredPosition = new Vector2(shift, cardRow.anchoredPosition.y);
        }
    }

    public void SetAbilityCharge(float progress01, float secondsLeft)
    {
        if (abilityCard != null) abilityCard.SetCharge(progress01, secondsLeft);
    }

    public void PlayAbilityReadyPop()
    {
        if (abilityCard != null) abilityCard.Pop(readyPopScale, readyPopSeconds);
    }

    private Color GetTierColor(AbilitySampleTier tier)
    {
        return tier switch
        {
            AbilitySampleTier.Gold => goldTierColor,
            AbilitySampleTier.Prismatic => prismaticTierColor,
            AbilitySampleTier.Unique => uniqueTierColor,
            _ => silverTierColor
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
        UnityEditor.EditorApplication.delayCall += ApplyPreview;
    }

    private void ApplyPreview()
    {
        // Only in Prefab Mode or a scene; never on the prefab asset itself.
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid()) return;

        ActionBarDisplay display = new ActionBarDisplay { AbilityTier = previewAbilityTier };

        switch (previewState)
        {
            case PreviewState.PowerupEquippedAbilityCharging:
                display.HasPowerup = true;
                display.HasAbility = true;
                display.AbilityCharging = true;
                display.Equipped = HandSlotId.Map;
                break;

            case PreviewState.BothLoadedAbilityEquipped:
                display.HasPowerup = true;
                display.HasAbility = true;
                display.AbilityLoaded = true;
                display.Equipped = HandSlotId.Ability;
                break;

            case PreviewState.BothLoadedPowerupEquipped:
                display.HasPowerup = true;
                display.HasAbility = true;
                display.AbilityLoaded = true;
                display.Equipped = HandSlotId.Map;
                break;

            case PreviewState.AbilityChargingNoPowerup:
                display.HasAbility = true;
                display.AbilityCharging = true;
                display.Equipped = HandSlotId.None;
                break;

            case PreviewState.PowerupOnlyNoAbility:
                display.HasPowerup = true;
                display.Equipped = HandSlotId.Map;
                break;
        }

        Display(display, false);

        if (display.AbilityCharging) SetAbilityCharge(previewChargeProgress, previewSecondsLeft);
    }
#endif
}
