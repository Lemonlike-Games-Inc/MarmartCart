using System.Collections;
using UnityEngine;

/// <summary>
/// Resolves battle contacts from the leading cart's front battle trigger shape.
///
/// Normal battle rule:
/// - Hit your own non-vulnerable collected cart -> attacker loses.
/// - Hit another player's non-vulnerable collected cart -> attacker loses.
/// - Hit another player's leading cart -> attacker loses.
/// - Loose/uncollected carts do not count.
///
/// Vulnerable exception:
/// - If the contacted collected follower is Vulnerable, the attacker does NOT lose.
/// - The hit vulnerable cart itself is detached.
/// - Every defender cart behind that vulnerable cart is also detached and becomes loose.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class LeadingCartBattleController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private Rigidbody cartBody;

    [Tooltip("The four LeadingCartBehaviour virtual wheels that drive this Rigidbody.")]
    [SerializeField] private LeadingCartBehaviour[] wheelMovements;

    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private CartMaterialManager cartMaterialManager;

    private SnakeCartManager snakeCartManager;
    private SnakeMoveBackwardController moveBackwardController;

    #endregion

    #region Battle Loss Settings

    [Header("Battle Loss")]
    [Tooltip("How long this leading cart ignores further battle losses after losing.")]
    [Min(0f)]
    [SerializeField] private float ghostDuration = 4f;

    [Tooltip("How long wheel drive stays stopped after losing.")]
    [Min(0f)]
    [SerializeField] private float stopDuration = 2f;

    [Tooltip("Single centralized knockback impulse applied to the leading Rigidbody.")]
    [Min(0f)]
    [SerializeField] private float knockbackImpulse = 200f;

    #endregion

    #region Vulnerable Hit Settings

    [Header("Vulnerable Chain Hit")]
    [Tooltip("Short gate preventing compound battle triggers from resolving several vulnerable hits at once.")]
    [Min(0f)]
    [SerializeField] private float vulnerableHitCooldown = 0.12f;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private float ghostUntilTime;
    [SerializeField] private float nextVulnerableHitTime;
    [SerializeField] private bool checkoutBattleSuppressed;

    private Coroutine stopRoutine;

    public bool IsInGhostMode => Time.time < ghostUntilTime;
    public bool IsCheckoutBattleSuppressed => checkoutBattleSuppressed;
    public bool IsBattleDetectionSuppressed =>
        checkoutBattleSuppressed ||
        (moveBackwardController != null && moveBackwardController.IsMovingBackward);

    #endregion

    #region Events

    public System.Action OnBattleLost;
    public System.Action<ChainedCartManager, int> OnVulnerableCartHit;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartBody == null) Debug.LogError("[LeadingCartBattleController] Cart Rigidbody is not assigned.", this);

        if ((wheelMovements == null || wheelMovements.Length == 0) && cartBody != null)
        {
            wheelMovements = cartBody.GetComponentsInChildren<LeadingCartBehaviour>(true);
        }

        if (cartMaterialManager == null && cartBody != null)
        {
            cartMaterialManager = cartBody.GetComponentInChildren<CartMaterialManager>(true);
        }

        ValidateTriggerColliders();
    }

    private void Start()
    {
        snakeCartManager = GetComponentInParent<SnakeCartManager>();
        moveBackwardController = GetComponentInParent<SnakeMoveBackwardController>();

        if (snakeCartManager == null)
        {
            Debug.LogError("[LeadingCartBattleController] Could not find runtime SnakeCartManager owner.", this);
        }

        if (moveBackwardController == null)
        {
            Debug.LogWarning("[LeadingCartBattleController] SnakeMoveBackwardController was not found. MoveBackward battle suppression will be unavailable.", this);
        }
    }

    private void OnDisable()
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
        }

        ResetWheelMovement();
    }

    #endregion

    #region Battle Detection

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || IsInGhostMode || IsBattleDetectionSuppressed) return;

        // Ignore colliders belonging to this same leading-cart Rigidbody.
        if (other.attachedRigidbody == cartBody) return;

        ChainedCartManager contactedCart = other.GetComponentInParent<ChainedCartManager>();

        if (contactedCart != null && contactedCart.isCollectedByPlayer)
        {
            if (contactedCart.IsVulnerable)
            {
                ResolveVulnerableCartHit(contactedCart);
                return;
            }

            LoseBattle();
            return;
        }

        if (IsOtherLeadingCart(other)) LoseBattle();
    }

    private bool IsOtherLeadingCart(Collider other)
    {
        Rigidbody otherBody = other.attachedRigidbody;

        if (otherBody == null || otherBody == cartBody) return false;

        LeadingCartBattleController otherBattleController = otherBody.GetComponentInChildren<LeadingCartBattleController>(true);

        return otherBattleController != null && otherBattleController != this;
    }

    #endregion

    #region Vulnerable Hit Resolution

    private void ResolveVulnerableCartHit(ChainedCartManager vulnerableCart)
    {
        if (vulnerableCart == null || !vulnerableCart.IsVulnerable) return;
        if (Time.time < nextVulnerableHitTime) return;

        SnakeCartManager defenderSnake = vulnerableCart.GetComponentInParent<SnakeCartManager>();

        if (defenderSnake == null)
        {
            Debug.LogError("[LeadingCartBattleController] Vulnerable cart has no owning SnakeCartManager.", vulnerableCart);
            return;
        }

        nextVulnerableHitTime = Time.time + vulnerableHitCooldown;

        int detachedCount = defenderSnake.DetachFromVulnerableCart(vulnerableCart);

        OnVulnerableCartHit?.Invoke(vulnerableCart, detachedCount);
    }

    #endregion

    #region Battle Loss Resolution

    public void LoseBattle()
    {
        if (IsInGhostMode) return;

        EnterGhostMode(ghostDuration);
        DetachOwnChain();
        StopAndKnockBackCart();

        OnBattleLost?.Invoke();
    }

    private void DetachOwnChain()
    {
        if (snakeCartManager == null) return;

        snakeCartManager.DetachAllFollowers();
    }

    private void StopAndKnockBackCart()
    {
        if (cartBody == null) return;

        Vector3 knockbackDirection = GetSafeKnockbackDirection();

        if (stopRoutine != null) StopCoroutine(stopRoutine);
        stopRoutine = StartCoroutine(StopCartRoutine(knockbackDirection));
    }

    private IEnumerator StopCartRoutine(Vector3 knockbackDirection)
    {
        StopWheelMovement();

        cartBody.linearVelocity = Vector3.zero;

        if (knockbackImpulse > 0f)
        {
            cartBody.AddForce(knockbackDirection * knockbackImpulse, ForceMode.Impulse);
        }

        if (stopDuration > 0f) yield return new WaitForSeconds(stopDuration);

        ResetWheelMovement();
        stopRoutine = null;
    }

    private void StopWheelMovement()
    {
        if (wheelMovements == null) return;

        for (int i = 0; i < wheelMovements.Length; i++)
        {
            if (wheelMovements[i] != null) wheelMovements[i].SetSpeedToZero();
        }
    }

    private void ResetWheelMovement()
    {
        if (wheelMovements == null) return;

        for (int i = 0; i < wheelMovements.Length; i++)
        {
            if (wheelMovements[i] != null) wheelMovements[i].ResetSpeed();
        }
    }

    private Vector3 GetSafeKnockbackDirection()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);

        if (planarVelocity.sqrMagnitude > 0.001f) return -planarVelocity.normalized;

        Vector3 backward = -Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);

        if (backward.sqrMagnitude > 0.001f) return backward.normalized;

        return -Vector3.forward;
    }

    #endregion

    #region Silent Checkout Suppression

    /// <summary>
    /// Silently disables battle resolution while checkout owns this leader's
    /// movement. Unlike Ghost Mode, this does not trigger any material effect.
    /// </summary>
    public void SetCheckoutBattleSuppressed(bool suppressed)
    {
        checkoutBattleSuppressed = suppressed;
    }

    #endregion

    #region Ghost / Cooldown

    public void SetGhostMode(float duration)
    {
        EnterGhostMode(duration);
    }

    private void EnterGhostMode(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);

        ghostUntilTime = Mathf.Max(ghostUntilTime, Time.time + safeDuration);

        if (cartMaterialManager != null && safeDuration > 0f)
        {
            cartMaterialManager.SetGhostMode(safeDuration);
        }
    }

    #endregion

    #region Validation

    private void ValidateTriggerColliders()
    {
        Collider[] colliders = GetComponents<Collider>();

        if (colliders.Length == 0)
        {
            Debug.LogError("[LeadingCartBattleController] No battle trigger colliders found on this GameObject.", this);
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
            {
                Debug.LogError("[LeadingCartBattleController] Every battle detection Collider must have Is Trigger enabled.", colliders[i]);
            }
        }
    }

    #endregion
}
