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

    [Header("Card Styles")]
    [SerializeField]
    private ActionBarCardStyle powerupStyle = new ActionBarCardStyle(
        Color.white, new Color(0.97f, 0.94f, 0.86f), Color.clear, ActionBarCardMotion.None);

    [SerializeField]
    private ActionBarCardStyle silverStyle = new ActionBarCardStyle(
        new Color(0.78f, 0.81f, 0.86f), new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    [SerializeField]
    private ActionBarCardStyle goldStyle = new ActionBarCardStyle(
        new Color(1f, 0.78f, 0.2f), new Color(0.44f, 0.3f, 0.08f), Color.clear, ActionBarCardMotion.Shine);

    [SerializeField]
    private ActionBarCardStyle prismaticStyle = new ActionBarCardStyle(
        new Color(0.67f, 0.41f, 0.74f), new Color(0.24f, 0.19f, 0.38f), Color.clear, ActionBarCardMotion.Prismatic);

    [SerializeField]
    private ActionBarCardStyle uniqueStyle = new ActionBarCardStyle(
        new Color(0.74f, 0.04f, 0.12f), new Color(0.24f, 0.03f, 0.05f), new Color(0.88f, 0.7f, 0.33f), ActionBarCardMotion.Pulse);

    [Tooltip("Effect timings shared by every card on this bar.")]
    [SerializeField] private ActionBarCardTimings cardTimings = new ActionBarCardTimings();

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
            powerupCard.Display(powerupStyle, cardTimings, !display.HasPowerup, false, powerupEquipped, animate);
            powerupCard.SetTargetScale(powerupEquipped ? equippedScale : otherScale, resizeSeconds, animate);
        }

        if (abilityCard != null)
        {
            abilityCard.SetVisible(display.HasAbility);

            if (display.HasAbility)
            {
                abilityCard.Display(
                    GetTierStyle(display.AbilityTier),
                    cardTimings,
                    false,
                    display.AbilityCharging,
                    abilityEquipped,
                    animate);
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
        if (abilityCard != null) abilityCard.PlayReady(readyPopScale, readyPopSeconds);
    }

    private ActionBarCardStyle GetTierStyle(AbilitySampleTier tier)
    {
        return tier switch
        {
            AbilitySampleTier.Gold => goldStyle,
            AbilitySampleTier.Prismatic => prismaticStyle,
            AbilitySampleTier.Unique => uniqueStyle,
            _ => silverStyle
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
