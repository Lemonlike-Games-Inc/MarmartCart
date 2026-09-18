using System;
using UnityEngine;

public enum ArenaEightWayDirection
{
    North = 0,
    NorthEast = 1,
    East = 2,
    SouthEast = 3,
    South = 4,
    SouthWest = 5,
    West = 6,
    NorthWest = 7
}

[Serializable]
public sealed class ArenaDirectionalArrowStyle
{
    [Header("Main Shape")]
    [SerializeField] private Color color = Color.white;

    [Min(0.01f)]
    [SerializeField] private float shaftLength = 1.25f;

    [Min(0.01f)]
    [SerializeField] private float shaftWidth = 0.28f;

    [Min(0.01f)]
    [SerializeField] private float headLength = 0.55f;

    [Min(0.01f)]
    [SerializeField] private float headWidth = 0.8f;

    [Header("Contrast Underlay")]
    [SerializeField] private bool drawUnderlay = true;

    [SerializeField]
    private Color underlayColor = new Color(0.04f, 0.04f, 0.04f, 0.72f);

    [Tooltip(
        "World-space expansion added to every side of the arrow before the " +
        "main colored arrow is drawn."
    )]
    [Min(0f)]
    [SerializeField] private float underlayExpansion = 0.08f;

    public Color Color => color;
    public float ShaftLength => shaftLength;
    public float ShaftWidth => shaftWidth;
    public float HeadLength => headLength;
    public float HeadWidth => headWidth;
    public bool DrawUnderlay => drawUnderlay;
    public Color UnderlayColor => underlayColor;
    public float UnderlayExpansion => underlayExpansion;

    public ArenaDirectionalArrowStyle()
    {
    }

    public ArenaDirectionalArrowStyle(Color mainColor)
    {
        color = mainColor;
    }

    public void Validate()
    {
        shaftLength = Mathf.Max(0.01f, shaftLength);
        shaftWidth = Mathf.Max(0.01f, shaftWidth);
        headLength = Mathf.Max(0.01f, headLength);
        headWidth = Mathf.Max(0.01f, headWidth);
        underlayExpansion = Mathf.Max(0f, underlayExpansion);
    }
}

[CreateAssetMenu(
    menuName = "Marmart Carts/Match Flow/Arena Directional Indicator Profile",
    fileName = "ArenaDirectionalIndicatorProfile"
)]
public sealed class ArenaDirectionalIndicatorProfile : ScriptableObject
{
    #region Visibility

    [Header("Zone Guidance Visibility")]
    [Tooltip(
        "Disabled: five guidance arrows appear only during ZoneLoot Warning. " +
        "Enabled: they remain through Active and disappear exactly when " +
        "Closing begins."
    )]
    [SerializeField] private bool remainGuidanceDuringActive;

    #endregion

    #region Shared World Layout

    [Header("Shared World Layout")]
    [Tooltip("Multiplies every arrow dimension and radial distance together.")]
    [Min(0.01f)]
    [SerializeField] private float masterScale = 1f;

    [Tooltip(
        "Distance along the World Center transform's Up axis. Keep this " +
        "slightly above the floor to prevent z-fighting."
    )]
    [SerializeField] private float heightOffset = 0.06f;

    [Header("Checkout Layout")]
    [Tooltip(
        "Shared distance from World Center to each North/East/South/West " +
        "checkout arrow."
    )]
    [Min(0f)]
    [SerializeField] private float checkoutArrowDistance = 2.75f;

    [Header("Zone Guidance Layout")]
    [Tooltip(
        "Shared distance from World Center to the four surrounding guidance " +
        "arrow anchors. The fifth anchor stays at the exact center."
    )]
    [Min(0f)]
    [SerializeField] private float guidanceAnchorDistance = 4f;

    [Tooltip(
        "Direction of the first surrounding anchor. The other three are " +
        "generated at 90-degree intervals. NorthWest produces the requested " +
        "top-left, top-right, bottom-right, bottom-left arrangement."
    )]
    [SerializeField]
    private ArenaEightWayDirection firstGuidanceAnchorDirection =
        ArenaEightWayDirection.NorthWest;

    [Tooltip(
        "Distance from World Center to each virtual zone target. This affects " +
        "which 45-degree direction surrounding arrows choose. Keeping this " +
        "moderately beyond the anchor ring makes the corner arrows differ " +
        "naturally instead of all copying the center arrow."
    )]
    [Min(0.01f)]
    [SerializeField] private float zoneTargetDistance = 7f;

