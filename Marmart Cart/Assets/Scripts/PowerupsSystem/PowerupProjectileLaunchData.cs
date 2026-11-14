using System;
using UnityEngine;

public enum PowerupProjectileCompletionReason
{
    Landed,
    LeadingCartHit,
    ChainedCartHit,
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

    public Quaternion CollisionRotation;
    public LayerMask CartTargetMask;
    public LayerMask EnvironmentBlockingMask;
    public bool HasEffectOnLeadingCart;
    public bool HasEffectOnChainedCarts;
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
    public PowerupCartTarget HitTarget;
    public PowerupCartTargetKind HitCartKind;
    public int HitPlayerIndex;
    public Collider HitCollider;
    public PlayerPowerupController HitOwnerController;
    public CartControlScript HitLeadingCartControl;
    public ChainedCartManager HitChainedCartManager;
}
