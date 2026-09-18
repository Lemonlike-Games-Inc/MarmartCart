using UnityEngine;

/// <summary>
/// Gameplay-only tuning for the non-projectile Cola Mentos duration effect.
/// Presentation assets live in ColaMentosPresentationProfile.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Cola Mentos Profile",
    fileName = "ColaMentosPowerupProfile"
)]
public class ColaMentosPowerupProfile : ScriptableObject
{
    [Header("Duration Boost")]
    [Tooltip(
        "Scaled gameplay seconds that the fixed target-speed override remains " +
        "active. Freeze and Checkout do not pause this timer."
    )]
    [Min(0.05f)]
    [SerializeField] private float durationSeconds = 4f;

    [Tooltip(
        "Absolute LeadingCartBehaviour target speed during the effect. This " +
        "replaces normal, overloaded, Drift, and Hype-Speed-Up target-speed " +
        "calculations, but it never overrides Freeze, Checkout, or crash stop."
    )]
    [Min(0f)]
    [SerializeField] private float fixedTargetSpeed = 30f;

    [Tooltip(
        "Prevent activating another stored power-up while Cola is active. " +
        "The player may still collect/replace the inventory slot; use becomes " +
        "available when Cola ends. Disable this to allow Cola refreshes or " +
        "cross-power-up overlap."
    )]
    [SerializeField] private bool blockOtherPowerupUseWhileActive = true;

    public float DurationSeconds => durationSeconds;
    public float FixedTargetSpeed => fixedTargetSpeed;
    public bool BlockOtherPowerupUseWhileActive =>
        blockOtherPowerupUseWhileActive;

    private void OnValidate()
    {
        durationSeconds = Mathf.Max(0.05f, durationSeconds);
        fixedTargetSpeed = Mathf.Max(0f, fixedTargetSpeed);
    }
}
