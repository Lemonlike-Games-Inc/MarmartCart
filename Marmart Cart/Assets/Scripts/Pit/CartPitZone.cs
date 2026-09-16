using UnityEngine;

/// <summary>
/// Checkout V2 lane capture and automatic lane driving.
///
/// Flow:
/// 1) a player's LEADING cart enters this generous capture trigger;
/// 2) validate that it has real CargoEntries and is travelling generally in
///    the lane's intended direction;
/// 3) remove player driving control and silently suppress battle resolution;
/// 4) auto-drive the leading Rigidbody through Entry Waypoints;
/// 5) stop at the final entry waypoint and run hybrid manual/automatic checkout;
/// 6) auto-drive through Exit Waypoints;
/// 7) restore control and apply the normal visible post-checkout Ghost Mode.
///
/// The actual cargo checkout loop is owned by CheckOutManager.
/// </summary>
[DisallowMultipleComponent]
public class CartPitZone : MonoBehaviour
{
    private const int MaxSupportedPlayers = 4;

    private enum CheckoutLaneState
    {
        Idle,
        AutoEntering,
        CheckingOut,
        AutoExiting
    }

    #region Entry Capture

    [Header("Entry Capture")]
    [Tooltip("Required horizontal travel direction. Leave zero to use this trigger's forward direction.")]
    [SerializeField] private Vector3 requiredEntryDirection;

    [Tooltip("Dot-product threshold against actual planar travel direction. 0.2 accepts roughly within 78 degrees of lane forward.")]
    [Range(-1f, 1f)]
    [SerializeField] private float directionThreshold = 0.2f;

    [Tooltip("Below this planar speed, cart facing direction is used as the fallback intent direction.")]
    [Min(0f)]
    [SerializeField] private float minimumVelocityForDirectionCheck = 0.5f;

    #endregion

    #region Auto Drive Path

    [Header("Auto Drive - Entry")]
    [Tooltip("Ordered points used after capture. The FINAL point is the checkout stop position.")]
    [SerializeField] private Transform[] entryWaypoints;

    [Header("Auto Drive - Exit")]
    [Tooltip(
        "Ordered guide points used after checkout. Intermediate points are normal auto-drive waypoints. " +
        "The FINAL point is a CONTROL HANDOFF point: the cart does not stop or snap there. " +
        "Control returns while preserving forward exit momentum."
    )]
    [SerializeField] private Transform[] exitWaypoints;

    [Header("Auto Drive Tuning")]
    [Min(0.1f)]
    [SerializeField] private float autoDriveSpeed = 8f;

    [Min(1f)]
    [SerializeField] private float autoDriveRotationSpeed = 240f;

    [Min(0.01f)]
    [SerializeField] private float waypointReachDistance = 0.15f;

    [Tooltip("When reaching the final waypoint, snap to its authored rotation before starting the next phase.")]
    [SerializeField] private bool snapToFinalWaypointRotation = true;

    #endregion

    #region Checkout

    [Header("Checkout")]
    [Min(0f)]
    [SerializeField] private float ghostDurationAfterCheckout = 3f;

    [Tooltip("Checkout lane number (1..4). Used by camera selection.")]
    [Range(1, 4)]
    [SerializeField] private int myLaneNumber = 1;

    #endregion

    #region Camera

    [Header("Per-Player Camera Managers (index 0..3 = P1..P4)")]
    [SerializeField] private PlayerCameraManager[] playerCameraManagers = new PlayerCameraManager[MaxSupportedPlayers];

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private CheckOutManager checkOutManager;
    [Tooltip("Optional scene reference. Automatically resolved when left empty.")]
    [SerializeField] private PlayerWorldHUDSystem playerWorldHUDSystem;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private CheckoutLaneState laneState = CheckoutLaneState.Idle;
    [SerializeField] private bool stationOccupied;
    [SerializeField] private int occupyingPlayerIndex;
    [SerializeField] private int currentWaypointIndex;

    private SnakeCartManager enteredSnakeCartManager;
    private CargoCapacityController enteredCargoController;

