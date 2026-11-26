using UnityEngine;

public enum CartCrashCause
{
    /// <summary>LoseBattle() called from code with no contact data.</summary>
    Other,
    OwnChain,
    RivalChain,
    RivalLeadingCart
}

/// <summary>
/// One battle loss, raised by LeadingCartBattleController.OnCrashed for the
/// player who crashed and by OnCausedCrash for the rival who was hit.
/// Player indices are 1-4; 0 means none.
/// </summary>
public struct CartCrashInfo
{
    public CartCrashCause Cause;
    public int CrashedPlayerIndex;
    public int OtherPlayerIndex;

    /// <summary>World point on the hit collider closest to the crashing cart.</summary>
    public Vector3 HitPoint;

    /// <summary>Planar travel direction of the crashing cart just before the hit.</summary>
    public Vector3 HitDirection;

    /// <summary>Planar speed of the crashing cart just before the hit.</summary>
    public float SpeedBeforeHit;

    /// <summary>Follower carts the crashing player lost.</summary>
    public int CartsLost;

    public bool IsIntoRival => Cause == CartCrashCause.RivalChain || Cause == CartCrashCause.RivalLeadingCart;
}
