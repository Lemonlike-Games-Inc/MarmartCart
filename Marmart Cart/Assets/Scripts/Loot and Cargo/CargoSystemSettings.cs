using UnityEngine;

[CreateAssetMenu(fileName = "CargoSystemSettings", menuName = "Marmart Carts/Cargo/Cargo System Settings")]
public class CargoSystemSettings : ScriptableObject
{
    [Header("Gameplay Capacity")]
    [Min(1)][SerializeField] private int capacityPerCart = 6;
    [Min(1)][SerializeField] private int overflowRoundSize = 3;
    [Min(1)][SerializeField] private int technicalMaxCargoPerCart = 100;

    [Header("Overload Movement Penalty")]
    [Tooltip("Maps GLOBAL overload amount directly to a base movement-speed multiplier. X = overload load amount, Y = multiplier.")]
    [SerializeField]
    private AnimationCurve overloadSpeedMultiplierCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(3f, 0.9f),
        new Keyframe(6f, 0.75f),
        new Keyframe(12f, 0.5f),
        new Keyframe(20f, 0.15f)
    );

    [Tooltip("Hard lower clamp for overload movement multiplier. Keeps extreme overload technically movable.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float minimumOverloadSpeedMultiplier = 0.1f;

    [Header("Prototype Cargo Visual Layout")]
    [SerializeField] private bool matchPrototypeSafeSlotsToCapacity = true;
    [Min(1)][SerializeField] private int prototypeSafeVisualSlotCount = 6;
    [Min(1)][SerializeField] private int prototypeSlotsPerLayer = 3;
    [SerializeField] private Vector3 prototypeCargoCenter = new Vector3(0f, 0.65f, 0f);
    [Min(0.01f)][SerializeField] private float prototypeHorizontalSpacing = 0.28f;
    [Min(0.01f)][SerializeField] private float prototypeLayerHeight = 0.24f;
    [Min(0.01f)][SerializeField] private float prototypeCargoSphereScale = 0.20f;
    [Min(0f)][SerializeField] private float prototypeLayerDepthStagger = 0.06f;

    public int CapacityPerCart => capacityPerCart;
    public int OverflowRoundSize => overflowRoundSize;
    public int TechnicalMaxCargoPerCart => technicalMaxCargoPerCart;
    public AnimationCurve OverloadSpeedMultiplierCurve => overloadSpeedMultiplierCurve;
    public float MinimumOverloadSpeedMultiplier => minimumOverloadSpeedMultiplier;
    public int PrototypeSafeVisualSlotCount => prototypeSafeVisualSlotCount;
    public int PrototypeSlotsPerLayer => prototypeSlotsPerLayer;
    public Vector3 PrototypeCargoCenter => prototypeCargoCenter;
    public float PrototypeHorizontalSpacing => prototypeHorizontalSpacing;
    public float PrototypeLayerHeight => prototypeLayerHeight;
    public float PrototypeCargoSphereScale => prototypeCargoSphereScale;
    public float PrototypeLayerDepthStagger => prototypeLayerDepthStagger;

    public float GetOverloadSpeedMultiplier(int globalOverloadAmount)
    {
        if (globalOverloadAmount <= 0) return 1f;

        float evaluatedMultiplier = overloadSpeedMultiplierCurve != null
            ? overloadSpeedMultiplierCurve.Evaluate(globalOverloadAmount)
            : 1f;

        return Mathf.Clamp(evaluatedMultiplier, minimumOverloadSpeedMultiplier, 1f);
    }

    private void OnValidate()
    {
        capacityPerCart = Mathf.Max(1, capacityPerCart);
        overflowRoundSize = Mathf.Max(1, overflowRoundSize);
        technicalMaxCargoPerCart = Mathf.Max(capacityPerCart, technicalMaxCargoPerCart);
        minimumOverloadSpeedMultiplier = Mathf.Clamp(minimumOverloadSpeedMultiplier, 0.01f, 1f);

        prototypeSafeVisualSlotCount = Mathf.Max(1, prototypeSafeVisualSlotCount);
        prototypeSlotsPerLayer = Mathf.Max(1, prototypeSlotsPerLayer);
        prototypeHorizontalSpacing = Mathf.Max(0.01f, prototypeHorizontalSpacing);
        prototypeLayerHeight = Mathf.Max(0.01f, prototypeLayerHeight);
        prototypeCargoSphereScale = Mathf.Max(0.01f, prototypeCargoSphereScale);
        prototypeLayerDepthStagger = Mathf.Max(0f, prototypeLayerDepthStagger);

        if (matchPrototypeSafeSlotsToCapacity) prototypeSafeVisualSlotCount = capacityPerCart;
    }
}
