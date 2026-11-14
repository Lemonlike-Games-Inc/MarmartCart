using UnityEngine;

public class CheckoutTask : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private int playerIndex = 1; // 1 or 2

    [Header("Gate")]
    [SerializeField] private TutorialGate gateToUnlock;

    [Header("Goal")]
    [Tooltip("Minimum score gain required to complete this task")]
    [SerializeField] private float requiredScoreGain = 1f;

    [Header("Reference")]
    [SerializeField] private CashScoreManager cashScoreManager;

    [Header("Progress (Read Only)")]
    [SerializeField] private float startScore;
    [SerializeField] private float currentScore;
    [SerializeField] private bool completed = false;

    private void Start()
    {
        if (cashScoreManager == null)
        {
            Debug.LogError("CheckoutTask missing CashScoreManager reference.");
            enabled = false;
            return;
        }

        startScore = GetPlayerScore();
    }

    private void Update()
    {
        if (completed) return;

        currentScore = GetPlayerScore();

        if (currentScore - startScore >= requiredScoreGain)
        {
            completed = true;
            gateToUnlock.OpenGate();
        }
    }

    private float GetPlayerScore()
    {
        return 0;
        //return playerIndex == 1
        //    ? cashScoreManager.p1TotalScore
        //    : cashScoreManager.p2TotalScore;
    }

    // Optional reset
    public void ResetMission()
    {
        startScore = GetPlayerScore();
        completed = false;
        gateToUnlock.CloseGate();
    }
}