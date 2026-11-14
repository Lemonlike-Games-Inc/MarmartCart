using UnityEngine;

public class BowlingBallBehavior : MonoBehaviour
{
    [SerializeField] private PowerupProjectile powerupProjectile;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject hitObj = other.gameObject;

        // Ignore self or friendly fire
        if (hitObj.CompareTag(powerupProjectile.ownerTag)) return;

        // Check for Chained Cart
        if (hitObj.TryGetComponent(out ChainedCartManager hitCart))
        {
            if (hitCart.isCollectedByPlayer)
            {
                var cartRoot = hitObj.transform.root;
                if (cartRoot.GetComponentInChildren<CartControlScript>().GetIsInPit()) return;

                // Detach that cart
                Vector3 hitDirection = -rb.linearVelocity.normalized;
                hitCart.OnDetach(hitDirection);
                Destroy(gameObject);
                return;
            }
        }

        // Check for Leading Cart
        if (hitObj.TryGetComponent(out LeadingCartRaycaster leadingCart))
        {
            var rootCart = hitObj.transform.root;
            if (rootCart.GetComponentInChildren<CartControlScript>().GetIsInPit()) return;

            leadingCart.DetachSelfCompletely();

            Destroy(gameObject);
            return;
        }

        if (hitObj.CompareTag("Obstacles"))
        {
            Destroy(gameObject);
        }

        if (hitObj.CompareTag("Walls"))
        {
            Destroy(gameObject);
        }
    }
}
