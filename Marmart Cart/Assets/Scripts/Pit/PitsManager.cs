using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PitsManager : MonoBehaviour
{
    //[Header("Phase Durations")]
    //[SerializeField] private float phase1Duration = 60f;
    //[SerializeField] private float phase2Duration = 60f;

    //[Header("Phase Settings")]
    //[SerializeField] private float phase1Interval = 12f;
    //[SerializeField] private float phase1DurationActive = 8f;
    //[SerializeField] private int phase1PitsPerCycle = 1;

    //[SerializeField] private float phase2Interval = 8f;
    //[SerializeField] private float phase2DurationActive = 10f;
    //[SerializeField] private int phase2PitsPerCycle = 2;

    //[SerializeField] private float phase3Interval = 5f;
    //[SerializeField] private float phase3DurationActive = 12f;
    //[SerializeField] private int phase3PitsPerCycle = 3;

    //private List<CheckOutManager> allPits = new List<CheckOutManager>();
    //private float elapsedGameTime = 0f;
    //private float nextToggleTime = 0f;
    //private int currentPhase = 0;

    //private float interval;
    //private float activeDuration;
    //private int pitsPerCycle;

    //[SerializeField] private TextMeshProUGUI timePromptP1;
    //[SerializeField] private TextMeshProUGUI timePromptP2;
    //[SerializeField] private GameObject promptBGP1;
    //[SerializeField] private GameObject promptBGP2;
    //private bool hasShownCountdown = false;

    //void Start()
    //{
    //    GameObject[] pitObjects = GameObject.FindGameObjectsWithTag("CheckOutStation");
    //    foreach (var pit in pitObjects)
    //    {
    //        var manager = pit.GetComponentInChildren<CheckOutManager>();
    //        if (manager != null)
    //        {
    //            allPits.Add(manager);
    //            manager.DisableStation(); // start with all pits disabled
    //        }
    //    }

    //    UpdatePhase();
    //    nextToggleTime = Time.time + interval;
    //}

    //void Update()
    //{
    //    elapsedGameTime += Time.deltaTime;
    //    UpdatePhase();
    //    float timeUntilNext = nextToggleTime - Time.time;

    //    if (!hasShownCountdown && timeUntilNext <= 5f)
    //    {
    //        StartCoroutine(ShowCountdown("Opening", 5));
    //        hasShownCountdown = true;
    //    }

    //    if (Time.time >= nextToggleTime)
    //    {
    //        ActivateRandomPits();
    //        nextToggleTime = Time.time + interval;
    //        hasShownCountdown = false;
    //    }
    //}

    //void UpdatePhase()
    //{
    //    if (elapsedGameTime < phase1Duration && currentPhase != 1)
    //    {
    //        currentPhase = 1;
    //        interval = phase1Interval;
    //        activeDuration = phase1DurationActive;
    //        pitsPerCycle = phase1PitsPerCycle;
    //    }
    //    else if (elapsedGameTime < phase1Duration + phase2Duration && currentPhase != 2)
    //    {
    //        currentPhase = 2;
    //        interval = phase2Interval;
    //        activeDuration = phase2DurationActive;
    //        pitsPerCycle = phase2PitsPerCycle;
    //    }
    //    else if (elapsedGameTime >= phase1Duration + phase2Duration && currentPhase != 3)
    //    {
    //        currentPhase = 3;
    //        interval = phase3Interval;
    //        activeDuration = phase3DurationActive;
    //        pitsPerCycle = phase3PitsPerCycle;
    //    }
    //}

    //void ActivateRandomPits()
    //{
    //    List<CheckOutManager> available = new List<CheckOutManager>();
    //    foreach (var pit in allPits)
    //    {
    //        if (!pit.gameObject.activeInHierarchy) continue;
    //        if (!pit.enabled) continue;
    //        if (!pit.gameObject.activeSelf) continue;

    //        if (!pit.IsStationAvailable()) // Add a getter if needed
    //            available.Add(pit);
    //    }

    //    int count = Mathf.Min(pitsPerCycle, available.Count);
    //    for (int i = 0; i < count; i++)
    //    {
    //        int index = Random.Range(0, available.Count);
    //        var chosenPit = available[index];
    //        available.RemoveAt(index);

    //        chosenPit.EnableStation();
    //        StartCoroutine(DisableAfterTime(chosenPit, activeDuration));
    //    }
    //}

    //IEnumerator DisableAfterTime(CheckOutManager pit, float delay)
    //{
    //    // Start countdown 5s before disabling
    //    yield return new WaitForSeconds(delay - 5f);
    //    StartCoroutine(ShowCountdown("Closing", 5));

    //    yield return new WaitForSeconds(5f);
    //    pit.DisableStation();
    //}
    //IEnumerator ShowCountdown(string action, int seconds)
    //{
    //    timePromptP1.gameObject.SetActive(true);
    //    promptBGP1.SetActive(true);
    //    for (int i = seconds; i > 0; i--)
    //    {
    //        timePromptP1.text = $"Lanes {action} in {i}!";
    //        yield return new WaitForSeconds(1f);
    //    }

    //    timePromptP1.gameObject.SetActive(false);
    //    promptBGP1.SetActive(false);

    //    timePromptP2.gameObject.SetActive(true);
    //    promptBGP2.SetActive(true);
    //    for (int i = seconds; i > 0; i--)
    //    {
    //        timePromptP2.text = $"Lanes {action} in {i}!";
    //        yield return new WaitForSeconds(1f);
    //    }

    //    timePromptP2.gameObject.SetActive(false);
    //    promptBGP2.SetActive(false);
    //}
}
