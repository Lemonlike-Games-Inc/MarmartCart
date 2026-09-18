using UnityEngine;

public enum CheckoutStationId
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Match-flow wrapper around one existing checkout station.
///
/// Closing a station prevents NEW entry. If a player is already checking out,
/// their committed checkout session is allowed to finish normally.
/// </summary>
[DisallowMultipleComponent]
public class CheckoutStationFlowController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private CheckoutStationId stationId = CheckoutStationId.North;

    [Header("Checkout")]
    [SerializeField] private CheckOutManager checkOutManager;

    [Header("Pit Availability Visual")]
    [Tooltip(
        "Root containing the checkout pit's open-state mesh, lights, and VFX. " +
        "It is enabled only while this station is open; telegraph and closed " +
        "states keep it disabled. Assign a child object, not this controller's " +
        "own GameObject."
    )]
    [SerializeField] private GameObject onOffVisuals;

    [Header("Optional Prototype State Visuals")]
    [SerializeField] private GameObject telegraphIndicator;
    [SerializeField] private GameObject openIndicator;
    [SerializeField] private GameObject closedIndicator;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isOpen;
    [SerializeField] private bool isTelegraphing;

    public CheckoutStationId StationId => stationId;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (checkOutManager == null) checkOutManager = GetComponent<CheckOutManager>();

        RefreshPitAvailabilityVisual();
    }

    private void OnEnable()
    {
        RefreshPitAvailabilityVisual();
    }

    public void SetTelegraphing(bool telegraphing)
    {
        isTelegraphing = telegraphing;

        if (telegraphing)
        {
            SetOpen(false);
            if (telegraphIndicator != null) telegraphIndicator.SetActive(true);
            return;
        }

        if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        isTelegraphing = false;

        if (checkOutManager != null)
        {
            if (open) checkOutManager.EnableStation();
            else checkOutManager.DisableStation();
        }

        if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
        if (openIndicator != null) openIndicator.SetActive(open);
        if (closedIndicator != null) closedIndicator.SetActive(!open);

        RefreshPitAvailabilityVisual();
    }

    private void RefreshPitAvailabilityVisual()
    {
        if (onOffVisuals == null) return;

        if (onOffVisuals == gameObject)
        {
            Debug.LogError(
                "[CheckoutStationFlowController] On Off Visuals must reference " +
                "a child visual root, not the CheckoutStationFlowController's " +
                "own GameObject.",
                this
            );
            return;
        }

        onOffVisuals.SetActive(isOpen);
    }
}
