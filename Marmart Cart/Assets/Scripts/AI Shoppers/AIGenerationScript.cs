using UnityEngine;

/// <summary>
/// Spawns pooled AI shoppers inside a rectangular ground area.
///
/// Spawn flow:
/// - Spawn a configurable amount immediately when the game starts.
/// - Continue spawning waves using the existing three-phase rate settings.
/// - Every AI receives exactly one PrepareForSpawn() call when taken from a pool.
/// </summary>
[DisallowMultipleComponent]
public class AIGenerationScript : MonoBehaviour
{
    #region Spawn Area

    [Header("Spawn Box Boundary Settings")]
    [SerializeField] private float boxWidth = 40f;
    [SerializeField] private float boxLength = 30f;
    [SerializeField] private LayerMask groundLayer;

    [Min(0.1f)]
    [SerializeField] private float raycastHeight = 20f;

    [Min(0f)]
    [SerializeField] private float spawnHeightAboveGround = 0.5f;

    [Min(1)]
    [SerializeField] private int maxSpawnPositionAttempts = 50;

    #endregion

    #region Initial Spawn

    [Header("Initial Spawn")]
    [Tooltip("Number of AI spawned immediately after the pools are initialized.")]
    [Min(0)]
    [SerializeField] private int initialSpawnAmount = 6;

    #endregion

    #region Phase 1

    [Header("Phase 1 Settings")]
    [Min(0f)]
    [SerializeField] private float phase1Duration = 60f;

    [Min(0.05f)]
    [SerializeField] private float phase1SpawnInterval = 6f;

    [Min(1)]
    [SerializeField] private int phase1AIsPerWave = 3;

    #endregion

    #region Phase 2

    [Header("Phase 2 Settings")]
    [Min(0f)]
    [SerializeField] private float phase2Duration = 60f;

    [Min(0.05f)]
    [SerializeField] private float phase2SpawnInterval = 5f;

    [Min(1)]
    [SerializeField] private int phase2AIsPerWave = 4;

    #endregion

    #region Phase 3

    [Header("Phase 3 Settings")]
    [Min(0.05f)]
    [SerializeField] private float phase3SpawnInterval = 4f;

    [Min(1)]
    [SerializeField] private int phase3AIsPerWave = 6;

    #endregion

    #region Pools

    [Header("AI Prefabs")]
    [SerializeField] private GameObjectPool[] aiPools;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private int currentPhase;
    [SerializeField] private float elapsedGameTime;
    [SerializeField] private float spawnInterval;
    [SerializeField] private int aiPerWave;

    private float nextSpawnTime;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializePools();

        UpdateGamePhase();

        // Explicit initial population instead of relying on nextSpawnTime
        // defaulting to zero and accidentally creating an immediate wave.
        SpawnAICount(initialSpawnAmount);

        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        elapsedGameTime += Time.deltaTime;

        UpdateGamePhase();

        if (Time.time < nextSpawnTime) return;

        SpawnAICount(aiPerWave);
        nextSpawnTime = Time.time + spawnInterval;
    }

    #endregion

    #region Pool Setup

    private void InitializePools()
    {
        if (aiPools == null) return;

        for (int i = 0; i < aiPools.Length; i++)
        {
            if (aiPools[i] != null) aiPools[i].SpawnPool();
        }
    }

    #endregion

    #region Phase Control

    private void UpdateGamePhase()
    {
        if (elapsedGameTime < phase1Duration)
        {
            if (currentPhase != 1)
            {
                currentPhase = 1;
                spawnInterval = phase1SpawnInterval;
                aiPerWave = phase1AIsPerWave;
            }

            return;
        }

        if (elapsedGameTime < phase1Duration + phase2Duration)
        {
            if (currentPhase != 2)
            {
                currentPhase = 2;
                spawnInterval = phase2SpawnInterval;
                aiPerWave = phase2AIsPerWave;
            }

            return;
        }

        if (currentPhase != 3)
        {
            currentPhase = 3;
            spawnInterval = phase3SpawnInterval;
            aiPerWave = phase3AIsPerWave;
        }
    }

    #endregion

    #region Spawning

    private void SpawnAICount(int count)
    {
        if (count <= 0 || aiPools == null || aiPools.Length == 0) return;

        for (int i = 0; i < count; i++)
        {
            if (!TryGetValidSpawnPosition(out Vector3 groundPosition)) continue;

            GameObjectPool randomPool = GetRandomValidPool();
            if (randomPool == null) return;

            Vector3 spawnPosition = groundPosition + Vector3.up * spawnHeightAboveGround;

            GameObject ai = randomPool.GetGameObject(spawnPosition, Quaternion.identity);
            if (ai == null) continue;

            AIShopperBehaviour aiBehaviour = ai.GetComponent<AIShopperBehaviour>();

            if (aiBehaviour != null)
            {
                aiBehaviour.PrepareForSpawn();
            }
            else
            {
                Debug.LogWarning("[AIGenerationScript] Spawned AI is missing AIShopperBehaviour.", ai);
            }
        }
    }

    private GameObjectPool GetRandomValidPool()
    {
        int validPoolCount = 0;

        for (int i = 0; i < aiPools.Length; i++)
        {
            if (aiPools[i] != null) validPoolCount++;
        }

        if (validPoolCount == 0) return null;

        int selected = Random.Range(0, validPoolCount);

        for (int i = 0; i < aiPools.Length; i++)
        {
            if (aiPools[i] == null) continue;

            if (selected == 0) return aiPools[i];
            selected--;
        }

        return null;
    }

    #endregion

    #region Spawn Position

    private bool TryGetValidSpawnPosition(out Vector3 groundPosition)
    {
        groundPosition = Vector3.zero;

        float halfWidth = boxWidth * 0.5f;
        float halfLength = boxLength * 0.5f;

        for (int attempt = 0; attempt < maxSpawnPositionAttempts; attempt++)
        {
            float xOffset = Random.Range(-halfWidth, halfWidth);
            float zOffset = Random.Range(-halfLength, halfLength);

            Vector3 rayOrigin = transform.position + new Vector3(xOffset, raycastHeight, zOffset);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
            {
                groundPosition = hit.point;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Validation / Gizmos

    private void OnValidate()
    {
        boxWidth = Mathf.Max(0.1f, boxWidth);
        boxLength = Mathf.Max(0.1f, boxLength);
        raycastHeight = Mathf.Max(0.1f, raycastHeight);
        spawnHeightAboveGround = Mathf.Max(0f, spawnHeightAboveGround);
        maxSpawnPositionAttempts = Mathf.Max(1, maxSpawnPositionAttempts);

        initialSpawnAmount = Mathf.Max(0, initialSpawnAmount);

        phase1Duration = Mathf.Max(0f, phase1Duration);
        phase2Duration = Mathf.Max(0f, phase2Duration);

        phase1SpawnInterval = Mathf.Max(0.05f, phase1SpawnInterval);
        phase2SpawnInterval = Mathf.Max(0.05f, phase2SpawnInterval);
        phase3SpawnInterval = Mathf.Max(0.05f, phase3SpawnInterval);

        phase1AIsPerWave = Mathf.Max(1, phase1AIsPerWave);
        phase2AIsPerWave = Mathf.Max(1, phase2AIsPerWave);
        phase3AIsPerWave = Mathf.Max(1, phase3AIsPerWave);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        Vector3 center = transform.position + Vector3.up * (raycastHeight * 0.5f);
        Vector3 size = new Vector3(boxWidth, raycastHeight, boxLength);

        Gizmos.DrawWireCube(center, size);
    }

    #endregion
}
