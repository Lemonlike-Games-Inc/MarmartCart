using UnityEngine;

public class MovementTask : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player1"; // or Player2

    [Header("Gate")]
    [SerializeField] private TutorialGate gateToUnlock;

    [Header("Progress")]
    public bool wp0;
    public bool wp1;
    public bool wp2;
    public bool wp3;

    public bool IsComplete =>
        wp0 && wp1 && wp2 && wp3;

    public void MarkWaypoint(int index)
    {
        switch (index)
        {
            case 0: wp0 = true; break;
            case 1: wp1 = true; break;
            case 2: wp2 = true; break;
            case 3: wp3 = true; break;
        }

        if (IsComplete)
        {
            gateToUnlock.OpenGate();
        }
    }

    public bool MatchesPlayer(string tag)
    {
        return tag == playerTag;
    }

    // Optional reset for testing
    public void ResetMission()
    {
        wp0 = wp1 = wp2 = wp3 = false;
        gateToUnlock.CloseGate();
    }
}
