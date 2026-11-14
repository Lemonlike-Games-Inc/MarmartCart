using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MovementWaypoint : MonoBehaviour
{
    [Range(0, 3)]
    public int waypointIndex;

    [Header("Player Missions")]
    [SerializeField] private MovementTask player1Mission;
    [SerializeField] private MovementTask player2Mission;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player1"))
        {
            player1Mission.MarkWaypoint(waypointIndex);
            Destroy(this.gameObject);
        }
        else if (other.CompareTag("Player2"))
        {
            player2Mission.MarkWaypoint(waypointIndex);
            Destroy(this.gameObject);
        }
    }
}
