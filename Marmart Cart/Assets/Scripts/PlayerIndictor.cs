using UnityEngine;

public class PlayerIndictor : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private bool isForPlayer1 = true;

    public Transform player1;        // The player this indicator follows (e.g. Player 1)
    public Transform player2;        // The target player (e.g. Player 2)

    private Vector3 direction;

    private void Start()
    {
        player1 = GameObject.FindGameObjectWithTag("Player1").transform;

        player2 = GameObject.FindGameObjectWithTag("Player2").transform;
    }

    void Update()
    {
        if (player1 == null || player2 == null) return;


        if (isForPlayer1)
        {
            direction = player2.position - player1.position;
        }
        else
        {
            direction = player1.position - player2.position;
        }

        // Compute angle in degrees (relative to world forward)
        float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

        // Rotate around Z (2D sprite rotation)
        transform.rotation = Quaternion.Euler(90f, 0f, angle - 67f);
    }
}
