using UnityEngine;

public enum PolicyStopReason
{
    /// <summary>The Round ended (PostGame), also after the final Round.</summary>
    RoundEnded,

    /// <summary>The match flow failed to start and PreGame came back.</summary>
    MatchStartFailed,

    /// <summary>A load began: next Round, F1 to F4, or the main menu.</summary>
    LoadStarted,

    /// <summary>The director or this effect was disabled or destroyed (scene unload).</summary>
    Teardown
}

/// <summary>
/// What one Policy does during a Round. One component per Policy, a child of
/// MarmartPolicyDirector in the gameplay scene, referencing its definition.
/// The director starts it when the Round starts and stops it on every exit.
///
/// Rules for every effect:
/// - Never assume it is the only Policy running.
/// - Change other systems only through add/remove handles, and release them in OnPolicyStopped.
/// - OnPolicyStopped also runs during scene teardown: no coroutines, no new objects.
/// </summary>
public abstract class PolicyEffect : MonoBehaviour
{
    [Tooltip("The Policy this component runs.")]
    [SerializeField] private PolicyDefinition definition;

    private MarmartPolicyDirector director;

    public PolicyDefinition Definition => definition;
    public bool IsRunning { get; private set; }

    internal void StartPolicy(MarmartPolicyDirector owner)
    {
        if (IsRunning) return;

        director = owner;
        IsRunning = true;
        OnPolicyStarted();
    }

    internal void StopPolicy(PolicyStopReason reason)
    {
        if (!IsRunning) return;

        IsRunning = false;
        OnPolicyStopped(reason);
        director = null;
    }

    /// <summary>The Round started. Gameplay time runs from here.</summary>
    protected abstract void OnPolicyStarted();

    /// <summary>Release every handle and hide everything. No coroutines.</summary>
    protected abstract void OnPolicyStopped(PolicyStopReason reason);

    /// <summary>
    /// A moment the announcer and sound can react to, such as "rain incoming"
    /// or "lights out". Passed on by the director's PolicyCue event.
    /// </summary>
    protected void RaiseCue(string cue)
    {
        if (director != null) director.RaiseCue(this, cue);
    }

    protected virtual void OnDisable()
    {
        StopPolicy(PolicyStopReason.Teardown);
    }
}
