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
    public ItemDefinition Item;
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
    // The thrower's leading cart can be hit after the apex and while rolling,
    // even with IgnoreOwnerInFlight on (ItemDefinition.HitsThrower).
    public bool CanHitOwner;
    public bool IgnoreCheckoutTargets;

    public Vector3 VisualSpinAxis;
    public float VisualSpinDegreesPerSecond;
    public float VisualScaleMultiplier;

    // From the item's ProjectileRollEffect; RollSpeed 0 means no roll.
    // RollMaxDistance is PositiveInfinity when it rolls until it hits.
    public float RollSpeed;
    public float RollMaxDistance;
    public float RollHeight;
    public Vector3 RollDirection;
    public float RollHitRadius;
    public LayerMask RollCartMask;
    public LayerMask RollBlockingMask;
}

[Serializable]
public struct PowerupProjectileCompletion
{
    public uint ShotVersion;
    public int PatternEntryIndex;
    public ItemDefinition Item;
    public int OwnerPlayerIndex;
    public PowerupProjectileCompletionReason Reason;
    public Vector3 Position;
    public Vector3 SurfaceNormal;
    public PowerupCartTarget HitTarget;
    public PowerupCartTargetKind HitCartKind;
    public int HitPlayerIndex;
    public Collider HitCollider;
    public PlayerItemHand HitOwnerController;
    public CartControlScript HitLeadingCartControl;
    public ChainedCartManager HitChainedCartManager;
}
