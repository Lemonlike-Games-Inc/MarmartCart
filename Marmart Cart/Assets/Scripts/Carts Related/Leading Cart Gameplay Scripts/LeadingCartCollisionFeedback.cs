using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central physical-collision feedback for the leading cart.
///
/// Child collider relays forward collision-enter messages here.
/// Each collision is matched against the first configured feedback rule whose
/// LayerMask contains the contacted object's layer.
///
/// This system is feedback-only:
/// - no battle result
/// - no stall state
/// - no powerup logic
///
/// Different environment layers can therefore play different SFX now, while
/// future VFX/camera/rumble feedback can be added here without touching the
/// physical collider objects.
/// </summary>
public class LeadingCartCollisionFeedback : MonoBehaviour
{
    #region Feedback Rule

    [System.Serializable]
    public class CollisionFeedbackRule
    {
        [Tooltip("Optional label used only to keep the Inspector readable.")]
        public string ruleName = "Collision";

        [Tooltip("Layers that use this feedback rule.")]
        public LayerMask layers;

        [Tooltip("Ignore contacts below this relative impact speed. Set to 0 to react to every collision enter.")]
        [Min(0f)]
        public float minImpactSpeed = 1f;

        [Tooltip("Prevents compound colliders from repeatedly triggering this same feedback rule.")]
        [Min(0f)]
        public float cooldown = 0.1f;

        [Tooltip("SFX key passed to SfxManager. Leave empty for no sound.")]
        public string sfxName;

        [System.NonSerialized]
        public float nextAllowedTime;
    }

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private SfxManager sfxManager;

    #endregion

    #region Feedback Rules

    [Header("Collision Feedback Rules")]
    [Tooltip("Rules are checked from top to bottom. The first matching LayerMask wins.")]
    [SerializeField] private List<CollisionFeedbackRule> feedbackRules = new List<CollisionFeedbackRule>();

    #endregion

    #region Events

    /// <summary>
    /// Fired after a collision successfully passes its rule's layer, speed,
    /// and cooldown checks.
    ///
    /// Future VFX, camera shake, rumble, etc. can subscribe here.
    /// </summary>
    public System.Action<Collision> OnCollisionFeedback;

    #endregion

    #region Collision Handling

    public void HandleCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;

        int hitLayer = collision.collider.gameObject.layer;
        CollisionFeedbackRule rule = FindRuleForLayer(hitLayer);

        if (rule == null) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < rule.minImpactSpeed) return;
        if (Time.time < rule.nextAllowedTime) return;

        rule.nextAllowedTime = Time.time + rule.cooldown;

        if (sfxManager != null && !string.IsNullOrWhiteSpace(rule.sfxName))
        {
            sfxManager.PlaySFX(rule.sfxName);
        }

        OnCollisionFeedback?.Invoke(collision);
    }

    private CollisionFeedbackRule FindRuleForLayer(int layer)
    {
        for (int i = 0; i < feedbackRules.Count; i++)
        {
            CollisionFeedbackRule rule = feedbackRules[i];

            if (rule == null) continue;
            if ((rule.layers.value & (1 << layer)) != 0) return rule;
        }

        return null;
    }

    #endregion
}

