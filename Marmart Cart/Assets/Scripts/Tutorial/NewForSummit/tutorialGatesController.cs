using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tutorial timeline gates.
/// Every authored time is an absolute timestamp measured from t = 0.
/// Times are NOT stacked.
/// </summary>
[DisallowMultipleComponent]
public class tutorialGatesController : MonoBehaviour
{
    [Header("Tutorial Timeline - Absolute Seconds From 0")]
    [Min(0f)]
    [SerializeField] private float tutorialSession2StartAt = 10f;

    [Tooltip(
        "Absolute tutorial time when the Checkout hint becomes visible. " +
        "This is independent from the session boundaries."
    )]
    [Min(0f)]
    [SerializeField] private float tutorialCheckoutHintStartAt = 20f;

    [Tooltip(
        "Absolute tutorial time when Tutorial Session 3 begins. " +
        "Aim + Activate Powerup hints unlock at this exact same boundary."
    )]
    [Min(0f)]
    [SerializeField] private float tutorialSession3StartAt = 30f;

    [Header("Session 2 Object Changes")]
    [SerializeField]
    private List<GameObject> objectsToDisableAtS2 =
        new List<GameObject>();

    [SerializeField]
    private List<GameObject> objectsToEnableAtS2 =
        new List<GameObject>();

    [Header("Later Tutorial Object Changes")]
    [SerializeField]
    private List<GameObject> objectsToDisableAtS4 =
        new List<GameObject>();

    [SerializeField]
    private List<GameObject> objectsToEnableAtS4 =
        new List<GameObject>();

    [Header("Runtime - Read Only")]
    [SerializeField] private float elapsedTutorialTime;
    [SerializeField] private bool session2Triggered;
    [SerializeField] private bool checkoutHintsUnlocked;
    [SerializeField] private bool session3Triggered;
    [SerializeField] private bool advancedControlHintsUnlocked;

    public float ElapsedTutorialTime => elapsedTutorialTime;
    public bool CheckoutHintsUnlocked => checkoutHintsUnlocked;
    public bool AdvancedControlHintsUnlocked => advancedControlHintsUnlocked;

    public event Action OnCheckoutHintsUnlocked;
    public event Action OnAdvancedControlHintsUnlocked;

    private void Start()
    {
        elapsedTutorialTime = 0f;
        EvaluateTimeline();
    }

    private void Update()
    {
        elapsedTutorialTime += Time.deltaTime;
        EvaluateTimeline();
    }

    private void EvaluateTimeline()
    {
        if (!session2Triggered &&
            elapsedTutorialTime >= tutorialSession2StartAt)
        {
            session2Triggered = true;

            SetObjectsActive(objectsToDisableAtS2, false);
            SetObjectsActive(objectsToEnableAtS2, true);
        }

        if (!checkoutHintsUnlocked &&
            elapsedTutorialTime >= tutorialCheckoutHintStartAt)
        {
            checkoutHintsUnlocked = true;
            OnCheckoutHintsUnlocked?.Invoke();
        }

        if (!session3Triggered &&
            elapsedTutorialTime >= tutorialSession3StartAt)
        {
            session3Triggered = true;

            SetObjectsActive(objectsToDisableAtS4, false);
            SetObjectsActive(objectsToEnableAtS4, true);

            advancedControlHintsUnlocked = true;
            OnAdvancedControlHintsUnlocked?.Invoke();
        }
    }

    private static void SetObjectsActive(
        List<GameObject> objects,
        bool active)
    {
        if (objects == null) return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject target = objects[i];

            if (target != null)
                target.SetActive(active);
        }
    }

    private void OnValidate()
    {
        tutorialSession2StartAt =
            Mathf.Max(0f, tutorialSession2StartAt);

        tutorialCheckoutHintStartAt =
            Mathf.Max(0f, tutorialCheckoutHintStartAt);

        tutorialSession3StartAt =
            Mathf.Max(0f, tutorialSession3StartAt);
    }
}
