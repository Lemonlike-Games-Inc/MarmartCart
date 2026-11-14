using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ComboDealsManager : MonoBehaviour
{
    [SerializeField] private float generationFrequency = 30f;
    [SerializeField] private float comboDuration = 60f;
    [SerializeField] private float baseReward = 100f;
    [SerializeField] private GameObject comboDealPrefab;
    [SerializeField] private Transform comboUIParent;

    private List<ComboDeal> activeDeals = new();
    private int dealIndex = 0;
    private float nextGenerationTime = 0f;

    [SerializeField]
    private ComboDealUIController uiController;
    void Update()
    {
        if (Time.time >= nextGenerationTime)
        {
            StartCoroutine(DelayedGenerateComboDeal());
            nextGenerationTime = Time.time + generationFrequency;
        }

        // Clean up expired deals
        activeDeals.RemoveAll(d => d.IsExpired || d.IsCompleted);
    }
    private IEnumerator DelayedGenerateComboDeal()
    {
        yield return 1f;

        GenerateComboDeal();
    }
    private void GenerateComboDeal()
    {
        var reqs = new Dictionary<CartRarity, int>
        {
            { CartRarity.Common,     (Random.value < 0.9f) ? Random.Range(2, 10) : 0 },  // 90% chance to appear
            { CartRarity.Rare,       (Random.value < 0.6f) ? Random.Range(1, 6) : 0 },  // 60% chance
            { CartRarity.Epic,       (Random.value < 0.25f) ? Random.Range(1, 3) : 0 }, // 25% chance
            { CartRarity.Legendary,  (Random.value < 0.05f) ? 1 : 0 }                  // 5% chance for 1 only
        };

        // Ensure at least one cart is required
        if (reqs.Values.All(v => v == 0))
        {
            reqs[CartRarity.Common] = Random.Range(2, 5);
        }

        // Calculate scaled reward
        float scaledReward =
            baseReward +
            (reqs[CartRarity.Common] * 5f) +
            (reqs[CartRarity.Rare] * 10f) +
            (reqs[CartRarity.Epic] * 50f) +
            (reqs[CartRarity.Legendary] * 250f);

        //old code for single ui controller
        GameObject dealGO = Instantiate(comboDealPrefab, comboUIParent);
        dealGO.GetComponent<ComboDeal>().uiController = uiController;
        ComboDeal deal = dealGO.GetComponent<ComboDeal>();
        deal.Initialize(dealIndex++, scaledReward, reqs, comboDuration);
        activeDeals.Add(deal);

    }

    public void SubmitCartToCombos(CartRarity rarity, int playerIndex)
    {
        foreach (var deal in activeDeals)
        {
            if (deal.SubmitCart(rarity, playerIndex))
            {
                Debug.Log($"Player {playerIndex} submitted {rarity} to Deal {deal.DealID}");
                return;
            }
        }
    }

    public List<ComboDeal> GetActiveDeals() => activeDeals;
}
