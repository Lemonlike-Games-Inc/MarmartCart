using UnityEngine;

/// <summary>
/// Cart Over-stock: while it runs, every cart restock spawns more carts (both
/// Cart Restock and Checkout + Restock sessions). Works through a budget
/// handle on CartRestockSpawner; the Match Flow profiles are never changed.
/// </summary>
[DisallowMultipleComponent]
public sealed class CartOverstockPolicyEffect : PolicyEffect
{
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private CartRestockSpawner restockSpawner;

    [Tooltip("Each restock's authored cart budget is multiplied by this. Placeholder until the resource loop review.")]
    [Min(1f)]
    [SerializeField] private float budgetMultiplier = 1.5f;

    [Tooltip("Carts added to each restock after the multiplier.")]
    [Min(0)]
    [SerializeField] private int extraCartsPerRestock;

    private int budgetHandle;

    private void Awake()
    {
        if (restockSpawner == null) restockSpawner = FindFirstObjectByType<CartRestockSpawner>();
    }

    protected override void OnPolicyStarted()
    {
        if (restockSpawner == null)
        {
            Debug.LogError("[Policy] Cart Over-stock: no CartRestockSpawner in the scene. It does nothing.", this);
            return;
        }

        budgetHandle = restockSpawner.AddBudgetModifier(budgetMultiplier, extraCartsPerRestock);
        Debug.Log($"[Policy] Cart Over-stock: restocks x{budgetMultiplier:0.##} +{extraCartsPerRestock} carts.", this);
    }

    protected override void OnPolicyStopped(PolicyStopReason reason)
    {
        if (restockSpawner != null) restockSpawner.RemoveBudgetModifier(budgetHandle);
        budgetHandle = 0;
    }
}
