using UnityEngine;

public class CollectCartTask : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player1"; // or Player2

    [Header("Gate")]
    [SerializeField] private TutorialGate gateToUnlock;

    [Header("Goal")]
    [SerializeField] private int requiredCartCount = 10;

    [Header("Progress (read-only)")]
    [SerializeField] private int currentCartCount = 0;
    [SerializeField] private bool completed = false;

    public bool IsComplete => completed;
    public void IncreaseCount()
    {
        if (completed) return;

        currentCartCount++;

        if (currentCartCount >= requiredCartCount)
        {
            completed = true;
            gateToUnlock.OpenGate();
        }
    }

    public bool MatchesPlayer(string tag) => tag == playerTag;

    public void ResetMission()
    {
        currentCartCount = 0;
        completed = false;
        gateToUnlock.CloseGate();
    }
}