using System;
using UnityEngine;

public enum PowerupProjectileCompletionReason
{
    Landed,
    LeadingCartHit,
    EnvironmentBlocked,
    Cancelled
}

[Serializable]
public struct ResolvedPowerupProjectileLaunch
{
    public uint ShotVersion;
    public int PatternEntryIndex;
    public PowerupId PowerupId;
    public int OwnerPlayerIndex;

    public Vector3 StartPosition;
    public Vector3 LandingPosition;
    public Vector3 LandingNormal;
    public float FlightDuration;
    public float ArcHeight;

    public PowerupProjectileSweepShape SweepShape;
    public float SphereRadius;
    public Vector3 BoxHalfExtents;
    public Quaternion CollisionRotation;
    public LayerMask LeadingCartTargetMask;
    public LayerMask EnvironmentBlockingMask;
    public bool IgnoreOwnerInFlight;
    public bool IgnoreCheckoutTargets;

    public Vector3 VisualSpinAxis;
    public float VisualSpinDegreesPerSecond;
    public float VisualScaleMultiplier;
}

[Serializable]
public struct PowerupProjectileCompletion
{
    public uint ShotVersion;
    public int PatternEntryIndex;
    public PowerupId PowerupId;
    public int OwnerPlayerIndex;
    public PowerupProjectileCompletionReason Reason;
    public Vector3 Position;
    public Vector3 SurfaceNormal;
    public LeadingCartPowerupTarget HitTarget;
}
