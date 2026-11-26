using UnityEngine;

/// <summary>
/// One temporary zoom offset on one player's camera, from
/// CameraManager.AddZoomOffset. The offset is added to the cart-count
/// orthographic size (positive = zoom out). Change SizeOffset freely, for
/// example every frame from a curve; call CameraManager.RemoveZoomOffset when done.
/// </summary>
public sealed class CameraZoomHandle
{
    private CameraManager owner;
    private float sizeOffset;

    public int PlayerIndex { get; }

    /// <summary>False once removed, or after CameraManager was disabled.</summary>
    public bool IsActive => owner != null;

    public float SizeOffset
    {
        get => sizeOffset;
        set
        {
            if (Mathf.Approximately(sizeOffset, value)) return;

            sizeOffset = value;

            if (owner != null) owner.RefreshPlayerLens(PlayerIndex);
        }
    }

    internal CameraZoomHandle(CameraManager owner, int playerIndex, float sizeOffset)
    {
        this.owner = owner;
        PlayerIndex = playerIndex;
        this.sizeOffset = sizeOffset;
    }

    internal void Release()
    {
        owner = null;
    }
}
