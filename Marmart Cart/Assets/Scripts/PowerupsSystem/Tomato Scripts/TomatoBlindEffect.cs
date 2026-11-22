using UnityEngine;

/// <summary>
/// Item effect: a leading cart hit by this item is blinded. Throw details
/// (projectile, pattern, preview) live in the item's ThrowItemUse.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Effects/Tomato Blind",
    fileName = "TomatoBlindEffect"
)]
public class TomatoBlindEffect : ItemEffectDefinition
{
    [Header("Blind Effect")]
    [Tooltip("Scaled gameplay seconds before the target's Tomato blind state ends.")]
    [Min(0.05f)]
    [SerializeField] private float blindDurationSeconds = 3f;

    [Tooltip(
        "Maximum splash count advertised to the later Canvas presenter. " +
        "Every additional valid Tomato hit still publishes a refresh event."
    )]
    [Range(1, 12)]
    [SerializeField] private int maximumVisualSplashCount = 3;

    [Tooltip(
        "When another Tomato hits an already blinded player, restart the " +
        "blind lifetime from that impact."
    )]
    [SerializeField] private bool refreshDurationOnAdditionalHit = true;

    [Header("Self-Hit")]
    [Tooltip(
        "On: a Tomato that comes down on, or rolls into, the thrower's own " +
        "leading cart blinds the thrower too. It never hits the thrower on the way up."
    )]
    [SerializeField] private bool blindsThrower = true;

    public float BlindDurationSeconds => blindDurationSeconds;
    public bool BlindsThrower => blindsThrower;
    public override bool HitsThrower => blindsThrower;
    public int MaximumVisualSplashCount => maximumVisualSplashCount;
    public bool RefreshDurationOnAdditionalHit =>
        refreshDurationOnAdditionalHit;

    private void OnValidate()
    {
        blindDurationSeconds = Mathf.Max(0.05f, blindDurationSeconds);
        maximumVisualSplashCount = Mathf.Clamp(
            maximumVisualSplashCount,
            1,
            12
        );
    }
}
