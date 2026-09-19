using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Converts GameTimeManager's PostGame boundary into the complete in-scene
/// final-results ceremony.
///
/// Responsibilities:
/// - commit already-submitted checkout sessions;
/// - stop checkout/gameplay input and deterministic cart movement;
/// - hide follower chains and ordinary HUD presentation;
/// - move real leading carts onto authored result poses;
/// - switch every local viewport to its own results camera;
/// - start all score towers concurrently after one shared delay.
/// </summary>
[DisallowMultipleComponent]
public sealed class MatchResultsPresentationController : MonoBehaviour
{
    private const int MaxPlayers = CashScoreManager.MaxPlayers;

    [Serializable]
    private sealed class PlayerStageBinding
    {
        [Range(1, MaxPlayers)]
        public int playerIndex = 1;

        [Tooltip("Optional manual runtime source. Auto-resolved by GetPlayerId when empty.")]
        public SnakeCartManager snakeCartManager;

        [Tooltip("Exact final world position and rotation for this player's real leading cart.")]
        public Transform cartStagePose;

        public FinalScoreCargoTower cargoTower;

        [Tooltip("Optional manual camera binding. Auto-resolved by Player Index when empty.")]
        public PlayerCameraManager playerCameraManager;
    }

    #region References

    [Header("Core References")]
    [SerializeField] private GameTimeManager gameTimeManager;
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private MatchResultsCargoLedger cargoLedger;
    [SerializeField] private MatchResultsPresentationProfile profile;

    [Header("Results Scene Kit")]
    [Tooltip(
        "Lighting, authored cargo slots, score/rank text, spotlights, and other " +
        "results-only scenery. It starts disabled and is enabled at PostGame."
    )]
    [SerializeField] private GameObject resultsEnvironmentRoot;

    [SerializeField]
    private PlayerStageBinding[] playerStages =
        new PlayerStageBinding[MaxPlayers];

    [Header("HUD Transition")]
    [Tooltip("Kept active; only its expired timer is hidden in Results Mode.")]
    [SerializeField] private MatchViewportOverlayRenderer viewportOverlayRenderer;

    [Tooltip(
        "Ordinary HUD/VFX presenter GameObjects to hide for results. Do not put " +
        "the MatchViewportOverlayRenderer here because the final leaderboard remains."
    )]
    [SerializeField] private GameObject[] presentationObjectsToHide;

    [Tooltip(
        "Optional individual renderers/presenters to disable while keeping their GameObjects alive."
    )]
    [SerializeField] private Behaviour[] presentationBehavioursToDisable;

    #endregion

    #region Lock / Cleanup

    [Header("PostGame Lock")]
    [SerializeField] private bool hideFollowerCarts = true;

    [Tooltip(
        "Stops active checkout coroutines/auto-drive after already-submitted score is committed."
    )]
    [SerializeField] private bool disableCheckoutRuntime = true;

    [Tooltip(
        "Disables Rigidbody collision detection on staged leading carts so their authored " +
        "result poses cannot fight the arena or each other."
    )]
    [SerializeField] private bool disableStagedCartCollisions = true;

    #endregion

    #region Events / Runtime

    [Header("Feedback Events")]
    [SerializeField] private UnityEvent onResultsPresentationStarted;
    [SerializeField] private UnityEvent onAllScoreRevealsCompleted;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool resultsActive;
    [SerializeField] private bool allScoreRevealsComplete;
    [SerializeField] private int activePlayerCount;
    [SerializeField] private int stagedPlayerCount;

    private readonly int[] finalScores = new int[MaxPlayers];
    private readonly int[] finalRanks = new int[MaxPlayers];
    private readonly int[] rankOrder = new int[MaxPlayers] { 1, 2, 3, 4 };
    private readonly MatchResultsPlayerSnapshot[] snapshots =
        new MatchResultsPlayerSnapshot[MaxPlayers];

    private GameTimeManager subscribedGameTimeManager;
    private Coroutine presentationRoutine;

    public bool ResultsActive => resultsActive;
    public bool AllScoreRevealsComplete => allScoreRevealsComplete;
    public int ActivePlayerCount => activePlayerCount;

    public event Action OnResultsPresentationStarted;
    public event Action OnAllScoreRevealsCompleted;

    #endregion

    #region Unity

