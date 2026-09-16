using System;
using UnityEngine;

/// <summary>
/// Exact semantic targeting result for one local player. This is gameplay
/// state, not smoothed presentation state, and is also suitable for taking an
/// immutable launch snapshot when a targeted use request is accepted.
/// </summary>
[Serializable]
public struct PowerupAimState
{
    public int PlayerIndex;
    public bool Visible;
    public bool TargetValid;
    public PowerupId PowerupId;

    public Vector2 RawAimInput;
    public float RawAimMagnitude;
    public float AdjustedAimMagnitude;
    public Vector3 AimDirection;

    public Vector3 RangeOrigin;
    public Vector3 StartPosition;
    public Vector3 LandingPosition;
    public Vector3 LandingNormal;

    public float RequestedRange;
    public float ResolvedPlanarDistance;
    public float FlightDuration;
    public float ArcHeight;
    public float ImpactPreviewRadius;

    public uint Revision;
}
