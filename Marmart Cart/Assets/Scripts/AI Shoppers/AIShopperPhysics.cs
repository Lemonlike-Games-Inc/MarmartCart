using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Physical hit / knockout handling for an AI shopper.
///
/// Player ownership is resolved through SnakeCartManager instead of relying on
/// the exact collider GameObject carrying Player1/Player2/etc. tags.
///
/// This supports the refactored cart hierarchy:
/// ChainOfCarts
/// ├── Leading Cart
/// │   └── child physical colliders
/// ├── C1
/// ├── C2
/// └── ...
///
/// A hit from either the leader or any collected follower resolves back to the
/// owning SnakeCartManager and therefore the correct player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class AIShopperPhysics : MonoBehaviour
{
    #region Reward Settings

    [Header("Reward Settings")]
    [SerializeField] private float rewardMeterAmount = 2f;

    #endregion

    #region Knockout Settings

    [Header("Knockout Settings")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float upwardForce = 5f;
    [SerializeField] private float spinTorque = 5f;
    [SerializeField] private float destructionDelay = 2f;

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private GameObjectPool targetPool;
    [SerializeField] private SfxManager sfxManager;

    private Rigidbody rb;
    private NavMeshAgent navAgent;
    private AIShopperBehaviour shopperBehaviour;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isKnockedOut;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        navAgent = GetComponent<NavMeshAgent>();
        shopperBehaviour = GetComponent<AIShopperBehaviour>();

        if (shopperBehaviour == null)
        {
            Debug.LogError("[AIShopperPhysics] AIShopperBehaviour is missing.", this);
        }

        if (navAgent == null)
        {
            Debug.LogError("[AIShopperPhysics] NavMeshAgent is missing.", this);
        }
    }

    #endregion

    #region Hit Detection

    private void OnTriggerEnter(Collider other)
    {
        if (isKnockedOut || other == null) return;

        SnakeCartManager attackingSnake = other.GetComponentInParent<SnakeCartManager>();
        if (attackingSnake == null) return;

        int playerIndex = attackingSnake.GetPlayerId();
        if (playerIndex < 1 || playerIndex > 4) return;

        RewardAttackingPlayer(attackingSnake);
        PlayHitSfx();

        rb.isKinematic = false;

        if (shopperBehaviour != null)
        {
            shopperBehaviour.OnKnockOut(playerIndex);
        }

        KnockOut();
    }

    private void RewardAttackingPlayer(SnakeCartManager attackingSnake)
    {
        if (attackingSnake == null) return;

        var snakeBody = attackingSnake.GetSnakeBody();
        if (snakeBody == null || snakeBody.Count == 0 || snakeBody[0] == null) return;

        CartControlScript cartControl = snakeBody[0].GetComponentInChildren<CartControlScript>(true);
        if (cartControl != null) cartControl.AddHype(rewardMeterAmount);
    }

    private void PlayHitSfx()
    {
        if (sfxManager == null) return;

        sfxManager.PlaySFX(Random.value < 0.5f ? "HitCharacter1" : "HitCharacter2");
    }

    #endregion

    #region Knockout

    private void KnockOut()
    {
        if (rb == null || isKnockedOut) return;

        isKnockedOut = true;

        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            1f,
            Random.Range(-1f, 1f)
        ).normalized;

        Vector3 knockback = randomDirection * knockbackForce + Vector3.up * upwardForce;
        rb.AddForce(knockback, ForceMode.Impulse);

        Vector3 randomTorque = new Vector3(
            Random.Range(-spinTorque, spinTorque),
            Random.Range(-spinTorque, spinTorque),
            Random.Range(-spinTorque, spinTorque)
        );

        rb.AddTorque(randomTorque, ForceMode.Impulse);

        DisableAI();
        StartCoroutine(ReturnToPool());
    }

    private void DisableAI()
    {
        if (navAgent != null) navAgent.enabled = false;
        if (shopperBehaviour != null) shopperBehaviour.enabled = false;
    }

    private IEnumerator ReturnToPool()
    {
        yield return new WaitForSeconds(destructionDelay);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        if (shopperBehaviour != null)
        {
            shopperBehaviour.ResetState();
            shopperBehaviour.enabled = true;
        }

        if (navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        isKnockedOut = false;

        if (targetPool != null)
        {
            targetPool.ReturnObject(gameObject);
        }
        else
        {
            Debug.LogWarning("[AIShopperPhysics] Target Pool is not assigned.", this);
        }
    }

    #endregion

    #region Public API

    public bool IsKnockedOut()
    {
        return isKnockedOut;
    }

    #endregion
}
