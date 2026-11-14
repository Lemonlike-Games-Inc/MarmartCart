using UnityEngine;

public static class AssistEvaluator
{
    public static AssistEvaluationResult EvaluatePlayer(
        PlayerAssistState player,
        int firstPlaceScore,
        int lastPlaceScore,
        int currentZoneStock,
        AssistTuningData tuning)
    {
        AssistEvaluationResult result = new AssistEvaluationResult();

        // -------------------------
        // Hard fail checks
        // -------------------------
        if (player == null)
        {
            result.eligible = false;
            result.reason = "Player state is null.";
            return result;
        }

        if (tuning == null)
        {
            result.eligible = false;
            result.reason = "Tuning data is null.";
            return result;
        }

        if (currentZoneStock <= 0)
        {
            result.eligible = false;
            result.reason = "Zone has no stock.";
            return result;
        }

        if (player.IsOnCooldown(tuning.claimCooldown))
        {
            result.eligible = false;
            result.reason = "Player is on assist cooldown.";
            return result;
        }

        if (player.cartCount > tuning.maxCartCountToBeEligible)
        {
            result.eligible = false;
            result.reason = "Player has too many carts to qualify.";
            return result;
        }

        // -------------------------
        // Score components
        // -------------------------
        result.rankScore = GetRankScore(player.rank, tuning);
        result.gapScore = GetGapScore(player.scoreGapToFirst, tuning);
        result.cartScore = GetCartScore(player.cartCount, tuning);
        result.closeMatchPenalty = IsMatchGloballyClose(firstPlaceScore, lastPlaceScore, tuning)
            ? tuning.globalClosePenalty
            : 0;

        result.finalNeedScore =
            result.rankScore +
            result.gapScore +
            result.cartScore -
            result.closeMatchPenalty;

        if (result.finalNeedScore < tuning.minNeedScoreToQualify)
        {
            result.eligible = false;
            result.claimAmount = tuning.minAssistPerClaim;
            result.reason = "Need score below qualification threshold.";
            return result;
        }

        result.claimAmount = GetClaimAmountFromNeedScore(result.finalNeedScore, tuning);
        result.claimAmount = Mathf.Min(result.claimAmount, tuning.maxAssistPerClaim);
        result.claimAmount = Mathf.Min(result.claimAmount, currentZoneStock);

        if (result.claimAmount <= 0)
        {
            result.eligible = false;
            result.reason = "Claim amount resolved to 0.";
            return result;
        }

        result.eligible = true;
        result.reason = "Eligible.";
        return result;
    }

    private static int GetRankScore(int rank, AssistTuningData tuning)
    {
        switch (rank)
        {
            case 1: return tuning.firstPlaceScore;
            case 2: return tuning.secondPlaceScore;
            case 3: return tuning.thirdPlaceScore;
            case 4: return tuning.fourthPlaceScore;
            default: return 0;
        }
    }

    private static int GetGapScore(int scoreGapToFirst, AssistTuningData tuning)
    {
        if (scoreGapToFirst >= tuning.hugeGapThreshold)
            return tuning.hugeGapScore;

        if (scoreGapToFirst >= tuning.largeGapThreshold)
            return tuning.largeGapScore;

        if (scoreGapToFirst >= tuning.mediumGapThreshold)
            return tuning.mediumGapScore;

        return 0;
    }

    private static int GetCartScore(int cartCount, AssistTuningData tuning)
    {
        if (cartCount <= tuning.veryLowCartThreshold)
            return tuning.veryLowCartScore;

        if (cartCount <= tuning.lowCartThreshold)
            return tuning.lowCartScore;

        return 0;
    }

    private static bool IsMatchGloballyClose(int firstPlaceScore, int lastPlaceScore, AssistTuningData tuning)
    {
        int spread = firstPlaceScore - lastPlaceScore;
        return spread <= tuning.globalCloseSpreadThreshold;
    }

    private static int GetClaimAmountFromNeedScore(int needScore, AssistTuningData tuning)
    {
        if (needScore >= tuning.highNeedThreshold)
            return tuning.highNeedClaimAmount;

        if (needScore >= tuning.mediumNeedThreshold)
            return tuning.mediumNeedClaimAmount;

        return tuning.lowNeedClaimAmount;
    }
}