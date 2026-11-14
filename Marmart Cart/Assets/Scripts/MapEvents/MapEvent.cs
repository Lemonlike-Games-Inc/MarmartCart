using UnityEngine;

public enum MapEventType
{
    NormalItemSale,
    RareItemSale,
    CartRain,
    PowerupStorm,
    ShopperRush
}
[System.Serializable]
public class NormalItemSaleConfig
{
    [Header("Shelf Restock Timing & Spawn")]
    public float warningDuration = 10f;

    // Range of total normal items to spawn in this event
    public Vector2Int totalNormalItemsRange = new Vector2Int(10, 20);

    public float spawnInterval = 1.0f;
    public int itemsPerSpawn = 3;

    public int GetRandomTotal() =>
        Random.Range(totalNormalItemsRange.x, totalNormalItemsRange.y + 1);
}
[System.Serializable]
public class RareItemSaleConfig
{
    [Header("Rare Sale Timing & Spawn")]
    public float warningDuration = 10f;

    [Tooltip("Range of total rare items to spawn")]
    public Vector2Int totalRareItemsRange = new Vector2Int(5, 10);

    public float spawnInterval = 1.0f;
    public int itemsPerSpawn = 2;

    public int GetRandomTotal() => Random.Range(totalRareItemsRange.x, totalRareItemsRange.y + 1);
}

[System.Serializable]
public class CartRainConfig
{
    [Header("Cart Rain Timing & Spawn")]
    public float warningDuration = 10f;

    public Vector2Int totalCartsRange = new Vector2Int(15, 25);

    public float spawnInterval = 1f;
    public int itemsPerSpawn = 4;

    public int GetRandomTotal() => Random.Range(totalCartsRange.x, totalCartsRange.y + 1);
}

[System.Serializable]
public class PowerupStormConfig
{
    [Header("Powerup Storm Timing & Spawn")]
    public float warningDuration = 5f;

    public Vector2Int totalPowerupsRange = new Vector2Int(1, 3);

    public float spawnInterval = 1f;
    public int itemsPerSpawn = 1;

    public int GetRandomTotal() => Random.Range(totalPowerupsRange.x, totalPowerupsRange.y + 1);
}

[System.Serializable]
public class ShopperRushConfig
{
    [Header("Shopper Rush Timing & Spawn")]
    public float warningDuration = 5f;

    public Vector2Int totalShoppersRange = new Vector2Int(20, 40);

    public float spawnInterval = 1.0f;
    public int shoppersPerSpawn = 2;

    public int GetRandomTotal() => Random.Range(totalShoppersRange.x, totalShoppersRange.y + 1);
}