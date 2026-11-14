using UnityEngine;
using UnityEngine.VFX;

public class PowerupProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private LayerMask hitMask;

    private Rigidbody rb;
    public string ownerTag;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 direction, int ownerPlayerIndex)
    {
        ownerTag = "Player" + ownerPlayerIndex;
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }
}
