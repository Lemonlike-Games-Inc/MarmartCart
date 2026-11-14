using UnityEngine;
using UnityEngine.AI;

public class AIShopperBehaviour : MonoBehaviour
{
    public AIState currentState = AIState.Wandering;

    [Header("Movement")]
    [SerializeField] private float collectRange = 5f;
    [SerializeField] private float wanderRange = 20f;
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float escapeSpeedMultiplier = 2f;

    [Header("Carrying Cart Visuals")]
    [SerializeField] private GameObject commonVisual;
    [SerializeField] private GameObject rareVisual;
    [SerializeField] private GameObject epicVisual;
    [SerializeField] private GameObject legendaryVisual;
    [SerializeField] private GameObject emptyCartVisual;
    [SerializeField] private GameObject itemCartVisual;
    [SerializeField] private GameObject expensiveItemCartVisual;

    [Header("Related Events")]
    private const int MaxSupportedPlayers = 4;
    [SerializeField] private GameEvent[] collectEmptyCartEvent = new GameEvent[MaxSupportedPlayers];
    [SerializeField] private GameEvent[] collectNormalGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];
    [SerializeField] private GameEvent[] collectExpensiveGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];

    [Header("Starting Carry Chance")]
    [Tooltip("Chance that a newly spawned AI starts already carrying an empty cart.")]
    [Range(0f, 1f)]
    [SerializeField] private float startingItemChance = 0.5f;

    [Header("Runtime Carry State")]
    [SerializeField] private bool carryingItem;
    [SerializeField] private bool carryingNormalItem;
    [SerializeField] private bool carryingExpensiveItem;

    [Header("Pool")]
    [SerializeField] private GameObjectPool targetPool;

    private NavMeshAgent agent;
    private Transform targetItem;
    private Transform targetExit;

    private GameObject runningVFX;
    private GameObject hittingVFX;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent != null) agent.speed = baseSpeed;
    }

    private void Start()
    {
        // Keep these old hierarchy references for tonight so no prefab setup changes are required.
        if (transform.childCount > 0)
        {
            Transform visualRoot = transform.GetChild(0);

            if (visualRoot.childCount > 0) runningVFX = visualRoot.GetChild(0).gameObject;
            if (visualRoot.childCount > 1) hittingVFX = visualRoot.GetChild(1).gameObject;
        }

        if (runningVFX != null) runningVFX.SetActive(false);
        if (hittingVFX != null) hittingVFX.SetActive(false);

        if (commonVisual != null) commonVisual.SetActive(false);
        if (rareVisual != null) rareVisual.SetActive(false);
        if (epicVisual != null) epicVisual.SetActive(false);
        if (legendaryVisual != null) legendaryVisual.SetActive(false);

        // Do NOT roll here. AIGenerationScript calls PrepareForSpawn().
        // Just make the current carry state visually correct in case this Start
        // happens after the pool has already prepared the AI.
        ApplyCarryVisuals();
    }

    private void Update()
    {
        switch (currentState)
        {
            case AIState.Seeking:
                SeekTarget();
                break;

            case AIState.Collecting:
                CollectItem();
                break;

            case AIState.Escaping:
                Escape();
                break;

            case AIState.Wandering:
                Wander();
                break;
        }
    }

    private void SeekTarget()
    {
        if (targetItem == null)
        {
            currentState = AIState.Wandering;
            return;
        }

        ChainedCartManager itemManager = targetItem.GetComponent<ChainedCartManager>();

        if (itemManager != null && !itemManager.isAvailable)
        {
            targetItem = null;
            currentState = AIState.Wandering;
            return;
        }

        agent.SetDestination(targetItem.position);

        if (Vector3.Distance(transform.position, targetItem.position) <= collectRange)
        {
            currentState = AIState.Collecting;
        }
    }

    private void CollectItem()
    {
        if (targetItem == null)
        {
            currentState = AIState.Wandering;
            return;
        }

        ChainedCartManager itemManager = targetItem.GetComponent<ChainedCartManager>();

        if (itemManager != null)
        {
            //itemManager.CollectByAI();

            // All three collected-cart cases now consistently mean
            // carryingItem = true.
            carryingItem = true;

            //if (itemManager.HasGroceryItem() && itemManager.isCarryingNormalGroceryItem())
            //{
            //    carryingNormalItem = true;
            //    carryingExpensiveItem = false;
            //}
            //else if (itemManager.HasGroceryItem() && itemManager.isCarryingExpensiveGroceryItem())
            //{
            //    carryingNormalItem = false;
            //    carryingExpensiveItem = true;
            //}
            //else
            //{
            //    // Empty cart.
            //    carryingNormalItem = false;
            //    carryingExpensiveItem = false;
            //}

            ApplyCarryVisuals();
        }

        Destroy(targetItem.gameObject);
        targetItem = null;

        FindNearestExit();

        if (agent != null) agent.speed = baseSpeed * escapeSpeedMultiplier;

        currentState = AIState.Escaping;
    }

    private void Escape()
    {
        if (targetExit != null)
        {
            if (runningVFX != null) runningVFX.SetActive(true);

            agent.SetDestination(targetExit.position);

            if (Vector3.Distance(transform.position, targetExit.position) <= collectRange)
            {
                ResetState();

                if (targetPool != null) targetPool.ReturnObject(gameObject);
            }
        }
        else
        {
            if (runningVFX != null) runningVFX.SetActive(false);
            currentState = AIState.Wandering;
        }
    }

    private void Wander()
    {
        if (agent == null || !agent.enabled || agent.hasPath) return;

        Vector3 randomDirection = Random.insideUnitSphere * wanderRange + transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, wanderRange, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState != AIState.Wandering || !other.CompareTag("Item")) return;

        ChainedCartManager itemManager = other.GetComponent<ChainedCartManager>();
        if (itemManager != null && !itemManager.isAvailable) return;

        targetItem = other.transform;
        currentState = AIState.Seeking;
    }

    private void FindRandomTargetItem()
    {
        GameObject[] items = GameObject.FindGameObjectsWithTag("Item");

        if (items.Length == 0)
        {
            currentState = AIState.Wandering;
            return;
        }

        // Try a few random carts so pooled/collected carts are not selected.
        int attempts = Mathf.Min(items.Length, 10);

        for (int i = 0; i < attempts; i++)
        {
            GameObject candidate = items[Random.Range(0, items.Length)];
            ChainedCartManager itemManager = candidate.GetComponent<ChainedCartManager>();

            if (itemManager != null && !itemManager.isAvailable) continue;

            targetItem = candidate.transform;
            currentState = AIState.Seeking;
            return;
        }

        currentState = AIState.Wandering;
    }

    private void FindNearestExit()
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        float shortestDistance = Mathf.Infinity;
        targetExit = null;

        foreach (GameObject exit in exits)
        {
            float distance = Vector3.Distance(transform.position, exit.transform.position);

            if (distance >= shortestDistance) continue;

            shortestDistance = distance;
            targetExit = exit.transform;
        }
    }

    /// <summary>
    /// Called once when this AI is actually taken from the pool for a new spawn.
    /// This is the ONLY place that rolls the starting 50% cart chance.
    /// </summary>
    public void PrepareForSpawn()
    {
        ResetState();

        carryingItem = Random.value < startingItemChance;
        carryingNormalItem = false;
        carryingExpensiveItem = false;

        ApplyCarryVisuals();
        FindRandomTargetItem();
    }

    /// <summary>
    /// Called when hit by a player's cart.
    /// A reward is only raised when this AI is actually carrying something.
    /// Starting generic carry = empty cart reward.
    /// </summary>
    public void OnKnockOut(int playerIndex)
    {
        if (hittingVFX != null) hittingVFX.SetActive(true);
        if (runningVFX != null) runningVFX.SetActive(false);

        int arrayIndex = playerIndex - 1;

        if (arrayIndex >= 0 && arrayIndex < MaxSupportedPlayers && carryingItem)
        {
            if (carryingNormalItem)
            {
                collectNormalGroceryItemCartEvent[arrayIndex]?.Raise();
            }
            else if (carryingExpensiveItem)
            {
                collectExpensiveGroceryItemCartEvent[arrayIndex]?.Raise();
            }
            else
            {
                // Generic carryingItem means this AI has an empty cart.
                collectEmptyCartEvent[arrayIndex]?.Raise();
            }
        }

        ClearCarryState();
    }

    /// <summary>
    /// Resets reusable AI state but DOES NOT roll starting equipment.
    /// AIGenerationScript calls PrepareForSpawn() when the AI is spawned again.
    /// </summary>
    public void ResetState()
    {
        currentState = AIState.Wandering;

        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = baseSpeed;
            agent.ResetPath();
        }

        targetItem = null;
        targetExit = null;

        if (runningVFX != null) runningVFX.SetActive(false);
        if (hittingVFX != null) hittingVFX.SetActive(false);

        ClearCarryState();
    }

    private void ClearCarryState()
    {
        carryingItem = false;
        carryingNormalItem = false;
        carryingExpensiveItem = false;

        ApplyCarryVisuals();
    }

    private void ApplyCarryVisuals()
    {
        // Generic item = empty cart.
        bool showEmptyCart = carryingItem && !carryingNormalItem && !carryingExpensiveItem;
        bool showNormalItem = carryingItem && carryingNormalItem;
        bool showExpensiveItem = carryingItem && carryingExpensiveItem;

        if (emptyCartVisual != null) emptyCartVisual.SetActive(showEmptyCart);
        if (itemCartVisual != null) itemCartVisual.SetActive(showNormalItem);
        if (expensiveItemCartVisual != null) expensiveItemCartVisual.SetActive(showExpensiveItem);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wanderRange);
    }
}
