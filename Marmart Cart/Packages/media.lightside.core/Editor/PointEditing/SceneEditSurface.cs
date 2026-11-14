using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSide
{
    /// <summary>
    /// SceneView implementation of <see cref="IEditSurface"/>. Points are edited on a flat working plane given
    /// by a world basis (origin + right + up) — for a UI element, its RectTransform plane. The pointer
    /// is projected onto that plane through the SceneView camera ray, and picking is measured in screen pixels so
    /// the hit radius is constant regardless of camera distance or zoom. Use only inside an <c>OnSceneGUI</c>
    /// (Handles) context, where <see cref="HandleUtility"/> and <see cref="Event.current"/> are valid.
    /// </summary>
    public sealed class SceneEditSurface : IEditSurface
    {
        /// <summary>Sides of the polygon a dot is filled with; enough that its edge reads as a curve at any handle size.</summary>
        private const int DiscSegments = 24;

        private static readonly Vector3[] disc = new Vector3[DiscSegments];

        private Vector3 origin;
        private Vector3 right;
        private Vector3 up;
        private Vector3 normal;

        /// <summary>Creates a surface for the plane spanned by <paramref name="right"/> and <paramref name="up"/> through <paramref name="origin"/> (world space).</summary>
        public SceneEditSurface(Vector3 origin, Vector3 right, Vector3 up) => SetPlane(origin, right, up);

        /// <summary>Surface on the local XY plane of <paramref name="t"/> — the natural plane for a UI RectTransform.</summary>
        public static SceneEditSurface FromTransform(Transform t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new SceneEditSurface(t.position, t.right, t.up);
        }

        /// <summary>Repoints the working plane; call when the edited object moves so projection stays in sync.</summary>
        public void SetPlane(Vector3 origin, Vector3 right, Vector3 up)
        {
            this.origin = origin;
            this.right = right.normalized;
            this.up = up.normalized;
            normal = Vector3.Cross(this.right, this.up).normalized;
        }

        /// <inheritdoc/>
        public Vector2 PointerPlane => WorldToPlane(PointerWorld());

        /// <inheritdoc/>
        public Vector2 WorldToPlane(Vector3 world)
        {
            var d = world - origin;
            return new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up));
        }

        /// <inheritdoc/>
        public Vector3 PlaneToWorld(Vector2 plane) => origin + right * plane.x + up * plane.y;

        /// <inheritdoc/>
        public float PointerScreenDistance(Vector3 world)
            => Vector2.Distance(HandleUtility.WorldToGUIPoint(world), Event.current.mousePosition);

        /// <inheritdoc/>
        public void DrawMarquee(Vector2 planeA, Vector2 planeB, Color fill, Color outline)
        {
            var verts = new[]
            {
                PlaneToWorld(new Vector2(planeA.x, planeA.y)),
                PlaneToWorld(new Vector2(planeB.x, planeA.y)),
                PlaneToWorld(new Vector2(planeB.x, planeB.y)),
                PlaneToWorld(new Vector2(planeA.x, planeB.y)),
            };
            using var overlay = Overlay.Begin();
            Handles.DrawSolidRectangleWithOutline(verts, fill, outline);
        }

        /// <inheritdoc/>
        public void DrawCurve(Vector2 from, Vector2 fromHandle, Vector2 toHandle, Vector2 to, Color color,
            float widthPixels)
        {
            using var overlay = Overlay.Begin();
            Handles.DrawBezier(PlaneToWorld(from), PlaneToWorld(to), PlaneToWorld(fromHandle),
                PlaneToWorld(toHandle), color, null, widthPixels);
        }

        /// <inheritdoc/>
        public void DrawLine(Vector2 planeA, Vector2 planeB, Color color, float widthPixels)
        {
            using var overlay = Overlay.Begin();
            Handles.color = color;
            Handles.DrawAAPolyLine(widthPixels, PlaneToWorld(planeA), PlaneToWorld(planeB));
        }

        /// <inheritdoc/>
        public void DrawDot(Vector2 plane, float radiusPixels, Color color)
        {
            var world = PlaneToWorld(plane);
            var radius = radiusPixels * PlaneUnitsPerPixel(world);
            for (var i = 0; i < DiscSegments; i++)
            {
                var angle = i * (2f * Mathf.PI / DiscSegments);
                disc[i] = world + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
            }
            using var overlay = Overlay.Begin();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(disc);
        }

        /// <inheritdoc/>
        public void DrawCircle(Vector2 plane, float radiusPixels, float widthPixels, Color color)
        {
            var world = PlaneToWorld(plane);
            using var overlay = Overlay.Begin();
            Handles.color = color;
            Handles.DrawWireDisc(world, normal, radiusPixels * PlaneUnitsPerPixel(world), widthPixels);
        }

        /// <summary>
        /// Draws over the scene rather than into it for the length of its scope. Handles sit on the very plane
        /// they edit, where equal depth leaves them fighting the surface they annotate.
        /// </summary>
        private readonly struct Overlay : IDisposable
        {
            private readonly CompareFunction previous;

            private Overlay(CompareFunction previous) => this.previous = previous;

            public static Overlay Begin()
            {
                var previous = Handles.zTest;
                Handles.zTest = CompareFunction.Always;
                return new Overlay(previous);
            }

            public void Dispose() => Handles.zTest = previous;
        }

        /// <inheritdoc/>
        public float PlaneUnitsPerPixel(Vector2 plane) => PlaneUnitsPerPixel(PlaneToWorld(plane));

        private float PlaneUnitsPerPixel(Vector3 world)
        {
            float pixels = Vector2.Distance(HandleUtility.WorldToGUIPoint(world),
                HandleUtility.WorldToGUIPoint(world + right));
            return pixels > 1e-4f ? 1f / pixels : 0f;
        }

        private Vector3 PointerWorld()
        {
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var plane = new Plane(normal, origin);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : origin;
        }
    }
}
