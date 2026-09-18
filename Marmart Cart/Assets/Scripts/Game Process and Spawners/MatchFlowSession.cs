using System;
using System.Collections.Generic;
using UnityEngine;

public enum MatchFlowSessionType
{
    FreePlay,
    CartRestock,
    ZoneLoot,
    CheckoutWindow,
    EndGameWrap
}

public enum ArenaZoneId
{
    ZoneA,
    ZoneB,
    ZoneC,
    ZoneD
}

[Flags]
public enum CheckoutStationMask
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    All = North | East | South | West
}

[Serializable]
public class MatchFlowSession
{
    [Tooltip("Designer-facing name only. Example: 'Opening Cart Grab' or 'Final Checkout'.")]
    public string label = "Session";

    public MatchFlowSessionType type = MatchFlowSessionType.FreePlay;

    [Tooltip(
        "FreePlay: free-play duration.\n" +
        "CartRestock: total distribution duration.\n" +
        "ZoneLoot: active loot-drop duration after telegraph.\n" +
        "CheckoutWindow: how long selected checkout stations remain open.\n" +
        "EndGameWrap: final gameplay buffer before the Director requests match end."
    )]
    [Min(0f)]
    public float duration = 5f;

    [Tooltip("Used only by ZoneLoot and CheckoutWindow. This time is ADDED before active Duration.")]
    [Min(0f)]
    public float telegraphDuration = 2f;

    [Tooltip(
        "Used only by ZoneLoot. Added after the active loot-drop Duration. " +
        "No new zone loot is released, but the ArenaZone remains Active so " +
        "its power-up spawners and existing gameplay stay available."
    )]
    [Min(0f)]
    public float closingDuration = 0f;

    [Tooltip("CartRestock = exact carts released. ZoneLoot = exact zone-wide loot budget.")]
    [Min(0)]
    public int resourceBudget = 8;

    [Tooltip("How many resources become due per normal pulse. Budget remains authoritative.")]
    [Min(1)]
    public int batchSize = 1;

    [Tooltip("Used only by ZoneLoot.")]
    public ArenaZoneId zone = ArenaZoneId.ZoneA;

    [Tooltip("Used only by CheckoutWindow.")]
    public CheckoutStationMask checkoutStations = CheckoutStationMask.North | CheckoutStationMask.South;

    public float GetPlannedDuration()
    {
        switch (type)
        {
            case MatchFlowSessionType.ZoneLoot:
                return
                    Mathf.Max(0f, telegraphDuration) +
                    Mathf.Max(0f, duration) +
                    Mathf.Max(0f, closingDuration);

            case MatchFlowSessionType.CheckoutWindow:
                return Mathf.Max(0f, telegraphDuration) + Mathf.Max(0f, duration);

            default:
                return Mathf.Max(0f, duration);
        }
    }
}

[CreateAssetMenu(menuName = "Marmart Carts/Match Flow Profile", fileName = "MatchFlowProfile")]
public class MatchFlowProfile : ScriptableObject
{
    [Tooltip(
        "Exact authored match timeline. " +
        "A valid gameplay profile must contain exactly one EndGameWrap and it must be the final session."
    )]
    [SerializeField] private List<MatchFlowSession> sessions = new List<MatchFlowSession>();

    public IReadOnlyList<MatchFlowSession> Sessions => sessions;

    public float GetPlannedDuration()
    {
        float total = 0f;

        for (int i = 0; i < sessions.Count; i++)
        {
            MatchFlowSession session = sessions[i];
            if (session != null) total += session.GetPlannedDuration();
        }

        return total;
    }

    public int GetEndGameWrapCount()
    {
        int count = 0;

        for (int i = 0; i < sessions.Count; i++)
        {
            MatchFlowSession session = sessions[i];

            if (session != null && session.type == MatchFlowSessionType.EndGameWrap)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsEndGameWrapLast()
    {
        if (sessions == null || sessions.Count == 0) return false;

        MatchFlowSession finalSession = sessions[sessions.Count - 1];
        return finalSession != null && finalSession.type == MatchFlowSessionType.EndGameWrap;
    }

    public bool HasValidTerminalEndGameWrap()
    {
        return GetEndGameWrapCount() == 1 && IsEndGameWrapLast();
    }
}
