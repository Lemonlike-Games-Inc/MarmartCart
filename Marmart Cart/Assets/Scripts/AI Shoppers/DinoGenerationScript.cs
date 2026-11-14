using UnityEngine;

public class DinoGenerationScript : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnCenter;

    [Header("Spawn Settings")]
    [SerializeField] private float radius = 20f; // Radius for item spawn around the player
    [SerializeField] private float minDistanceFromCenter = 5f; // Minimum distance from the center to spawn items
    [SerializeField] private LayerMask groundLayer; // Layer mask for ground detection

    [Header("Phase 1 Settings")]
    [SerializeField] private float phase1Duration = 60f;
    [SerializeField] private float phase1SpawnInterval = 15f;
    [SerializeField] private int phase1ItemsPerSpawn = 5;

    [Header("Phase 2 Settings")]
    [SerializeField] private float phase2Duration = 60f; // Phase 2 starts after Phase 1
    [SerializeField] private float phase2SpawnInterval = 10f;
    [SerializeField] private int phase2ItemsPerSpawn = 6;


    [Header("Phase 3 Settings")]
    [SerializeField] private float phase3SpawnInterval = 10f;
    [SerializeField] private int phase3ItemsPerSpawn = 8;

    private int currentPhase = 0;
    private float elapsedGameTime = 0f;
    private float spawnInterval;
    private int itemsPerSpawn;

    [Header("Item Prefabs")]
    [SerializeField] private GameObject dinoPrefab;

    private float nextSpawnTime;
    public GameObject existingDino;
    private void Start()
    {
        spawnCenter = this.transform;
        UpdateGamePhase(); // Initialize phase on start
        nextSpawnTime = Time.time + spawnInterval; // Delay first spawn until interval passes
    }

    void Update()
    {
        // Update game time progression
        elapsedGameTime += Time.deltaTime;

        // Adjust spawn settings based on game progression
        UpdateGamePhase();

        // Spawn items at intervals
        if (Time.time >= nextSpawnTime)
        {
            SpawnItems();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void UpdateGamePhase()
    {
        if (elapsedGameTime < phase1Duration && currentPhase != 1)
        {
            // Phase 1
            currentPhase = 1;
            spawnInterval = phase1SpawnInterval;
            itemsPerSpawn = phase1ItemsPerSpawn;
        }
        else if (elapsedGameTime >= phase1Duration && elapsedGameTime < phase1Duration + phase2Duration && currentPhase != 2)
        {
            // Phase 2
            currentPhase = 2;
            spawnInterval = phase2SpawnInterval;
            itemsPerSpawn = phase2ItemsPerSpawn;
        }
        else if (elapsedGameTime >= phase1Duration + phase2Duration && currentPhase != 3)
        {
            // Phase 3
            currentPhase = 3;
            spawnInterval = phase3SpawnInterval;
            itemsPerSpawn = phase3ItemsPerSpawn;
        }
    }

    private void SpawnItems()
    {
        if (existingDino != null)
        {
            existingDino.GetComponent<DinoBehaviour>().IncreaseLevel();
        }
        else
        {
            Vector3 spawnPosition = GetValidSpawnPosition();
            if (spawnPosition != Vector3.zero)
            {
                existingDino = Instantiate(dinoPrefab, spawnPosition + new Vector3(0, -5f, 0), Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("Failed to find a valid spawn position after multiple attempts.");
            }
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 500; // Limit the number of retries to prevent infinite loops
        int attempts = 0;

        while (attempts < maxRetries)
        {
            // Generate a random point within the radius, ensuring it’s beyond minDistanceFromCenter
            Vector2 randomPoint = Random.insideUnitCircle * radius;
            if (randomPoint.magnitude >= minDistanceFromCenter)
            {
                Vector3 spawnPosition = new Vector3(randomPoint.x, 20, randomPoint.y) + spawnCenter.position;

                // Raycast to detect any surface
                if (Physics.Raycast(spawnPosition, Vector3.down, out RaycastHit hit, Mathf.Infinity))
                {
                    // Check if the hit object is on the ground layer
                    if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                    {
                        return hit.point; // Valid ground point
                    }
                    else
                    {
                        // If not on ground layer, continue trying
                        //Debug.Log($"Invalid hit on layer {hit.collider.gameObject.layer}, retrying...");
                    }
                }
            }

            attempts++;
        }

        return Vector3.zero; // Return an invalid position if no ground is found after retries
    }
    public GameObject GetExistingDino()
    {
        return existingDino;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
