using System;
using System.Collections;
using UnityEngine;

public enum MatchAreaIndicatorSelection
{
    None = 0,
    All = 1,
    Center = 2,
    ZoneA = 3,
    ZoneB = 4,
    ZoneC = 5,
    ZoneD = 6
}

public enum MatchAreaLightTransitionMode
{
    Instant = 0,
    Flicker = 1,
    IntensityFade = 2
}

/// <summary>
/// Presentation-only owner of the five authored arena light roots.
///
/// MatchFlowDirector selects one semantic area for each session. This class
/// guarantees that the corresponding roots are presented as one exclusive
/// set; gameplay spawners and ArenaZone do not own these lights.
/// </summary>
[DisallowMultipleComponent]
public class MatchFlowAreaIndicatorController : MonoBehaviour
{
    private sealed class LightGroupRuntime
    {
        public GameObject Root;
        public Light[] Lights;
        public float[] AuthoredIntensities;
        public Coroutine TransitionRoutine;
        public bool DesiredOn;
        public bool HasDesiredState;
    }

    #region Setup

    [Header("Five Area Light Roots")]
    [SerializeField] private GameObject centerLightRoot;
    [SerializeField] private GameObject zoneALightRoot;
    [SerializeField] private GameObject zoneBLightRoot;
    [SerializeField] private GameObject zoneCLightRoot;
    [SerializeField] private GameObject zoneDLightRoot;

    [Header("Unfocused Session Presentation")]
    [Tooltip("Recommended: All, because FreePlay permits roaming anywhere.")]
    [SerializeField]
    private MatchAreaIndicatorSelection freePlaySelection =
        MatchAreaIndicatorSelection.All;

    [Tooltip("Recommended: None, providing a clear end-of-match transition.")]
    [SerializeField]
    private MatchAreaIndicatorSelection endGameWrapSelection =
        MatchAreaIndicatorSelection.None;

    [Tooltip("Applied after the complete authored match flow finishes.")]
    [SerializeField]
    private MatchAreaIndicatorSelection postGameSelection =
        MatchAreaIndicatorSelection.None;

    [Header("Light Transitions")]
    [Tooltip(
        "Instant performs the original hard cut. Flicker flashes the lights " +
        "before settling. Intensity Fade smoothly interpolates brightness."
    )]
    [SerializeField]
    private MatchAreaLightTransitionMode transitionMode =
        MatchAreaLightTransitionMode.IntensityFade;

    [Tooltip(
        "Uses unscaled time so presentation transitions still finish if " +
        "gameplay time is paused or slowed."
    )]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Intensity Fade Settings")]
    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 0.35f;

    [Min(0f)]
    [SerializeField] private float fadeOutSeconds = 0.25f;

    [Tooltip("Maps normalized fade time to normalized light intensity.")]
    [SerializeField]
    private AnimationCurve intensityFadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Flicker Settings")]
    [Tooltip(
        "Number of unstable flashes before the lights settle into their " +
        "requested state."
    )]
    [Range(1, 8)]
    [SerializeField] private int flickerPulseCount = 2;

    [Min(0f)]
    [SerializeField] private float flickerOnSeconds = 0.055f;

    [Min(0f)]
    [SerializeField] private float flickerOffSeconds = 0.075f;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private MatchAreaIndicatorSelection currentSelection;
    [SerializeField] private bool hasAppliedSelection;
    [SerializeField] private int selectionChangeCount;
    [SerializeField] private int cachedLightCount;

    private LightGroupRuntime centerGroup;
    private LightGroupRuntime zoneAGroup;
    private LightGroupRuntime zoneBGroup;
    private LightGroupRuntime zoneCGroup;
    private LightGroupRuntime zoneDGroup;
    private bool lightGroupsInitialized;

    public MatchAreaIndicatorSelection CurrentSelection => currentSelection;
    public MatchAreaLightTransitionMode TransitionMode => transitionMode;
    public bool HasAppliedSelection => hasAppliedSelection;
    public int SelectionChangeCount => selectionChangeCount;
    public int CachedLightCount => cachedLightCount;

