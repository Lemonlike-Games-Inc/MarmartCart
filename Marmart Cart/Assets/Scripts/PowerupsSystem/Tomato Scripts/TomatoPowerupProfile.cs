using UnityEngine;

/// <summary>
/// Tomato gameplay-effect tuning. Projectile count, spread, flight, and
/// collision policy remain in PowerupProjectileProfile.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Tomato Profile",
    fileName = "TomatoPowerupProfile"
)]
public class TomatoPowerupProfile : ScriptableObject
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

    public float BlindDurationSeconds => blindDurationSeconds;
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
