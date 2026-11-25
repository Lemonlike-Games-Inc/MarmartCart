using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector hooks for one Policy: sound, VFX or announcer lines when its card
/// is revealed, when it starts and ends, and on its cues. Wire each hook in the
/// Inspector, for example to SfxManager.PlaySFX or a Feel player. Code should
/// subscribe to MarmartPolicyDirector and PolicyRevealStage instead.
///
/// Sits on the Policy's own effect object (Meta Game Controller/Marmart Policies/...).
/// More than one can sit there, for example one for sound and one for VFX.
/// Cue names are listed on each effect; the console also logs every cue as
/// "[Policy] Cue: name 'cue'".
/// </summary>
public sealed class PolicyUnityEventRelay : MonoBehaviour
{
    [Serializable]
    private sealed class CueHook
    {
        [Tooltip("Blackout: lights-warning, lights-out, lights-on. Tomato Rain: rain-warning, rain-start, rain-end.")]
        public string cue;

        public UnityEvent onCue = new UnityEvent();
    }

    [Header("Source")]
    [Tooltip("Auto-found (parents first) if left empty.")]
    [SerializeField] private MarmartPolicyDirector director;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PolicyRevealStage revealStage;

    [Header("Filter")]
    [Tooltip("The Policy these hooks are for. Empty: every Policy.")]
    [SerializeField] private PolicyDefinition policy;

    [Header("Hooks")]
    [Tooltip("This Policy's card flips in the pre-round reveal.")]
    [SerializeField] private UnityEvent onRevealed = new UnityEvent();

    [Tooltip("The Round starts with this Policy on (at the end of the countdown).")]
    [SerializeField] private UnityEvent onStarted = new UnityEvent();

    [Tooltip("The Round with this Policy ends. Not called on a load or a menu return.")]
    [SerializeField] private UnityEvent onRoundEnded = new UnityEvent();

    [Tooltip("Moments inside the Round, named by the Policy's effect.")]
    [SerializeField] private CueHook[] cues = new CueHook[0];

    private void Awake()
    {
        if (director == null) director = GetComponentInParent<MarmartPolicyDirector>();
        if (director == null) director = FindFirstObjectByType<MarmartPolicyDirector>();
        if (revealStage == null) revealStage = FindFirstObjectByType<PolicyRevealStage>();
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.PolicyStarted += HandleStarted;
            director.PolicyStopped += HandleStopped;
            director.PolicyCue += HandleCue;
        }

        if (revealStage != null) revealStage.CardRevealed += HandleCardRevealed;
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.PolicyStarted -= HandleStarted;
            director.PolicyStopped -= HandleStopped;
            director.PolicyCue -= HandleCue;
        }

        if (revealStage != null) revealStage.CardRevealed -= HandleCardRevealed;
    }

    private bool Matches(PolicyDefinition definition)
    {
        return policy == null || definition == policy;
    }

    private void HandleCardRevealed(int index, PolicyDefinition definition)
    {
        if (Matches(definition)) onRevealed.Invoke();
    }

    private void HandleStarted(PolicyDefinition definition)
    {
        if (Matches(definition)) onStarted.Invoke();
    }

    // Only a real Round end: on a load or teardown, sound targets may already be gone.
    private void HandleStopped(PolicyDefinition definition, PolicyStopReason reason)
    {
        if (reason == PolicyStopReason.RoundEnded && Matches(definition)) onRoundEnded.Invoke();
    }

    private void HandleCue(PolicyDefinition definition, string cue)
    {
        if (!Matches(definition)) return;

        for (int i = 0; i < cues.Length; i++)
        {
            CueHook hook = cues[i];
            if (hook != null && string.Equals(hook.cue, cue, StringComparison.Ordinal)) hook.onCue.Invoke();
        }
    }
}
