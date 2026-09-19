using System.Collections;
using UnityEngine;

/// <summary>
/// Per-leading-cart drift audio presentation.
///
/// It subscribes to CartDriftController's semantic lifecycle events, owns two
/// dedicated loop voices, and runs one temporary coroutine only while a drift
/// is active or fading out. No SfxManager state and no permanent Update loop
/// are used.
/// </summary>
[DisallowMultipleComponent]
public sealed class CartDriftAudioPresenter : MonoBehaviour
{
    #region Authoring

    [Header("References")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private CartDriftAudioProfile audioProfile;

    [Tooltip(
        "Optional transform where automatically created 3D voices should live. " +
        "Defaults to this component's transform."
    )]
    [SerializeField] private Transform audioAnchor;

    [Header("Dedicated Loop Voices")]
    [Tooltip(
        "Optional dedicated source. Leave empty to create one under a runtime child."
    )]
    [SerializeField] private AudioSource normalLoopSource;

    [Tooltip(
        "Optional dedicated source. Leave empty to create one under a runtime child."
    )]
    [SerializeField] private AudioSource tightLoopSource;

    [Tooltip("Automatically create whichever dedicated loop voices are unassigned.")]
    [SerializeField] private bool createMissingAudioSources = true;

    #endregion

    #region Runtime Diagnostics

    [Header("Runtime - Read Only")]
    [SerializeField] private bool driftAudioActive;
    [SerializeField] private bool fadeOutRequested;
    [SerializeField] private float currentSpeedResponse;
    [SerializeField] private float currentTightBlend;
    [SerializeField] private float currentPitch = 1f;
    [SerializeField] private float currentNormalVolume;
    [SerializeField] private float currentTightVolume;
    [SerializeField] private float currentFadeEnvelope;
    [SerializeField] private float currentPerDriftPitchOffset;

    private Coroutine driftAudioRoutine;
    private Transform generatedSourceRoot;
    private bool subscribed;
    private bool configurationErrorLogged;

    public bool DriftAudioActive => driftAudioActive;
    public float CurrentSpeedResponse => currentSpeedResponse;
    public float CurrentTightBlend => currentTightBlend;
    public float CurrentPitch => currentPitch;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDriftController();
        TryPrepareAudioVoices();
    }

    private void OnEnable()
    {
        ResolveDriftController();
        TryPrepareAudioVoices();
        Subscribe();

        if (driftController != null && driftController.IsDrifting)
            BeginDriftAudio();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopDriftAudioImmediately();
    }

    private void OnValidate()
    {
        if (normalLoopSource != null && normalLoopSource == tightLoopSource)
        {
            Debug.LogWarning(
                "[CartDriftAudioPresenter] Normal and Tight Loop Source must be " +
                "two different AudioSource components.",
                this
            );
        }
    }

    #endregion

    #region Subscription

    private void ResolveDriftController()
    {
        if (driftController != null) return;

        driftController = GetComponent<CartDriftController>();

        if (driftController == null)
            driftController = GetComponentInParent<CartDriftController>();

        if (driftController == null)
            driftController = GetComponentInChildren<CartDriftController>(true);
    }

    private void Subscribe()
    {
        if (subscribed || driftController == null) return;

        driftController.OnDriftStarted += HandleDriftStarted;
        driftController.OnDriftEndedClean += HandleDriftEndedClean;
        driftController.OnDriftInterrupted += HandleDriftInterrupted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || driftController == null) return;

