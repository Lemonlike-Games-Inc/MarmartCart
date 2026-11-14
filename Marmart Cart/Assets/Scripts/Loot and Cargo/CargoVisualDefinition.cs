using UnityEngine;

[CreateAssetMenu(fileName = "CargoVisual", menuName = "Marmart Carts/Cargo/Cargo Visual Definition")]
public class CargoVisualDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;
    [SerializeField] private LootCategory category = LootCategory.Produce;

    [Header("Final / Optional Visual")]
    [SerializeField] private GameObject cargoVisualPrefab;

    [Header("Prototype Fallback")]
    [SerializeField] private Color debugColor = Color.green;
    [Min(0.01f)][SerializeField] private float debugScaleMultiplier = 1f;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public LootCategory Category => category;
    public GameObject CargoVisualPrefab => cargoVisualPrefab;
    public Color DebugColor => debugColor;
    public float DebugScaleMultiplier => debugScaleMultiplier;

    private void OnValidate()
    {
        debugScaleMultiplier = Mathf.Max(0.01f, debugScaleMultiplier);
    }
}
