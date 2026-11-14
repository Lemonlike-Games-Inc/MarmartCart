using UnityEngine;

[CreateAssetMenu(fileName = "AssistTuningData", menuName = "Marmart/Assist Tuning Data")]
public class AssistTuningData : ScriptableObject
{
    [Header("Hard gates")]
    public int maxCartCountToBeEligible = 5;
    public float claimCooldown = 12f;

    [Header("Need score requirement")]
    public int minNeedScoreToQualify = 3;

    [Header("Rank score")]
    public int firstPlaceScore = 0;
    public int secondPlaceScore = 1;
    public int thirdPlaceScore = 2;
    public int fourthPlaceScore = 3;

    [Header("Gap to first thresholds")]
    public int mediumGapThreshold = 300;
    public int largeGapThreshold = 700;
    public int hugeGapThreshold = 1200;

    public int mediumGapScore = 1;
    public int largeGapScore = 2;
    public int hugeGapScore = 3;

    [Header("Low cart thresholds")]
    public int lowCartThreshold = 4;
    public int veryLowCartThreshold = 2;

    public int lowCartScore = 1;
    public int veryLowCartScore = 2;

    [Header("Global close match")]
    public int globalCloseSpreadThreshold = 500;
    public int globalClosePenalty = 1;

    [Header("Claim mapping")]
    public int lowNeedClaimAmount = 1;
    public int mediumNeedClaimAmount = 2;
    public int highNeedClaimAmount = 3;

    public int mediumNeedThreshold = 5;
    public int highNeedThreshold = 7;

    [Header("Safety cap")]
    public int minAssistPerClaim = 1;
    public int maxAssistPerClaim = 3; 
}