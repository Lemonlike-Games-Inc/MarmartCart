using UnityEngine;

public class TomatoBehavior : MonoBehaviour
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
                hitCart.transform.parent.GetChild(0).GetComponent<LeadingCartRaycaster>().TriggerTomatoSplash();   //trigger the UI effect
                Destroy(gameObject);
                return;
            }
        }

        // Check for Leading Cart
        if (hitObj.TryGetComponent(out LeadingCartRaycaster leadingCart))
        {
            //Debug.Log("Tomato hit leading cart!");
            leadingCart.TriggerTomatoSplash();   //trigger the UI effect
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
