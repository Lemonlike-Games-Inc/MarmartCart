using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MartConditionManager : MonoBehaviour
{
    [Header("Tracked Shelf Tag")]
    [SerializeField] private string obstacleTag = "Obstacles";

    [SerializeField] private List<GameObject> registeredShelves = new();
    [SerializeField] private int initialShelfCount = 0;

    private float checkTimer = 0f;
    private float checkInterval = 0.1f; // Check every 1/10 second

    public float percent;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI conditionText;
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(obstacleTag) && !registeredShelves.Contains(other.gameObject))
        {
            registeredShelves.Add(other.gameObject);
        }
    }

    void Start()
    {
        StartCoroutine(DelayedRegisterComplete());
    }

    private IEnumerator DelayedRegisterComplete()
    {
        yield return new WaitForSeconds(0.5f); // Give time for OnTriggerEnter to populate list
        initialShelfCount = registeredShelves.Count;
        Debug.Log($"[Mart Init] Registered {initialShelfCount} shelves.");
        // ✅ Disable the trigger collider to stop further interference
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
        {
            col.enabled = false;
            Debug.Log("[Mart Init] Trigger collider disabled after registration.");
        }
    }

    void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;

            registeredShelves.RemoveAll(shelf => shelf == null);

            if (initialShelfCount > 0)
            {
                percent = (float)registeredShelves.Count / initialShelfCount;
                conditionText.text = $"{Mathf.RoundToInt(percent * 100)}%";
                //Debug.Log($"[Mart Health] {percent:P0} ({registeredShelves.Count}/{initialShelfCount})");
            }
        }
    }
}
