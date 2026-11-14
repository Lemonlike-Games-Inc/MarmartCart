using UnityEngine;

/// <summary>
/// Presentation-only Ice asset references and tuning.
///
/// None of these values decide whether Freeze is valid or how long gameplay
/// remains frozen. They only control particles, the victim overlay, and the
/// visual fade tail after Ice gameplay has already ended.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Ice Presentation Profile",
    fileName = "IcePresentationProfile"
)]
public class IcePresentationProfile : ScriptableObject
{
    [Header("Successful Freeze Impact VFX")]
    [Tooltip(
        "Particle prefab played only after IceFrozen successfully starts or " +
        "refreshes on a leading cart. Ground/environment impacts that apply " +
        "no Freeze do not play this effect."
    )]
    [SerializeField] private GameObject impactVfxPrefab;

    [Min(0)]
    [SerializeField] private int impactVfxPrewarmCount = 4;

    [SerializeField] private bool allowImpactVfxPoolGrowth = true;

    [Tooltip(
        "Maximum allocated impact instances. Use 0 for no explicit cap when " +
        "pool growth is enabled."
    )]
    [Min(0)]
    [SerializeField] private int maximumImpactVfxInstances = 16;

    [Tooltip(
        "Scaled-time safety timeout for an accidentally looping particle " +
        "system. Normal effects return as soon as every particle finishes."
    )]
    [Min(0.1f)]
    [SerializeField] private float maximumImpactVfxLifetimeSeconds = 8f;

    [Tooltip("World-space offset added to the successful Freeze position.")]
    [SerializeField] private Vector3 impactVfxPositionOffset;

    [Tooltip(
        "Use the victim CartControlScript object's world rotation as the " +
        "impact prefab's base rotation."
    )]
    [SerializeField] private bool orientImpactVfxToVictim = true;

    [Tooltip("Rotation applied after the optional victim rotation.")]
    [SerializeField] private Vector3 impactVfxEulerOffset;

    [Header("Frozen Victim Visual")]
    [Tooltip(
        "Standalone frozen-mesh prefab spawned by the scene presenter. It is " +
        "not added to the leading-cart prefab."
    )]
    [SerializeField] private GameObject frozenVictimVisualPrefab;

    [Tooltip(
        "Position relative to the struck CartControlScript component's " +
        "GameObject transform. TransformPoint converts this to world space."
    )]
    [SerializeField] private Vector3 frozenVictimLocalPosition;

    [Tooltip(
        "Rotation relative to the struck CartControlScript component's " +
        "GameObject transform."
    )]
    [SerializeField] private Vector3 frozenVictimLocalEulerAngles;

    [Tooltip("Multiplier applied to the frozen visual prefab's authored scale.")]
    [SerializeField] private Vector3 frozenVictimScaleMultiplier = Vector3.one;

    [Tooltip(
        "Also multiply the visual by the victim transform's world scale. " +
        "Normally leave this disabled when every runtime cart uses scale 1."
    )]
    [SerializeField] private bool inheritVictimWorldScale;

    [Min(0)]
    [SerializeField] private int frozenVisualPrewarmCount = 4;

    [SerializeField] private bool allowFrozenVisualPoolGrowth = true;

    [Tooltip(
        "Maximum allocated victim visuals. Use 0 for no explicit cap when " +
        "pool growth is enabled."
    )]
    [Min(0)]
    [SerializeField] private int maximumFrozenVisualInstances = 8;

    [Header("Shared Ice Mesh Fade")]
    [Tooltip(
        "Scaled seconds that the victim frozen mesh and landed hazard mesh " +
        "remain for their visual fade after gameplay has ended."
    )]
    [Min(0f)]
    [SerializeField] private float visualFadeOutSeconds = 1f;

    [Tooltip(
        "Base Map alpha restored whenever a pooled Ice mesh is reused. " +
        "175 matches the authored default requested for these materials."
    )]
    [Range(0, 255)]
    [SerializeField] private int visualStartAlphaByte = 175;

    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public int ImpactVfxPrewarmCount => impactVfxPrewarmCount;
    public bool AllowImpactVfxPoolGrowth => allowImpactVfxPoolGrowth;
    public int MaximumImpactVfxInstances => maximumImpactVfxInstances;
    public float MaximumImpactVfxLifetimeSeconds =>
        maximumImpactVfxLifetimeSeconds;
    public Vector3 ImpactVfxPositionOffset => impactVfxPositionOffset;
    public bool OrientImpactVfxToVictim => orientImpactVfxToVictim;
    public Vector3 ImpactVfxEulerOffset => impactVfxEulerOffset;

    public GameObject FrozenVictimVisualPrefab => frozenVictimVisualPrefab;
    public Vector3 FrozenVictimLocalPosition => frozenVictimLocalPosition;
    public Vector3 FrozenVictimLocalEulerAngles =>
        frozenVictimLocalEulerAngles;
    public Vector3 FrozenVictimScaleMultiplier =>
        frozenVictimScaleMultiplier;
    public bool InheritVictimWorldScale => inheritVictimWorldScale;
    public int FrozenVisualPrewarmCount => frozenVisualPrewarmCount;
    public bool AllowFrozenVisualPoolGrowth =>
        allowFrozenVisualPoolGrowth;
    public int MaximumFrozenVisualInstances =>
        maximumFrozenVisualInstances;

    public float VisualFadeOutSeconds => visualFadeOutSeconds;
    public int VisualStartAlphaByte => visualStartAlphaByte;

    private void OnValidate()
    {
        impactVfxPrewarmCount = Mathf.Max(0, impactVfxPrewarmCount);
        maximumImpactVfxInstances = Mathf.Max(
            0,
            maximumImpactVfxInstances
        );

        if (maximumImpactVfxInstances > 0)
        {
            impactVfxPrewarmCount = Mathf.Min(
                impactVfxPrewarmCount,
                maximumImpactVfxInstances
            );
        }

        maximumImpactVfxLifetimeSeconds = Mathf.Max(
            0.1f,
            maximumImpactVfxLifetimeSeconds
        );

        frozenVisualPrewarmCount = Mathf.Max(0, frozenVisualPrewarmCount);
        maximumFrozenVisualInstances = Mathf.Max(
            0,
            maximumFrozenVisualInstances
        );

        if (maximumFrozenVisualInstances > 0)
        {
            frozenVisualPrewarmCount = Mathf.Min(
                frozenVisualPrewarmCount,
                maximumFrozenVisualInstances
            );
        }

        frozenVictimScaleMultiplier.x = Mathf.Max(
            0.0001f,
            frozenVictimScaleMultiplier.x
        );
        frozenVictimScaleMultiplier.y = Mathf.Max(
            0.0001f,
            frozenVictimScaleMultiplier.y
        );
        frozenVictimScaleMultiplier.z = Mathf.Max(
            0.0001f,
            frozenVictimScaleMultiplier.z
        );

        visualFadeOutSeconds = Mathf.Max(0f, visualFadeOutSeconds);
        visualStartAlphaByte = Mathf.Clamp(visualStartAlphaByte, 0, 255);
    }
}
