using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MapEventManager : MonoBehaviour
{
    [Header("Sections")]
    [SerializeField] private MapSection[] sections;

    [Header("Player Event Pointers")]
    [SerializeField] private MapEventPointer[] playerPointers;

    [Header("Event Loop")]
    [SerializeField] private bool autoStart = true;
    [Min(0f)][SerializeField] private float firstEventDelay = 5f;
    [Min(0.1f)][SerializeField] private float eventDuration = 12f;
    [Min(0f)][SerializeField] private float timeBetweenEvents = 3f;

    [Header("Runtime - Read Only")]
    [SerializeField] private MapSection activeSection;
    [SerializeField] private bool isRunningEvent;

    private Coroutine eventLoopRoutine;

    public MapSection ActiveSection => activeSection;
    public bool IsRunningEvent => isRunningEvent;

    private void Start()
    {
        SetAllSectionsInactive();
        ClearAllPointers();

        if (autoStart) StartEventLoop();
    }

    private void OnDisable()
    {
        if (eventLoopRoutine != null)
        {
            StopCoroutine(eventLoopRoutine);
            eventLoopRoutine = null;
        }

        StopCurrentEvent();
    }

    public void StartEventLoop()
    {
        if (eventLoopRoutine != null) return;
        eventLoopRoutine = StartCoroutine(EventLoop());
    }

    public void StopEventLoop()
    {
        if (eventLoopRoutine != null)
        {
            StopCoroutine(eventLoopRoutine);
            eventLoopRoutine = null;
        }

        StopCurrentEvent();
    }

    private IEnumerator EventLoop()
    {
        if (firstEventDelay > 0f) yield return new WaitForSeconds(firstEventDelay);

        while (true)
        {
            MapSection section = PickRandomValidSection();

            if (section == null)
            {
                Debug.LogWarning("[MapEventManager] No valid MapSection with an EventItemGenerator is assigned.", this);
                yield return new WaitForSeconds(1f);
                continue;
            }

            yield return RunSaleEvent(section);

            if (timeBetweenEvents > 0f) yield return new WaitForSeconds(timeBetweenEvents);
        }
    }

    private IEnumerator RunSaleEvent(MapSection section)
    {
        if (section == null || isRunningEvent) yield break;

        EventItemGenerator generator = section.EventGenerator;
        if (generator == null) yield break;

        isRunningEvent = true;
        activeSection = section;

        section.SetEventActive(true);
        SetAllPointers(section.EventCenter);
        generator.StartSaleEvent(eventDuration);

        yield return new WaitForSeconds(eventDuration);

        generator.StopEvent();
        section.SetEventActive(false);
        ClearAllPointers();

        activeSection = null;
        isRunningEvent = false;
    }

    private MapSection PickRandomValidSection()
    {
        if (sections == null || sections.Length == 0) return null;

        int validCount = 0;

        for (int i = 0; i < sections.Length; i++)
        {
            if (IsValidSection(sections[i])) validCount++;
        }

        if (validCount == 0) return null;

        int selectedValidIndex = Random.Range(0, validCount);

        for (int i = 0; i < sections.Length; i++)
        {
            MapSection section = sections[i];
            if (!IsValidSection(section)) continue;

            if (selectedValidIndex == 0) return section;
            selectedValidIndex--;
        }

        return null;
    }

    private bool IsValidSection(MapSection section)
    {
        return section != null && section.EventGenerator != null;
    }

    private void StopCurrentEvent()
    {
        if (activeSection != null)
        {
            if (activeSection.EventGenerator != null) activeSection.EventGenerator.StopEvent();
            activeSection.SetEventActive(false);
        }

        ClearAllPointers();
        activeSection = null;
        isRunningEvent = false;
    }

    private void SetAllSectionsInactive()
    {
        if (sections == null) return;

        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i] != null) sections[i].SetEventActive(false);
        }
    }

    private void SetAllPointers(Transform target)
    {
        if (playerPointers == null) return;

        for (int i = 0; i < playerPointers.Length; i++)
        {
            if (playerPointers[i] != null) playerPointers[i].SetTarget(target);
        }
    }

    private void ClearAllPointers()
    {
        if (playerPointers == null) return;

        for (int i = 0; i < playerPointers.Length; i++)
        {
            if (playerPointers[i] != null) playerPointers[i].ClearTarget();
        }
    }

    [ContextMenu("Start Random Event Now")]
    private void StartRandomEventNow()
    {
        if (!Application.isPlaying || isRunningEvent) return;

        MapSection section = PickRandomValidSection();
        if (section != null) StartCoroutine(RunSaleEvent(section));
    }

    [ContextMenu("Stop Current Event")]
    private void StopCurrentEventFromInspector()
    {
        if (!Application.isPlaying) return;
        StopCurrentEvent();
    }
}
