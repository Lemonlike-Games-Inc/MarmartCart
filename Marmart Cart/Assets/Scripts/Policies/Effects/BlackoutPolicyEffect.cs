using UnityEngine;

/// <summary>
/// Blackout: the store goes dark in short windows. Before each window the
/// lights flicker as a warning; during it each player sees only a lit circle
/// around their own leading cart, everything else blocked, the HUD included.
/// Window and gap lengths are random within their ranges, in game time.
///
/// Works through per-player handles on PlayerViewDarkness, so another Policy
/// or a personal Blackout can darken views at the same time. The lit radius
/// is per player (one handle each), so one player can later be left out or
/// see further. HUD While Dark sets what each HUD part does in the dark.
/// Cues: "lights-warning", "lights-out", "lights-on".
/// </summary>
[DisallowMultipleComponent]
public sealed class BlackoutPolicyEffect : PolicyEffect
{
    private const int MaxPlayers = PlayerViewDarkness.MaxPlayers;

    private enum Phase
    {
        Waiting,
        Warning,
        Dark
    }

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PlayerViewDarkness darkness;

    [Header("Windows (game seconds, random between X and Y)")]
    [Tooltip("From the start of the Round to the first warning.")]
    [SerializeField] private Vector2 firstWindowDelay = new Vector2(15f, 25f);

    [Tooltip("How long the store stays dark.")]
    [SerializeField] private Vector2 windowSeconds = new Vector2(5f, 10f);

    [Tooltip("Lit time between the end of one window and the next warning.")]
    [SerializeField] private Vector2 gapSeconds = new Vector2(15f, 30f);

    [Header("Warning")]
    [Tooltip("Seconds the lights flicker before a window.")]
    [Min(0f)]
    [SerializeField] private float warningSeconds = 1.5f;

    [Tooltip("How many times the lights blink during the warning.")]
    [Min(1)]
    [SerializeField] private int warningBlinks = 3;

    [Tooltip("Darkness of each warning blink.")]
    [Range(0f, 1f)]
    [SerializeField] private float warningStrength = 0.8f;

    [Header("Darkness")]
    [Tooltip("Radius of the lit circle around each player's leading cart, in world metres.")]
    [Min(0f)]
    [SerializeField] private float litRadius = 5f;

    [Min(0f)]
    [SerializeField] private float fadeInSeconds = 0.2f;

    [Min(0f)]
    [SerializeField] private float fadeOutSeconds = 0.6f;

    [Header("HUD While Dark")]
    [Tooltip("Per HUD part: Covered (under the darkness, seen only inside the lit circle), Hidden (gone, also inside the lit circle) or On Top (drawn above the darkness). Read live.")]
    [SerializeField] private DarknessHudRules hudWhileDark = new DarknessHudRules();

    [Header("Runtime - Read Only")]
    [SerializeField] private Phase phase = Phase.Waiting;
    [SerializeField] private float phaseTime;
    [SerializeField] private float phaseDuration;

    private readonly int[] handles = new int[MaxPlayers];

    protected override void OnPolicyStarted()
    {
        if (darkness == null)
            darkness = PlayerViewDarkness.Instance != null ? PlayerViewDarkness.Instance : FindFirstObjectByType<PlayerViewDarkness>();

        if (darkness == null)
        {
            Debug.LogError("[Policy] Blackout: no PlayerViewDarkness in the scene. It does nothing.", this);
            return;
        }

        int playerCount = MetaRoundController.ActivePlayerCount;

        for (int i = 0; i < playerCount && i < MaxPlayers; i++)
        {
            handles[i] = darkness.Add(i + 1, litRadius, hudWhileDark);
        }

        EnterPhase(Phase.Waiting, RandomIn(firstWindowDelay));
    }

    protected override void OnPolicyStopped(PolicyStopReason reason)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (darkness != null) darkness.Remove(handles[i]);
            handles[i] = 0;
        }

        phase = Phase.Waiting;
    }

    private void Update()
    {
        if (!IsRunning || darkness == null) return;

        phaseTime += Time.deltaTime;

        if (phaseTime >= phaseDuration) AdvancePhase();

        SetStrength(CurrentStrength());
    }

    private void AdvancePhase()
    {
        switch (phase)
        {
            case Phase.Waiting:
                EnterPhase(Phase.Warning, warningSeconds);
                RaiseCue("lights-warning");
                break;

            case Phase.Warning:
                EnterPhase(Phase.Dark, RandomIn(windowSeconds));
                Debug.Log($"[Policy] Blackout: lights out for {phaseDuration:0.0} s.", this);
                RaiseCue("lights-out");
                break;

            case Phase.Dark:
                EnterPhase(Phase.Waiting, RandomIn(gapSeconds));
                Debug.Log($"[Policy] Blackout: lights on. Next warning in {phaseDuration:0.0} s.", this);
                RaiseCue("lights-on");
                break;
        }
    }

    private float CurrentStrength()
    {
        switch (phase)
        {
            case Phase.Warning:
            {
                // Short dark blinks, evenly spaced across the warning.
                float period = phaseDuration / warningBlinks;
                bool blinkOn = period > 0f && Mathf.Repeat(phaseTime, period) < period * 0.35f;
                return blinkOn ? warningStrength : 0f;
            }

            case Phase.Dark:
            {
                float fadeIn = fadeInSeconds > 0f ? Mathf.Clamp01(phaseTime / fadeInSeconds) : 1f;
                float left = phaseDuration - phaseTime;
                float fadeOut = fadeOutSeconds > 0f ? Mathf.Clamp01(left / fadeOutSeconds) : 1f;
                return Mathf.Min(fadeIn, fadeOut);
            }

            default:
                return 0f;
        }
    }

    private void SetStrength(float strength)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (handles[i] != 0) darkness.SetStrength(handles[i], strength);
        }
    }

    private void EnterPhase(Phase next, float duration)
    {
        phase = next;
        phaseTime = 0f;
        phaseDuration = Mathf.Max(0f, duration);
    }

    private static float RandomIn(Vector2 range)
    {
        return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
