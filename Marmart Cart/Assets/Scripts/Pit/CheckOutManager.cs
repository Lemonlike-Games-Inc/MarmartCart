using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Checkout V2 - Hybrid Manual / Automatic Checkout.
///
/// Intended player experience:
/// - once the cart reaches the checkout stop, the player may press Checkout
///   once per physical loaded follower cart;
/// - each successful press checks out that cart immediately;
/// - there is NO player-controlled quit;
/// - if the player stops pressing, a hidden automatic fallback begins after a
///   short delay and continues at a fixed interval;
/// - manual presses remain valid while automatic fallback is active and can
///   always make the session finish faster.
///
/// CartPitZone owns auto-entry / auto-exit movement.
/// SnakeCartManager owns physical follower removal.
/// CashScoreManager owns score/session rewards.
/// </summary>
[DisallowMultipleComponent]
public class CheckOutManager : MonoBehaviour
{
    #region Dependencies

    [Header("Dependencies")]
    [SerializeField] private SnakeCartManager enteredSnakeCartManager;
    [SerializeField] private CargoCapacityController enteredCargoController;
    [SerializeField] private CartPitZone myPitZone;
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private CheckoutCargoDisplay checkoutCargoDisplay;

    #endregion

    #region Hybrid Checkout Timing

    [Header("Hybrid Checkout Timing")]
    [Tooltip("Hidden backup. Countdown starts the exact moment manual checkout becomes available at the checkout stop.")]
    [Min(0f)]
    [SerializeField] private float autoFallbackDelay = 1.5f;

    [Tooltip("Once automatic fallback is active, this is the maximum wait between automatic cart checkouts.")]
    [Min(0.01f)]
    [SerializeField] private float autoCheckoutInterval = 0.55f;

    [Tooltip("Small pause after the final loaded cart before the lane begins auto-exit.")]
    [Min(0f)]
    [SerializeField] private float postCheckoutDelay = 0.2f;

    #endregion

    #region Feedback

    [Header("Checkout Feedback")]
    [SerializeField] private string checkoutCartSfxKey = "CheckoutSingle";

    #endregion

    #region State

    [Header("State")]
    [SerializeField] private bool isCheckingOut;
    [SerializeField] private bool isStationAvailable = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool manualCheckoutEnabled;
    [SerializeField] private bool autoFallbackActive;
    [SerializeField] private int checkoutPlayerIndex;
    [SerializeField] private int cartsCheckedOutThisSession;
    [SerializeField] private int cargoCheckedOutThisSession;
    [SerializeField] private int lastCompactionMovedEntryCount;
    [SerializeField] private float nextAutomaticCheckoutTime;

    private Coroutine hybridCheckoutRoutine;
    private int lastCheckoutFrame = -1;

    private readonly List<CargoEntry> checkoutEntryBuffer = new List<CargoEntry>(16);

    #endregion

    #region Public State

    public bool IsCheckingOut => isCheckingOut;
    public bool IsManualCheckoutEnabled => isCheckingOut && manualCheckoutEnabled;
    public bool IsAutoFallbackActive => isCheckingOut && autoFallbackActive;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // isStationAvailable now keeps its serialized/externally assigned state.
        // MatchFlowDirector may intentionally start this station closed.

        if (checkoutCargoDisplay == null)
        {
            checkoutCargoDisplay = GetComponentInChildren<CheckoutCargoDisplay>(true);
        }

