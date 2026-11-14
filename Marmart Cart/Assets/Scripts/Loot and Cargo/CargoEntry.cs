using UnityEngine;

[System.Serializable]
public sealed class CargoEntry
{
    [SerializeField] private GroceryLootDefinition sourceLoot;
    [SerializeField] private CargoVisualDefinition selectedCargoVisual;
    [SerializeField] private int loadCost;
    [SerializeField] private int scoreValue;
    [SerializeField] private int visualSlotIndex = -1;

    [System.NonSerialized] private GameObject visualInstance;

    public GroceryLootDefinition SourceLoot => sourceLoot;
    public CargoVisualDefinition SelectedCargoVisual => selectedCargoVisual;
    public LootCategory Category => sourceLoot != null ? sourceLoot.Category : LootCategory.Produce;
    public int LoadCost => loadCost;
    public int ScoreValue => scoreValue;
    public int VisualSlotIndex => visualSlotIndex;
    public GameObject VisualInstance => visualInstance;

    public CargoEntry(GroceryLootDefinition lootDefinition)
    {
        sourceLoot = lootDefinition;
        selectedCargoVisual = lootDefinition != null ? lootDefinition.ChooseCargoVisual() : null;
        loadCost = lootDefinition != null ? lootDefinition.LoadCost : 0;
        scoreValue = lootDefinition != null ? lootDefinition.ScoreValue : 0;
        visualSlotIndex = -1;
        visualInstance = null;
    }

    public void SetVisualRuntime(int slotIndex, GameObject instance)
    {
        visualSlotIndex = slotIndex;
        visualInstance = instance;
    }

    public void ClearVisualRuntime()
    {
        visualSlotIndex = -1;
        visualInstance = null;
    }
}
