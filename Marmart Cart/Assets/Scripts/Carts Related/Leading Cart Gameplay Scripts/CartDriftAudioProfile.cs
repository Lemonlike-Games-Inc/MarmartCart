using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Shared designer tuning for the leading-cart drift loop.
///
/// The profile contains no runtime AudioSource state, so the same asset can be
/// shared safely by every player. Each CartDriftAudioPresenter owns its own
/// voices and samples its own small per-drift pitch variation.
/// </summary>
[CreateAssetMenu(
    fileName = "CartDriftAudioProfile",
    menuName = "Marmart Carts/Audio/Cart Drift Audio Profile")]
public sealed class CartDriftAudioProfile : ScriptableObject
{
    #region Clips / Routing

    [Header("Loop Clips")]
    [Tooltip("Loop used for wide and ordinary drifting.")]
    [SerializeField] private AudioClip normalDriftLoop;

    [Tooltip("More aggressive loop faded in as Current Tightness approaches 1.")]
    [SerializeField] private AudioClip tightDriftLoop;

    [Header("Output Routing")]
    [Tooltip("Optional mixer group, normally the game's SFX bus.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    #endregion

    #region Volume / Crossfade

    [Header("Layer Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float normalLoopVolume = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float tightLoopVolume = 0.75f;

    [Header("Tightness Crossfade")]
    [Tooltip("Tightness where the aggressive layer first becomes audible.")]
    [Range(0f, 1f)]
    [SerializeField] private float tightBlendStart = 0.3f;

    [Tooltip("Tightness where the aggressive layer reaches its full crossfade weight.")]
    [Range(0f, 1f)]
    [SerializeField] private float tightBlendFull = 0.85f;

    [Header("Speed Loudness")]
    [Tooltip("Planar speed at or below which the drift loop becomes silent.")]
    [Min(0f)]
    [SerializeField] private float silentAtSpeed = 1.5f;

    [Tooltip("Planar speed where the drift loop reaches full configured volume.")]
    [Min(0.01f)]
    [SerializeField] private float fullVolumeAtSpeed = 12f;

    #endregion

    #region Pitch / Timing

    [Header("Pitch Response")]
    [Tooltip("Pitch at Silent At Speed.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float minimumPitch = 0.92f;

    [Tooltip("Pitch at Full Volume At Speed, before the tightness boost.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float maximumPitch = 1.08f;

    [Tooltip("Additional pitch added at maximum drift tightness.")]
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float tightnessPitchBoost = 0.04f;

    [Tooltip(
        "One small pitch offset is sampled per drift and shared by both layers. " +
        "Values may be entered in either order."
    )]
    [SerializeField]
    private Vector2 perDriftPitchOffsetRange =
        new Vector2(-0.025f, 0.025f);

    [Header("Fade / Response")]
    [Min(0.001f)]
    [SerializeField] private float fadeInSeconds = 0.08f;

    [Min(0.001f)]
    [SerializeField] private float fadeOutSeconds = 0.14f;

    [Tooltip(
        "Approximate smoothing time for live volume and pitch changes while drifting."
    )]
    [Min(0.001f)]
    [SerializeField] private float parameterResponseSeconds = 0.06f;

    [Tooltip(
        "Start each loop at a random point for every drift, reducing repetition " +
        "and preventing several players from phasing together."
    )]
    [SerializeField] private bool randomizeLoopStartPosition = true;

    #endregion

    #region Spatial Audio

    [Header("3D Spatial Audio")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0.8f;

    [SerializeField]
    private AudioRolloffMode rolloffMode =
        AudioRolloffMode.Logarithmic;

    [Min(0.01f)]
    [SerializeField] private float minimumDistance = 2f;

    [Min(0.01f)]
    [SerializeField] private float maximumDistance = 28f;

    [Tooltip("Kept at zero by default because speed already controls pitch.")]
    [Range(0f, 5f)]
    [SerializeField] private float dopplerLevel = 0f;

    #endregion

    #region Public Values

    public AudioClip NormalDriftLoop => normalDriftLoop;
    public AudioClip TightDriftLoop => tightDriftLoop;
    public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
    public float NormalLoopVolume => Mathf.Clamp01(normalLoopVolume);
    public float TightLoopVolume => Mathf.Clamp01(tightLoopVolume);
    public float FadeInSeconds => Mathf.Max(0.001f, fadeInSeconds);
    public float FadeOutSeconds => Mathf.Max(0.001f, fadeOutSeconds);
    public float ParameterResponseSeconds =>
        Mathf.Max(0.001f, parameterResponseSeconds);
    public bool RandomizeLoopStartPosition => randomizeLoopStartPosition;
    public float SpatialBlend => Mathf.Clamp01(spatialBlend);
    public AudioRolloffMode RolloffMode => rolloffMode;
    public float MinimumDistance => Mathf.Max(0.01f, minimumDistance);
    public float MaximumDistance =>
        Mathf.Max(MinimumDistance + 0.01f, maximumDistance);
    public float DopplerLevel => Mathf.Clamp(dopplerLevel, 0f, 5f);

    #endregion

    #region Evaluation

    public float EvaluateTightBlend(float tightness)
    {
        float start = Mathf.Min(tightBlendStart, tightBlendFull);
        float end = Mathf.Max(tightBlendStart, tightBlendFull);

        if (Mathf.Approximately(start, end))
            return tightness >= end ? 1f : 0f;

        float normalized = Mathf.InverseLerp(start, end, tightness);
        return normalized * normalized * (3f - 2f * normalized);
    }

    public float EvaluateSpeedResponse(float planarSpeed)
    {
        float start = Mathf.Min(silentAtSpeed, fullVolumeAtSpeed);
        float end = Mathf.Max(silentAtSpeed, fullVolumeAtSpeed);

        if (Mathf.Approximately(start, end))
            return planarSpeed >= end ? 1f : 0f;

        float normalized = Mathf.InverseLerp(start, end, planarSpeed);
        return normalized * normalized * (3f - 2f * normalized);
    }

    public float EvaluatePitch(
        float planarSpeed,
        float tightness,
        float perDriftOffset)
    {
        float speedResponse = EvaluateSpeedResponse(planarSpeed);
        float speedPitch = Mathf.Lerp(minimumPitch, maximumPitch, speedResponse);

        return Mathf.Clamp(
            speedPitch + Mathf.Clamp01(tightness) * tightnessPitchBoost +
            perDriftOffset,
            0.1f,
            3f
        );
    }

    public float SamplePerDriftPitchOffset()
    {
        float minimum = Mathf.Min(
            perDriftPitchOffsetRange.x,
            perDriftPitchOffsetRange.y
        );
        float maximum = Mathf.Max(
            perDriftPitchOffsetRange.x,
            perDriftPitchOffsetRange.y
        );

        return Random.Range(minimum, maximum);
    }

    #endregion

    private void OnValidate()
    {
        normalLoopVolume = Mathf.Clamp01(normalLoopVolume);
        tightLoopVolume = Mathf.Clamp01(tightLoopVolume);
        tightBlendStart = Mathf.Clamp01(tightBlendStart);
        tightBlendFull = Mathf.Clamp(
            Mathf.Max(tightBlendStart, tightBlendFull),
            0f,
            1f
        );
        silentAtSpeed = Mathf.Max(0f, silentAtSpeed);
        fullVolumeAtSpeed = Mathf.Max(
            silentAtSpeed + 0.01f,
            fullVolumeAtSpeed
        );
        minimumPitch = Mathf.Clamp(minimumPitch, 0.1f, 3f);
        maximumPitch = Mathf.Clamp(
            Mathf.Max(minimumPitch, maximumPitch),
            0.1f,
            3f
        );
        tightnessPitchBoost = Mathf.Clamp(tightnessPitchBoost, -0.5f, 0.5f);
        fadeInSeconds = Mathf.Max(0.001f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0.001f, fadeOutSeconds);
        parameterResponseSeconds = Mathf.Max(0.001f, parameterResponseSeconds);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minimumDistance = Mathf.Max(0.01f, minimumDistance);
        maximumDistance = Mathf.Max(minimumDistance + 0.01f, maximumDistance);
        dopplerLevel = Mathf.Clamp(dopplerLevel, 0f, 5f);
    }
}
