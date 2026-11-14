using System.Collections.Generic;
using UnityEngine;

public class CartWheelTrackRenderer : MonoBehaviour
{
    public enum RenderCondition
    {
        Always,
        OnlyWhileDrifting,
        OnlyWhileNotDrifting,
        Manual
    }

    [System.Serializable]
    public class WheelTrack
    {
        public string trackName = "Wheel";
        public bool enabled = true;
        public Transform source;

        [HideInInspector] public TrailSegment activeSegment;
        [HideInInspector] public readonly List<TrailSegment> segments = new List<TrailSegment>();
    }

    public class TrailSegment
    {
        public LineRenderer lineRenderer;
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> times = new List<float>();

        public Vector3 lastPoint;
        public bool hasLastPoint;
    }

    [Header("References")]
    [SerializeField] private CartDriftController driftController;

    [Tooltip("Assign 2 or 4 wheel/contact transforms here.")]
    [SerializeField] private WheelTrack[] wheelTracks;

    [Header("Render Condition")]
    [SerializeField] private RenderCondition renderCondition = RenderCondition.OnlyWhileDrifting;

    [Tooltip("Used only when Render Condition is Manual.")]
    [SerializeField] private bool manualRenderEnabled = true;

    [Tooltip("If true, all trails clear immediately when render condition becomes false. If false, old segments fade naturally.")]
    [SerializeField] private bool clearImmediatelyWhenInactive = false;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 0.03f;
    [SerializeField] private float minPointDistance = 0.08f;
    [SerializeField] private float trailLifetime = 1.2f;
    [SerializeField] private int maxPointsPerSegment = 80;

    [Header("Ground Projection")]
    [SerializeField] private bool projectToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayStartHeight = 0.5f;
    [SerializeField] private float rayDistance = 2.0f;
    [SerializeField] private float groundYOffset = 0.025f;

    [Header("Line Renderer Style")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private int lineCornerVertices = 2;
    [SerializeField] private int lineCapVertices = 2;
    [SerializeField] private Gradient trailGradient;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private float sampleTimer = 0f;
    private bool wasRenderingLastFrame = false;
    private int segmentCounter = 0;

    private void Awake()
    {
        EnsureDefaultGradient();
    }

    private void OnValidate()
    {
        sampleInterval = Mathf.Max(0.001f, sampleInterval);
        minPointDistance = Mathf.Max(0f, minPointDistance);
        trailLifetime = Mathf.Max(0.01f, trailLifetime);
        maxPointsPerSegment = Mathf.Max(2, maxPointsPerSegment);
        lineWidth = Mathf.Max(0.001f, lineWidth);

        EnsureDefaultGradient();
        ApplyLineRendererSettingsToAllSegments();
    }

    private void Update()
    {
        if (wheelTracks == null || wheelTracks.Length == 0)
            return;

        bool shouldRender = ShouldRenderNow();

        if (shouldRender && !wasRenderingLastFrame)
        {
            BeginNewStroke();
        }

        if (!shouldRender && wasRenderingLastFrame)
        {
            EndCurrentStroke();

            if (clearImmediatelyWhenInactive)
                ClearAllTracks();
        }

        wasRenderingLastFrame = shouldRender;

        if (shouldRender)
        {
            sampleTimer += Time.deltaTime;

            if (sampleTimer >= sampleInterval)
            {
                sampleTimer = 0f;
                SampleAllTracks();
            }
        }

        PruneAllExpiredPoints();
        UpdateAllLineRenderers();
    }

    private bool ShouldRenderNow()
    {
        switch (renderCondition)
        {
            case RenderCondition.Always:
                return true;

            case RenderCondition.OnlyWhileDrifting:
                return driftController != null && driftController.IsDrifting;

            case RenderCondition.OnlyWhileNotDrifting:
                return driftController == null || !driftController.IsDrifting;

            case RenderCondition.Manual:
                return manualRenderEnabled;

            default:
                return true;
        }
    }

    private void BeginNewStroke()
    {
        sampleTimer = sampleInterval;

        if (wheelTracks == null)
            return;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            if (wheelTracks[i] == null)
                continue;

            // Important:
            // Do not clear old segments.
            // Just make sure the next sample creates a fresh segment.
            wheelTracks[i].activeSegment = null;
        }

        if (showDebugLogs)
            Debug.Log("[CartWheelTrackRenderer] Begin new trail stroke.");
    }

    private void EndCurrentStroke()
    {
        if (wheelTracks == null)
            return;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            if (wheelTracks[i] == null)
                continue;

            // Old active segment remains in the segment list and fades naturally.
            // Next drift will create a new segment.
            wheelTracks[i].activeSegment = null;
        }

