using UnityEngine;

/// <summary>
/// Pushes REAL cart Hype + drift risk/reward preview into PlayerWorldHUDStateSystem.
///
/// Attach once under the leading-cart/player hierarchy.
///
/// Player slot resolution:
/// - Player Index Override 1..4 = explicit.
/// - 0 = automatically resolve by checking which PlayerWorldHUDSystem
///       bound player root contains this component.
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

    [Header("HUD Systems")]
    [SerializeField]
    private PlayerWorldHUDSystem hudSystem;

    [SerializeField]
    private PlayerWorldHUDStateSystem stateSystem;

    #endregion

    #region Player Resolution

    [Header("Player Slot")]
    [Tooltip(
        "0 = automatically resolve from PlayerWorldHUDSystem binding. " +
        "1..4 = force a specific player slot."
    )]
    [Range(0, PlayerWorldHUDStateSystem.MaxPlayerSlots)]
    [SerializeField]
    private int playerIndexOverride = 0;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private int resolvedPlayerIndex;

    #endregion

    #region Unity

    private void Awake()
    {
        if (cartController == null)
        {
            cartController =
                GetComponentInParent<
                    CartControlScript
                >();
        }

        if (driftHypeReward == null)
        {
            driftHypeReward =
                GetComponentInParent<
                    CartDriftHypeReward
                >();
        }

        if (hudSystem == null)
        {
            hudSystem =
                FindFirstObjectByType<
                    PlayerWorldHUDSystem
                >();
        }

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<
                    PlayerWorldHUDStateSystem
                >();
        }
    }

    private void OnEnable()
    {
        SubscribeHUDBinding();
        ResolvePlayerIndex();
    }

    private void OnDisable()
    {
        UnsubscribeHUDBinding();

        if (stateSystem != null &&
            resolvedPlayerIndex > 0)
        {
            stateSystem.ClearAllDriftPreviews(
                resolvedPlayerIndex
            );
        }
    }

    private void OnValidate()
    {
        playerIndexOverride =
            Mathf.Clamp(
                playerIndexOverride,
                0,
                PlayerWorldHUDStateSystem.MaxPlayerSlots
            );
    }

    private void LateUpdate()
    {
        if (stateSystem == null ||
            cartController == null)
        {
            return;
        }

        if (resolvedPlayerIndex <= 0)
        {
            ResolvePlayerIndex();

            if (resolvedPlayerIndex <= 0)
            {
                return;
            }
        }

        PushHypeState();
        PushDriftPreviewState();
    }

    #endregion

    #region HUD Binding

    private void SubscribeHUDBinding()
    {
        if (hudSystem == null)
        {
            return;
        }

        hudSystem.OnPlayerHUDBound +=
            HandlePlayerHUDBound;

        hudSystem.OnPlayerHUDUnbound +=
            HandlePlayerHUDUnbound;
    }

    private void UnsubscribeHUDBinding()
    {
        if (hudSystem == null)
        {
            return;
        }

        hudSystem.OnPlayerHUDBound -=
            HandlePlayerHUDBound;

        hudSystem.OnPlayerHUDUnbound -=
            HandlePlayerHUDUnbound;
    }

    private void HandlePlayerHUDBound(
        int playerIndex,
        GameObject playerRoot,
        Transform hudAnchor)
    {
        if (playerIndexOverride > 0)
        {
            resolvedPlayerIndex =
                playerIndexOverride;

            return;
        }

        if (IsInsidePlayerRoot(playerRoot))
        {
            resolvedPlayerIndex =
                playerIndex;
        }
    }

    private void HandlePlayerHUDUnbound(
        int playerIndex)
    {
        if (playerIndexOverride > 0)
        {
            return;
        }

        if (resolvedPlayerIndex == playerIndex)
        {
            resolvedPlayerIndex = 0;
        }
    }

    private void ResolvePlayerIndex()
    {
        if (playerIndexOverride > 0)
        {
            resolvedPlayerIndex =
                playerIndexOverride;

            return;
        }

        resolvedPlayerIndex = 0;

        if (hudSystem == null)
        {
            return;
        }

        for (
            int playerIndex = 1;
            playerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots;
            playerIndex++)
        {
            GameObject playerRoot =
                hudSystem.GetPlayerRoot(
                    playerIndex
                );

            if (!IsInsidePlayerRoot(playerRoot))
            {
                continue;
            }

            resolvedPlayerIndex =
                playerIndex;

            return;
        }
    }

    private bool IsInsidePlayerRoot(
        GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return false;
        }

        Transform rootTransform =
            playerRoot.transform;

        return transform == rootTransform ||
               transform.IsChildOf(rootTransform);
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
