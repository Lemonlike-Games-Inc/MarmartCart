using System;
using UnityEngine;

public enum PlayerSessionGuideTargetKind
{
    None = 0,
    ZoneLoot = 1,
    CheckoutRestock = 2
}

public enum PlayerSessionGuideIndicationPhase
{
    None = 0,
    Countdown = 1,
    Active = 2
}

/// <summary>
/// Shared semantic forecast state. It contains no Shapes geometry or styling.
/// Every local-player renderer reads this same target/countdown and combines it
/// with that player's own HUDWorldAnchor.
/// </summary>
[Serializable]
public sealed class PlayerSessionGuideState
{
    [SerializeField] private bool visible;
    [SerializeField] private PlayerSessionGuideTargetKind targetKind;
    [SerializeField] private PlayerSessionGuideIndicationPhase indicationPhase;
    [SerializeField] private int sourceSessionIndex = -1;
    [SerializeField] private int targetSessionIndex = -1;
    [SerializeField] private ArenaZoneId targetZoneId = ArenaZoneId.ZoneA;
    [SerializeField] private ArenaZone targetZone;
    [SerializeField] private RandomGroundSpawnArea targetDirectArea;
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private string targetDisplayName = string.Empty;
    [SerializeField] private float secondsUntilActive;
    [SerializeField] private int countdownSeconds;

    public bool Visible => visible;
    public PlayerSessionGuideTargetKind TargetKind => targetKind;
    public PlayerSessionGuideIndicationPhase IndicationPhase => indicationPhase;
    public int SourceSessionIndex => sourceSessionIndex;
    public int TargetSessionIndex => targetSessionIndex;
    public ArenaZoneId TargetZoneId => targetZoneId;
    public ArenaZone TargetZone => targetZone;
    public RandomGroundSpawnArea TargetDirectArea => targetDirectArea;
    public Transform TargetAnchor => targetAnchor;
    public string TargetDisplayName => targetDisplayName;
    public float SecondsUntilActive => secondsUntilActive;
    public int CountdownSeconds => countdownSeconds;

    internal bool Apply(
        PlayerSessionGuideTargetKind kind,
        PlayerSessionGuideIndicationPhase phase,
        int sourceIndex,
        int targetIndex,
        ArenaZoneId zoneId,
        ArenaZone zone,
        RandomGroundSpawnArea directArea,
        Transform anchor,
        string displayName,
        float secondsRemaining)
    {
        float safeSeconds = Mathf.Max(0f, secondsRemaining);
        int safeCountdown =
            phase == PlayerSessionGuideIndicationPhase.Countdown
                ? Mathf.Max(1, Mathf.CeilToInt(safeSeconds))
                : 0;

        bool semanticChange =
            !visible ||
            targetKind != kind ||
            indicationPhase != phase ||
            sourceSessionIndex != sourceIndex ||
            targetSessionIndex != targetIndex ||
            targetZoneId != zoneId ||
            targetZone != zone ||
            targetDirectArea != directArea ||
            targetAnchor != anchor ||
            targetDisplayName != displayName ||
            countdownSeconds != safeCountdown;

        visible = true;
        targetKind = kind;
        indicationPhase = phase;
        sourceSessionIndex = sourceIndex;
        targetSessionIndex = targetIndex;
        targetZoneId = zoneId;
        targetZone = zone;
        targetDirectArea = directArea;
        targetAnchor = anchor;
        targetDisplayName = displayName ?? string.Empty;
        secondsUntilActive = safeSeconds;
        countdownSeconds = safeCountdown;

        return semanticChange;
    }

    internal bool Clear()
    {
        bool changed = visible || targetKind != PlayerSessionGuideTargetKind.None;

        visible = false;
        targetKind = PlayerSessionGuideTargetKind.None;
        indicationPhase = PlayerSessionGuideIndicationPhase.None;
        sourceSessionIndex = -1;
        targetSessionIndex = -1;
        targetZoneId = ArenaZoneId.ZoneA;
        targetZone = null;
        targetDirectArea = null;
        targetAnchor = null;
        targetDisplayName = string.Empty;
        secondsUntilActive = 0f;
        countdownSeconds = 0;

        return changed;
    }
}
