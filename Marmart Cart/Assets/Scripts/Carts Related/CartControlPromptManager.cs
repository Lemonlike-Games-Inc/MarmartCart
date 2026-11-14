using UnityEngine;
using UnityEngine.UI;

public class CartControlPromptManager : MonoBehaviour
{
    [SerializeField] CartControlScript cartController;

    [Header("Raycast Settings")]
    [SerializeField] GameObject moveBackwardPrompt;

    [Header("Speed-Up UI (Circular Fill)")]
    [Tooltip("Image with 'Filled' type set to Radial360 in the inspector.")]
    [SerializeField] private Image speedUpFillImage;

    [Tooltip("Second Image layered behind or above the real fuel image. This previews pending drift fuel.")]
    [SerializeField] private Image speedUpPendingFillImage;

    [Tooltip("FillAmount value that represents 'full' (e.g. 0.35 if your ring is not a full circle).")]
    [Range(0f, 1f)]
    [SerializeField] private float desiredMaxFill = 0.35f;

    [Header("Drift Fuel Preview")]
    [SerializeField] private CartDriftHypeReward driftHypeReward;

    [Tooltip("If true, the actual fuel UI smoothly moves toward the real fuel value.")]
    [SerializeField] private bool animateRealFuelFill = true;

    [SerializeField] private float realFuelFillLerpSpeed = 12f;

    [Tooltip("If true, the pending fuel shade smoothly moves toward the preview value.")]
    [SerializeField] private bool animatePendingFuelFill = true;

    [SerializeField] private float pendingFuelFillLerpSpeed = 16f;

    [Tooltip("If false, pending fuel layer is hidden when there is no pending drift reward.")]
    [SerializeField] private bool keepPendingImageVisibleWhenEmpty = false;

    private float displayedRealFill = 0f;
    private float displayedPendingFill = 0f;

    private void Awake()
    {
        if (!cartController)
            cartController = GetComponentInParent<CartControlScript>();

        if (!driftHypeReward)
            driftHypeReward = GetComponentInParent<CartDriftHypeReward>();
    }

    void Update()
    {
        UpdateMoveBackwardPrompt();
        UpdateSpeedUpUI();
    }

    private void UpdateMoveBackwardPrompt()
    {
        if (moveBackwardPrompt == null || cartController == null)
            return;

        moveBackwardPrompt.SetActive(cartController.GetCanMoveBackward());
    }

    private void UpdateSpeedUpUI()
    {
        if (cartController == null)
            return;

        float realMeter01 = Mathf.Clamp01(cartController.CurrentHype / 100f);
        float targetRealFill = realMeter01 * desiredMaxFill;

        if (animateRealFuelFill)
        {
            displayedRealFill = Mathf.Lerp(
                displayedRealFill,
                targetRealFill,
                Time.deltaTime * realFuelFillLerpSpeed
            );
        }
        else
        {
            displayedRealFill = targetRealFill;
        }

        if (speedUpFillImage != null)
            speedUpFillImage.fillAmount = displayedRealFill;

        UpdatePendingFuelLayer(realMeter01);
    }

    private void UpdatePendingFuelLayer(float realMeter01)
    {
        if (speedUpPendingFillImage == null)
            return;

        float previewMeter01 = realMeter01;

        bool hasPendingReward =
            driftHypeReward != null &&
            driftHypeReward.IsTrackingDrift &&
            driftHypeReward.HasPendingReward;

        if (hasPendingReward)
            previewMeter01 = driftHypeReward.PreviewHypeAmount;

        float targetPendingFill = previewMeter01 * desiredMaxFill;

        if (!hasPendingReward && !keepPendingImageVisibleWhenEmpty)
            targetPendingFill = 0f;

        if (animatePendingFuelFill)
        {
            displayedPendingFill = Mathf.Lerp(
                displayedPendingFill,
                targetPendingFill,
                Time.deltaTime * pendingFuelFillLerpSpeed
            );
        }
        else
        {
            displayedPendingFill = targetPendingFill;
        }

        speedUpPendingFillImage.fillAmount = displayedPendingFill;

        if (!keepPendingImageVisibleWhenEmpty)
            speedUpPendingFillImage.gameObject.SetActive(displayedPendingFill > 0.001f);
    }
}
