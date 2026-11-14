using UnityEngine;

/// <summary>
/// Presentation-only Tomato asset references and tuning.
///
/// None of these settings can change whether a Tomato hit is valid or how
/// long the gameplay effect lasts. Keeping them separate lets artists replace
/// the impact prefab or blind Sprite without touching gameplay profiles.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Tomato Presentation Profile",
    fileName = "TomatoPresentationProfile"
)]
public class TomatoPresentationProfile : ScriptableObject
{
    [Header("Impact VFX")]
    [Tooltip(
        "Prefab spawned at every physical Tomato impact. The root may be an " +
        "empty GameObject; at least one ParticleSystem must exist on it or " +
        "one of its children."
    )]
    [SerializeField] private GameObject impactVfxPrefab;

    [Tooltip("Impact instances prepared when the presenter starts.")]
    [Min(0)]
    [SerializeField] private int impactVfxPrewarmCount = 8;

    [Tooltip(
        "Allow the impact pool to grow when several Tomatoes complete in the " +
        "same short window."
    )]
    [SerializeField] private bool allowImpactVfxPoolGrowth = true;

    [Tooltip("Hard safety limit for simultaneously allocated impact instances.")]
    [Min(1)]
    [SerializeField] private int maximumImpactVfxInstances = 24;

    [Tooltip(
        "Scaled-time safety timeout for a looping or incorrectly configured " +
        "particle prefab. Normal non-looping systems return as soon as all " +
        "particles finish."
    )]
    [Min(0.1f)]
    [SerializeField] private float maximumImpactVfxLifetimeSeconds = 8f;

    [Tooltip("Small offset along the contacted surface normal.")]
    [Min(0f)]
    [SerializeField] private float impactSurfaceOffset = 0.02f;

    [Tooltip(
        "Rotate the prefab so its local up direction points away from the hit " +
        "surface. Disable this if the authored effect should retain one world " +
        "orientation."
    )]
    [SerializeField] private bool alignImpactVfxUpToSurfaceNormal = true;

    [Tooltip(
        "Applied after surface alignment. Use this when the particle prefab's " +
        "authored emission axis is not local up."
    )]
    [SerializeField] private Vector3 impactVfxEulerOffset;

    [Header("Blind Canvas")]
    [Tooltip(
        "Sprite rendered over only the struck player's camera viewport. Import " +
        "the texture as Sprite (2D and UI)."
    )]
    [SerializeField] private Sprite blindOverlaySprite;

    [Tooltip("Optional UI material. Leave empty to use Unity's default UI material.")]
    [SerializeField] private Material blindOverlayMaterial;

    [SerializeField] private Color blindOverlayTint = Color.white;

    [Tooltip("Opacity used by the first valid Tomato hit.")]
    [Range(0f, 1f)]
    [SerializeField] private float firstHitOpacity = 0.72f;

    [Tooltip(
        "Opacity added by each additional hit while the same blind effect is " +
        "active. SplashCount is capped by TomatoPowerupProfile."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float additionalHitOpacity = 0.08f;

    [Tooltip("Final opacity clamp after additional hits.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumBlindOpacity = 0.95f;

    [Tooltip("Scaled seconds used for the first appearance of a new blind effect.")]
    [Min(0f)]
    [SerializeField] private float blindFadeInSeconds = 0.06f;

    [Tooltip(
        "Scaled seconds before gameplay expiry over which the overlay fades " +
        "out. Refreshing the gameplay duration also postpones this fade."
    )]
    [Min(0f)]
    [SerializeField] private float blindFadeOutSeconds = 0.35f;

    [Tooltip(
        "Preserve the source Sprite's aspect ratio. Disable for an image " +
        "authored to stretch across the complete viewport."
    )]
    [SerializeField] private bool preserveBlindSpriteAspect;

    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public int ImpactVfxPrewarmCount => impactVfxPrewarmCount;
    public bool AllowImpactVfxPoolGrowth => allowImpactVfxPoolGrowth;
    public int MaximumImpactVfxInstances => maximumImpactVfxInstances;
    public float MaximumImpactVfxLifetimeSeconds =>
        maximumImpactVfxLifetimeSeconds;
    public float ImpactSurfaceOffset => impactSurfaceOffset;
    public bool AlignImpactVfxUpToSurfaceNormal =>
        alignImpactVfxUpToSurfaceNormal;
    public Vector3 ImpactVfxEulerOffset => impactVfxEulerOffset;

    public Sprite BlindOverlaySprite => blindOverlaySprite;
    public Material BlindOverlayMaterial => blindOverlayMaterial;
    public Color BlindOverlayTint => blindOverlayTint;
    public float FirstHitOpacity => firstHitOpacity;
    public float AdditionalHitOpacity => additionalHitOpacity;
    public float MaximumBlindOpacity => maximumBlindOpacity;
    public float BlindFadeInSeconds => blindFadeInSeconds;
    public float BlindFadeOutSeconds => blindFadeOutSeconds;
    public bool PreserveBlindSpriteAspect => preserveBlindSpriteAspect;

    private void OnValidate()
    {
        impactVfxPrewarmCount = Mathf.Max(0, impactVfxPrewarmCount);
        maximumImpactVfxInstances = Mathf.Max(
            1,
            maximumImpactVfxInstances
        );

        impactVfxPrewarmCount = Mathf.Min(
            impactVfxPrewarmCount,
            maximumImpactVfxInstances
        );

        maximumImpactVfxLifetimeSeconds = Mathf.Max(
            0.1f,
            maximumImpactVfxLifetimeSeconds
        );

        impactSurfaceOffset = Mathf.Max(0f, impactSurfaceOffset);
        firstHitOpacity = Mathf.Clamp01(firstHitOpacity);
        additionalHitOpacity = Mathf.Clamp01(additionalHitOpacity);
        maximumBlindOpacity = Mathf.Clamp01(maximumBlindOpacity);
        blindFadeInSeconds = Mathf.Max(0f, blindFadeInSeconds);
        blindFadeOutSeconds = Mathf.Max(0f, blindFadeOutSeconds);
    }
}
