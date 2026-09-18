using UnityEngine;

/// <summary>
/// Presentation-only tuning for the standalone Cola Mentos cart attachment.
/// The duration and target speed remain in ColaMentosPowerupProfile.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Cola Mentos Presentation Profile",
    fileName = "ColaMentosPresentationProfile"
)]
public class ColaMentosPresentationProfile : ScriptableObject
{
    [Header("Cart Attachment")]
    [Tooltip(
        "Standalone mesh/VFX prefab shown while ColaMentosBoost is active. " +
        "It is pooled at scene level and does not need to be hidden inside " +
        "the leading-cart prefab."
    )]
    [SerializeField] private GameObject cartVisualPrefab;

    [Tooltip(
        "Position relative to the GameObject holding CartControlScript."
    )]
    [SerializeField] private Vector3 localPosition;

    [Tooltip(
        "Rotation relative to the GameObject holding CartControlScript."
    )]
    [SerializeField] private Vector3 localEulerAngles;

    [Tooltip("Multiplier applied to the prefab's authored root scale.")]
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;

    [Tooltip(
        "Also multiply by the CartControlScript transform's world scale. " +
        "Normally leave disabled when runtime carts use scale 1."
    )]
    [SerializeField] private bool inheritCartWorldScale;

    [Header("Looping VFX")]
    [Tooltip(
        "Restart every ParticleSystem when an already-active Cola duration is " +
        "refreshed by another Cola pickup."
    )]
    [SerializeField] private bool restartParticlesOnRefresh = true;

    [Header("Pool")]
    [Min(0)]
    [SerializeField] private int prewarmCount = 4;

    [SerializeField] private bool allowPoolGrowth = true;

    [Tooltip(
        "Maximum allocated attachment instances. Use 0 for no explicit cap " +
        "when growth is enabled. Four is sufficient for the normal 4P case."
    )]
    [Min(0)]
    [SerializeField] private int maximumInstances = 4;

    public GameObject CartVisualPrefab => cartVisualPrefab;
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 ScaleMultiplier => scaleMultiplier;
    public bool InheritCartWorldScale => inheritCartWorldScale;
    public bool RestartParticlesOnRefresh => restartParticlesOnRefresh;
    public int PrewarmCount => prewarmCount;
    public bool AllowPoolGrowth => allowPoolGrowth;
    public int MaximumInstances => maximumInstances;

    private void OnValidate()
    {
        prewarmCount = Mathf.Max(0, prewarmCount);
        maximumInstances = Mathf.Max(0, maximumInstances);

        if (maximumInstances > 0)
        {
            prewarmCount = Mathf.Min(prewarmCount, maximumInstances);
        }

        scaleMultiplier.x = Mathf.Max(0.0001f, scaleMultiplier.x);
        scaleMultiplier.y = Mathf.Max(0.0001f, scaleMultiplier.y);
        scaleMultiplier.z = Mathf.Max(0.0001f, scaleMultiplier.z);
    }
}
