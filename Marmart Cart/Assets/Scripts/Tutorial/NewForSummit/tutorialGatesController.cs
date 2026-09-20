using System.Collections.Generic;
using UnityEngine;

public class tutorialGatesController : MonoBehaviour
{
    [Header("Tutorial Session Timing")]
    [SerializeField] private float tutorialSession2StartAt = 10f;
    [SerializeField] private float tutorialSession3StartAt = 20f;

    [Header("Session 2")]
    [SerializeField] private List<GameObject> objectsToDisableAtS2 = new();
    [SerializeField] private List<GameObject> objectsToEnableAtS2 = new();

    [Header("Session 4")]
    [SerializeField] private List<GameObject> objectsToDisableAtS3 = new();
    [SerializeField] private List<GameObject> objectsToEnableAtS3 = new();

    private float timer;

    private bool session2Triggered;
    private bool session4Triggered;

    private void Update()
    {
        timer += Time.deltaTime;

        if (!session2Triggered && timer >= tutorialSession2StartAt)
        {
            session2Triggered = true;

            SetObjectsActive(objectsToDisableAtS2, false);
            SetObjectsActive(objectsToEnableAtS2, true);
        }

        if (!session4Triggered && timer >= tutorialSession3StartAt)
        {
            session4Triggered = true;

            SetObjectsActive(objectsToDisableAtS3, false);
            SetObjectsActive(objectsToEnableAtS3, true);
        }
    }

    private void SetObjectsActive(List<GameObject> objects, bool active)
    {
        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}