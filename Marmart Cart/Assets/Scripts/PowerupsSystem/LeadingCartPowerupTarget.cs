using UnityEngine;

/// <summary>
/// Backward-compatible alias for scenes that already added the first
/// leading-only marker. New setup should use PowerupCartTarget directly.
/// Do not add both components to the same GameObject.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class LeadingCartPowerupTarget : PowerupCartTarget
{
}
