using UnityEngine;

[System.Serializable]
public struct AssistEvaluationResult
{
    public bool eligible;
    public int finalNeedScore;
    public int claimAmount;

    public int rankScore;
    public int gapScore;
    public int cartScore;
    public int closeMatchPenalty;

    public string reason;

    public override string ToString()
    {
        return $"Eligible: {eligible}, FinalNeed: {finalNeedScore}, Claim: {claimAmount}, " +
               $"RankScore: {rankScore}, GapScore: {gapScore}, CartScore: {cartScore}, " +
               $"ClosePenalty: {closeMatchPenalty}, Reason: {reason}";
    }
}