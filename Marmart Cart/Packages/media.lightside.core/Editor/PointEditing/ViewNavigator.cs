using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// The span of data a canvas shows, and the gesture that moves it: the middle button drags the view, and
    /// with the primary modifier held it scales about the point it was pressed on — each axis by the movement
    /// along it, so a horizontal drag stretches the horizontal axis alone — and whatever sits under the pointer
    /// stays under it. One navigation for every 2-D data canvas, the way G moves points in every point
    /// editor. The view never follows the data — a canvas frames what the navigator says and nothing else.
    /// </summary>
    public sealed class ViewNavigator
    {
        private const float ZoomPerPixel = 0.006f;
        private const float MinSpan = 1e-4f;
        private const float MaxSpan = 1e6f;

        private readonly Rect home;
        private Rect startView;
        private Vector2 startScale;
        private Vector2 pressGui;
        private Vector2 anchor;
        private bool zooming;

        /// <summary>Raised whenever the shown span changes, so the canvas repaints.</summary>
        public event Action Changed;

        /// <summary>Creates a navigator showing <paramref name="home"/>, the span <see cref="Reset"/> returns to.</summary>
        public ViewNavigator(Rect home)
        {
            this.home = home;
            View = home;
        }

        /// <summary>The data span currently shown.</summary>
        public Rect View { get; private set; }

        /// <summary>Whether a navigation gesture is running; a canvas leaves its own gestures alone while it is.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>Shows the span the navigator was created with.</summary>
        public void Reset() => Show(home);

        /// <summary>Shows <paramref name="view"/>, held to the spans a canvas can map.</summary>
        public void Show(Rect view)
        {
            var width = Mathf.Clamp(view.width, MinSpan, MaxSpan);
            var height = Mathf.Clamp(view.height, MinSpan, MaxSpan);
            View = new Rect(view.x, view.y, width, height);
            Changed?.Invoke();
        }

        /// <summary>
        /// Takes the press that starts a navigation, and reports whether it did. The middle button drags;
        /// holding Control or Command scales about <paramref name="gui"/> instead.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="surface"/> is <see langword="null"/>.</exception>
        public bool HandlePointerDown(int button, EventModifiers modifiers, Vector2 gui,
            RectEditSurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (button != 2) return false;

            IsBusy = true;
            zooming = (modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            pressGui = gui;
            startView = View;
            startScale = surface.Scale;
            anchor = surface.GuiToData(gui);
            return true;
        }

        /// <summary>Continues the running gesture, and reports whether one was running.</summary>
        public bool HandlePointerMove(Vector2 gui)
        {
            if (!IsBusy) return false;
            var moved = gui - pressGui;

            if (zooming)
            {
                var horizontal = Mathf.Exp(moved.x * ZoomPerPixel);
                var vertical = Mathf.Exp(-moved.y * ZoomPerPixel);
                Show(new Rect(
                    anchor.x - (anchor.x - startView.xMin) / horizontal,
                    anchor.y - (anchor.y - startView.yMin) / vertical,
                    startView.width / horizontal,
                    startView.height / vertical));
                return true;
            }

            Show(new Rect(
                startView.x - moved.x / Mathf.Max(startScale.x, 1e-6f),
                startView.y + moved.y / Mathf.Max(startScale.y, 1e-6f),
                startView.width,
                startView.height));
            return true;
        }

        /// <summary>Ends the running gesture, and reports whether one was running.</summary>
        public bool HandlePointerUp()
        {
            if (!IsBusy) return false;
            IsBusy = false;
            return true;
        }
    }
}
