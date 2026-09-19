using UnityEngine;

/// <summary>
/// Shared timing and score-to-visual rules for the in-scene final ceremony.
/// Player-specific positions remain authored on each FinalScoreCargoTower.
/// </summary>
[CreateAssetMenu(
    fileName = "MatchResultsPresentationProfile",
    menuName = "Marmart Carts/Results/Match Results Presentation Profile")]
public sealed class MatchResultsPresentationProfile : ScriptableObject
{
    #region Score Representation

    [Header("Score To Cargo Representation")]
    [Tooltip(
        "Ordinary score carried by one repeated real-cargo visual. Keep at 1 " +
        "for exact score == visual count; raise it to compress very tall results."
    )]
    [Min(1)]
    [SerializeField] private int baseScorePointsPerVisual = 1;

    [Tooltip(
        "Milestone/streak score carried by one special reward visual. Keep at 1 " +
        "for one reward point == one special cargo display."
    )]
    [Min(1)]
    [SerializeField] private int bonusScorePointsPerVisual = 1;

    [Tooltip(
        "Safety cap only. If a score would exceed this many visual instances, " +
        "both points-per-visual values are increased uniformly for that player. " +
        "Set 0 for no cap. The displayed score always remains exact."
    )]
    [Min(0)]
    [SerializeField] private int maximumVisualTokensPerPlayer = 1200;

    [Header("Cargo Prefabs")]
    [Tooltip(
        "Used for ordinary score only when no submitted CargoVisualDefinition " +
        "prefab was recorded for the required score token."
    )]
    [SerializeField] private GameObject fallbackCargoVisualPrefab;

    [Tooltip("Special visual used exclusively for milestone/streak reward points.")]
    [SerializeField] private GameObject bonusCargoVisualPrefab;

    [Tooltip("Optional material for the generated debug sphere when no prefab is available.")]
    [SerializeField] private Material prototypeFallbackMaterial;

    [Min(0.01f)]
    [SerializeField] private float prototypeFallbackScale = 0.22f;

    #endregion

    #region Stack Layout

    [Header("Repeated Stack Layers")]
    [Tooltip(
        "Distance between repeated copies of the tower's authored first-layer slots. " +
        "Assign 2 or 3 first-layer slots on each tower to get 2 or 3 cargo per level."
    )]
    [Min(0.01f)]
    [SerializeField] private float layerHeight = 0.35f;

    [Tooltip(
        "Local offset in the tower's authored orientation from the moving cart " +
        "to the point where each incoming cargo visual first appears."
    )]
    [SerializeField] private Vector3 spawnOffsetFromCart = new Vector3(0f, -0.35f, 0f);

    [Header("Ordinary Cargo Rotation")]
    [Tooltip(
        "Give each ordinary cargo visual a random final yaw relative to its authored " +
        "slot rotation. The special bonus/reward cargo is never randomized."
    )]
    [SerializeField] private bool randomizeOrdinaryCargoFinalYaw = true;

    [Tooltip(
        "Inclusive minimum/maximum local Y rotation, in degrees, sampled once when " +
        "an ordinary cargo visual spawns. Values may be entered in either order."
    )]
    [SerializeField]
    private Vector2 ordinaryCargoFinalYawRange =
        new Vector2(0f, 360f);

    #endregion

    #region Reveal Motion

    [Header("Reveal Timing")]
    [Tooltip("Delay after staging/camera switching before every player begins together.")]
    [Min(0f)]
    [SerializeField] private float ceremonyStartDelay = 0.65f;

    [Tooltip("Real-time interval between visual-token launches. Flights may overlap.")]
    [Min(0.001f)]
    [SerializeField] private float tokenSpawnInterval = 0.045f;

    [Tooltip("Real-time travel duration from beneath the cart to a tower slot.")]
    [Min(0.01f)]
    [SerializeField] private float tokenTravelDuration = 0.3f;

    [Tooltip("Travel easing. The default gives a fast, readable arrival without physics.")]
    [SerializeField]
    private AnimationCurve tokenTravelCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Small arc along the stack direction during token travel.")]
    [Min(0f)]
    [SerializeField] private float tokenTravelArcHeight = 0.18f;

    [Range(0.01f, 1f)]
    [SerializeField] private float tokenStartScaleMultiplier = 0.6f;

    [Tooltip("Degrees of local Y spin completed during one token flight.")]
    [SerializeField] private float tokenSpinDegrees = 360f;