    #endregion

    #region Zone Mapping

    [Header("Zone To Eight-Way Direction Mapping")]
    [Tooltip("Assign the actual map direction occupied by Zone A.")]
    [SerializeField]
    private ArenaEightWayDirection zoneADirection =
        ArenaEightWayDirection.NorthWest;

    [Tooltip("Assign the actual map direction occupied by Zone B.")]
    [SerializeField]
    private ArenaEightWayDirection zoneBDirection =
        ArenaEightWayDirection.NorthEast;

    [Tooltip("Assign the actual map direction occupied by Zone C.")]
    [SerializeField]
    private ArenaEightWayDirection zoneCDirection =
        ArenaEightWayDirection.SouthWest;

    [Tooltip("Assign the actual map direction occupied by Zone D.")]
    [SerializeField]
    private ArenaEightWayDirection zoneDDirection =
        ArenaEightWayDirection.SouthEast;

    #endregion

    #region Appearance

    [Header("Zone Guidance Arrow Appearance")]
    [SerializeField]
    private ArenaDirectionalArrowStyle guidanceArrowStyle =
        new ArenaDirectionalArrowStyle(
            new Color(1f, 0.72f, 0.12f, 0.96f)
        );

    [Header("Checkout Arrow Appearance")]
    [SerializeField]
    private ArenaDirectionalArrowStyle checkoutArrowStyle =
        new ArenaDirectionalArrowStyle(
            new Color(0.18f, 1f, 0.68f, 0.96f)
        );

    #endregion

    #region Public API

    public bool RemainGuidanceDuringActive =>
        remainGuidanceDuringActive;

    public float MasterScale => Mathf.Max(0.01f, masterScale);
    public float HeightOffset => heightOffset;
    public float CheckoutArrowDistance => checkoutArrowDistance * MasterScale;
    public float GuidanceAnchorDistance => guidanceAnchorDistance * MasterScale;
    public float ZoneTargetDistance => zoneTargetDistance * MasterScale;

    public ArenaDirectionalArrowStyle GuidanceArrowStyle =>
        guidanceArrowStyle;

    public ArenaDirectionalArrowStyle CheckoutArrowStyle =>
        checkoutArrowStyle;

    public ArenaEightWayDirection GetZoneDirection(ArenaZoneId zoneId)
    {
        switch (zoneId)
        {
            case ArenaZoneId.ZoneA:
                return zoneADirection;

            case ArenaZoneId.ZoneB:
                return zoneBDirection;

            case ArenaZoneId.ZoneC:
                return zoneCDirection;

            case ArenaZoneId.ZoneD:
                return zoneDDirection;

            default:
                return ArenaEightWayDirection.North;
        }
    }

    public ArenaEightWayDirection GetGuidanceAnchorDirection(int index)
    {
        int normalizedIndex = Mathf.Abs(index) % 4;
        int directionStep =
            ((int)firstGuidanceAnchorDirection + normalizedIndex * 2) % 8;

        return (ArenaEightWayDirection)directionStep;
    }

    #endregion

    #region Validation

    private void OnEnable()
    {
        EnsureStyles();
    }

    private void OnValidate()
    {
        masterScale = Mathf.Max(0.01f, masterScale);
        checkoutArrowDistance = Mathf.Max(0f, checkoutArrowDistance);
        guidanceAnchorDistance = Mathf.Max(0f, guidanceAnchorDistance);
        zoneTargetDistance = Mathf.Max(
            guidanceAnchorDistance + 0.01f,
            zoneTargetDistance
        );

        EnsureStyles();
        guidanceArrowStyle.Validate();
        checkoutArrowStyle.Validate();
    }

    private void EnsureStyles()
    {
        if (guidanceArrowStyle == null)
        {
            guidanceArrowStyle = new ArenaDirectionalArrowStyle(
                new Color(1f, 0.72f, 0.12f, 0.96f)
            );
        }

        if (checkoutArrowStyle == null)
        {
            checkoutArrowStyle = new ArenaDirectionalArrowStyle(
                new Color(0.18f, 1f, 0.68f, 0.96f)
            );
        }
    }

    #endregion
}
