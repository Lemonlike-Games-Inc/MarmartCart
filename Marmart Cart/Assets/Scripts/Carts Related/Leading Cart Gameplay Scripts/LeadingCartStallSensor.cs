using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Front blockage sensor for the leading cart.
///
/// Any trigger overlap on an included blocking layer counts as a front blockage.
/// This script only reports blockage; stall gameplay logic lives in
/// LeadingCartStallController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LeadingCartStallSensor : MonoBehaviour
{
    #region Detection Settings

    [Header("Detection")]
    [Tooltip("Layers that should count as blocking the front of the cart.")]
    [SerializeField] private LayerMask blockingLayers;

    #endregion

    #region Runtime

    private readonly HashSet<Collider> blockingContacts = new HashSet<Collider>();

    public bool IsBlocked => blockingContacts.Count > 0;
    public int BlockingContactCount => blockingContacts.Count;

    #endregion

    #region Events

    public System.Action<bool> OnBlockedChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Collider sensorCollider = GetComponent<Collider>();

        if (!sensorCollider.isTrigger)
        {
            Debug.LogError("[LeadingCartStallSensor] Stall sensor Collider must have Is Trigger enabled.", this);
        }
    }

    private void OnDisable()
    {
        ClearContacts();
    }

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(Collider other)
    {
        if (!IsBlockingLayer(other.gameObject.layer)) return;

        bool wasBlocked = IsBlocked;
        blockingContacts.Add(other);

        if (!wasBlocked && IsBlocked) OnBlockedChanged?.Invoke(true);
    }
    private void OnTriggerStay(Collider other)
    {
        if (!IsBlockingLayer(other.gameObject.layer)) return;

        bool wasBlocked = IsBlocked;
        blockingContacts.Add(other);

        if (!wasBlocked && IsBlocked) OnBlockedChanged?.Invoke(true);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!blockingContacts.Remove(other)) return;

        if (!IsBlocked) OnBlockedChanged?.Invoke(false);
    }

    private bool IsBlockingLayer(int layer)
    {
        return (blockingLayers.value & (1 << layer)) != 0;
    }

    private void ClearContacts()
    {
        bool wasBlocked = IsBlocked;
        blockingContacts.Clear();

        if (wasBlocked) OnBlockedChanged?.Invoke(false);
    }

    #endregion
}