    private void Reset()
    {
        EnsurePlayerStageArray();
    }

    private void Awake()
    {
        EnsurePlayerStageArray();
        ResolveReferences();
        ResolveRuntimePlayerBindings();

        if (resultsEnvironmentRoot != null)
            resultsEnvironmentRoot.SetActive(false);

        viewportOverlayRenderer?.SetResultsMode(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGameTimeManager();
    }

    private void Start()
    {
        ResolveReferences();
        ResolveRuntimePlayerBindings();
        SubscribeToGameTimeManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameTimeManager();
        StopPresentationRoutine();
    }

    private void OnValidate()
    {
        EnsurePlayerStageArray();
    }

    #endregion

    #region Lifecycle

    private void HandleMatchStarted()
    {
        // The supported rematch flow reloads the gameplay scene. This reset is
        // still useful for direct-scene testing before any carts are staged.
        StopPresentationRoutine();
        resultsActive = false;
        allScoreRevealsComplete = false;
        stagedPlayerCount = 0;

        if (resultsEnvironmentRoot != null)
            resultsEnvironmentRoot.SetActive(false);

        viewportOverlayRenderer?.SetResultsMode(false);
    }

    private void HandlePostGameEntered()
    {
        if (resultsActive) return;

        StopPresentationRoutine();
        presentationRoutine = StartCoroutine(RunPresentation());
    }

    [ContextMenu("TEST - Start Final Results Presentation")]
    public void StartResultsPresentationNow()
    {
        HandlePostGameEntered();
    }

    private IEnumerator RunPresentation()
    {
        ResolveReferences();
        ResolveRuntimePlayerBindings();

        if (!ValidateRequiredSetup())
        {
            presentationRoutine = null;
            yield break;
        }

        resultsActive = true;
        allScoreRevealsComplete = false;
        stagedPlayerCount = 0;

        // An occupied checkout may have submitted real cargo but not yet driven
        // far enough to call EndCheckoutSession. Bank exactly that accepted work
        // before stopping checkout coroutines and capturing final scores.
        cashScoreManager.CommitAllActiveCheckoutSessions();

        if (disableCheckoutRuntime)
            DisableCheckoutRuntimeSystems();

        ApplyPresentationVisibility();

        if (resultsEnvironmentRoot != null)
            resultsEnvironmentRoot.SetActive(true);

        viewportOverlayRenderer?.SetResultsMode(true);

        activePlayerCount = Mathf.Clamp(
            GMode.Instance != null ? GMode.Instance.PlayerCount() : 2,
            1,
            MaxPlayers
        );

        CaptureFinalScoresAndRanks();

        for (int playerIndex = 1; playerIndex <= activePlayerCount; playerIndex++)
        {
            PlayerStageBinding stage = GetStage(playerIndex);
            if (stage == null) continue;

            if (!TryResolveLeadingCart(stage, out GameObject leadingCart))
            {
                Debug.LogError(
                    $"[MatchResultsPresentationController] Could not resolve Player {playerIndex}'s leading cart.",
                    this
                );
                continue;
            }

            LockAndStagePlayer(stage, leadingCart);
            stagedPlayerCount++;

            Transform focusTarget = stage.cargoTower != null
                ? stage.cargoTower.CameraFocusTarget
                : leadingCart.transform;

            stage.playerCameraManager?.EnterResultsPresentation(focusTarget);
        }

        onResultsPresentationStarted?.Invoke();
        OnResultsPresentationStarted?.Invoke();

        if (profile.CeremonyStartDelay > 0f)
            yield return new WaitForSecondsRealtime(profile.CeremonyStartDelay);

        int startedTowerCount = 0;

        for (int playerIndex = 1; playerIndex <= activePlayerCount; playerIndex++)
        {
            PlayerStageBinding stage = GetStage(playerIndex);
            if (stage == null || stage.cargoTower == null) continue;

            if (!TryResolveLeadingCart(stage, out GameObject leadingCart)) continue;

            MatchResultsPlayerSnapshot snapshot = snapshots[playerIndex - 1];

            if (stage.cargoTower.BeginPresentation(
                    leadingCart.transform,
                    snapshot,
                    profile))
            {
                startedTowerCount++;
            }
        }

        while (startedTowerCount > 0 && !AreAllStartedTowersComplete())
        {
            yield return null;
        }

        allScoreRevealsComplete = true;
        presentationRoutine = null;

        onAllScoreRevealsCompleted?.Invoke();
        OnAllScoreRevealsCompleted?.Invoke();
    }

    #endregion

    #region Final Score / Rank

    private void CaptureFinalScoresAndRanks()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            bool active = i < activePlayerCount;
            finalScores[i] = active
                ? Mathf.Max(0, cashScoreManager.GetPlayerScore(i + 1))
                : 0;
            finalRanks[i] = 0;
            rankOrder[i] = i + 1;
            snapshots[i] = null;
        }

