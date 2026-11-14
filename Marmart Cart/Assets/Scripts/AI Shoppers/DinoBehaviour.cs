using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class DinoBehaviour : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    [SerializeField] private int currentLevel = 1;

    [Header("Attack Settings (Stage-based)")]
    [SerializeField] private float[] stageAttackRadius = { 10f, 12f, 15f };
    [SerializeField] private float[] stageAttackInterval = { 15f, 12f, 10f };
    [SerializeField] private int[] stagePathLength = { 10, 15, 20 };
    [SerializeField] private int[] stageTurnCounts = { 1, 2, 3 };
    [SerializeField] private LayerMask obstacleLayer;

    [SerializeField] private TextMeshProUGUI attackTimerText;
    [SerializeField] private TextMeshProUGUI levelText;
    private float attackTimer;
    private float originalY;

    private bool hasSelectedObject = false;

    [Header("Movement Settings")]
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float angleStep = 15f;
    [SerializeField] private float raycastDistance = 1000f;
    private bool isMoving = false;
    private Vector3 moveTarget;
    [SerializeField] private DinoTileManager dinoTileManager;

    [SerializeField] private GameTimeManager gameTimeManager;

    [SerializeField] private GameObject bonusItemPrefab;
    [SerializeField] private LayerMask groundLayer;
    private List<Collider> selectedObstacles = new List<Collider>();

    private Light attackRadiusLight;

    private DinoGenerationScript dinoSpawner;
    void Awake()
    {
        gameTimeManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameTimeManager>();

        dinoSpawner = FindFirstObjectByType<DinoGenerationScript>();

        attackRadiusLight = this.transform.GetChild(2).GetComponent<Light>();
    }
    void Start()
    {
        currentLevel = 1;

        currentHealth = maxHealth;
        gameTimeManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameTimeManager>();


        attackTimer = stageAttackInterval[currentLevel - 1];
        attackTimerText = this.gameObject.transform.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>();
        levelText = this.gameObject.transform.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>();
        levelText.text = $"Level {currentLevel}";

        dinoTileManager.SetPathLength(stagePathLength[currentLevel - 1]);
        dinoTileManager.SetTurnCounts(stageTurnCounts[currentLevel - 1]);
        dinoTileManager.GeneratePath();

        originalY = transform.position.y;
    }

    void Update()
    {
        levelText.text = $"Level {currentLevel}";
        dinoTileManager.SetPathLength(stagePathLength[currentLevel - 1]);
        dinoTileManager.SetTurnCounts(stageTurnCounts[currentLevel - 1]);

        if (isMoving)
        {
            dinoTileManager.ClearExistingTiles();
            // Rotate around Y while moving
            this.gameObject.transform.GetChild(0).transform.Rotate(Vector3.up, 180f * Time.deltaTime); // Adjust speed as needed
            attackTimerText.text = $"Moving...";
            MoveTowardsTarget();
            return;
        }
        else
        {
            if(!hasSelectedObject)
            {
                SelectObjects();
            }
            // Subtle idle bounce on Z axis
            float bounce = Mathf.Sin(Time.time * 1f) * 0.5f; // Adjust frequency & amplitude
            Vector3 basePosition = new Vector3(transform.position.x, originalY, transform.position.z);
            transform.position = basePosition + new Vector3(0f, bounce, 0f);

            attackTimer -= Time.deltaTime;
            attackTimerText.text = $"{Mathf.FloorToInt(attackTimer)}";
            if (attackTimer <= 0f)
            {
                PerformAttack();
                TryFindMovementDirection();
                DeselectObjects();
                attackTimer = stageAttackInterval[currentLevel - 1];
            }
        }

        attackRadiusLight.innerSpotAngle = stageAttackRadius[currentLevel - 1] + 40f;
        attackRadiusLight.spotAngle = attackRadiusLight.innerSpotAngle * 1.05f;
    }

    private void SelectObjects()
    {
        selectedObstacles.Clear(); // clear previous selection

        float radius = stageAttackRadius[currentLevel - 1];
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, obstacleLayer);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Obstacles"))
            {
                selectedObstacles.Add(hit);

                var outline = hit.GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = true;

                var shelf = hit.GetComponent<ShelvesBehavior>();
                if (shelf != null)
                    shelf.BeginSuckEffect();
            }
        }

        hasSelectedObject = true;
    }

    private void DeselectObjects()
    {
        foreach (var hit in selectedObstacles)
        {
            if (hit == null) continue;

            var outline = hit.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;

            var shelf = hit.GetComponent<ShelvesBehavior>();
            if (shelf != null)
                shelf.StopAndRestore();
        }

        selectedObstacles.Clear();
        hasSelectedObject = false;
    }
    private void PerformAttack()
    {
        foreach (var hit in selectedObstacles)
        {
            if (hit == null) continue;
            Destroy(hit.gameObject);
        }

    }
    private void TryFindMovementDirection()
    {
        float randomStartAngle = Random.Range(0f, 360f);
        Vector3 origin = transform.position;
        origin.y = 1f;

        for (float angleOffset = 0; angleOffset < 360f; angleOffset += angleStep)
        {
            float angle = randomStartAngle + angleOffset;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, obstacleLayer))
            {
                if (hit.collider.CompareTag("Obstacles"))
                {
                    moveTarget = new Vector3(hit.point.x, this.transform.position.y, hit.point.z);
                    isMoving = true;
                    hasSelectedObject = false;
                    return;
                }
            }
        }
    }
    private void MoveTowardsTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, moveTarget) < 0.01f)
        {
            isMoving = false;
            dinoTileManager.GeneratePath();
        }
    }
    public void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
        {
            // Debug.Log("Dino defeated!");
            for (int i = 0; i < 10; i++)
            {
                // Keep trying to find a valid spawn position
                Vector3 spawnPosition = GetValidSpawnPosition();
                if (spawnPosition != Vector3.zero)
                {
                    Instantiate(bonusItemPrefab, spawnPosition + new Vector3(0, 10f, 0), Quaternion.identity);
                }
                else
                {
                    //Debug.LogWarning("Failed to find a valid spawn position after multiple attempts.");
                }
            }
            DeselectObjects();
            dinoTileManager.ClearExistingTiles();
            dinoSpawner.existingDino = null;
            Destroy(this.gameObject); // Or trigger death animation/event instead
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 50; // Limit the number of retries to prevent infinite loops
        int attempts = 0;

        while (attempts < maxRetries)
        {
            // Generate a random point within the radius, ensuring it’s beyond minDistanceFromCenter
            Vector2 randomPoint = Random.insideUnitCircle * 20f;
            if (randomPoint.magnitude >= 1f)
            {
                Vector3 spawnPosition = new Vector3(randomPoint.x, 20, randomPoint.y) + this.transform.position;

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
                        // Debug.Log($"Invalid hit on layer {hit.collider.gameObject.layer}, retrying...");
                    }
                }
            }

            attempts++;
        }

        return Vector3.zero; // Return an invalid position if no ground is found after retries
    }
    public void IncreaseLevel()
    {
        currentLevel++;
        Debug.Log($"Dino leveled up! Now Level {currentLevel}");
        // You could trigger a visual effect or difficulty recalculation here
    }
    public void AddToAttackTimer(float time)
    {
        attackTimer += time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stageAttackRadius[currentLevel - 1]);
    }
}
