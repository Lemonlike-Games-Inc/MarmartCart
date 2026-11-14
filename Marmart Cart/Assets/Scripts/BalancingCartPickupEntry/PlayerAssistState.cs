using UnityEngine;

[System.Serializable]
public class PlayerAssistState
{
    public int playerId;

    [Header("Live match data")]
    public int score;
    public int cartCount;

    [Header("Computed standings")]
    public int rank = 1;                 // 1 = first, 4 = last
    public int scoreGapToFirst = 0;

    [Header("Assist tracking")]
    public float lastAssistClaimTime = -999f;

    public bool IsOnCooldown(float cooldown)
    {
        return Time.time - lastAssistClaimTime < cooldown;
    }

    public float GetCooldownRemaining(float cooldown)
    {
        return Mathf.Max(0f, cooldown - (Time.time - lastAssistClaimTime));
    }
}