using UnityEngine;

public class DinoIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    [SerializeField] private Transform player;
    private Transform dino;
    [SerializeField] private DinoGenerationScript dinoSpawner;

    private void Start()
    {
        
        dinoSpawner = FindFirstObjectByType<DinoGenerationScript>();
    }
    public void RegisterPlayer(bool isPlayer1)
    {
        GameObject playerRef;
        if (isPlayer1)
            playerRef = GameObject.FindGameObjectWithTag("Player1");
        else
            playerRef = GameObject.FindGameObjectWithTag("Player2");

        player = isPlayer1 ? playerRef?.transform : playerRef?.transform;
    }
    private void Update()
    {
        if (dinoSpawner == null || player == null)
        {
            Debug.Log("Null");
            return;
        }

            var dinoObj = dinoSpawner.GetExistingDino(); // safer than FindFirstObjectByType
        if (dinoObj != null)
        {
            GetComponent<MeshRenderer>().enabled = true;
            dino = dinoObj.transform;
            // Position ball in direction of dino
            Vector3 direction = dino.position - player.position;
            direction.y = 0f;
            direction.Normalize();

            transform.position = player.position + direction * offsetDistance + Vector3.up * -1f;
        }
        else
        {
            dino = null;
            GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
