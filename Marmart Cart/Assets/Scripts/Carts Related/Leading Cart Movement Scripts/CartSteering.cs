using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the visual steering angle of the cart's virtual wheels.
///
/// Normal driving steers toward CartControlScript.desiredDirection.
/// While drifting, steering is driven directly by CartDriftController.
/// </summary>
public class CartSteering : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private CartDriftController driftController;

    [Tooltip("Wheel objects whose local Y rotation should match the current steering angle.")]
    [SerializeField] private List<GameObject> objectsToRotate = new List<GameObject>();

    #endregion

    #region Normal Steering

    [Header("Normal Steering")]
    [SerializeField] private float maxRotationAngle = 30f;
    [SerializeField] private float rotationSpeed = 80f;

    #endregion

    #region Drift Steering

    [Header("Drift Steering")]
    [SerializeField] private float driftSteerDegreesPerSecond = 220f;

    #endregion

    #region Runtime

    private float currentRotationAngle;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (ShouldUseDriftSteering()) UpdateDriftSteering();
        else UpdateDirectionSeekSteering();

        ApplyWheelRotation();
    }

    #endregion

    #region Steering Modes

    private bool ShouldUseDriftSteering()
    {
        return driftController != null && driftController.IsDrifting;
    }

    private void UpdateDirectionSeekSteering()
    {
        Vector3 desiredDirection = cartControlInput.desiredDirection;

        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            float angleDifference = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
            float targetWheelAngle = Mathf.Clamp(angleDifference, -maxRotationAngle, maxRotationAngle);

            currentRotationAngle = Mathf.Lerp(currentRotationAngle, targetWheelAngle, Time.deltaTime * rotationSpeed);
        }
        else
        {
            currentRotationAngle = Mathf.MoveTowards(currentRotationAngle, 0f, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateDriftSteering()
    {
        float targetWheelAngle = driftController.DriftSteeringAngle;
        currentRotationAngle = Mathf.MoveTowards(currentRotationAngle, targetWheelAngle, driftSteerDegreesPerSecond * Time.deltaTime);
    }

    #endregion

    #region Wheel Visuals

    private void ApplyWheelRotation()
    {
        foreach (GameObject obj in objectsToRotate)
        {
            if (obj != null) obj.transform.localRotation = Quaternion.Euler(0f, currentRotationAngle, 0f);
        }
    }

    #endregion

    #region Public API

    public float GetCurrentSteeringAngle()
    {
        return currentRotationAngle;
    }

    #endregion
}
