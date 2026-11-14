using UnityEngine;

/// <summary>
/// Thin collision relay placed on each physical collider child of the leading cart.
///
/// The physical collider remains responsible only for collision geometry.
/// This component forwards collision-enter callbacks to the single parent
/// LeadingCartCollisionFeedback controller.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LeadingCartCollisionRelay : MonoBehaviour
{
    #region Reference

    [SerializeField] private LeadingCartCollisionFeedback collisionFeedback;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (collisionFeedback == null)
        {
            collisionFeedback = GetComponentInParent<LeadingCartCollisionFeedback>();
        }

        if (collisionFeedback == null)
        {
            Debug.LogError(
                "[LeadingCartCollisionRelay] Could not find LeadingCartCollisionFeedback in the parent hierarchy.",
                this
            );
        }
    }

    #endregion

    #region Collision Relay

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionFeedback != null)
        {
            collisionFeedback.HandleCollisionEnter(collision);
        }
    }

    #endregion
}
