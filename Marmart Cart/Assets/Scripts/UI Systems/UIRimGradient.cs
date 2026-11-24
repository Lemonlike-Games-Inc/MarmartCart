using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Colours a UI Graphic by angle around its centre from a Gradient, so a
/// card rim shows a band of colours running all the way round (the Prismatic
/// tier). The mesh is cut into small pieces first, because a Sliced Image
/// alone has too few vertices for more than a four-corner blend. Multiplies
/// the existing vertex colours, so the Graphic's colour and a UIVertexGradient
/// still shade it. Offset turns the colours around the rim (the motion).
///
/// Added at runtime by the card views through Apply; no prefab needs it.
/// The gradient should end on the colour it starts with, so the band has no seam.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class UIRimGradient : BaseMeshEffect
{
    private const int MaxPiecesPerSide = 32;

    private static readonly List<UIVertex> Stream = new List<UIVertex>(64);
    private static readonly List<UIVertex> Output = new List<UIVertex>(1024);

    [Tooltip("Largest piece of the cut mesh, in UI pixels. Smaller = smoother colour band, more vertices.")]
    [Min(2f)]
    [SerializeField] private float pieceSize = 12f;

    private Gradient gradient;
    private float offset;
    private Rect rect;

    /// <summary>
    /// Shows <paramref name="rimGradient"/> on the target, or turns it off when null.
    /// Adds the effect the first time it is needed, in Play Mode only. Returns it, or null.
    /// </summary>
    public static UIRimGradient Apply(Graphic target, Gradient rimGradient)
    {
        if (target == null) return null;

        UIRimGradient effect = target.GetComponent<UIRimGradient>();

        if (rimGradient == null)
        {
            if (effect != null && effect.enabled) effect.enabled = false;
            return effect;
        }

        if (effect == null)
        {
            if (!Application.isPlaying) return null;
            effect = target.gameObject.AddComponent<UIRimGradient>();
        }

        effect.gradient = rimGradient;
        effect.offset = 0f;
        effect.enabled = true;
        target.SetVerticesDirty();
        return effect;
    }

    /// <summary>Turns the colours around the rim. 1 = one full turn.</summary>
    public void SetOffset(float turns)
    {
        turns = Mathf.Repeat(turns, 1f);
        if (Mathf.Approximately(turns, offset)) return;

        offset = turns;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || gradient == null || vh.currentVertCount == 0) return;

        rect = graphic.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        Stream.Clear();
        Output.Clear();
        vh.GetUIVertexStream(Stream);

        int count = Stream.Count - Stream.Count % 3;
        int i = 0;

        while (i < count)
        {
            // Image quads arrive as two triangles: (v0, v1, v2) and (v2, v3, v0).
            if (i + 6 <= count && IsQuad(i))
            {
                AddPieces(Stream[i], Stream[i + 1], Stream[i + 2], Stream[i + 4]);
                i += 6;
                continue;
            }

            for (int k = 0; k < 3; k++)
            {
                UIVertex vertex = Stream[i + k];
                Tint(ref vertex);
                Output.Add(vertex);
            }

            i += 3;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(Output);
    }

    private static bool IsQuad(int i)
    {
        return Stream[i + 3].position == Stream[i + 2].position &&
               Stream[i + 5].position == Stream[i].position;
    }

    // v0 bottom-left, v1 top-left, v2 top-right, v3 bottom-right.
    private void AddPieces(UIVertex v0, UIVertex v1, UIVertex v2, UIVertex v3)
    {
        float width = Mathf.Abs(v3.position.x - v0.position.x);
        float height = Mathf.Abs(v1.position.y - v0.position.y);

        int columns = Mathf.Clamp(Mathf.CeilToInt(width / pieceSize), 1, MaxPiecesPerSide);
        int rows = Mathf.Clamp(Mathf.CeilToInt(height / pieceSize), 1, MaxPiecesPerSide);

        for (int x = 0; x < columns; x++)
        {
            float u0 = (float)x / columns;
            float u1 = (float)(x + 1) / columns;

            for (int y = 0; y < rows; y++)
            {
                float w0 = (float)y / rows;
                float w1 = (float)(y + 1) / rows;

                UIVertex a = At(v0, v1, v2, v3, u0, w0);
                UIVertex b = At(v0, v1, v2, v3, u0, w1);
                UIVertex c = At(v0, v1, v2, v3, u1, w1);
                UIVertex d = At(v0, v1, v2, v3, u1, w0);

                Output.Add(a);
                Output.Add(b);
                Output.Add(c);
                Output.Add(c);
                Output.Add(d);
                Output.Add(a);
            }
        }
    }

    private UIVertex At(UIVertex v0, UIVertex v1, UIVertex v2, UIVertex v3, float u, float w)
    {
        UIVertex bottom = Lerp(v0, v3, u);
        UIVertex top = Lerp(v1, v2, u);
        UIVertex vertex = Lerp(bottom, top, w);
        Tint(ref vertex);
        return vertex;
    }

    private void Tint(ref UIVertex vertex)
    {
        Vector2 local = new Vector2(
            (vertex.position.x - rect.center.x) / rect.width,
            (vertex.position.y - rect.center.y) / rect.height);

        float angle = Mathf.Atan2(local.y, local.x) / (2f * Mathf.PI) + 0.5f;
        Color color = gradient.Evaluate(Mathf.Repeat(angle + offset, 1f));
        vertex.color = (Color)vertex.color * color;
    }

    private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
    {
        UIVertex vertex = a;
        vertex.position = Vector3.LerpUnclamped(a.position, b.position, t);
        vertex.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
        vertex.color = Color32.LerpUnclamped(a.color, b.color, t);
        return vertex;
    }
}
