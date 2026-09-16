using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GroceryLootPickup : MonoBehaviour
{
    [Header("Loot Definition")]
    [SerializeField] private GroceryLootDefinition lootDefinition;

    [Header("Pickup")]
    [SerializeField] private bool destroyOnCollected = true;
    [Min(0f)][SerializeField] private float destroyDelay = 0f;

    [Header("Lifetime")]
    [Tooltip("Automatically destroys this uncollected pickup after this many seconds. 0 disables self cleanup.")]
    [Min(0f)][SerializeField] private float selfCleanTime = 20f;

    [Header("Prototype Presentation")]
    [SerializeField] private bool useDefinitionDebugPresentation = true;
    [SerializeField] private Transform debugVisualRoot;
    [SerializeField] private Renderer debugRenderer;
    [SerializeField] private Material prototypeSharedMaterial;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool collected;
    [SerializeField] private bool collectionArmed = true;

    private Collider pickupCollider;
    private Vector3 baseDebugVisualScale = Vector3.one;
    private MaterialPropertyBlock materialPropertyBlock;
    private Coroutine armRoutine;

    public GroceryLootDefinition LootDefinition => lootDefinition;
    public bool IsCollected => collected;

    public event Action<GroceryLootPickup, CargoCapacityController> OnCollected;

    public void Initialize(GroceryLootDefinition definition)
    {
        lootDefinition = definition;
        collected = false;
        collectionArmed = true;

        if (pickupCollider != null) pickupCollider.enabled = true;

        ApplyDefinitionPresentation();
        RestartSelfCleanTimer();
    }

    /// <summary>
    /// Useful for freshly spilled cargo so it cannot be instantly recollected
    /// while still overlapping the cart/player that caused the spill.
    /// </summary>
    public void ArmCollectionAfterDelay(float delay)
    {
        if (armRoutine != null)
        {
            StopCoroutine(armRoutine);
            armRoutine = null;
        }

        if (delay <= 0f)
        {
            collectionArmed = true;
            if (pickupCollider != null) pickupCollider.enabled = true;
            return;
        }

        collectionArmed = false;
        if (pickupCollider != null) pickupCollider.enabled = false;
        armRoutine = StartCoroutine(ArmAfterDelayRoutine(delay));
    }

    private IEnumerator ArmAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        armRoutine = null;

        if (collected) yield break;

        collectionArmed = true;
        if (pickupCollider != null) pickupCollider.enabled = true;
    }

    private void Awake()
    {
        materialPropertyBlock = new MaterialPropertyBlock();
        pickupCollider = GetComponent<Collider>();

        if (debugVisualRoot != null) baseDebugVisualScale = debugVisualRoot.localScale;

        ValidateSetup();
        ApplyDefinitionPresentation();
    }

    private void Start()
    {
        RestartSelfCleanTimer();
    }

    private void OnDisable()
    {
        if (armRoutine != null)
        {
            StopCoroutine(armRoutine);
            armRoutine = null;
        }
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        selfCleanTime = Mathf.Max(0f, selfCleanTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!collectionArmed || collected || other == null || lootDefinition == null) return;

        CargoCapacityController cargoController = other.GetComponentInParent<CargoCapacityController>();
        if (cargoController == null) return;

        TryCollect(cargoController);
    }

    public bool TryCollect(CargoCapacityController cargoController)
    {
        if (!collectionArmed || collected || cargoController == null || lootDefinition == null) return false;
        if (!cargoController.TryAddLoot(lootDefinition, out _)) return false;

        collected = true;
        collectionArmed = false;
        CancelInvoke(nameof(SelfClean));

        if (pickupCollider != null) pickupCollider.enabled = false;

        OnCollected?.Invoke(this, cargoController);

        if (destroyOnCollected) Destroy(gameObject, destroyDelay);
        else gameObject.SetActive(false);

        return true;
    }

    public void ApplyDefinitionPresentation()
    {
        if (!useDefinitionDebugPresentation || lootDefinition == null) return;

        if (debugVisualRoot != null)
        {
            debugVisualRoot.localScale = baseDebugVisualScale * lootDefinition.WorldDebugScale;
        }

        if (debugRenderer != null)
        {
            if (prototypeSharedMaterial != null) debugRenderer.sharedMaterial = prototypeSharedMaterial;
            if (materialPropertyBlock == null) materialPropertyBlock = new MaterialPropertyBlock();

            debugRenderer.GetPropertyBlock(materialPropertyBlock);

            Color color = lootDefinition.WorldDebugColor;
            materialPropertyBlock.SetColor("_BaseColor", color);
            materialPropertyBlock.SetColor("_Color", color);

            debugRenderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    private void RestartSelfCleanTimer()
    {
        CancelInvoke(nameof(SelfClean));

        if (!Application.isPlaying || collected || selfCleanTime <= 0f) return;

        Invoke(nameof(SelfClean), selfCleanTime);
    }

    private void SelfClean()
    {
        if (!collected) Destroy(gameObject);
    }

    private void ValidateSetup()
    {
        if (lootDefinition == null) Debug.LogWarning("[GroceryLootPickup] GroceryLootDefinition is not assigned.", this);

        if (pickupCollider == null)
        {
            Debug.LogError(
                "[GroceryLootPickup] No Collider found on the same GameObject. Put the pickup trigger Collider on the same object as GroceryLootPickup.",
                this
            );

            return;
        }
    }
}
