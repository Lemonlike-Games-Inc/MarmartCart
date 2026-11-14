using UnityEngine;

public class GroceryItemBehavior : MonoBehaviour, ISpawnerHoldable
{
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private float selfCleanTime = 20f;
    [SerializeField] private bool isExpensiveItem = false;

    private bool _heldBySpawner = false;
    public void OnSpawnerHoldStart()
    {
        _heldBySpawner = true;
    }

    public void OnSpawnerHoldEnd()
    {
        _heldBySpawner = false;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        // Automatically destroy the powerup item after 30 seconds if not collected
        if (_heldBySpawner) {return; }
        Destroy(this.gameObject, selfCleanTime);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //private void OnTriggerEnter(Collider other)
    //{
    //    if (!isExpensiveItem)
    //    {
    //        if (other.gameObject.CompareTag("Player1"))
    //        {
    //            SnakeCartManager p1SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p1SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p1SnakeCartManager.CollectNormalGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player2"))
    //        {
    //            SnakeCartManager p2SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p2SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p2SnakeCartManager.CollectNormalGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player3"))
    //        {
    //            SnakeCartManager p3SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p3SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p3SnakeCartManager.CollectNormalGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player4"))
    //        {
    //            SnakeCartManager p4SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p4SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p4SnakeCartManager.CollectNormalGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //    }
    //    else
    //    {
    //        if (other.gameObject.CompareTag("Player1"))
    //        {
    //            SnakeCartManager p1SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p1SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p1SnakeCartManager.CollectExpensiveGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player2"))
    //        {
    //            SnakeCartManager p2SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p2SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p2SnakeCartManager.CollectExpensiveGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player3"))
    //        {
    //            SnakeCartManager p3SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p3SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p3SnakeCartManager.CollectExpensiveGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //        else if (other.gameObject.CompareTag("Player4"))
    //        {
    //            SnakeCartManager p4SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
    //            if (p4SnakeCartManager.HasEmptyCartForGroceryItem())
    //            {
    //                sfxManager.PlaySFX("CollectGroceryItem");
    //                p4SnakeCartManager.CollectExpensiveGroceryItem();
    //                TutorialGroceryTaskManager.Instance?.NotifyGroceryCollected(other.tag);
    //                Destroy(this.gameObject);
    //            }
    //        }
    //    }
    //}
}
