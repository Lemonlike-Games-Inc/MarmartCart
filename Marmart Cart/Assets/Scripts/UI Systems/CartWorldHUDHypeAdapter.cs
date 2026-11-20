using UnityEngine;

/// <summary>
/// Pushes REAL cart Hype + drift risk/reward preview into PlayerWorldHUDStateSystem.
///
/// Attach once under the leading-cart hierarchy. The player number comes from
/// the parent SnakeCartManager; a cart outside a player's chain pushes nothing.
///
/// This adapter intentionally owns ONLY:
/// - Hype
/// - drift success preview
/// - drift failure preview
///
/// It does not overwrite Load / Speed / Streak state.
/// </summary>
[DisallowMultipleComponent]
public class CartWorldHUDHypeAdapter : MonoBehaviour
{
    #region References

    [Header("Cart")]
    [SerializeField]
    private CartControlScript cartController;

    [SerializeField]
    private CartDriftHypeReward driftHypeReward;

    [Header("HUD State")]
    [SerializeField]
    private PlayerWorldHUDStateSystem stateSystem;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private int resolvedPlayerIndex;

    #endregion

    #region Unity

    private void Awake()
    {
        if (cartController == null)
        {
            cartController = GetComponentInParent<CartControlScript>();
        }

        if (driftHypeReward == null)
        {
            driftHypeReward = GetComponentInParent<CartDriftHypeReward>();
        }

        if (stateSystem == null)
        {
            stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        }

        SnakeCartManager owner = GetComponentInParent<SnakeCartManager>();
        resolvedPlayerIndex = owner != null ? owner.GetPlayerId() : 0;
    }

    private void OnDisable()
    {
        if (stateSystem != null &&
            resolvedPlayerIndex > 0)
        {
            stateSystem.ClearAllDriftPreviews(
                resolvedPlayerIndex
            );
        }
    }

    private void LateUpdate()
    {
        if (stateSystem == null ||
            cartController == null ||
            resolvedPlayerIndex <= 0)
        {
            return;
        }

        PushHypeState();
        PushDriftPreviewState();
    }

    #endregion

    #region State Push

    private void PushHypeState()
    {
        stateSystem.SetHype(
            resolvedPlayerIndex,
            cartController.CurrentHype,
            cartController.MaxHype,
            cartController.CurrentHypeBurnMultiplier,
            cartController.CurrentHypeBurnPerSecond,
            cartController.IsSpeedingUp()
        );
    }

    private void PushDriftPreviewState()
    {
        if (driftHypeReward == null ||
            !driftHypeReward.IsTrackingDrift ||
            !driftHypeReward.HasPendingReward)
        {
            stateSystem.ClearAllDriftPreviews(
                resolvedPlayerIndex
            );

            return;
        }

        stateSystem.SetDriftRewardPreview(
            resolvedPlayerIndex,
            true,
            driftHypeReward.EffectivePendingHypeReward
        );

        stateSystem.SetDriftPenaltyPreview(
            resolvedPlayerIndex,
            driftHypeReward.FailedDriftHypePenaltyEnabled,
            driftHypeReward.PotentialFailedDriftHypePenalty
        );
    }

    #endregion
}
