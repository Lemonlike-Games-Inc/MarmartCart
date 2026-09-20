using System;
using UnityEngine;

public enum MatchFlowSessionType
{
    FreePlay = 0,
    CartRestock = 1,
    ZoneLoot = 2,
    CheckoutWindow = 3,
    EndGameWrap = 4,

    [InspectorName("Checkout + Restock")]
    CheckoutRestock = 5,

    [InspectorName("Tutorial Zone Loot")]
    TutorialZoneLoot = 6
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
        "Tutorial Zone Loot: active loot-drop duration for all four zones after telegraph.\n" +
        "CheckoutWindow: how long selected checkout stations remain open.\n" +
        "Checkout + Restock: how long checkout and cart distribution are active together.\n" +
        "EndGameWrap: final gameplay buffer before the Director requests match end."
    )]
    [Min(0f)]
    public float duration = 5f;

    [Tooltip("Used by ZoneLoot, Tutorial Zone Loot, CheckoutWindow, and Checkout + Restock. This time is ADDED before active Duration.")]
    [Min(0f)]
    public float telegraphDuration = 2f;

    [Tooltip(
        "Used by ZoneLoot, Tutorial Zone Loot, and Checkout + Restock immediately after Active. " +
        "This is the longer player-breathing window.\n" +
        "ZoneLoot keeps the selected zone/power-ups active without releasing new loot.\n" +
        "Tutorial Zone Loot keeps all four zones/power-ups active without releasing new loot.\n" +
        "Checkout + Restock remains quiet with checkout stations closed and " +
        "no new carts released."
    )]
    [Min(0f)]
    public float freePlayDuration = 0f;

    [Tooltip(
        "Used by ZoneLoot, Tutorial Zone Loot, and Checkout + Restock after FreePlay and immediately " +
        "before the next session. The Player HUD Session Guide begins when " +
        "this short phase starts. Gameplay remains quiet while the current " +
        "session's area-light selection stays active."
    )]
    [Min(0f)]
    public float closingDuration = 0f;

    [Tooltip(
        "CartRestock / Checkout + Restock = exact carts released. " +
        "ZoneLoot = exact budget for the selected zone. " +
        "Tutorial Zone Loot = exact budget PER zone, applied independently to all four zones."
    )]
    [Min(0)]
    public int resourceBudget = 8;

    [Tooltip("How many resources become due per normal pulse. Budget remains authoritative.")]
    [Min(1)]
    public int batchSize = 1;

    [Tooltip("Used only by ZoneLoot. Tutorial Zone Loot always targets all four zones.")]
    public ArenaZoneId zone = ArenaZoneId.ZoneA;

    [Tooltip("Used by CheckoutWindow and Checkout + Restock.")]
    public CheckoutStationMask checkoutStations = CheckoutStationMask.North | CheckoutStationMask.South;

    public float GetPlannedDuration()
    {
        switch (type)
        {
            case MatchFlowSessionType.ZoneLoot:
            case MatchFlowSessionType.TutorialZoneLoot:
            case MatchFlowSessionType.CheckoutRestock:
                return
                    Mathf.Max(0f, telegraphDuration) +
                    Mathf.Max(0f, duration) +
                    Mathf.Max(0f, freePlayDuration) +
                    Mathf.Max(0f, closingDuration);

            case MatchFlowSessionType.CheckoutWindow:
                return Mathf.Max(0f, telegraphDuration) + Mathf.Max(0f, duration);

            default:
                return Mathf.Max(0f, duration);
        }
    }
}