        // Stable allocation-free sort: score descending, player index ascending.
        for (int i = 1; i < activePlayerCount; i++)
        {
            int keyPlayer = rankOrder[i];
            int keyScore = finalScores[keyPlayer - 1];
            int j = i - 1;

            while (j >= 0)
            {
                int comparedPlayer = rankOrder[j];
                int comparedScore = finalScores[comparedPlayer - 1];

                bool keyBefore =
                    keyScore > comparedScore ||
                    (keyScore == comparedScore && keyPlayer < comparedPlayer);

                if (!keyBefore) break;

                rankOrder[j + 1] = comparedPlayer;
                j--;
            }

            rankOrder[j + 1] = keyPlayer;
        }

        int denseRank = 1;
        int previousScore = int.MinValue;

        for (int row = 0; row < activePlayerCount; row++)
        {
            int playerIndex = rankOrder[row];
            int score = finalScores[playerIndex - 1];

            if (row == 0)
                denseRank = 1;
            else if (score < previousScore)
                denseRank++;

            finalRanks[playerIndex - 1] = denseRank;
            previousScore = score;
        }

        for (int playerIndex = 1; playerIndex <= activePlayerCount; playerIndex++)
        {
            snapshots[playerIndex - 1] = cargoLedger.CreateSnapshot(
                playerIndex,
                finalScores[playerIndex - 1],
                finalRanks[playerIndex - 1]
            );
        }
    }

    #endregion

    #region Cart Staging / Lock

    private void LockAndStagePlayer(
        PlayerStageBinding stage,
        GameObject leadingCart)
    {
        if (stage.snakeCartManager != null)
        {
            if (hideFollowerCarts)
            {
                List<GameObject> body = stage.snakeCartManager.GetSnakeBody();

                if (body != null)
                {
                    for (int i = 1; i < body.Count; i++)
                    {
                        if (body[i] != null) body[i].SetActive(false);
                    }
                }
            }

            stage.snakeCartManager.enabled = false;
        }

        CartControlScript control =
            leadingCart.GetComponentInChildren<CartControlScript>(true);

        if (control != null)
        {
            control.DisableControl();
            control.DisallowDrift();
            control.DisallowSpeedingUp();
            control.DisallowMoveBackward();
            control.SetPowerupInputEnabled(false);
            control.SetAimInputEnabled(false);
            control.SetActiveCheckoutHandler(null);
        }

        PlayerPowerupController powerupController =
            stage.snakeCartManager != null
                ? stage.snakeCartManager.GetComponentInChildren<PlayerPowerupController>(true)
                : leadingCart.GetComponentInChildren<PlayerPowerupController>(true);

        if (powerupController != null)
        {
            powerupController.SetUseBlocked(
                PowerupUseBlockReason.ExternallyDisabled,
                true
            );
        }

        SnakeMoveBackwardController moveBackwardController =
            stage.snakeCartManager != null
                ? stage.snakeCartManager.GetComponent<SnakeMoveBackwardController>()
                : null;

        if (moveBackwardController != null)
            moveBackwardController.enabled = false;

        CartDriftController[] driftControllers =
            leadingCart.GetComponentsInChildren<CartDriftController>(true);

        for (int i = 0; i < driftControllers.Length; i++)
        {
            CartDriftController drift = driftControllers[i];
            if (drift == null) continue;
            drift.InterruptDrift("Final results");
            drift.enabled = false;
        }

        LeadingCartBehaviour[] wheelBehaviours =
            leadingCart.GetComponentsInChildren<LeadingCartBehaviour>(true);

        for (int i = 0; i < wheelBehaviours.Length; i++)
        {
            LeadingCartBehaviour wheel = wheelBehaviours[i];
            if (wheel == null) continue;
            wheel.SetSpeedToZero();
            wheel.enabled = false;
        }

        LeadingCartStallController[] stallControllers =
            leadingCart.GetComponentsInChildren<LeadingCartStallController>(true);

        for (int i = 0; i < stallControllers.Length; i++)
        {
            LeadingCartStallController stall = stallControllers[i];
            if (stall == null) continue;
            stall.SetStallDetectionSuppressed(true);
            stall.enabled = false;
        }

        Rigidbody[] bodies = leadingCart.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null) continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            if (disableStagedCartCollisions)
                body.detectCollisions = false;
        }

        if (stage.cartStagePose != null)
        {
            leadingCart.transform.SetPositionAndRotation(
                stage.cartStagePose.position,
                stage.cartStagePose.rotation
            );
        }
    }

    private bool TryResolveLeadingCart(
        PlayerStageBinding stage,
        out GameObject leadingCart)
    {
        leadingCart = null;
        if (stage == null || stage.snakeCartManager == null) return false;

        List<GameObject> body = stage.snakeCartManager.GetSnakeBody();
        if (body == null || body.Count == 0 || body[0] == null) return false;

        leadingCart = body[0];
        return true;
    }

    #endregion

    #region Visibility / Checkout Stop

    private void ApplyPresentationVisibility()
    {
        if (presentationObjectsToHide != null)
        {
            for (int i = 0; i < presentationObjectsToHide.Length; i++)
            {
                GameObject target = presentationObjectsToHide[i];
                if (target != null) target.SetActive(false);
            }
        }

        if (presentationBehavioursToDisable != null)
        {
            for (int i = 0; i < presentationBehavioursToDisable.Length; i++)
            {
                Behaviour target = presentationBehavioursToDisable[i];
                if (target != null) target.enabled = false;
            }
        }
    }

    private static void DisableCheckoutRuntimeSystems()
    {
        CheckOutManager[] checkoutManagers =
            FindObjectsByType<CheckOutManager>(FindObjectsSortMode.None);

        for (int i = 0; i < checkoutManagers.Length; i++)
        {
            if (checkoutManagers[i] != null)
                checkoutManagers[i].enabled = false;
        }

        CartPitZone[] pitZones =
            FindObjectsByType<CartPitZone>(FindObjectsSortMode.None);

        for (int i = 0; i < pitZones.Length; i++)
        {
            if (pitZones[i] != null)
                pitZones[i].enabled = false;
        }
    }

    #endregion

    #region Binding / Validation

    private void ResolveReferences()
    {
        if (gameTimeManager == null)
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        if (cashScoreManager == null)
            cashScoreManager = FindFirstObjectByType<CashScoreManager>();

        if (cargoLedger == null)
            cargoLedger = MatchResultsCargoLedger.Instance;

        if (cargoLedger == null)
            cargoLedger = FindFirstObjectByType<MatchResultsCargoLedger>();

        if (viewportOverlayRenderer == null)
            viewportOverlayRenderer = FindFirstObjectByType<MatchViewportOverlayRenderer>();
    }

    private void ResolveRuntimePlayerBindings()
    {
        EnsurePlayerStageArray();

        SnakeCartManager[] snakes =
            FindObjectsByType<SnakeCartManager>(FindObjectsSortMode.None);

        PlayerCameraManager[] cameras =
            FindObjectsByType<PlayerCameraManager>(FindObjectsSortMode.None);

        FinalScoreCargoTower[] towers =
            resultsEnvironmentRoot != null
                ? resultsEnvironmentRoot.GetComponentsInChildren<FinalScoreCargoTower>(true)
                : FindObjectsByType<FinalScoreCargoTower>(FindObjectsSortMode.None);

        for (int i = 0; i < playerStages.Length; i++)
        {
            PlayerStageBinding stage = playerStages[i];
            if (stage == null) continue;

            int playerIndex = stage.playerIndex;

            if (stage.snakeCartManager == null)
            {
                for (int j = 0; j < snakes.Length; j++)
                {
                    if (snakes[j] != null && snakes[j].GetPlayerId() == playerIndex)
                    {
                        stage.snakeCartManager = snakes[j];
                        break;
                    }
                }
            }

            if (stage.playerCameraManager == null)
            {
                for (int j = 0; j < cameras.Length; j++)
                {
                    if (cameras[j] != null && cameras[j].PlayerIndex == playerIndex)
                    {
                        stage.playerCameraManager = cameras[j];
                        break;
                    }
                }
            }

            if (stage.cargoTower == null)
            {
                for (int j = 0; j < towers.Length; j++)
                {
                    if (towers[j] != null && towers[j].PlayerIndex == playerIndex)
                    {
                        stage.cargoTower = towers[j];
                        break;
                    }
                }
            }
        }
    }

    private bool ValidateRequiredSetup()
    {
        bool valid = true;

        if (cashScoreManager == null)
        {
            Debug.LogError("[MatchResultsPresentationController] CashScoreManager is missing.", this);
            valid = false;
        }

        if (cargoLedger == null)
        {
            Debug.LogError("[MatchResultsPresentationController] MatchResultsCargoLedger is missing.", this);
            valid = false;
        }

        if (profile == null)
        {
            Debug.LogError("[MatchResultsPresentationController] Results profile is missing.", this);
            valid = false;
        }

        if (resultsEnvironmentRoot == null)
        {
            Debug.LogError("[MatchResultsPresentationController] Results Environment Root is missing.", this);
            valid = false;
        }

        int expectedPlayers = Mathf.Clamp(
            GMode.Instance != null ? GMode.Instance.PlayerCount() : 2,
            1,
            MaxPlayers
        );

        for (int playerIndex = 1; playerIndex <= expectedPlayers; playerIndex++)
        {
            PlayerStageBinding stage = GetStage(playerIndex);

            if (stage == null ||
                stage.cartStagePose == null ||
                stage.cargoTower == null ||
                stage.snakeCartManager == null)
            {
                Debug.LogError(
                    $"[MatchResultsPresentationController] Player {playerIndex} needs a SnakeCartManager, Cart Stage Pose, and Cargo Tower.",
                    this
                );
                valid = false;
            }
        }

        return valid;
    }

    private PlayerStageBinding GetStage(int playerIndex)
    {
        if (playerStages == null) return null;

        for (int i = 0; i < playerStages.Length; i++)
        {
            PlayerStageBinding stage = playerStages[i];
            if (stage != null && stage.playerIndex == playerIndex) return stage;
        }

        return null;
    }

    private bool AreAllStartedTowersComplete()
    {
        for (int playerIndex = 1; playerIndex <= activePlayerCount; playerIndex++)
        {
            PlayerStageBinding stage = GetStage(playerIndex);

            if (stage != null &&
                stage.cargoTower != null &&
                stage.cargoTower.IsPresenting)
            {
                return false;
            }
        }

        return true;
    }

    private void EnsurePlayerStageArray()
    {
        if (playerStages == null || playerStages.Length != MaxPlayers)
        {
            PlayerStageBinding[] oldStages = playerStages;
            playerStages = new PlayerStageBinding[MaxPlayers];

            if (oldStages != null)
            {
                int copyCount = Mathf.Min(oldStages.Length, MaxPlayers);
                for (int i = 0; i < copyCount; i++) playerStages[i] = oldStages[i];
            }
        }

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (playerStages[i] == null) playerStages[i] = new PlayerStageBinding();
            playerStages[i].playerIndex = i + 1;
        }
    }

    private void SubscribeToGameTimeManager()
    {
        if (subscribedGameTimeManager == gameTimeManager) return;

        UnsubscribeFromGameTimeManager();
        subscribedGameTimeManager = gameTimeManager;

        if (subscribedGameTimeManager == null) return;

        subscribedGameTimeManager.OnMatchStarted += HandleMatchStarted;
        subscribedGameTimeManager.OnPostGameEntered += HandlePostGameEntered;
    }

    private void UnsubscribeFromGameTimeManager()
    {
        if (subscribedGameTimeManager == null) return;

        subscribedGameTimeManager.OnMatchStarted -= HandleMatchStarted;
        subscribedGameTimeManager.OnPostGameEntered -= HandlePostGameEntered;
        subscribedGameTimeManager = null;
    }

    private void StopPresentationRoutine()
    {
        if (presentationRoutine == null) return;
        StopCoroutine(presentationRoutine);
        presentationRoutine = null;
    }

    #endregion
}
