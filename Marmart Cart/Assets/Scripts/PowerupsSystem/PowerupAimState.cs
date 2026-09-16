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
    public PowerupId PowerupId;

    public Vector2 RawAimInput;
    public float RawAimMagnitude;
    public float AdjustedAimMagnitude;
    public Vector3 AimDirection;

    public Vector3 RangeOrigin;
    public Vector3 StartPosition;
    public Vector3 LandingPosition;
    public Vector3 LandingNormal;

    // Presentation follows the unchanged ground-target trajectory only until
    // its first configured shelf/wall hit. When unobstructed these equal the
    // authoritative landing values and normalized time is 1.
    public bool TrajectoryObstructed;
    public float PreviewEndNormalizedTime;
    public Vector3 PreviewEndPosition;
    public Vector3 PreviewEndNormal;

    public float RequestedRange;
    public float ResolvedPlanarDistance;
    public float FlightDuration;
    public float ArcHeight;
    public float ImpactPreviewRadius;

    public uint Revision;
}
