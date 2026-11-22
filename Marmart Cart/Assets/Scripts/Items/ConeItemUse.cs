using UnityEngine;

/// <summary>
/// Aimed as a flat cone from the leading cart (U-shape Magnet): hold the right
/// stick to show the cone, LT uses it. The effect system decides what the
/// cone does.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Uses/Cone",
    fileName = "Cone Use"
)]
public sealed class ConeItemUse : ItemUseDefinition
{
    [Tooltip("Metres from the leading cart to the far edge of the cone.")]
    [Min(1f)]
    [SerializeField] private float coneLength = 20f;

    [Tooltip("Full width of the cone in degrees.")]
    [Range(5f, 180f)]
    [SerializeField] private float coneAngleDegrees = 70f;

    public override bool RequiresAim => true;

    public float ConeLength => coneLength;
    public float ConeAngleDegrees => coneAngleDegrees;
}
