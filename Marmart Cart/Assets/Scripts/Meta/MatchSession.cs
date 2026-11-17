using System;
using UnityEngine;

/// <summary>
/// Match data that survives the gameplay-scene reload between Rounds.
///
/// Rules:
/// - Plain values only. Never a scene object or an event.
/// - Changes only through CommitRound or SkipRound, right before the next Round
///   loads. Any other reload replays the current Round with nothing awarded.
/// - Empty data means Round 1, so pressing Play in a gameplay scene works.
/// - Cleared on Restart Match, on entering the main menu and on Play Mode start.
///
/// Player indices are 1-based, like CashScoreManager.
/// </summary>
public static class MatchSession
{
    public const int RoundsPerMatch = 3;
    public const int MaxPlayers = CashScoreManager.MaxPlayers;

    private static int roundNumber = 1;
    private static readonly int[] matchPoints = new int[MaxPlayers];
    private static readonly int[] bankedByRound = new int[RoundsPerMatch * MaxPlayers];

    /// <summary>The 1-based Round this load plays.</summary>
    public static int RoundNumber => roundNumber;

    /// <summary>The final Round has no next load, so it is never committed or skipped.</summary>
    public static bool IsFinalRound => roundNumber >= RoundsPerMatch;

    // Runs on every Play Mode start, also when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayModeStart()
    {
        ResetData();
    }

    public static void Clear()
    {
        ResetData();
        Debug.Log("[Meta] Match data cleared.");
    }

    /// <summary>
    /// Records this Round's banked values, gives each winner 1 Match Point and
    /// moves to the next Round. Call only right before loading the next Round.
    /// Returns false on the final Round.
    /// </summary>
    public static bool CommitRound(int[] bankedValues, bool[] roundWinners, int playerCount)
    {
        if (IsFinalRound || bankedValues == null || roundWinners == null) return false;

        int count = Mathf.Min(
            Mathf.Min(playerCount, MaxPlayers),
            Mathf.Min(bankedValues.Length, roundWinners.Length));
        int roundOffset = (roundNumber - 1) * MaxPlayers;

        for (int i = 0; i < count; i++)
        {
            bankedByRound[roundOffset + i] = Mathf.Max(0, bankedValues[i]);
            if (roundWinners[i]) matchPoints[i]++;
        }

        roundNumber++;
        return true;
    }

    /// <summary>
    /// Moves to the next Round with nothing banked and no Match Point awarded.
    /// Returns false on the final Round.
    /// </summary>
    public static bool SkipRound()
    {
        if (IsFinalRound) return false;

        roundNumber++;
        return true;
    }

    public static int GetMatchPoints(int playerIndex)
    {
        return IsValidPlayer(playerIndex) ? matchPoints[playerIndex - 1] : 0;
    }

    public static int GetBankedValue(int round, int playerIndex)
    {
        if (round < 1 || round > RoundsPerMatch || !IsValidPlayer(playerIndex)) return 0;
        return bankedByRound[(round - 1) * MaxPlayers + playerIndex - 1];
    }

    public static int GetTotalBanked(int playerIndex)
    {
        int total = 0;
        for (int round = 1; round <= RoundsPerMatch; round++)
        {
            total += GetBankedValue(round, playerIndex);
        }

        return total;
    }

    private static bool IsValidPlayer(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= MaxPlayers;
    }

    private static void ResetData()
    {
        roundNumber = 1;
        Array.Clear(matchPoints, 0, matchPoints.Length);
        Array.Clear(bankedByRound, 0, bankedByRound.Length);
    }
}
