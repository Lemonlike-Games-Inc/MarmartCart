using UnityEngine;

/// <summary>
/// One step of the pre-round that runs before the Round title, such as the
/// Ability pick (later the Policy pop and the Section reveal).
/// MetaRoundController runs its stages in list order while PreGame keeps the
/// world frozen, so a stage counts real time (Time.unscaledTime).
/// </summary>
public abstract class PreRoundStage : MonoBehaviour
{
    /// <summary>Whether this stage has anything to do before this Round.</summary>
    public abstract bool RunsBeforeRound(int round);

    /// <summary>Starts the stage. MetaRoundController then waits for IsFinished.</summary>
    public abstract void Begin(int round);

    /// <summary>True once the stage is done and the pre-round can move on.</summary>
    public abstract bool IsFinished { get; }

    /// <summary>A load started during the stage: stop at once and hide, without applying anything.</summary>
    public abstract void Cancel();
}