        if (showDebugLogs)
            Debug.Log("[CartWheelTrackRenderer] End trail stroke.");
    }

    private void SampleAllTracks()
    {
        float now = Time.time;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            WheelTrack track = wheelTracks[i];

            if (track == null || !track.enabled || track.source == null)
                continue;

            if (track.activeSegment == null)
                track.activeSegment = CreateSegment(track, i);

            TrailSegment segment = track.activeSegment;

            Vector3 point = GetTrackPoint(track.source);

            if (segment.hasLastPoint)
            {
                float sqrDistance = (point - segment.lastPoint).sqrMagnitude;

                if (sqrDistance < minPointDistance * minPointDistance)
                    continue;
            }

            segment.points.Add(point);
            segment.times.Add(now);

            segment.lastPoint = point;
            segment.hasLastPoint = true;

            while (segment.points.Count > maxPointsPerSegment)
            {
                segment.points.RemoveAt(0);
                segment.times.RemoveAt(0);
            }
        }
    }

    private TrailSegment CreateSegment(WheelTrack track, int trackIndex)
    {
        TrailSegment segment = new TrailSegment();

        string safeName = string.IsNullOrEmpty(track.trackName)
            ? $"Wheel_{trackIndex}"
            : track.trackName;

        GameObject lineObject = new GameObject($"{safeName}_TrailSegment_{segmentCounter++}");
        lineObject.transform.SetParent(transform);
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localRotation = Quaternion.identity;
        lineObject.transform.localScale = Vector3.one;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        segment.lineRenderer = line;

        ApplyLineRendererSettings(line);

        track.segments.Add(segment);

        if (showDebugLogs)
            Debug.Log($"[CartWheelTrackRenderer] Created segment for {safeName}");

        return segment;
    }

    private Vector3 GetTrackPoint(Transform source)
    {
        if (!projectToGround)
            return source.position + Vector3.up * groundYOffset;

        Vector3 rayStart = source.position + Vector3.up * rayStartHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, groundMask))
            return hit.point + Vector3.up * groundYOffset;

        return source.position + Vector3.up * groundYOffset;
    }

    private void PruneAllExpiredPoints()
    {
        float expireBefore = Time.time - trailLifetime;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            WheelTrack track = wheelTracks[i];

            if (track == null)
                continue;

            for (int s = track.segments.Count - 1; s >= 0; s--)
            {
                TrailSegment segment = track.segments[s];

                if (segment == null)
                {
                    track.segments.RemoveAt(s);
                    continue;
                }

                while (segment.times.Count > 0 && segment.times[0] < expireBefore)
                {
                    segment.times.RemoveAt(0);
                    segment.points.RemoveAt(0);
                }

                if (segment.points.Count == 0)
                {
                    segment.hasLastPoint = false;

                    bool isActiveSegment = track.activeSegment == segment;

                    if (!isActiveSegment)
                    {
                        if (segment.lineRenderer != null)
                            Destroy(segment.lineRenderer.gameObject);

                        track.segments.RemoveAt(s);
                    }
                }
            }
        }
    }

    private void UpdateAllLineRenderers()
    {
        for (int i = 0; i < wheelTracks.Length; i++)
        {
            WheelTrack track = wheelTracks[i];

            if (track == null)
                continue;

            for (int s = 0; s < track.segments.Count; s++)
            {
                TrailSegment segment = track.segments[s];

                if (segment == null || segment.lineRenderer == null)
                    continue;

                if (!track.enabled)
                {
                    segment.lineRenderer.positionCount = 0;
                    continue;
                }

                segment.lineRenderer.positionCount = segment.points.Count;

                for (int p = 0; p < segment.points.Count; p++)
                {
                    segment.lineRenderer.SetPosition(p, segment.points[p]);
                }
            }
        }
    }

    public void ClearAllTracks()
    {
        if (wheelTracks == null)
            return;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            WheelTrack track = wheelTracks[i];

            if (track == null)
                continue;

            for (int s = track.segments.Count - 1; s >= 0; s--)
            {
                TrailSegment segment = track.segments[s];

                if (segment != null && segment.lineRenderer != null)
                    Destroy(segment.lineRenderer.gameObject);
            }

            track.segments.Clear();
            track.activeSegment = null;
        }
    }

    public void SetManualRenderEnabled(bool enabled)
    {
        if (manualRenderEnabled == enabled)
            return;

        manualRenderEnabled = enabled;

        if (!enabled)
        {
            EndCurrentStroke();

            if (clearImmediatelyWhenInactive)
                ClearAllTracks();
        }
        else
        {
            BeginNewStroke();
        }
    }

    private void ApplyLineRendererSettingsToAllSegments()
    {
        if (wheelTracks == null)
            return;

        for (int i = 0; i < wheelTracks.Length; i++)
        {
            WheelTrack track = wheelTracks[i];

            if (track == null)
                continue;

            for (int s = 0; s < track.segments.Count; s++)
            {
                TrailSegment segment = track.segments[s];

                if (segment == null || segment.lineRenderer == null)
                    continue;

                ApplyLineRendererSettings(segment.lineRenderer);
            }
        }
    }

    private void ApplyLineRendererSettings(LineRenderer line)
    {
        if (line == null)
            return;

        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.numCornerVertices = lineCornerVertices;
        line.numCapVertices = lineCapVertices;
        line.colorGradient = trailGradient;

        if (lineMaterial != null)
            line.material = lineMaterial;
    }

    private void EnsureDefaultGradient()
    {
        if (trailGradient != null && trailGradient.colorKeys != null && trailGradient.colorKeys.Length > 0)
            return;

        trailGradient = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(1f, 1f)
        };

        trailGradient.SetKeys(colorKeys, alphaKeys);
    }
}