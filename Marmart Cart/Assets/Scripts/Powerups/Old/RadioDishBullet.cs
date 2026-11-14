using UnityEngine;

public class RadioDishBullet : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed;
    [SerializeField] private float delayAttackTime = 0.5f;
    public void Initialize(Vector3 target, float bulletSpeed)
    {
        targetPosition = target;
        speed = bulletSpeed;

        // Optional: Look toward target
        transform.LookAt(targetPosition);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

        //if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        //{
        //    Destroy(gameObject); // Optional: VFX on hit
        //}
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<DinoBehaviour>(out var dino))
        {
            dino.AddToAttackTimer(delayAttackTime);
            Debug.Log("Hit");
            // Optional: hit VFX/sound
            Destroy(gameObject,0.05f);
        }
    }
}
