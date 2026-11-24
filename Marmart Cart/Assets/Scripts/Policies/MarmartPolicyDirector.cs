using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Scene-local Marmart Policies for this Round. Gameplay scenes only: without
/// a MetaRoundController (tutorial, test scenes) there are no Policies.
///
/// - On load it asks its source for this Round's Policies: the Fixed Schedule,
///   or (later) a random pool. Nothing has to survive the scene load, because
///   the list follows MatchSession.RoundNumber.
/// - It starts each Policy's effect when the match starts, before the first
///   match-flow session, and stops them on Round end, a failed start, any load
///   (next Round, F1 to F4, main menu) and teardown.
/// - Presentation (reveal, banners, sound, announcer) reads ActivePolicies and
///   listens to the events; it never changes a Policy.
/// </summary>
[DisallowMultipleComponent]
public sealed class MarmartPolicyDirector : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("On: this Round's Policies come from the Fixed Schedule. Off: from the Random Pool.")]
    [SerializeField] private bool useFixedSchedule = true;

    [SerializeField] private PolicySchedule fixedSchedule;

    [Tooltip("Used when Use Fixed Schedule is off. No random pool asset exists yet.")]
    [SerializeField] private PolicyRoundSource randomPool;

    [Header("Effects")]
    [Tooltip("One effect per Policy. Auto-found in children if left empty.")]
    [SerializeField] private PolicyEffect[] effects = new PolicyEffect[0];

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [Tooltip("Auto-found if left empty. Without one there are no Policies.")]
    [SerializeField] private MetaRoundController roundController;

    [Header("Debug")]
    [SerializeField] private bool logPolicies = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int round;
    [SerializeField] private bool policiesRunning;
    [SerializeField] private List<PolicyDefinition> activePolicies = new List<PolicyDefinition>(4);

    // Same order as activePolicies; null where a Policy has no effect component.
    private readonly List<PolicyEffect> activeEffects = new List<PolicyEffect>(4);

    /// <summary>A Policy's effect started: the Round began.</summary>
    public event Action<PolicyDefinition> PolicyStarted;

    /// <summary>A Policy's effect stopped, and why.</summary>
    public event Action<PolicyDefinition, PolicyStopReason> PolicyStopped;

    /// <summary>A running Policy reached a moment for sound and the announcer (for example "lights out").</summary>
    public event Action<PolicyDefinition, string> PolicyCue;

    public int Round => round;
    public bool HasPolicies => activePolicies.Count > 0;
    public bool PoliciesRunning => policiesRunning;

    /// <summary>This Round's Policies, in reveal order. Set in Awake and fixed for the load.</summary>
    public IReadOnlyList<PolicyDefinition> ActivePolicies => activePolicies;

    private void Awake()
    {
        if (gameTimeManager == null) gameTimeManager = FindFirstObjectByType<GameTimeManager>();
        if (roundController == null) roundController = FindFirstObjectByType<MetaRoundController>();
        if (effects == null || effects.Length == 0) effects = GetComponentsInChildren<PolicyEffect>(true);

        round = MatchSession.RoundNumber;
        ChoosePolicies();
    }

    private void OnEnable()
    {
        if (gameTimeManager != null)
        {
            gameTimeManager.OnMatchStarting += HandleMatchStarting;
            gameTimeManager.OnPreGameEntered += HandlePreGameEntered;
            gameTimeManager.OnPostGameEntered += HandlePostGameEntered;
        }

        if (roundController != null) roundController.LoadStarted += HandleLoadStarted;
    }

    private void OnDisable()
    {
        if (gameTimeManager != null)
        {
            gameTimeManager.OnMatchStarting -= HandleMatchStarting;
            gameTimeManager.OnPreGameEntered -= HandlePreGameEntered;
            gameTimeManager.OnPostGameEntered -= HandlePostGameEntered;
        }

        if (roundController != null) roundController.LoadStarted -= HandleLoadStarted;

        StopPolicies(PolicyStopReason.Teardown);
    }

    #region Choosing

    private void ChoosePolicies()
    {
        activePolicies.Clear();
        activeEffects.Clear();

        if (roundController == null)
        {
            if (logPolicies) Debug.Log("[Policy] No MetaRoundController in this scene: no Policies.", this);
            return;
        }

        if (gameTimeManager == null)
        {
            Debug.LogError("[Policy] GameTimeManager not found: Policies cannot start.", this);
            return;
        }

        PolicyRoundSource source = useFixedSchedule ? fixedSchedule : randomPool;

        if (source == null)
        {
            Debug.LogWarning(
                $"[Policy] {(useFixedSchedule ? "Fixed Schedule" : "Random Pool")} is empty: no Policies in Round {round}.",
                this
            );
            return;
        }

        source.GetPolicies(round, activePolicies);

        for (int i = 0; i < activePolicies.Count; i++)
        {
            activeEffects.Add(FindEffect(activePolicies[i]));
        }

        if (logPolicies)
            Debug.Log($"[Policy] Round {round} Policies: {FormatPolicies()} (from {source.name})", this);
    }

    private PolicyEffect FindEffect(PolicyDefinition policy)
    {
        PolicyEffect found = null;

        for (int i = 0; i < effects.Length; i++)
        {
            PolicyEffect effect = effects[i];
            if (effect == null || effect.Definition != policy) continue;

            if (found == null)
                found = effect;
            else
                Debug.LogWarning($"[Policy] {policy.DisplayName} has more than one effect; {found.name} is used.", effect);
        }

        if (found == null)
            Debug.LogWarning($"[Policy] {policy.DisplayName} has no effect component under {name}: it is revealed but does nothing.", this);

        return found;
    }

    #endregion

    #region Running

    private void HandleMatchStarting()
    {
        if (policiesRunning || activePolicies.Count == 0) return;

        policiesRunning = true;

        for (int i = 0; i < activePolicies.Count; i++)
        {
            PolicyEffect effect = activeEffects[i];
            if (effect == null || !effect.isActiveAndEnabled) continue;

            effect.StartPolicy(this);
            if (logPolicies) Debug.Log($"[Policy] Started: {activePolicies[i].DisplayName}", effect);
            PolicyStarted?.Invoke(activePolicies[i]);
        }
    }

    private void HandlePreGameEntered()
    {
        StopPolicies(PolicyStopReason.MatchStartFailed);
    }

    private void HandlePostGameEntered()
    {
        StopPolicies(PolicyStopReason.RoundEnded);
    }

    private void HandleLoadStarted()
    {
        StopPolicies(PolicyStopReason.LoadStarted);
    }

    private void StopPolicies(PolicyStopReason reason)
    {
        if (!policiesRunning) return;

        policiesRunning = false;

        for (int i = activePolicies.Count - 1; i >= 0; i--)
        {
            PolicyEffect effect = activeEffects[i];
            if (effect == null || !effect.IsRunning) continue;

            effect.StopPolicy(reason);
            if (logPolicies) Debug.Log($"[Policy] Stopped: {activePolicies[i].DisplayName} ({reason})", this);
            PolicyStopped?.Invoke(activePolicies[i], reason);
        }
    }

    internal void RaiseCue(PolicyEffect effect, string cue)
    {
        if (effect == null || effect.Definition == null) return;

        if (logPolicies) Debug.Log($"[Policy] Cue: {effect.Definition.DisplayName} '{cue}'", effect);
        PolicyCue?.Invoke(effect.Definition, cue);
    }

    #endregion

    private string FormatPolicies()
    {
        if (activePolicies.Count == 0) return "none";

        StringBuilder text = new StringBuilder(64);

        for (int i = 0; i < activePolicies.Count; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append(activePolicies[i].DisplayName);
        }

        return text.ToString();
    }
}
