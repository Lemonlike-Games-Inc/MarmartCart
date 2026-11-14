using UnityEngine;

[DisallowMultipleComponent]
public class MapSection : MonoBehaviour
{
    [Header("Section")]
    [SerializeField] private string sectionName = "Section";

    [Tooltip("Center point that player event arrows point toward. Defaults to this transform.")]
    [SerializeField] private Transform eventCenter;

    [Header("Sale Event")]
    [SerializeField] private EventItemGenerator eventGenerator;

    [Tooltip("Transparent plane/box shown while this section's sale event is active.")]
    [SerializeField] private GameObject groundHighlight;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isEventActive;

    public string SectionName => sectionName;
    public Transform EventCenter => eventCenter != null ? eventCenter : transform;
    public EventItemGenerator EventGenerator => eventGenerator;
    public bool IsEventActive => isEventActive;

    private void Awake()
    {
        SetEventActive(false);
    }

    public void SetEventActive(bool active)
    {
        isEventActive = active;

        if (groundHighlight != null) groundHighlight.SetActive(active);
    }
}
