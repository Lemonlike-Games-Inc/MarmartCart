using UnityEngine;

[CreateAssetMenu(fileName = "GroceryLoot", menuName = "Marmart Carts/Cargo/Grocery Loot Definition")]
public class GroceryLootDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;
    [SerializeField] private LootCategory category = LootCategory.Produce;

    [Header("Gameplay")]
    [Min(1)][SerializeField] private int loadCost = 1;
    [Min(0)][SerializeField] private int scoreValue = 1;

    [Header("World Pickup Presentation")]
    [SerializeField] private GameObject worldPickupPrefab;
    [SerializeField] private Color worldDebugColor = Color.green;
    [Min(0.01f)][SerializeField] private float worldDebugScale = 1f;

    [Header("Possible In-Cart Cargo Variants")]
    [SerializeField] private CargoVisualDefinition[] possibleCargoVisuals;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public LootCategory Category => category;
    public int LoadCost => loadCost;
    public int ScoreValue => scoreValue;
    public GameObject WorldPickupPrefab => worldPickupPrefab;
    public Color WorldDebugColor => worldDebugColor;
    public float WorldDebugScale => worldDebugScale;
    public int CargoVisualVariantCount => possibleCargoVisuals != null ? possibleCargoVisuals.Length : 0;

    public CargoVisualDefinition ChooseCargoVisual()
    {
        if (possibleCargoVisuals == null || possibleCargoVisuals.Length == 0) return null;

        int startIndex = Random.Range(0, possibleCargoVisuals.Length);

        for (int offset = 0; offset < possibleCargoVisuals.Length; offset++)
        {
            int index = (startIndex + offset) % possibleCargoVisuals.Length;
            CargoVisualDefinition candidate = possibleCargoVisuals[index];
            if (candidate == null) continue;

            if (candidate.Category != category)
            {
                Debug.LogWarning($"[GroceryLootDefinition] '{name}' ({category}) references cargo visual '{candidate.name}' ({candidate.Category}). Skipping it.", this);
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void OnValidate()
    {
        loadCost = Mathf.Max(1, loadCost);
        scoreValue = Mathf.Max(0, scoreValue);
        worldDebugScale = Mathf.Max(0.01f, worldDebugScale);
    }
}