    [Header("Cart / Top Kit Rise")]
    [Tooltip(
        "Approximate smoothing time used while the actual cart, its camera focus, " +
        "and its score/rank kit rise with completed cargo."
    )]
    [Min(0.01f)]
    [SerializeField] private float cartRiseSmoothTime = 0.12f;

    [Tooltip("Small pause after the final token lands before rank/spotlight appear.")]
    [Min(0f)]
    [SerializeField] private float completionRevealDelay = 0.18f;

    #endregion

    #region Text

    [Header("World Text")]
    [Tooltip("{0} is the live score integer.")]
    [SerializeField] private string scoreTextFormat = "{0}";

    [Tooltip("{0} is the ordinal rank label, for example 1ST.")]
    [SerializeField] private string rankTextFormat = "{0}";

    #endregion

    public int BaseScorePointsPerVisual => Mathf.Max(1, baseScorePointsPerVisual);
    public int BonusScorePointsPerVisual => Mathf.Max(1, bonusScorePointsPerVisual);
    public int MaximumVisualTokensPerPlayer =>
        maximumVisualTokensPerPlayer <= 0
            ? 0
            : Mathf.Max(2, maximumVisualTokensPerPlayer);
    public GameObject FallbackCargoVisualPrefab => fallbackCargoVisualPrefab;
    public GameObject BonusCargoVisualPrefab => bonusCargoVisualPrefab;
    public Material PrototypeFallbackMaterial => prototypeFallbackMaterial;
    public float PrototypeFallbackScale => Mathf.Max(0.01f, prototypeFallbackScale);
    public float LayerHeight => Mathf.Max(0.01f, layerHeight);
    public Vector3 SpawnOffsetFromCart => spawnOffsetFromCart;
    public bool RandomizeOrdinaryCargoFinalYaw => randomizeOrdinaryCargoFinalYaw;
    public Vector2 OrdinaryCargoFinalYawRange =>
        new Vector2(
            Mathf.Min(
                ordinaryCargoFinalYawRange.x,
                ordinaryCargoFinalYawRange.y
            ),
            Mathf.Max(
                ordinaryCargoFinalYawRange.x,
                ordinaryCargoFinalYawRange.y
            )
        );
    public float CeremonyStartDelay => Mathf.Max(0f, ceremonyStartDelay);
    public float TokenSpawnInterval => Mathf.Max(0.001f, tokenSpawnInterval);
    public float TokenTravelDuration => Mathf.Max(0.01f, tokenTravelDuration);
    public float TokenTravelArcHeight => Mathf.Max(0f, tokenTravelArcHeight);
    public float TokenStartScaleMultiplier => Mathf.Clamp(tokenStartScaleMultiplier, 0.01f, 1f);
    public float TokenSpinDegrees => tokenSpinDegrees;
    public float CartRiseSmoothTime => Mathf.Max(0.01f, cartRiseSmoothTime);
    public float CompletionRevealDelay => Mathf.Max(0f, completionRevealDelay);
    public string ScoreTextFormat => string.IsNullOrEmpty(scoreTextFormat) ? "{0}" : scoreTextFormat;
    public string RankTextFormat => string.IsNullOrEmpty(rankTextFormat) ? "{0}" : rankTextFormat;

    public float EvaluateTokenTravel(float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        if (tokenTravelCurve == null || tokenTravelCurve.length == 0) return t;
        return Mathf.Clamp01(tokenTravelCurve.Evaluate(t));
    }

    private void OnValidate()
    {
        baseScorePointsPerVisual = Mathf.Max(1, baseScorePointsPerVisual);
        bonusScorePointsPerVisual = Mathf.Max(1, bonusScorePointsPerVisual);
        maximumVisualTokensPerPlayer =
            maximumVisualTokensPerPlayer <= 0
                ? 0
                : Mathf.Max(2, maximumVisualTokensPerPlayer);
        prototypeFallbackScale = Mathf.Max(0.01f, prototypeFallbackScale);
        layerHeight = Mathf.Max(0.01f, layerHeight);
        ceremonyStartDelay = Mathf.Max(0f, ceremonyStartDelay);
        tokenSpawnInterval = Mathf.Max(0.001f, tokenSpawnInterval);
        tokenTravelDuration = Mathf.Max(0.01f, tokenTravelDuration);
        tokenTravelArcHeight = Mathf.Max(0f, tokenTravelArcHeight);
        tokenStartScaleMultiplier = Mathf.Clamp(tokenStartScaleMultiplier, 0.01f, 1f);
        cartRiseSmoothTime = Mathf.Max(0.01f, cartRiseSmoothTime);
        completionRevealDelay = Mathf.Max(0f, completionRevealDelay);
    }
}
