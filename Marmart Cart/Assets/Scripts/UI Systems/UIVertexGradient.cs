using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shades a UI Graphic from top to bottom by multiplying its vertex colours,
/// so one tint (the Graphic's own colour) gets a lit or metallic look without
/// a shader or new art. Works with Simple and Sliced Images.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class UIVertexGradient : BaseMeshEffect
{
    [Tooltip("Multiplier at the top edge.")]
    [SerializeField] private Color top = Color.white;

    [Tooltip("Multiplier at the bottom edge.")]
    [SerializeField] private Color bottom = new Color(0.6f, 0.6f, 0.6f, 1f);

    public void SetColors(Color topColor, Color bottomColor)
    {
        top = topColor;
        bottom = bottomColor;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        Rect rect = graphic.rectTransform.rect;
        if (rect.height <= 0f) return;

        UIVertex vertex = default;

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            float t = Mathf.Clamp01((vertex.position.y - rect.yMin) / rect.height);
            Color color = vertex.color;
            vertex.color = color * Color.Lerp(bottom, top, t);

            vh.SetUIVertex(vertex, i);
        }
    }
}