    public event Action<MatchAreaIndicatorSelection> OnSelectionChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureLightGroupsInitialized();

        // PreGame is the authored initial state, so apply it immediately and
        // avoid a startup flash while the scene is first becoming visible.
        ApplySelection(MatchAreaIndicatorSelection.All, false);
    }

    #endregion

    #region Match Flow API

    public void ShowPreGame()
    {
        ApplySelection(MatchAreaIndicatorSelection.All);
    }

    public void ShowPostGame()
    {
        ApplySelection(postGameSelection);
    }

    public void ShowForSession(MatchFlowSession session)
    {
        if (session == null)
        {
            ApplySelection(MatchAreaIndicatorSelection.None);
            return;
        }

        switch (session.type)
        {
            case MatchFlowSessionType.FreePlay:
                ApplySelection(freePlaySelection);
                break;

            case MatchFlowSessionType.CartRestock:
            case MatchFlowSessionType.CheckoutWindow:
            case MatchFlowSessionType.CheckoutRestock:
                ApplySelection(MatchAreaIndicatorSelection.Center);
                break;

            case MatchFlowSessionType.ZoneLoot:
                ShowZone(session.zone);
                break;

            case MatchFlowSessionType.EndGameWrap:
                ApplySelection(endGameWrapSelection);
                break;

            default:
                ApplySelection(MatchAreaIndicatorSelection.None);
                break;
        }
    }

    public void ShowZone(ArenaZoneId zoneId)
    {
        switch (zoneId)
        {
            case ArenaZoneId.ZoneA:
                ApplySelection(MatchAreaIndicatorSelection.ZoneA);
                break;

            case ArenaZoneId.ZoneB:
                ApplySelection(MatchAreaIndicatorSelection.ZoneB);
                break;

            case ArenaZoneId.ZoneC:
                ApplySelection(MatchAreaIndicatorSelection.ZoneC);
                break;

            case ArenaZoneId.ZoneD:
                ApplySelection(MatchAreaIndicatorSelection.ZoneD);
                break;

            default:
                ApplySelection(MatchAreaIndicatorSelection.None);
                break;
        }
    }

    public void ShowCenter()
    {
        ApplySelection(MatchAreaIndicatorSelection.Center);
    }

    public void ShowAll()
    {
        ApplySelection(MatchAreaIndicatorSelection.All);
    }

    public void ShowNone()
    {
        ApplySelection(MatchAreaIndicatorSelection.None);
    }

    public void SetSelection(MatchAreaIndicatorSelection selection)
    {
        ApplySelection(selection);
    }

    #endregion

    #region Selection Application

    private void ApplySelection(
        MatchAreaIndicatorSelection selection,
        bool animate = true
    )
    {
        EnsureLightGroupsInitialized();

        bool changed =
            !hasAppliedSelection ||
            currentSelection != selection;

        currentSelection = selection;
        hasAppliedSelection = true;

        bool showAll = selection == MatchAreaIndicatorSelection.All;

        SetGroupState(
            centerGroup,
            showAll || selection == MatchAreaIndicatorSelection.Center,
            animate
        );

        SetGroupState(
            zoneAGroup,
            showAll || selection == MatchAreaIndicatorSelection.ZoneA,
            animate
        );

        SetGroupState(
            zoneBGroup,
            showAll || selection == MatchAreaIndicatorSelection.ZoneB,
            animate
        );

        SetGroupState(
            zoneCGroup,
            showAll || selection == MatchAreaIndicatorSelection.ZoneC,
            animate
        );

        SetGroupState(
            zoneDGroup,
            showAll || selection == MatchAreaIndicatorSelection.ZoneD,
            animate
        );

        if (!changed) return;

        selectionChangeCount++;
        OnSelectionChanged?.Invoke(currentSelection);
    }

    #endregion

    #region Light Group Cache

    private void EnsureLightGroupsInitialized()
    {
        if (lightGroupsInitialized) return;

        centerGroup = BuildLightGroup(centerLightRoot, "Center");
        zoneAGroup = BuildLightGroup(zoneALightRoot, "Zone A");
        zoneBGroup = BuildLightGroup(zoneBLightRoot, "Zone B");
        zoneCGroup = BuildLightGroup(zoneCLightRoot, "Zone C");
        zoneDGroup = BuildLightGroup(zoneDLightRoot, "Zone D");

        cachedLightCount =
            CountLights(centerGroup) +
            CountLights(zoneAGroup) +
            CountLights(zoneBGroup) +
            CountLights(zoneCGroup) +
            CountLights(zoneDGroup);

        lightGroupsInitialized = true;
    }

    private LightGroupRuntime BuildLightGroup(GameObject root, string label)
    {
        if (root == null) return null;

        if (root == gameObject || transform.IsChildOf(root.transform))
        {
            Debug.LogError(
                $"[MatchFlowAreaIndicatorController] {label} Light Root " +
                "cannot be the controller GameObject or one of its parents. " +
                "Disabling that light group would also disable this controller.",
                this
            );
            return null;
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        float[] authoredIntensities = new float[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            authoredIntensities[i] = Mathf.Max(0f, lights[i].intensity);
        }

        if (lights.Length == 0)
        {
            Debug.LogWarning(
                $"[MatchFlowAreaIndicatorController] {label} Light Root " +
                "contains no Unity Light components.",
                root
            );
        }

        return new LightGroupRuntime
        {
            Root = root,
            Lights = lights,
            AuthoredIntensities = authoredIntensities
        };
    }

    private int CountLights(LightGroupRuntime group)
    {
        return group != null && group.Lights != null
            ? group.Lights.Length
            : 0;
    }

    #endregion

    #region Light Transitions

    private void SetGroupState(
        LightGroupRuntime group,
        bool turnOn,
        bool animate
    )
    {
        if (group == null || group.Root == null) return;

        bool requestAlreadyActive =
            group.HasDesiredState &&
            group.DesiredOn == turnOn;

        group.DesiredOn = turnOn;
        group.HasDesiredState = true;

        // A non-animated request is authoritative. This matters when another
        // component calls ShowPreGame during Awake before this component's
        // own Awake applies the stable initial state.
        if (requestAlreadyActive && animate) return;

        if (group.TransitionRoutine != null)
        {
            StopCoroutine(group.TransitionRoutine);
            group.TransitionRoutine = null;
        }

        if (!animate || transitionMode == MatchAreaLightTransitionMode.Instant)
        {
            ApplyImmediateState(group, turnOn);
            return;
        }

        if (!turnOn && !group.Root.activeSelf)
        {
            ApplyImmediateState(group, false);
            return;
        }

        switch (transitionMode)
        {
            case MatchAreaLightTransitionMode.Flicker:
                {
                    if (flickerOnSeconds <= 0f && flickerOffSeconds <= 0f)
                    {
                        ApplyImmediateState(group, turnOn);
                        break;
                    }

                    group.TransitionRoutine =
                        StartCoroutine(FlickerGroup(group, turnOn));
                    break;
                }

            case MatchAreaLightTransitionMode.IntensityFade:
                {
                    float fadeDuration = turnOn
                        ? fadeInSeconds
                        : fadeOutSeconds;

                    if (fadeDuration <= 0f)
                    {
                        ApplyImmediateState(group, turnOn);
                        break;
                    }

                    group.TransitionRoutine =
                        StartCoroutine(FadeGroup(group, turnOn));
                    break;
                }

            default:
                ApplyImmediateState(group, turnOn);
                break;
        }
    }

    private void ApplyImmediateState(LightGroupRuntime group, bool turnOn)
    {
        if (turnOn)
        {
            SetAuthoredIntensities(group);
            group.Root.SetActive(true);
            return;
        }

        group.Root.SetActive(false);

        // Restore the authored values while hidden so cache reuse never
        // leaves a light permanently at zero intensity.
        SetAuthoredIntensities(group);
    }

    private IEnumerator FadeGroup(LightGroupRuntime group, bool turnOn)
    {
        float duration = Mathf.Max(
            0f,
            turnOn ? fadeInSeconds : fadeOutSeconds
        );

        if (duration <= 0f)
        {
            ApplyImmediateState(group, turnOn);
            group.TransitionRoutine = null;
            yield break;
        }

        if (turnOn && !group.Root.activeSelf)
        {
            SetIntensityMultiplier(group, 0f);
            group.Root.SetActive(true);
        }

        if (!turnOn && !group.Root.activeSelf)
        {
            SetAuthoredIntensities(group);
            group.TransitionRoutine = null;
            yield break;
        }

        float[] startIntensities = CaptureCurrentIntensities(group);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float shapedTime = intensityFadeCurve != null
                ? Mathf.Clamp01(intensityFadeCurve.Evaluate(normalizedTime))
                : normalizedTime;

            for (int i = 0; i < group.Lights.Length; i++)
            {
                Light light = group.Lights[i];
                if (light == null) continue;

                float destination = turnOn
                    ? group.AuthoredIntensities[i]
                    : 0f;

                light.intensity = Mathf.Lerp(
                    startIntensities[i],
                    destination,
                    shapedTime
                );
            }

            elapsed += GetTransitionDeltaTime();
            yield return null;
        }

        if (turnOn)
        {
            SetAuthoredIntensities(group);
        }
        else
        {
            SetIntensityMultiplier(group, 0f);
            group.Root.SetActive(false);
            SetAuthoredIntensities(group);
        }

        group.TransitionRoutine = null;
    }

    private IEnumerator FlickerGroup(LightGroupRuntime group, bool turnOn)
    {
        if (!group.Root.activeSelf)
        {
            SetIntensityMultiplier(group, 0f);
            group.Root.SetActive(true);
        }

        int pulseCount = Mathf.Max(1, flickerPulseCount);

        if (turnOn)
        {
            SetIntensityMultiplier(group, 0f);
            yield return WaitForTransitionSeconds(flickerOffSeconds);

            for (int i = 0; i < pulseCount; i++)
            {
                SetAuthoredIntensities(group);
                yield return WaitForTransitionSeconds(flickerOnSeconds);

                SetIntensityMultiplier(group, 0f);
                yield return WaitForTransitionSeconds(flickerOffSeconds);
            }

            SetAuthoredIntensities(group);
        }
        else
        {
            SetAuthoredIntensities(group);

            for (int i = 0; i < pulseCount; i++)
            {
                SetIntensityMultiplier(group, 0f);
                yield return WaitForTransitionSeconds(flickerOffSeconds);

                SetAuthoredIntensities(group);
                yield return WaitForTransitionSeconds(flickerOnSeconds);
            }

            SetIntensityMultiplier(group, 0f);
            group.Root.SetActive(false);
            SetAuthoredIntensities(group);
        }

        group.TransitionRoutine = null;
    }

    private IEnumerator WaitForTransitionSeconds(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);

        while (remaining > 0f)
        {
            remaining -= GetTransitionDeltaTime();
            yield return null;
        }
    }

    private float GetTransitionDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float[] CaptureCurrentIntensities(LightGroupRuntime group)
    {
        float[] values = new float[group.Lights.Length];

        for (int i = 0; i < group.Lights.Length; i++)
        {
            Light light = group.Lights[i];
            values[i] = light != null ? Mathf.Max(0f, light.intensity) : 0f;
        }

        return values;
    }

    private void SetAuthoredIntensities(LightGroupRuntime group)
    {
        for (int i = 0; i < group.Lights.Length; i++)
        {
            Light light = group.Lights[i];
            if (light == null) continue;

            light.intensity = group.AuthoredIntensities[i];
        }
    }

    private void SetIntensityMultiplier(
        LightGroupRuntime group,
        float multiplier
    )
    {
        multiplier = Mathf.Max(0f, multiplier);

        for (int i = 0; i < group.Lights.Length; i++)
        {
            Light light = group.Lights[i];
            if (light == null) continue;

            light.intensity = group.AuthoredIntensities[i] * multiplier;
        }
    }

    #endregion
}
