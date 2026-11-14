using UnityEngine;

public class CollectItemTask : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player1"; // Player1 / Player2

    [Header("Gate")]
    [SerializeField] private TutorialGate gateToUnlock;

    [Header("Goal")]
    [SerializeField] private int requiredItemCount = 10;

    [Header("Progress (Read Only)")]
    [SerializeField] private int currentItemCount = 0;
    [SerializeField] private bool completed = false;

    public bool IsComplete => completed;

    // 🔹 Called when a grocery item is successfully collected
    public void IncreaseCount()
    {
        if (completed) return;

        currentItemCount++;

        if (currentItemCount >= requiredItemCount)
        {
            completed = true;
            gateToUnlock.OpenGate();
        }
    }

    public bool MatchesPlayer(string tag)
    {
        return tag == playerTag;
    }

    public void ResetMission()
    {
        currentItemCount = 0;
        completed = false;
        gateToUnlock.CloseGate();
    }
}