using UnityEngine;

public class GrabberBehavior : MonoBehaviour
{
    //private enum GrabbedPayloadType
    //{
    //    None,
    //    NormalGrocery,
    //    ExpensiveGrocery,
    //    NormalPowerup,
    //    GoldPowerup
    //}

    //[Header("Owner References")]
    //[SerializeField] private PowerupsManager ownerPowerupsManager;
    //[SerializeField] private SnakeCartManager ownerSnakeCartManager;

    //[Header("Visual Payloads (inside head)")]
    //[SerializeField] private GameObject visualNormalGrocery;
    //[SerializeField] private GameObject visualExpensiveGrocery;
    //[SerializeField] private GameObject visualNormalPowerup;
    //[SerializeField] private GameObject visualGoldPowerup;

    //[Header("Tags")]
    //[SerializeField] private string normalGroceryTag = "NormalGrocery";
    //[SerializeField] private string expensiveGroceryTag = "ExpensiveGrocery";
    //[SerializeField] private string normalPowerupTag = "PowerupNormal";
    //[SerializeField] private string goldPowerupTag = "PowerupGold";

    //private bool hasGrabbedSomething = false;
    //private GrabbedPayloadType grabbedType = GrabbedPayloadType.None;

    //// For groceries items
    //private bool groceryCanReward = false; // whether we can actually give the grocery at retract time because of capacity
    //private bool groceryIsExpensive = false; // which type of grocery we grabbed

    //void Awake()
    //{
    //    SetAllVisuals(false);
    //}

    //private void OnTriggerEnter(Collider other)
    //{
    //    if (hasGrabbedSomething) return; // only first hit matters

    //    GameObject hitObj = other.gameObject;

    //    // 1. Normal grocery
    //    if (hitObj.CompareTag(normalGroceryTag))
    //    {
    //        HandleGroceryHit(hitObj, isExpensive: false);
    //        return;
    //    }

    //    // 2. Expensive grocery
    //    if (hitObj.CompareTag(expensiveGroceryTag))
    //    {
    //        HandleGroceryHit(hitObj, isExpensive: true);
    //        return;
    //    }

    //    // 3. Normal powerup basket
    //    if (hitObj.CompareTag(normalPowerupTag))
    //    {
    //        HandlePowerupBasketHit(hitObj, GrabbedPayloadType.NormalPowerup);
    //        return;
    //    }

    //    // 4. Gold powerup basket
    //    if (hitObj.CompareTag(goldPowerupTag))
    //    {
    //        HandlePowerupBasketHit(hitObj, GrabbedPayloadType.GoldPowerup);
    //        return;
    //    }
    //}

    //// ---------------- GROCERY ----------------

    //private void HandleGroceryHit(GameObject item, bool isExpensive)
    //{
    //    if (ownerSnakeCartManager == null)
    //    {
    //        Debug.LogWarning("GrabberBehavior: No SnakeCartManager assigned.");
    //        return;
    //    }

    //    // Decide at HIT time whether this should eventually reward
    //    groceryCanReward = CheckHasSpaceForGrocery(isExpensive);
    //    groceryIsExpensive = isExpensive;

    //    // We still visually grab and remove the item either way
    //    Destroy(item);
    //    hasGrabbedSomething = true;
    //    grabbedType = isExpensive ? GrabbedPayloadType.ExpensiveGrocery
    //                              : GrabbedPayloadType.NormalGrocery;

    //    SetAllVisuals(false);
    //    if (isExpensive)
    //    {
    //        if (visualExpensiveGrocery != null) visualExpensiveGrocery.SetActive(true);
    //    }
    //    else
    //    {
    //        if (visualNormalGrocery != null) visualNormalGrocery.SetActive(true);
    //    }
    //}

    //// Just a capacity check, no actual assignment yet
    //private bool CheckHasSpaceForGrocery(bool isExpensive)
    //{
    //    return ownerSnakeCartManager.HasEmptyCartForGroceryItem();
    //}

    //// Actually assign the grocery to a free cart when retract ends
    //private void GiveGroceryToPlayer(bool isExpensive)
    //{
    //    if(isExpensive)
    //    {
    //        ownerSnakeCartManager.CollectExpensiveGroceryItem();
    //    }
    //    else
    //    {
    //        ownerSnakeCartManager.CollectNormalGroceryItem();
    //    }
    //}

    //// ---------------- POWERUP ----------------

    //private void HandlePowerupBasketHit(GameObject item, GrabbedPayloadType type)
    //{
    //    if (ownerPowerupsManager == null)
    //    {
    //        Debug.LogWarning("GrabberBehavior: No PowerupsManager assigned.");
    //        return;
    //    }

    //    Destroy(item);
    //    hasGrabbedSomething = true;
    //    grabbedType = type;

    //    SetAllVisuals(false);
    //    if (type == GrabbedPayloadType.NormalPowerup && visualNormalPowerup != null)
    //        visualNormalPowerup.SetActive(true);
    //    else if (type == GrabbedPayloadType.GoldPowerup && visualGoldPowerup != null)
    //        visualGoldPowerup.SetActive(true);
    //}

    //private void SetAllVisuals(bool state)
    //{
    //    if (visualNormalGrocery != null) visualNormalGrocery.SetActive(state);
    //    if (visualExpensiveGrocery != null) visualExpensiveGrocery.SetActive(state);
    //    if (visualNormalPowerup != null) visualNormalPowerup.SetActive(state);
    //    if (visualGoldPowerup != null) visualGoldPowerup.SetActive(state);
    //}

    //// ---------------- CALLED BY ANIMATOR ----------------

    ///// <summary>
    ///// Called by GrabberAnimator when the grabber fully retracts.
    ///// This is where ALL rewards are actually applied.
    ///// </summary>
    //public void OnRetractComplete()
    //{
    //    if (!hasGrabbedSomething)
    //    {
    //        ClearState();
    //        return;
    //    }

    //    switch (grabbedType)
    //    {
    //        case GrabbedPayloadType.NormalGrocery:
    //            if (groceryCanReward)
    //            {
    //                GiveGroceryToPlayer(groceryIsExpensive);
    //            }
    //            break;
    //        case GrabbedPayloadType.ExpensiveGrocery:
    //            if (groceryCanReward)
    //            {
    //                GiveGroceryToPlayer(groceryIsExpensive);
    //            }
    //            break;

    //        case GrabbedPayloadType.NormalPowerup:
    //            if (ownerPowerupsManager != null)
    //                ownerPowerupsManager.RollRandomPowerup(PowerupTier.Normal);
    //            break;

    //        case GrabbedPayloadType.GoldPowerup:
    //            if (ownerPowerupsManager != null)
    //                ownerPowerupsManager.RollRandomPowerup(PowerupTier.Gold);
    //            break;
    //    }

    //    ClearState();
    //}

    //private void ClearState()
    //{
    //    hasGrabbedSomething = false;
    //    grabbedType = GrabbedPayloadType.None;
    //    groceryCanReward = false;
    //    groceryIsExpensive = false;
    //    SetAllVisuals(false);
    //}

    //// For runtime hookup if needed
    //public void SetOwner(PowerupsManager pMgr, SnakeCartManager snake)
    //{
    //    ownerPowerupsManager = pMgr;
    //    ownerSnakeCartManager = snake;
    //}
}
