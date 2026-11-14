using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Direct manipulation of a <see cref="RectPlacement"/> on an <see cref="IEditSurface"/>: four corners and
    /// four edges resize it, its body moves it, a ringed dot re-hangs its pivot without moving it, and a stem
    /// above the top edge turns it. Handle sizes are screen pixels, so they read the same at any zoom, and the
    /// box is drawn turned about the pivot exactly as the placement resolves.
    /// </summary>
    /// <remarks>
    /// Gestures: <c>Shift</c> keeps the aspect while resizing a corner, locks a move to one axis, and snaps a
    /// turn to 15°; <c>Alt</c> resizes about the centre; <c>Ctrl</c> (<c>Cmd</c>) snaps to the editor's move
    /// grid; <c>Esc</c> abandons the gesture. Call from a <see cref="Handles"/> context — an <c>OnSceneGUI</c>
    /// or <see cref="SceneView.duringSceneGui"/> — once per event for one placement: control ids are claimed in
    /// a fixed order, so the call must not be skipped on any event type.
    /// </remarks>
    public static class RectPlacementHandles
    {
        private const int Pivot = 8;
        private const int Turn = 9;
        private const int Body = 10;
        private const int HandleCount = 11;

        private const float CornerRadius = 4.5f;
        private const float EdgeRadius = 3.5f;
        private const float PivotRadius = 5.5f;
        private const float PivotCore = 1.75f;
        private const float TurnRadius = 4f;
        private const float HoverGrowth = 1.5f;
        private const float TurnStemPixels = 26f;
        private const float FrameWidth = 1.5f;
        private const float GuideWidth = 1f;
        private const float RingWidth = 1.6f;
        private const float PickPixelRadius = 11f;
        private const float CursorPixelRadius = 11f;
        private const float TurnSnapDegrees = 15f;

        private static readonly bool[] movesMinX = { true, false, false, true, false, false, false, true };
        private static readonly bool[] movesMaxX = { false, true, true, false, false, true, false, false };
        private static readonly bool[] movesMinY = { true, true, false, false, true, false, false, false };
        private static readonly bool[] movesMaxY = { false, false, true, true, false, false, true, false };

        private static int activeHandle = -1;
        private static int activeControl;
        private static RectPlacement gestureStart;
        private static Vector2 gesturePointer;
        private static GUIStyle readoutStyle;

        /// <summary>Whether a handle gesture is in progress.</summary>
        public static bool IsBusy => activeHandle >= 0;

        /// <summary>What the gesture in progress is doing, for the undo entry it earns; empty while idle.</summary>
        public static string GestureName => activeHandle switch
        {
            < 0 => string.Empty,
            Body => "Move Rect",
            Pivot => "Set Rect Pivot",
            Turn => "Turn Rect",
            _ => "Resize Rect",
        };

        /// <summary>
        /// Draws and drives the handles of <paramref name="placement"/> inside <paramref name="parent"/>, and
        /// reports whether this event changed it. <paramref name="accent"/> carries the frame and the grabbed
        /// handle; resting ones take the shared knot palette.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="surface"/> is <see langword="null"/>.</exception>
        public static bool Draw(IEditSurface surface, Rect parent, ref RectPlacement placement, Color accent)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (activeHandle >= 0 && GUIUtility.hotControl != activeControl) activeHandle = -1;

            Span<Vector2> points = stackalloc Vector2[HandleCount];
            Place(surface, parent, placement, points);
            Span<int> ids = stackalloc int[HandleCount];
            for (var i = 0; i < HandleCount; i++)
                ids[i] = GUIUtility.GetControlID(FocusType.Passive);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.Layout:
                    for (var i = 0; i < Body; i++)
                        HandleUtility.AddControl(ids[i],
                            surface.PointerScreenDistance(surface.PlaneToWorld(points[i])) - PickPixelRadius);
                    if (Encloses(points, surface.PointerPlane)) HandleUtility.AddControl(ids[Body], 0f);
                    return false;

                case EventType.Repaint:
                    Render(surface, parent, placement, points, ids, accent);
                    return false;

                case EventType.MouseDown:
                    if (e.button != 0 || activeHandle >= 0) return false;
                    var hit = IndexOf(ids, HandleUtility.nearestControl);
                    if (hit < 0) return false;
                    activeHandle = hit;
                    activeControl = ids[hit];
                    gestureStart = placement;
                    gesturePointer = surface.PointerPlane;
                    GUIUtility.hotControl = activeControl;
                    e.Use();
                    return false;

                case EventType.MouseDrag:
                    if (activeHandle < 0) return false;
                    placement = Apply(activeHandle, parent, gestureStart, gesturePointer,
                        surface.PointerPlane, e.shift, e.alt, e.control || e.command);
                    e.Use();
                    return true;

                case EventType.KeyDown:
                    if (activeHandle < 0 || e.keyCode != KeyCode.Escape) return false;
                    placement = gestureStart;
                    End();
                    e.Use();
                    return true;

                case EventType.MouseUp:
                    if (activeHandle < 0) return false;
                    End();
                    e.Use();
                    return false;
            }
            return false;
        }

        /// <summary>Abandons an in-flight gesture, for a host that stops editing while the mouse is down.</summary>
        public static void Cancel() => End();

        private static void End()
        {
            if (activeHandle < 0) return;
            if (GUIUtility.hotControl == activeControl) GUIUtility.hotControl = 0;
            activeHandle = -1;
        }

        /// <summary>The eleven handle positions in surface-plane coordinates: four corners, four edge midpoints, the pivot, the turn stem's end, and the body's centre.</summary>
        private static void Place(IEditSurface surface, Rect parent, in RectPlacement placement,
            Span<Vector2> into)
        {
            var rect = placement.Resolve(parent);
            var pivot = placement.PivotOf(rect);
            var turn = placement.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(turn), sin = Mathf.Sin(turn);

            into[0] = Spin(new Vector2(rect.xMin, rect.yMin), pivot, cos, sin);
            into[1] = Spin(new Vector2(rect.xMax, rect.yMin), pivot, cos, sin);
            into[2] = Spin(new Vector2(rect.xMax, rect.yMax), pivot, cos, sin);
            into[3] = Spin(new Vector2(rect.xMin, rect.yMax), pivot, cos, sin);
            for (var i = 0; i < 4; i++) into[4 + i] = (into[i] + into[(i + 1) % 4]) * 0.5f;
            into[Pivot] = pivot;
            into[Turn] = into[6] + new Vector2(-sin, cos) *
                (TurnStemPixels * surface.PlaneUnitsPerPixel(into[6]));
            into[Body] = Spin(rect.center, pivot, cos, sin);
        }

        private static RectPlacement Apply(int handle, Rect parent, in RectPlacement origin,
            Vector2 from, Vector2 to, bool shift, bool alt, bool snap)
        {
            var rect = origin.Resolve(parent);
            var pivot = origin.PivotOf(rect);
            var turn = origin.rotation * Mathf.Deg2Rad;
            var result = origin;

            if (handle == Turn)
            {
                var swept = Mathf.Atan2(to.y - pivot.y, to.x - pivot.x) -
                            Mathf.Atan2(from.y - pivot.y, from.x - pivot.x);
                var turned = origin.rotation + swept * Mathf.Rad2Deg;
                if (shift) turned = Mathf.Round(turned / TurnSnapDegrees) * TurnSnapDegrees;
                result.rotation = Mathf.Repeat(turned, 360f);
                return result;
            }

            if (handle == Body)
            {
                var moved = to - from;
                if (shift)
                    moved = Mathf.Abs(moved.x) >= Mathf.Abs(moved.y)
                        ? new Vector2(moved.x, 0f)
                        : new Vector2(0f, moved.y);
                var next = origin.anchoredPosition + moved;
                result.anchoredPosition = snap ? Snapped(next) : next;
                return result;
            }

            if (handle == Pivot)
            {
                var carried = Unspin(to - from, turn);
                var next = new Vector2(
                    rect.width > 1e-4f ? origin.pivot.x + carried.x / rect.width : origin.pivot.x,
                    rect.height > 1e-4f ? origin.pivot.y + carried.y / rect.height : origin.pivot.y);
                if (snap) next = new Vector2(Mathf.Round(next.x * 2f) * 0.5f, Mathf.Round(next.y * 2f) * 0.5f);
                var anchoredMin = parent.min + Vector2.Scale(origin.anchorMin, parent.size);
                var anchoredMax = parent.min + Vector2.Scale(origin.anchorMax, parent.size);
                result.pivot = next;
                result.anchoredPosition = pivot +
                    Rotate(Vector2.Scale(next - origin.pivot, rect.size), turn) - anchoredMin -
                    Vector2.Scale(next, anchoredMax - anchoredMin);
                return result;
            }

            var delta = Unspin(to - from, turn);
            var min = rect.min;
            var max = rect.max;
            if (movesMinX[handle]) min.x += delta.x;
            if (movesMaxX[handle]) max.x += delta.x;
            if (movesMinY[handle]) min.y += delta.y;
            if (movesMaxY[handle]) max.y += delta.y;
            if (alt)
            {
                if (movesMinX[handle]) max.x -= delta.x;
                if (movesMaxX[handle]) min.x -= delta.x;
                if (movesMinY[handle]) max.y -= delta.y;
                if (movesMaxY[handle]) min.y -= delta.y;
            }
            if (snap)
            {
                if (movesMinX[handle]) min.x = Snapped(min.x);
                if (movesMaxX[handle]) max.x = Snapped(max.x);
                if (movesMinY[handle]) min.y = Snapped(min.y);
                if (movesMaxY[handle]) max.y = Snapped(max.y);
            }
            if (shift && handle < 4) KeepAspect(rect, handle, alt, ref min, ref max);
            var target = Rect.MinMaxRect(min.x, min.y, Mathf.Max(max.x, min.x), Mathf.Max(max.y, min.y));
            if (turn != 0f)
            {
                var drift = pivot - (target.min + Vector2.Scale(origin.pivot, target.size));
                target.position += drift - Rotate(drift, turn);
            }
            return Reframe(parent, origin, target);
        }

        /// <summary>Re-proportions a dragged corner to the ratio the rectangle started at, growing about whichever point the gesture holds still.</summary>
        private static void KeepAspect(Rect rect, int handle, bool alt, ref Vector2 min, ref Vector2 max)
        {
            if (rect.width <= 1e-4f || rect.height <= 1e-4f) return;
            var scale = Mathf.Max(Mathf.Abs(max.x - min.x) / rect.width,
                Mathf.Abs(max.y - min.y) / rect.height);
            var size = new Vector2(rect.width * scale, rect.height * scale);
            if (alt)
            {
                min = rect.center - size * 0.5f;
                max = rect.center + size * 0.5f;
                return;
            }
            var held = new Vector2(movesMinX[handle] ? rect.xMax : rect.xMin,
                movesMinY[handle] ? rect.yMax : rect.yMin);
            var free = held + new Vector2(movesMinX[handle] ? -size.x : size.x,
                movesMinY[handle] ? -size.y : size.y);
            min = Vector2.Min(held, free);
            max = Vector2.Max(held, free);
        }

        /// <summary>The position and size that resolve to <paramref name="target"/> under the anchors and pivot <paramref name="origin"/> already holds.</summary>
        private static RectPlacement Reframe(Rect parent, in RectPlacement origin, Rect target)
        {
            var anchoredMin = parent.min + Vector2.Scale(origin.anchorMin, parent.size);
            var anchoredMax = parent.min + Vector2.Scale(origin.anchorMax, parent.size);
            var result = origin;
            result.sizeDelta = target.size - (anchoredMax - anchoredMin);
            result.anchoredPosition = target.min - anchoredMin +
                                      Vector2.Scale(result.sizeDelta, origin.pivot);
            return result;
        }

        private static void Render(IEditSurface surface, Rect parent, in RectPlacement placement,
            ReadOnlySpan<Vector2> points, ReadOnlySpan<int> ids, Color accent)
        {
            var frame = Fade(accent, 0.85f);
            var guide = Fade(accent, 0.26f);

            DrawRect(surface, parent, guide, GuideWidth);
            if (activeHandle >= 0)
            {
                var anchored = Rect.MinMaxRect(
                    parent.xMin + placement.anchorMin.x * parent.width,
                    parent.yMin + placement.anchorMin.y * parent.height,
                    parent.xMin + placement.anchorMax.x * parent.width,
                    parent.yMin + placement.anchorMax.y * parent.height);
                if (anchored != parent) DrawRect(surface, anchored, Fade(accent, 0.45f), GuideWidth);
            }

            for (var i = 0; i < 4; i++)
                surface.DrawLine(points[i], points[(i + 1) % 4], frame, FrameWidth);
            surface.DrawLine(points[6], points[Turn], frame, FrameWidth);

            for (var i = 4; i < 8; i++)
                surface.DrawDot(points[i], Grown(i, ids, EdgeRadius), Tint(i, ids, accent, frame));
            for (var i = 0; i < 4; i++)
                surface.DrawDot(points[i], Grown(i, ids, CornerRadius),
                    Tint(i, ids, accent, EditorResources.IconColor));
            surface.DrawDot(points[Turn], Grown(Turn, ids, TurnRadius), Tint(Turn, ids, accent, frame));

            var pivotColor = Tint(Pivot, ids, accent, EditorResources.IconColor);
            surface.DrawCircle(points[Pivot], Grown(Pivot, ids, PivotRadius), RingWidth, pivotColor);
            surface.DrawDot(points[Pivot], PivotCore, pivotColor);

            Overlay(surface, parent, placement, points);
        }

        /// <summary>Cursors that name each handle before it is grabbed, and the readout that follows a gesture.</summary>
        private static void Overlay(IEditSurface surface, Rect parent, in RectPlacement placement,
            ReadOnlySpan<Vector2> points)
        {
            Handles.BeginGUI();
            try
            {
                if (activeHandle >= 0)
                {
                    EditorGUIUtility.AddCursorRect(new Rect(0f, 0f, Screen.width, Screen.height),
                        CursorFor(activeHandle, surface, points));
                    DrawReadout(Readout(parent, placement));
                    return;
                }

                var body = ToScreen(surface, points[Body]);
                var extent = Rect.MinMaxRect(body.x, body.y, body.x, body.y);
                for (var i = 0; i < 4; i++)
                {
                    var corner = ToScreen(surface, points[i]);
                    extent = Rect.MinMaxRect(
                        Mathf.Min(extent.xMin, corner.x), Mathf.Min(extent.yMin, corner.y),
                        Mathf.Max(extent.xMax, corner.x), Mathf.Max(extent.yMax, corner.y));
                }
                EditorGUIUtility.AddCursorRect(extent, MouseCursor.MoveArrow);
                for (var i = 0; i < Body; i++)
                {
                    var at = ToScreen(surface, points[i]);
                    EditorGUIUtility.AddCursorRect(
                        new Rect(at.x - CursorPixelRadius, at.y - CursorPixelRadius,
                            CursorPixelRadius * 2f, CursorPixelRadius * 2f),
                        CursorFor(i, surface, points));
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        /// <summary>What the gesture is producing, in the units the fields carry.</summary>
        private static string Readout(Rect parent, in RectPlacement placement)
        {
            if (activeHandle == Turn) return Num(placement.rotation) + "°";
            if (activeHandle == Pivot)
                return Num(placement.pivot.x) + ", " + Num(placement.pivot.y);
            if (activeHandle == Body)
                return Num(placement.anchoredPosition.x) + ", " + Num(placement.anchoredPosition.y);
            var rect = placement.Resolve(parent);
            return Num(rect.width) + " × " + Num(rect.height);
        }

        private static void DrawReadout(string text)
        {
            readoutStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            var content = new GUIContent(text);
            var size = readoutStyle.CalcSize(content);
            var at = Event.current.mousePosition;
            var box = new Rect(at.x + 16f, at.y - size.y - 14f, size.x + 12f, size.y + 4f);
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.72f));
            GUI.Label(box, content, readoutStyle);
        }

        /// <summary>
        /// The cursor a handle deserves: resize arrows aligned to where the handle actually sits on screen, so a
        /// turned rectangle points the way it looks rather than the way it is stored.
        /// </summary>
        private static MouseCursor CursorFor(int handle, IEditSurface surface, ReadOnlySpan<Vector2> points)
        {
            if (handle == Body) return MouseCursor.MoveArrow;
            if (handle == Pivot) return MouseCursor.MoveArrow;
            if (handle == Turn) return MouseCursor.RotateArrow;

            var outward = ToScreen(surface, points[handle]) - ToScreen(surface, points[Body]);
            var degrees = Mathf.Repeat(Mathf.Atan2(-outward.y, outward.x) * Mathf.Rad2Deg, 180f);
            if (degrees < 22.5f || degrees >= 157.5f) return MouseCursor.ResizeHorizontal;
            if (degrees < 67.5f) return MouseCursor.ResizeUpRight;
            if (degrees < 112.5f) return MouseCursor.ResizeVertical;
            return MouseCursor.ResizeUpLeft;
        }

        private static void DrawRect(IEditSurface surface, Rect rect, Color color, float width)
        {
            var a = new Vector2(rect.xMin, rect.yMin);
            var b = new Vector2(rect.xMax, rect.yMin);
            var c = new Vector2(rect.xMax, rect.yMax);
            var d = new Vector2(rect.xMin, rect.yMax);
            surface.DrawLine(a, b, color, width);
            surface.DrawLine(b, c, color, width);
            surface.DrawLine(c, d, color, width);
            surface.DrawLine(d, a, color, width);
        }

        private static Vector2 ToScreen(IEditSurface surface, Vector2 plane)
            => HandleUtility.WorldToGUIPoint(surface.PlaneToWorld(plane));

        /// <summary>Whether the pointer is inside the drawn quad, which is convex however the placement is turned.</summary>
        private static bool Encloses(ReadOnlySpan<Vector2> corners, Vector2 point)
        {
            var sign = 0;
            for (var i = 0; i < 4; i++)
            {
                var edge = corners[(i + 1) % 4] - corners[i];
                var side = edge.x * (point.y - corners[i].y) - edge.y * (point.x - corners[i].x);
                if (Mathf.Abs(side) < 1e-6f) continue;
                var current = side > 0f ? 1 : -1;
                if (sign == 0) sign = current;
                else if (sign != current) return false;
            }
            return sign != 0;
        }

        private static bool Hovered(int handle, ReadOnlySpan<int> ids)
            => activeHandle >= 0
                ? activeControl == ids[handle]
                : HandleUtility.nearestControl == ids[handle];

        private static float Grown(int handle, ReadOnlySpan<int> ids, float radius)
            => Hovered(handle, ids) ? radius + HoverGrowth : radius;

        private static Color Tint(int handle, ReadOnlySpan<int> ids, Color hover, Color resting)
            => Hovered(handle, ids) ? hover : resting;

        private static int IndexOf(ReadOnlySpan<int> ids, int control)
        {
            for (var i = 0; i < ids.Length; i++)
                if (ids[i] == control) return i;
            return -1;
        }

        private static Color Fade(Color color, float alpha)
            => new(color.r, color.g, color.b, color.a * alpha);

        private static Vector2 Spin(Vector2 point, Vector2 pivot, float cos, float sin)
        {
            var d = point - pivot;
            return pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
        }

        private static Vector2 Rotate(Vector2 vector, float turn)
        {
            float cos = Mathf.Cos(turn), sin = Mathf.Sin(turn);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        private static Vector2 Unspin(Vector2 vector, float turn) => Rotate(vector, -turn);

        private static Vector2 Snapped(Vector2 value) => new(Snapped(value.x), Snapped(value.y));

        private static float Snapped(float value)
        {
            var step = Mathf.Max(EditorSnapSettings.move.x, 1e-4f);
            return Mathf.Round(value / step) * step;
        }

        private static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