        checkoutCargoDisplay?.ClearSession();
    }

    private void OnDisable()
    {
        StopHybridCheckoutRoutine();
        manualCheckoutEnabled = false;
        autoFallbackActive = false;

        checkoutCargoDisplay?.ClearSession();
    }

    private void OnValidate()
    {
        autoFallbackDelay = Mathf.Max(0f, autoFallbackDelay);
        autoCheckoutInterval = Mathf.Max(0.01f, autoCheckoutInterval);
        postCheckoutDelay = Mathf.Max(0f, postCheckoutDelay);
    }

    #endregion

    #region Setup

    public void SetSnakeCartManager(SnakeCartManager snakeCartManager)
    {
        enteredSnakeCartManager = snakeCartManager;
        enteredCargoController = enteredSnakeCartManager != null
            ? enteredSnakeCartManager.GetComponent<CargoCapacityController>()
            : null;
    }

    public void SetMyPitZone(CartPitZone pitZone)
    {
        myPitZone = pitZone;
    }

    public void SetCashScoreManager(CashScoreManager scoreManager)
    {
        cashScoreManager = scoreManager;
    }

    #endregion

    #region Session Start

    public void SetIsCheckingOut()
    {
        if (isCheckingOut) return;

        if (enteredSnakeCartManager == null || enteredCargoController == null)
        {
            Debug.LogError("[CheckOutManager] Cannot begin checkout: SnakeCartManager or CargoCapacityController is missing.", this);
            AbortCheckoutToExit();
            return;
        }

        if (!enteredCargoController.HasCheckoutCargo)
        {
            AbortCheckoutToExit();
            return;
        }

        checkoutPlayerIndex = enteredSnakeCartManager.GetPlayerId();

        if (cashScoreManager == null)
        {
            Debug.LogError("[CheckOutManager] CashScoreManager is missing. Checkout will not consume cargo without a scoring destination.", this);
            AbortCheckoutToExit();
            return;
        }

        // Checkout readiness is now one exact moment:
        // the leader has already reached the checkout stop before this method
        // is called. Compact synchronously, then enable manual input and begin
        // the hidden fallback countdown immediately.
        if (!enteredCargoController.CompactCargoForCheckout(out int movedEntries))
        {
            Debug.LogError("[CheckOutManager] Cargo checkout compaction failed. Checkout aborted safely.", this);
            AbortCheckoutToExit();
            return;
        }

        checkoutCargoDisplay?.BeginSession();

        isCheckingOut = true;
        manualCheckoutEnabled = true;
        autoFallbackActive = autoFallbackDelay <= 0f;

        cartsCheckedOutThisSession = 0;
        cargoCheckedOutThisSession = 0;
        lastCompactionMovedEntryCount = movedEntries;

        lastCheckoutFrame = -1;
        nextAutomaticCheckoutTime = Time.time + (autoFallbackActive ? 0f : autoFallbackDelay);

        StopHybridCheckoutRoutine();
        hybridCheckoutRoutine = StartCoroutine(HybridCheckoutRoutine());
    }

    private IEnumerator HybridCheckoutRoutine()
    {
        while (isCheckingOut)
        {
            if (!HasLoadedCargoRemaining()) break;

            if (!autoFallbackActive && Time.time >= nextAutomaticCheckoutTime)
            {
                autoFallbackActive = true;
                nextAutomaticCheckoutTime = Time.time;
            }

            if (autoFallbackActive && Time.time >= nextAutomaticCheckoutTime)
            {
                if (TryCheckoutNextCart())
                {
                    nextAutomaticCheckoutTime = Time.time + autoCheckoutInterval;
                }
                else if (!HasLoadedCargoRemaining())
                {
                    break;
                }
                else
                {
                    Debug.LogError("[CheckOutManager] Automatic checkout failed while loaded cargo still remains. Ending session safely.", this);
                    break;
                }
            }

            yield return null;
        }

        manualCheckoutEnabled = false;
        autoFallbackActive = false;

        // Let the final checked-out cart's visual wave actually land before
        // beginning the existing post-checkout hold / clearing the pile.
        while (checkoutCargoDisplay != null && checkoutCargoDisplay.IsAnimating)
        {
            yield return null;
        }

        if (postCheckoutDelay > 0f) yield return new WaitForSeconds(postCheckoutDelay);

        hybridCheckoutRoutine = null;

        if (isCheckingOut) FinishCheckoutSession();
    }

    #endregion

    #region Manual + Automatic Shared Operation

    /// <summary>
    /// Called by CartControlScript's existing CheckOut input action.
    /// One valid press consumes exactly one loaded physical follower cart.
    /// </summary>
    public void TryCheckoutCart()
    {
        if (!isCheckingOut || !manualCheckoutEnabled) return;

        if (!TryCheckoutNextCart()) return;

        // A successful manual press owns the immediate rhythm. Even after the
        // hidden fallback has activated, auto waits at most one normal interval
        // before helping again. Repeated presses cannot create a safe-zone stall
        // because every successful press permanently removes one loaded cart.
        nextAutomaticCheckoutTime = Time.time + autoCheckoutInterval;
    }

    private bool TryCheckoutNextCart()
    {
        if (!isCheckingOut || enteredCargoController == null) return false;

        // Prevent an input callback and the automatic fallback from consuming
        // two different carts during the same rendered frame.
        if (lastCheckoutFrame == Time.frameCount) return false;

        ChainCartCargo cargoCart = enteredCargoController.GetFirstLoadedCargoCart();
        if (cargoCart == null || cargoCart.CargoEntryCount <= 0) return false;

        if (!TryCheckoutCargoCart(cargoCart)) return false;

        lastCheckoutFrame = Time.frameCount;

        return true;
    }

    private bool TryCheckoutCargoCart(ChainCartCargo cargoCart)
    {
        if (cargoCart == null || enteredSnakeCartManager == null || cashScoreManager == null) return false;
        if (cargoCart.CargoEntryCount <= 0) return false;

        ChainedCartManager cartManager = cargoCart.GetComponentInParent<ChainedCartManager>();

        if (cartManager == null)
        {
            Debug.LogError("[CheckOutManager] Loaded ChainCartCargo has no owning ChainedCartManager.", cargoCart);
            return false;
        }

        checkoutEntryBuffer.Clear();

        IReadOnlyList<CargoEntry> entries = cargoCart.CargoEntries;

        for (int i = 0; i < entries.Count; i++)
        {
            CargoEntry entry = entries[i];
            if (entry != null) checkoutEntryBuffer.Add(entry);
        }

        if (checkoutEntryBuffer.Count <= 0) return false;

        // Consume topology first so scoring can never be duplicated if physical
        // cart removal fails.
        if (!enteredSnakeCartManager.TryRemoveFollowerForCheckout(cartManager)) return false;

        // Presentation-only copy. Uses the exact SelectedCargoVisual already
        // stored on each CargoEntry but does not mutate CargoEntry runtime state.
        checkoutCargoDisplay?.AddCargoWave(checkoutEntryBuffer);

        cashScoreManager.RegisterCargoCheckout(checkoutPlayerIndex, checkoutEntryBuffer);

        cartsCheckedOutThisSession++;
        cargoCheckedOutThisSession += checkoutEntryBuffer.Count;

        if (sfxManager != null && !string.IsNullOrEmpty(checkoutCartSfxKey))
        {
            sfxManager.PlaySFX(checkoutCartSfxKey);
        }

        return true;
    }

    private bool HasLoadedCargoRemaining()
    {
        if (enteredCargoController == null) return false;

        ChainCartCargo cargoCart = enteredCargoController.GetFirstLoadedCargoCart();
        return cargoCart != null && cargoCart.CargoEntryCount > 0;
    }

    #endregion

    #region Completion / Abort

    private void FinishCheckoutSession()
    {
        if (!isCheckingOut) return;

        isCheckingOut = false;
        manualCheckoutEnabled = false;
        autoFallbackActive = false;

        checkoutCargoDisplay?.ClearSession();

        if (myPitZone != null)
        {
            myPitZone.BeginAutoExitFromCheckout();
        }
        else
        {
            Debug.LogError("[CheckOutManager] CartPitZone reference is missing.", this);
            ClearEnteredPlayerState();
        }
    }

    private void AbortCheckoutToExit()
    {
        isCheckingOut = false;
        manualCheckoutEnabled = false;
        autoFallbackActive = false;

        StopHybridCheckoutRoutine();
        checkoutCargoDisplay?.ClearSession();

        if (myPitZone != null)
        {
            myPitZone.BeginAutoExitFromCheckout();
        }
        else
        {
            Debug.LogError("[CheckOutManager] CartPitZone reference is missing.", this);
            ClearEnteredPlayerState();
        }
    }


    /// <summary>
    /// Called by CartPitZone only after physical auto-exit is complete.
    /// </summary>
    public void NotifyPlayerExitedCheckoutLane()
    {
        StopHybridCheckoutRoutine();

        isCheckingOut = false;
        manualCheckoutEnabled = false;
        autoFallbackActive = false;

        checkoutCargoDisplay?.ClearSession();
        ClearEnteredPlayerState();
    }

    private void ClearEnteredPlayerState()
    {
        enteredSnakeCartManager = null;
        enteredCargoController = null;
        checkoutPlayerIndex = 0;
        nextAutomaticCheckoutTime = 0f;
        lastCheckoutFrame = -1;
    }

    #endregion

    #region Station State

    public void EnableStation()
    {
        isStationAvailable = true;
    }

    /// <summary>
    /// Prevents NEW players from entering this checkout station.
    /// An already-committed checkout session is allowed to finish normally.
    /// </summary>
    public void DisableStation()
    {
        isStationAvailable = false;
    }

    public bool IsStationAvailable()
    {
        return isStationAvailable;
    }

    #endregion

    #region Helpers

    private void StopHybridCheckoutRoutine()
    {
        if (hybridCheckoutRoutine == null) return;

        StopCoroutine(hybridCheckoutRoutine);
        hybridCheckoutRoutine = null;
    }

    #endregion
}