    private GameObject enteredLeader;
    private Rigidbody enteredLeaderBody;
    private CartControlScript enteredCartController;
    private PlayerPowerupController enteredPowerupController;
    private LeadingCartBehaviour[] enteredWheelBehaviours;
    private LeadingCartBattleController enteredBattleController;
    private SnakeMoveBackwardController enteredMoveBackwardController;

    private bool leaderWasKinematic;

    // Last meaningful planar direction used by auto-drive.
    // During AutoExiting this becomes the momentum direction handed back to
    // the player at the final exit waypoint.
    private Vector3 lastAutoDriveDirection;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Range is enforced here as well as in OnValidate so runtime-created or
        // legacy instances can never pass a lane outside the supported 1..4 set.
        myLaneNumber = Mathf.Clamp(myLaneNumber, 1, 4);

        if (checkOutManager == null) checkOutManager = GetComponent<CheckOutManager>();
        if (playerWorldHUDSystem == null)
            playerWorldHUDSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();

        if (checkOutManager == null)
        {
            Debug.LogError("[CartPitZone] CheckOutManager is missing on this checkout station.", this);
        }
    }

    private void Start()
    {
        if (checkOutManager == null) return;

        checkOutManager.SetMyPitZone(this);
        checkOutManager.SetCashScoreManager(cashScoreManager);
    }

    private void FixedUpdate()
    {
        switch (laneState)
        {
            case CheckoutLaneState.AutoEntering:
                TickAutoDrive(entryWaypoints, BeginCheckoutAtStop, true);
                break;

            case CheckoutLaneState.AutoExiting:
                TickAutoDrive(exitWaypoints, FinishCheckoutExit, false);
                break;
        }
    }

    private void OnDisable()
    {
        if (!stationOccupied) return;

        SetOccupyingPlayerHUDSuppressed(false);
        RestorePlayerControl(false);
        ClearRuntimeCheckoutState();
    }

    #endregion

    #region Entry Capture

    private void OnTriggerEnter(Collider other)
    {
        if (stationOccupied || laneState != CheckoutLaneState.Idle) return;
        if (checkOutManager == null || !checkOutManager.IsStationAvailable()) return;
        if (!TryResolveLeadingCart(other, out SnakeCartManager snakeManager, out GameObject leader)) return;

        CargoCapacityController cargoController = snakeManager.GetComponent<CargoCapacityController>();
        if (cargoController == null || !cargoController.HasCheckoutCargo) return;

        int playerIndex = snakeManager.GetPlayerId();
        if (playerIndex < 1 || playerIndex > MaxSupportedPlayers) return;

        int activePlayers = GMode.Instance != null ? GMode.Instance.PlayerCount() : 2;
        if (playerIndex > activePlayers) return;

        Rigidbody leaderBody = leader.GetComponent<Rigidbody>();
        if (leaderBody == null) return;

        if (!PassesEntryDirection(leader.transform, leaderBody)) return;

        CartControlScript cartControl = leader.GetComponentInChildren<CartControlScript>(true);
        PlayerPowerupController powerupController =
            snakeManager.GetComponentInChildren<PlayerPowerupController>(true);
        LeadingCartBehaviour[] wheelBehaviours = leader.GetComponentsInChildren<LeadingCartBehaviour>(true);
        LeadingCartBattleController battleController = leader.GetComponentInChildren<LeadingCartBattleController>(true);
        SnakeMoveBackwardController moveBackwardController = snakeManager.GetComponent<SnakeMoveBackwardController>();

        if (cartControl == null)
        {
            Debug.LogError("[CartPitZone] Leading cart is missing CartControlScript.", leader);
            return;
        }

        if (wheelBehaviours == null || wheelBehaviours.Length == 0)
        {
            Debug.LogError("[CartPitZone] Leading cart has no LeadingCartBehaviour wheel components.", leader);
            return;
        }

        // Do not fight over locomotion ownership with MoveBackward.
        if (moveBackwardController != null && moveBackwardController.IsMovingBackward) return;

        if (!HasValidWaypointPath(entryWaypoints, "Entry")) return;
        if (!HasValidWaypointPath(exitWaypoints, "Exit")) return;

        stationOccupied = true;
        occupyingPlayerIndex = playerIndex;

        enteredSnakeCartManager = snakeManager;
        enteredCargoController = cargoController;

        enteredLeader = leader;
        enteredLeaderBody = leaderBody;
        enteredCartController = cartControl;
        enteredPowerupController = powerupController;
        enteredWheelBehaviours = wheelBehaviours;
        enteredBattleController = battleController;
        enteredMoveBackwardController = moveBackwardController;

        BeginCheckoutCapture();
    }

    private bool TryResolveLeadingCart(Collider other, out SnakeCartManager snakeManager, out GameObject leader)
    {
        snakeManager = null;
        leader = null;

        if (other == null) return false;

        snakeManager = other.GetComponentInParent<SnakeCartManager>();
        if (snakeManager == null) return false;

        var snakeBody = snakeManager.GetSnakeBody();
        if (snakeBody == null || snakeBody.Count == 0 || snakeBody[0] == null) return false;

        leader = snakeBody[0];

        Rigidbody leaderBody = leader.GetComponent<Rigidbody>();

        if (leaderBody != null && other.attachedRigidbody == leaderBody) return true;

        return other.transform == leader.transform || other.transform.IsChildOf(leader.transform);
    }

    private bool PassesEntryDirection(Transform leaderTransform, Rigidbody leaderBody)
    {
        if (leaderTransform == null || leaderBody == null) return false;

        Vector3 requiredDirection = Vector3.ProjectOnPlane(requiredEntryDirection, Vector3.up);

        if (requiredDirection.sqrMagnitude < 0.0001f)
        {
            requiredDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (requiredDirection.sqrMagnitude < 0.0001f) return false;
        requiredDirection.Normalize();

        Vector3 intentDirection = Vector3.ProjectOnPlane(leaderBody.linearVelocity, Vector3.up);

        if (intentDirection.magnitude < minimumVelocityForDirectionCheck)
        {
            intentDirection = Vector3.ProjectOnPlane(leaderTransform.forward, Vector3.up);
        }

        if (intentDirection.sqrMagnitude < 0.0001f) return false;
        intentDirection.Normalize();

        return Vector3.Dot(intentDirection, requiredDirection) >= directionThreshold;
    }

    #endregion

    #region Capture / Control Ownership

    private void BeginCheckoutCapture()
    {
        laneState = CheckoutLaneState.AutoEntering;
        currentWaypointIndex = 0;
        lastAutoDriveDirection = Vector3.zero;

        SetOccupyingPlayerHUDSuppressed(true);

        PlayerCameraManager cameraManager = GetPlayerCameraManager(occupyingPlayerIndex);
        cameraManager?.EnterCheckoutLane(myLaneNumber);

        if (cashScoreManager != null)
        {
            cashScoreManager.StartCheckoutSession(occupyingPlayerIndex);
        }

        enteredCartController.SetInPit();
        enteredCartController.DisableControl();
        enteredCartController.DisallowSpeedingUp();

        // Checkout is one independent power-up blocker. Clearing it on exit
        // must not accidentally clear Freeze, match lifecycle, or a future
        // active-effect lock. Preserve the legacy input toggle only while old
        // player prefabs are being migrated.
        if (enteredPowerupController != null)
        {
            enteredPowerupController.SetUseBlocked(
                PowerupUseBlockReason.Checkout,
                true
            );
        }
        else
        {
            enteredCartController.DisallowActivatePowerUp();
        }

        enteredCartController.SetActiveCheckoutHandler(null);

        if (enteredBattleController != null)
        {
            enteredBattleController.SetCheckoutBattleSuppressed(true);
        }

        StopWheelDrive();

        leaderWasKinematic = enteredLeaderBody.isKinematic;

        enteredLeaderBody.linearVelocity = Vector3.zero;
        enteredLeaderBody.angularVelocity = Vector3.zero;
        enteredLeaderBody.isKinematic = true;
    }

    private void BeginCheckoutAtStop()
    {
        laneState = CheckoutLaneState.CheckingOut;

        if (enteredLeaderBody != null)
        {
            enteredLeaderBody.linearVelocity = Vector3.zero;
            enteredLeaderBody.angularVelocity = Vector3.zero;
        }

        // Driving remains disabled, but CartControlScript's checkout action is
        // independent from controllable/isInPit. Installing the handler here
        // makes only the Checkout button meaningful during the stopped session.
        if (enteredCartController != null)
        {
            enteredCartController.SetActiveCheckoutHandler(checkOutManager);
        }

        checkOutManager.SetSnakeCartManager(enteredSnakeCartManager);
        checkOutManager.SetIsCheckingOut();
    }

    /// <summary>
    /// Called by CheckOutManager after automatic cart processing is complete.
    /// Checkout protection remains active while the station auto-drives the
    /// player out of the lane.
    /// </summary>
    public void BeginAutoExitFromCheckout()
    {
        if (!stationOccupied) return;
        if (laneState == CheckoutLaneState.AutoExiting) return;

        if (enteredCartController != null)
        {
            enteredCartController.SetActiveCheckoutHandler(null);
        }

        laneState = CheckoutLaneState.AutoExiting;
        currentWaypointIndex = 0;
        lastAutoDriveDirection = Vector3.zero;
    }

    #endregion

    #region Auto Drive

    /// <summary>
    /// Drives the kinematic leader through an authored path.
    ///
    /// Entry path:
    /// stopAtFinalWaypoint = true
    /// -> final point is a real stop used to begin checkout.
    ///
    /// Exit path:
    /// stopAtFinalWaypoint = false
    /// -> final point is only the control-handoff threshold.
    /// -> the cart is NOT snapped/stopped there.
    /// -> FinishCheckoutExit() restores control with preserved exit momentum.
    /// </summary>
    private void TickAutoDrive(Transform[] waypoints, System.Action onPathFinished, bool stopAtFinalWaypoint)
    {
        if (enteredLeaderBody == null)
        {
            AbortCheckoutSession();
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            AbortCheckoutSession();
            return;
        }

        while (currentWaypointIndex < waypoints.Length && waypoints[currentWaypointIndex] == null)
        {
            currentWaypointIndex++;
        }

        if (currentWaypointIndex >= waypoints.Length)
        {
            onPathFinished?.Invoke();
            return;
        }

        Transform target = waypoints[currentWaypointIndex];

        Vector3 currentPosition = enteredLeaderBody.position;
        Vector3 targetPosition = target.position;

        // Checkout waypoints author X/Z path and yaw. Keep the leader's current
        // physics floor height so tiny waypoint Y errors cannot pop the cart.
        targetPosition.y = currentPosition.y;

        Vector3 planarToTarget = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
        float planarDistance = planarToTarget.magnitude;

        bool isFinalPoint = currentWaypointIndex == waypoints.Length - 1;

        // EXIT FINAL POINT:
        // This is not a stop. As soon as the auto-driven cart reaches the
        // handoff radius, restore normal player control without MovePosition()
        // snapping to the waypoint and without zeroing the exit velocity.
        if (isFinalPoint && !stopAtFinalWaypoint && planarDistance <= waypointReachDistance)
        {
            onPathFinished?.Invoke();
            return;
        }

        // Normal intermediate waypoint, or the checkout-stop point on ENTRY.
        if (planarDistance <= waypointReachDistance)
        {
            enteredLeaderBody.MovePosition(targetPosition);

            if (isFinalPoint && stopAtFinalWaypoint && snapToFinalWaypointRotation)
            {
                Quaternion flatTargetRotation = GetFlatWaypointRotation(target, enteredLeaderBody.rotation);
                enteredLeaderBody.MoveRotation(flatTargetRotation);
            }

            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Length)
            {
                onPathFinished?.Invoke();
            }

            return;
        }

        Vector3 desiredForward = planarToTarget.normalized;
        lastAutoDriveDirection = desiredForward;

        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            autoDriveSpeed * Time.fixedDeltaTime
        );

        enteredLeaderBody.MovePosition(nextPosition);

        if (desiredForward.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                enteredLeaderBody.rotation,
                desiredRotation,
                autoDriveRotationSpeed * Time.fixedDeltaTime
            );

            enteredLeaderBody.MoveRotation(nextRotation);
        }
    }

    private Quaternion GetFlatWaypointRotation(Transform waypoint, Quaternion fallback)
    {
        if (waypoint == null) return fallback;

        Vector3 forward = Vector3.ProjectOnPlane(waypoint.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f) return fallback;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    #endregion

    #region Exit / Release

    private void FinishCheckoutExit()
    {
        if (!stationOccupied) return;

        int exitingPlayerIndex = occupyingPlayerIndex;

        PlayerCameraManager cameraManager = GetPlayerCameraManager(exitingPlayerIndex);
        cameraManager?.ExitCheckout();

        SetOccupyingPlayerHUDSuppressed(false);

        if (cashScoreManager != null)
        {
            cashScoreManager.EndCheckoutSession(exitingPlayerIndex);
        }

        if (checkOutManager != null)
        {
            checkOutManager.NotifyPlayerExitedCheckoutLane();
        }

        RestorePlayerControl(true, true);
        ClearRuntimeCheckoutState();
    }

    private void RestorePlayerControl(bool applyExitGhost, bool preserveExitMomentum = false)
    {
        Vector3 exitVelocity = Vector3.zero;

        if (preserveExitMomentum)
        {
            Vector3 handoffDirection = GetExitHandoffDirection();

            if (handoffDirection.sqrMagnitude > 0.0001f)
            {
                exitVelocity = handoffDirection.normalized * autoDriveSpeed;
            }
        }

        if (enteredLeaderBody != null)
        {
            enteredLeaderBody.isKinematic = leaderWasKinematic;

            if (!enteredLeaderBody.isKinematic)
            {
                // Normal abort/disable restores to a safe stop.
                // Successful checkout EXIT instead receives the auto-drive
                // velocity so the cart flows directly back into player control.
                enteredLeaderBody.linearVelocity = preserveExitMomentum ? exitVelocity : Vector3.zero;
                enteredLeaderBody.angularVelocity = Vector3.zero;
            }
        }

        if (enteredCartController != null)
        {
            enteredCartController.SetOutPit();
            enteredCartController.EnableControl();
            enteredCartController.AllowSpeedingUp();

            if (enteredPowerupController != null)
            {
                enteredPowerupController.SetUseBlocked(
                    PowerupUseBlockReason.Checkout,
                    false
                );
            }
            else
            {
                enteredCartController.AllowActivatePowerUp();
            }

            enteredCartController.SetActiveCheckoutHandler(null);
        }

        ResetWheelDrive();

        if (enteredBattleController != null)
        {
            enteredBattleController.SetCheckoutBattleSuppressed(false);

            if (applyExitGhost && ghostDurationAfterCheckout > 0f)
            {
                enteredBattleController.SetGhostMode(ghostDurationAfterCheckout);
            }
        }
    }

    /// <summary>
    /// Prefer the actual direction the exit auto-drive was travelling.
    /// If the path was extremely short and never established one, fall back to
    /// the final exit waypoint's authored forward, then the leader's forward.
    /// </summary>
    private Vector3 GetExitHandoffDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(lastAutoDriveDirection, Vector3.up);

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        if (exitWaypoints != null)
        {
            for (int i = exitWaypoints.Length - 1; i >= 0; i--)
            {
                Transform waypoint = exitWaypoints[i];
                if (waypoint == null) continue;

                direction = Vector3.ProjectOnPlane(waypoint.forward, Vector3.up);

                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }
        }

        if (enteredLeader != null)
        {
            direction = Vector3.ProjectOnPlane(enteredLeader.transform.forward, Vector3.up);

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
        }

        return Vector3.zero;
    }

    private void AbortCheckoutSession()
    {
        if (!stationOccupied) return;

        Debug.LogError("[CartPitZone] Checkout auto-drive aborted because its runtime path/state became invalid.", this);

        int playerIndex = occupyingPlayerIndex;

        SetOccupyingPlayerHUDSuppressed(false);

        if (cashScoreManager != null)
        {
            cashScoreManager.EndCheckoutSession(playerIndex);
        }

        if (checkOutManager != null)
        {
            checkOutManager.NotifyPlayerExitedCheckoutLane();
        }

        RestorePlayerControl(false);
        ClearRuntimeCheckoutState();
    }

    private void ClearRuntimeCheckoutState()
    {
        laneState = CheckoutLaneState.Idle;
        stationOccupied = false;
        occupyingPlayerIndex = 0;
        currentWaypointIndex = 0;

        enteredSnakeCartManager = null;
        enteredCargoController = null;

        enteredLeader = null;
        enteredLeaderBody = null;
        enteredCartController = null;
        enteredPowerupController = null;
        enteredWheelBehaviours = null;
        enteredBattleController = null;
        enteredMoveBackwardController = null;

        leaderWasKinematic = false;
        lastAutoDriveDirection = Vector3.zero;
    }

    #endregion

    #region Wheel Drive

    private void StopWheelDrive()
    {
        if (enteredWheelBehaviours == null) return;

        for (int i = 0; i < enteredWheelBehaviours.Length; i++)
        {
            if (enteredWheelBehaviours[i] != null) enteredWheelBehaviours[i].SetSpeedToZero();
        }
    }

    private void ResetWheelDrive()
    {
        if (enteredWheelBehaviours == null) return;

        for (int i = 0; i < enteredWheelBehaviours.Length; i++)
        {
            if (enteredWheelBehaviours[i] != null) enteredWheelBehaviours[i].ResetSpeed();
        }
    }

    #endregion

    #region Helpers

    private bool HasValidWaypointPath(Transform[] waypoints, string pathName)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError($"[CartPitZone] {pathName} Waypoints must contain at least one Transform.", this);
            return false;
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null) return true;
        }

        Debug.LogError($"[CartPitZone] {pathName} Waypoints contains no valid Transform.", this);
        return false;
    }

    private PlayerCameraManager GetPlayerCameraManager(int playerIndex)
    {
        int index = playerIndex - 1;

        if (playerCameraManagers == null || index < 0 || index >= playerCameraManagers.Length) return null;

        return playerCameraManagers[index];
    }

    private void SetOccupyingPlayerHUDSuppressed(bool suppressed)
    {
        if (occupyingPlayerIndex <= 0) return;

        if (playerWorldHUDSystem == null)
            playerWorldHUDSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();

        if (playerWorldHUDSystem == null) return;

        playerWorldHUDSystem.SetCheckoutSuppressed(
            occupyingPlayerIndex,
            suppressed
        );
    }

    #endregion

    #region Gizmos / Validation

    private void OnValidate()
    {
        directionThreshold = Mathf.Clamp(directionThreshold, -1f, 1f);
        minimumVelocityForDirectionCheck = Mathf.Max(0f, minimumVelocityForDirectionCheck);

        autoDriveSpeed = Mathf.Max(0.1f, autoDriveSpeed);
        autoDriveRotationSpeed = Mathf.Max(1f, autoDriveRotationSpeed);
        waypointReachDistance = Mathf.Max(0.01f, waypointReachDistance);

        myLaneNumber = Mathf.Clamp(myLaneNumber, 1, 4);
        ghostDurationAfterCheckout = Mathf.Max(0f, ghostDurationAfterCheckout);
    }

    private void OnDrawGizmos()
    {
        DrawDirectionGizmo();
        DrawWaypointPath(entryWaypoints);
        DrawWaypointPath(exitWaypoints);
    }

    private void DrawDirectionGizmo()
    {
        Vector3 direction = Vector3.ProjectOnPlane(requiredEntryDirection, Vector3.up);

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        Gizmos.DrawLine(transform.position, transform.position + direction * 4f);
        Gizmos.DrawSphere(transform.position + direction * 4f, 0.1f);
    }

    private void DrawWaypointPath(Transform[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Vector3 previous = transform.position;
        bool hasPrevious = false;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform waypoint = waypoints[i];
            if (waypoint == null) continue;

            if (hasPrevious) Gizmos.DrawLine(previous, waypoint.position);

            Gizmos.DrawSphere(waypoint.position, 0.08f);

            previous = waypoint.position;
            hasPrevious = true;
        }
    }

    #endregion
}
