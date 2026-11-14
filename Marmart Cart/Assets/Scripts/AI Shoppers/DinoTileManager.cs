using System.Collections.Generic;
using UnityEngine;

public class DinoTileManager : MonoBehaviour
{
    [Header("Tile Prefabs")]
    public GameObject p1TilePrefab;
    public GameObject p2TilePrefab;

    [Header("Path Generation Settings")]
    [SerializeField] private int pathLength = 10;
    [SerializeField] private int turnCount = 3;
    [SerializeField] private float tileSpacing = 3.25f;
    [SerializeField] private float minStartDistance = 3f;
    [SerializeField] private float maxStartDistance = 6f;

    [Header("References")]
    public Transform dinoCenter;

    [Header("Manual Tile References")]
    public List<DinoTile> player1Tiles = new List<DinoTile>();
    public List<DinoTile> player2Tiles = new List<DinoTile>();

    private bool pathCompleted = false;

    [SerializeField] private DinoBehaviour dinoBehaviour;

    void Update()
    {
        if (!pathCompleted && AllTilesCovered())
        {
            pathCompleted = true;
            HandleFullCoverage();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            GeneratePath();
        }
    }
    public void SetPathLength(int length)
    {
        pathLength = length;
    }

    public void SetTurnCounts(int count)
    {
        turnCount = count;
    }

    public void GeneratePath()
    {
        ClearExistingTiles();

        Vector3 currentPos = GetRandomStartPosition();
        Vector3 currentDir = GetRandomDirection();
        // STEP 1: Precompute turn indices for even spacing
        List<int> turnIndices = new();
        for (int t = 1; t <= turnCount; t++)
        {
            int turnAt = Mathf.RoundToInt((float)t * (pathLength - 1) / (turnCount + 1));
            turnIndices.Add(turnAt);
        }

        for (int i = 0; i < pathLength; i++)
        {
            // Instantiate red tile
            GameObject p1Tile = Instantiate(p1TilePrefab, currentPos, Quaternion.identity);
            DinoTile p1TileComponent = p1Tile.GetComponent<DinoTile>();
            if (p1TileComponent != null) player1Tiles.Add(p1TileComponent);

            // Instantiate mirrored blue tile
            Vector3 mirroredPos = GetMirroredPosition(currentPos);
            GameObject p2Tile = Instantiate(p2TilePrefab, mirroredPos, Quaternion.identity);
            DinoTile p2TileComponent = p2Tile.GetComponent<DinoTile>();
            if (p2TileComponent != null) player2Tiles.Add(p2TileComponent);

            // Move forward
            currentPos += currentDir * tileSpacing;

            // Turn at specific, evenly spaced steps
            if (turnIndices.Contains(i))
            {
                currentDir = GetNextDirection(currentDir);
            }
        }

        // Clean up any tiles that destroyed themselves due to overlap
        player1Tiles.RemoveAll(t => t == null);
        player2Tiles.RemoveAll(t => t == null);
    }

    private bool AllTilesCovered()
    {
        if (player1Tiles.Count == 0 || player2Tiles.Count == 0) return false;

        foreach (var tile in player1Tiles)
        {
            if (tile == null) continue;
            if (!tile.IsCovered)
                return false;
        }

        foreach (var tile in player2Tiles)
        {
            if (tile == null) continue;
            if (!tile.IsCovered)
                return false;
        }

        return true;
    }

    private void HandleFullCoverage()
    {
        Debug.Log("All tiles covered! Dino can now take damage.");
        dinoBehaviour.TakeDamage();
    }

    public void ClearExistingTiles()
    {
        foreach (var tile in player1Tiles)
            if (tile) Destroy(tile.gameObject);

        foreach (var tile in player2Tiles)
            if (tile) Destroy(tile.gameObject);

        player1Tiles.Clear();
        player2Tiles.Clear();
        pathCompleted = false;
    }

    private Vector3 GetRandomStartPosition()
    {
        Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(minStartDistance, maxStartDistance);
        Vector3 offset3D = new Vector3(randomOffset.x, -dinoCenter.position.y, randomOffset.y);
        return RoundToGrid(dinoCenter.position + offset3D);
    }

    private Vector3 GetMirroredPosition(Vector3 original)
    {
        Vector3 center = dinoCenter.position;
        Vector3 offset = original - center;
        Vector3 result = center - offset;
        return new Vector3(result.x, 0f, result.z);
    }

    private Vector3 GetRandomDirection()
    {
        float[] baseAngles = { 0f, 120f, 240f };
        float angle = baseAngles[Random.Range(0, baseAngles.Length)];
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    private Vector3 GetNextDirection(Vector3 previousDirection)
    {
        float[] turnAngles = { -60f, 60f }; // Only left and right
        float chosenAngle = turnAngles[Random.Range(0, turnAngles.Length)];
        return Quaternion.Euler(0f, chosenAngle, 0f) * previousDirection.normalized;
    }

    private Vector3 RoundToGrid(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x),
            pos.y,
            Mathf.Round(pos.z)
        );
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, minStartDistance);
        Gizmos.DrawWireSphere(transform.position, maxStartDistance);
    }
}