        driftController.OnDriftStarted -= HandleDriftStarted;
        driftController.OnDriftEndedClean -= HandleDriftEndedClean;
        driftController.OnDriftInterrupted -= HandleDriftInterrupted;
        subscribed = false;
    }

    private void HandleDriftStarted()
    {
        BeginDriftAudio();
    }

    private void HandleDriftEndedClean(string reason)
    {
        RequestFadeOut();
    }

    private void HandleDriftInterrupted(string reason)
    {
        RequestFadeOut();
    }

    #endregion

    #region Voice Preparation

    private bool TryPrepareAudioVoices()
    {
        if (audioProfile == null)
        {
            LogConfigurationErrorOnce(
                "Assign a Cart Drift Audio Profile."
            );
            return false;
        }

        if (audioProfile.NormalDriftLoop == null &&
            audioProfile.TightDriftLoop == null)
        {
            LogConfigurationErrorOnce(
                "Assign at least one Normal/Tight drift loop clip in the profile."
            );
            return false;
        }

        if (!EnsureDistinctAudioSources()) return false;

        ConfigureLoopSource(normalLoopSource, audioProfile.NormalDriftLoop);
        ConfigureLoopSource(tightLoopSource, audioProfile.TightDriftLoop);

        configurationErrorLogged = false;
        return true;
    }

    private bool EnsureDistinctAudioSources()
    {
        if (normalLoopSource != null && normalLoopSource == tightLoopSource)
        {
            LogConfigurationErrorOnce(
                "Normal and Tight Loop Source must be different AudioSources."
            );
            return false;
        }

        if ((normalLoopSource == null || tightLoopSource == null) &&
            !createMissingAudioSources)
        {
            LogConfigurationErrorOnce(
                "Assign both dedicated AudioSources or enable Create Missing Audio Sources."
            );
            return false;
        }

        if (normalLoopSource == null)
            normalLoopSource = GetOrCreateGeneratedSource();

        if (tightLoopSource == null)
            tightLoopSource = GetOrCreateGeneratedSource();

        if (normalLoopSource == null || tightLoopSource == null ||
            normalLoopSource == tightLoopSource)
        {
            LogConfigurationErrorOnce(
                "Could not prepare two distinct drift-loop AudioSources."
            );
            return false;
        }

        return true;
    }

    private AudioSource GetOrCreateGeneratedSource()
    {
        if (generatedSourceRoot == null)
        {
            Transform parent = audioAnchor != null ? audioAnchor : transform;
            Transform existing = parent.Find("Runtime Drift Audio Voices");

            if (existing != null)
            {
                generatedSourceRoot = existing;
            }
            else
            {
                GameObject root = new GameObject("Runtime Drift Audio Voices");
                root.transform.SetParent(parent, false);
                generatedSourceRoot = root.transform;
            }
        }

        return generatedSourceRoot.gameObject.AddComponent<AudioSource>();
    }

    private void ConfigureLoopSource(AudioSource source, AudioClip clip)
    {
        if (source == null || audioProfile == null) return;

        source.Stop();
        source.playOnAwake = false;
        source.loop = true;
        source.clip = clip;
        source.volume = 0f;
        source.pitch = 1f;
        source.outputAudioMixerGroup = audioProfile.OutputMixerGroup;
        source.spatialBlend = audioProfile.SpatialBlend;
        source.rolloffMode = audioProfile.RolloffMode;
        source.minDistance = audioProfile.MinimumDistance;
        source.maxDistance = audioProfile.MaximumDistance;
        source.dopplerLevel = audioProfile.DopplerLevel;
    }

    #endregion

    #region Drift Runtime

    private void BeginDriftAudio()
    {
        if (!isActiveAndEnabled || driftController == null) return;
        if (!TryPrepareAudioVoices()) return;

        StopRoutineOnly();
        StopSourcesAndResetValues();

        fadeOutRequested = false;
        currentPerDriftPitchOffset =
            audioProfile.SamplePerDriftPitchOffset();
        currentPitch = audioProfile.EvaluatePitch(
            driftController.CurrentSpeed,
            driftController.CurrentTightness,
            currentPerDriftPitchOffset
        );

        StartLoopSource(normalLoopSource);
        StartLoopSource(tightLoopSource);

        driftAudioActive = true;
        driftAudioRoutine = StartCoroutine(DriftAudioRoutine());
    }

    private void StartLoopSource(AudioSource source)
    {
        if (source == null || source.clip == null) return;

        source.volume = 0f;
        source.pitch = currentPitch;

        if (audioProfile.RandomizeLoopStartPosition && source.clip.length > 0.02f)
        {
            source.time = Random.Range(0f, source.clip.length - 0.01f);
        }
        else
        {
            source.time = 0f;
        }

        source.Play();
    }

    private IEnumerator DriftAudioRoutine()
    {
        currentFadeEnvelope = 0f;

        while (!fadeOutRequested &&
               driftController != null &&
               driftController.IsDrifting)
        {
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            currentFadeEnvelope = Mathf.MoveTowards(
                currentFadeEnvelope,
                1f,
                deltaTime / audioProfile.FadeInSeconds
            );

            UpdateLiveAudioParameters(deltaTime);
            yield return null;
        }

        float fadeDuration = audioProfile.FadeOutSeconds;
        float fadeElapsed = 0f;
        float normalStartVolume = currentNormalVolume;
        float tightStartVolume = currentTightVolume;

        while (fadeElapsed < fadeDuration)
        {
            float normalized = Mathf.Clamp01(fadeElapsed / fadeDuration);
            float remaining = 1f - normalized;

            currentFadeEnvelope = remaining;
            currentNormalVolume = normalStartVolume * remaining;
            currentTightVolume = tightStartVolume * remaining;

            ApplyCurrentVolumeValues();

            fadeElapsed += Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            yield return null;
        }

        StopSourcesAndResetValues();
        driftAudioRoutine = null;
    }

    private void UpdateLiveAudioParameters(float deltaTime)
    {
        float tightness = Mathf.Clamp01(driftController.CurrentTightness);
        currentSpeedResponse =
            audioProfile.EvaluateSpeedResponse(driftController.CurrentSpeed);
        currentTightBlend = audioProfile.EvaluateTightBlend(tightness);

        bool hasNormalLayer = normalLoopSource != null &&
                              normalLoopSource.clip != null;
        bool hasTightLayer = tightLoopSource != null &&
                             tightLoopSource.clip != null;

        float normalWeight;
        float tightWeight;

        if (hasNormalLayer && hasTightLayer)
        {
            // Equal-power weights avoid the volume dip of a linear crossfade.
            float crossfadeAngle = currentTightBlend * Mathf.PI * 0.5f;
            normalWeight = Mathf.Cos(crossfadeAngle);
            tightWeight = Mathf.Sin(crossfadeAngle);
        }
        else
        {
            normalWeight = hasNormalLayer ? 1f : 0f;
            tightWeight = hasTightLayer ? 1f : 0f;
        }

        float targetNormalVolume =
            audioProfile.NormalLoopVolume * normalWeight *
            currentSpeedResponse * currentFadeEnvelope;
        float targetTightVolume =
            audioProfile.TightLoopVolume * tightWeight *
            currentSpeedResponse * currentFadeEnvelope;
        float targetPitch = audioProfile.EvaluatePitch(
            driftController.CurrentSpeed,
            tightness,
            currentPerDriftPitchOffset
        );

        float response = 1f - Mathf.Exp(
            -deltaTime / audioProfile.ParameterResponseSeconds
        );

        currentNormalVolume = Mathf.Lerp(
            currentNormalVolume,
            targetNormalVolume,
            response
        );
        currentTightVolume = Mathf.Lerp(
            currentTightVolume,
            targetTightVolume,
            response
        );
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, response);

        ApplyCurrentVolumeValues();

        if (normalLoopSource != null)
            normalLoopSource.pitch = currentPitch;

        if (tightLoopSource != null)
            tightLoopSource.pitch = currentPitch;
    }

    private void ApplyCurrentVolumeValues()
    {
        if (normalLoopSource != null)
            normalLoopSource.volume = Mathf.Clamp01(currentNormalVolume);

        if (tightLoopSource != null)
            tightLoopSource.volume = Mathf.Clamp01(currentTightVolume);
    }

    private void RequestFadeOut()
    {
        fadeOutRequested = true;
    }

    public void StopDriftAudioImmediately()
    {
        StopRoutineOnly();
        StopSourcesAndResetValues();
    }

    private void StopRoutineOnly()
    {
        if (driftAudioRoutine == null) return;

        StopCoroutine(driftAudioRoutine);
        driftAudioRoutine = null;
    }

    private void StopSourcesAndResetValues()
    {
        ResetSource(normalLoopSource);
        ResetSource(tightLoopSource);

        driftAudioActive = false;
        fadeOutRequested = false;
        currentSpeedResponse = 0f;
        currentTightBlend = 0f;
        currentPitch = 1f;
        currentNormalVolume = 0f;
        currentTightVolume = 0f;
        currentFadeEnvelope = 0f;
        currentPerDriftPitchOffset = 0f;
    }

    private static void ResetSource(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        source.volume = 0f;
        source.pitch = 1f;
    }

    #endregion

    private void LogConfigurationErrorOnce(string message)
    {
        if (configurationErrorLogged) return;

        configurationErrorLogged = true;
        Debug.LogError($"[CartDriftAudioPresenter] {message}", this);
    }
}
