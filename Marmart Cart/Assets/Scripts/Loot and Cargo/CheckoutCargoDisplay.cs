using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only checkout cargo pile.
///
/// Every checked-out CargoEntry creates a NEW visual-only copy using that
/// entry's already-selected CargoVisualDefinition. The authoritative CargoEntry
/// is not modified and this display never participates in cargo ownership,
/// scoring, capacity, spilling, or checkout rules.
///
/// Each physical cart is presented as ONE simultaneous wave:
/// - every cargo visual in that cart spawns at Wave Spawn Origin;
/// - all visuals travel at the same time;
/// - there is intentionally NO stagger;
/// - every visual lands in its precomputed checkout slot;
/// - all visuals in the wave use the same timing.
///
/// Slot model:
/// - authoredSlots define the designed first section of the pile;
/// - once those are exhausted, the last N authored slots become a repeating
///   overflow template;
/// - each repeated template layer moves along Stack Direction Reference.up;
/// - if Stack Direction Reference is empty, world up is used.
/// </summary>
[DisallowMultipleComponent]
public class CheckoutCargoDisplay : MonoBehaviour
{
    #region Slot Layout

    [Header("Predesigned Checkout Slots")]
    [Tooltip("Exact presentation slots used first, in order.")]
    [SerializeField] private Transform[] authoredSlots;

    [Tooltip("How many slots at the END of Authored Slots form the repeating overflow template.")]
    [Min(1)]
    [SerializeField] private int overflowTemplateSlotCount = 6;

    [Tooltip("Distance added for each repeated overflow layer.")]
    [Min(0.01f)]
    [SerializeField] private float overflowLayerHeight = 0.35f;

    [Header("Stack Direction")]
    [Tooltip("Optional Transform whose local UP (green Y axis) defines the infinite stack direction. If unassigned, world up is used.")]
    [SerializeField] private Transform stackDirectionReference;

    [Tooltip("Optional parent for spawned checkout visuals. If empty, a runtime child is created.")]
    [SerializeField] private Transform runtimeVisualRoot;

    #endregion

    #region Wave Animation

    [Header("Cargo Wave Animation")]
    [SerializeField] private bool animateCargoWaves = true;

    [Tooltip("Base spawn position for checkout cargo waves. If unassigned, this component's Transform is used.")]
    [SerializeField] private Transform waveSpawnOrigin;

    [Tooltip("When enabled, the effective wave spawn position rises with the current overflow/top layer of the checkout pile.")]
    [SerializeField] private bool waveOriginFollowsStackHeight = true;

    [Tooltip("Additional distance along Stack Direction added above the current top layer.")]
    [SerializeField] private float waveOriginTopLayerOffset = 0f;

    [Tooltip("Travel time for the entire cart's cargo wave. There is intentionally no per-item stagger.")]
    [Min(0.01f)]
    [SerializeField] private float waveTravelDuration = 0.22f;

    [Tooltip("Adds an arc along the configured stack direction while cargo travels.")]
    [Min(0f)]
    [SerializeField] private float waveArcHeight = 0.3f;

    [Tooltip("Shared travel curve for every item in the wave.")]
    [SerializeField] private AnimationCurve waveTravelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip(
        "Global presentation-only scale applied to every spawned checkout cargo visual. " +
        "1 = prefab's original scale, 0.5 = half size, 2 = double size. " +
        "This does not modify the source prefab or gameplay cargo."
    )]
    [Min(0.01f)]
    [SerializeField] private float finalVisualScaleMultiplier = 1f;

    [Tooltip("Visuals begin at this fraction of their FINAL checkout-display scale.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float waveStartScaleMultiplier = 0.75f;

    [Tooltip("Scale multiplier reached briefly when the wave lands.")]
    [Min(1f)]
    [SerializeField] private float arrivalPopScaleMultiplier = 1.12f;

    [Tooltip("Total duration of the arrival scale pop.")]
    [Min(0f)]
    [SerializeField] private float arrivalPopDuration = 0.1f;

    #endregion

    #region Prototype Fallback

    [Header("Prototype Fallback")]
    [Tooltip("Used only when a CargoEntry has no assigned cargo visual prefab.")]
    [Min(0.01f)]
    [SerializeField] private float prototypeSphereScale = 0.22f;

    [SerializeField] private Material prototypeSphereMaterial;

    #endregion

    #region Runtime Debug

    [Header("Runtime - Read Only")]
    [SerializeField] private bool sessionActive;
    [SerializeField] private int displayedCargoCount;
    [SerializeField] private int displayedWaveCount;
    [SerializeField] private int activeWaveAnimationCount;

    private readonly List<GameObject> spawnedVisuals = new List<GameObject>(64);
    private MaterialPropertyBlock materialPropertyBlock;

    #endregion

    #region Internal Wave Data

    private sealed class WaveVisual
    {
        public GameObject instance;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public Quaternion startRotation;
        public Quaternion targetRotation;
        public Vector3 targetScale;
    }

    #endregion

    #region Public State

    public bool SessionActive => sessionActive;
    public int DisplayedCargoCount => displayedCargoCount;
    public int DisplayedWaveCount => displayedWaveCount;
    public bool IsAnimating => activeWaveAnimationCount > 0;
    public int ActiveWaveAnimationCount => activeWaveAnimationCount;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureRuntimeVisualRoot();
        materialPropertyBlock = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        ClearSession();
    }

    private void OnValidate()
    {
        overflowTemplateSlotCount = Mathf.Max(1, overflowTemplateSlotCount);
        overflowLayerHeight = Mathf.Max(0.01f, overflowLayerHeight);

        waveTravelDuration = Mathf.Max(0.01f, waveTravelDuration);
        waveArcHeight = Mathf.Max(0f, waveArcHeight);
        finalVisualScaleMultiplier = Mathf.Max(0.01f, finalVisualScaleMultiplier);
        waveStartScaleMultiplier = Mathf.Clamp(waveStartScaleMultiplier, 0.05f, 1f);
        arrivalPopScaleMultiplier = Mathf.Max(1f, arrivalPopScaleMultiplier);
        arrivalPopDuration = Mathf.Max(0f, arrivalPopDuration);

        prototypeSphereScale = Mathf.Max(0.01f, prototypeSphereScale);
    }

    #endregion

    #region Session API

    public void BeginSession()
    {
        ClearSpawnedVisuals();

        sessionActive = true;
        displayedCargoCount = 0;
        displayedWaveCount = 0;
        activeWaveAnimationCount = 0;
    }

    /// <summary>
    /// Adds one whole physical cart's cargo as one simultaneous presentation wave.
    /// There is intentionally no stagger between individual items.
    /// </summary>
    public void AddCargoWave(IReadOnlyList<CargoEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;

        if (!sessionActive) BeginSession();

        List<WaveVisual> waveVisuals = new List<WaveVisual>(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            CargoEntry entry = entries[i];
            if (entry == null) continue;

            int slotIndex = displayedCargoCount;

            if (TryCreateWaveVisual(entry, slotIndex, out WaveVisual waveVisual))
            {
                waveVisuals.Add(waveVisual);
                displayedCargoCount++;
            }
        }

        if (waveVisuals.Count <= 0) return;

        displayedWaveCount++;

        if (!animateCargoWaves)
        {
            PlaceWaveImmediately(waveVisuals);
            return;
        }

        StartCoroutine(AnimateWave(waveVisuals));
    }

    public void ClearSession()
    {
        StopAllCoroutines();
        activeWaveAnimationCount = 0;

        ClearSpawnedVisuals();

        sessionActive = false;
        displayedCargoCount = 0;
        displayedWaveCount = 0;
    }

    #endregion

    #region Wave Creation

    private bool TryCreateWaveVisual(CargoEntry entry, int slotIndex, out WaveVisual waveVisual)
    {
        waveVisual = null;

        EnsureRuntimeVisualRoot();

        GetSlotWorldPose(slotIndex, out Vector3 targetPosition, out Quaternion targetRotation);

        Vector3 startPosition = GetCurrentWaveSpawnPosition();
        Quaternion startRotation = waveSpawnOrigin != null ? waveSpawnOrigin.rotation : transform.rotation;

        CargoVisualDefinition visualDefinition = entry.SelectedCargoVisual;
        GameObject visualInstance;

        if (visualDefinition != null && visualDefinition.CargoVisualPrefab != null)
        {
            visualInstance = Instantiate(visualDefinition.CargoVisualPrefab, runtimeVisualRoot);
            visualInstance.name = $"CheckoutCargo_{slotIndex}_{visualDefinition.DisplayName}";
            visualInstance.transform.SetPositionAndRotation(startPosition, startRotation);
        }
        else
        {
            visualInstance = CreatePrototypeSphere(entry, slotIndex, startPosition, startRotation);
        }

        if (visualInstance == null) return false;

        PrepareAsVisualOnly(visualInstance);

        // Preserve the source prefab's authored proportions, then apply one
        // checkout-display-only global scale. Wave/start/pop multipliers remain
        // relative to this final presentation scale.
        Vector3 targetScale = visualInstance.transform.localScale * finalVisualScaleMultiplier;

        if (animateCargoWaves)
        {
            visualInstance.transform.localScale = targetScale * waveStartScaleMultiplier;
        }
        else
        {
            visualInstance.transform.localScale = targetScale;
        }

        spawnedVisuals.Add(visualInstance);

        waveVisual = new WaveVisual
        {
            instance = visualInstance,
            startPosition = startPosition,
            targetPosition = targetPosition,
            startRotation = startRotation,
            targetRotation = targetRotation,
            targetScale = targetScale
        };

        return true;
    }

    private void PlaceWaveImmediately(List<WaveVisual> waveVisuals)
    {
        for (int i = 0; i < waveVisuals.Count; i++)
        {
            WaveVisual item = waveVisuals[i];
            if (item == null || item.instance == null) continue;

            item.instance.transform.SetPositionAndRotation(item.targetPosition, item.targetRotation);
            item.instance.transform.localScale = item.targetScale;
        }
    }

    #endregion

    #region Wave Animation

    private IEnumerator AnimateWave(List<WaveVisual> waveVisuals)
    {
        activeWaveAnimationCount++;

        float duration = Mathf.Max(0.01f, waveTravelDuration);
        float elapsed = 0f;
        Vector3 arcDirection = GetStackDirection();

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float travelT = waveTravelCurve != null
                ? Mathf.Clamp01(waveTravelCurve.Evaluate(normalizedTime))
                : normalizedTime;

            // Shared normalized time = no stagger. Every item in the cart wave
            // advances through its own trajectory simultaneously.
            float arc = 4f * normalizedTime * (1f - normalizedTime) * waveArcHeight;

            for (int i = 0; i < waveVisuals.Count; i++)
            {
                WaveVisual item = waveVisuals[i];
                if (item == null || item.instance == null) continue;

                Transform visualTransform = item.instance.transform;

                Vector3 basePosition = Vector3.Lerp(item.startPosition, item.targetPosition, travelT);
                visualTransform.position = basePosition + arcDirection * arc;
                visualTransform.rotation = Quaternion.Slerp(item.startRotation, item.targetRotation, travelT);
                visualTransform.localScale = Vector3.Lerp(
                    item.targetScale * waveStartScaleMultiplier,
                    item.targetScale,
                    travelT
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < waveVisuals.Count; i++)
        {
            WaveVisual item = waveVisuals[i];
            if (item == null || item.instance == null) continue;

            item.instance.transform.SetPositionAndRotation(item.targetPosition, item.targetRotation);
            item.instance.transform.localScale = item.targetScale;
        }

        if (arrivalPopDuration > 0f && arrivalPopScaleMultiplier > 1f)
        {
            yield return AnimateArrivalPop(waveVisuals);
        }

        activeWaveAnimationCount = Mathf.Max(0, activeWaveAnimationCount - 1);
    }

    private IEnumerator AnimateArrivalPop(List<WaveVisual> waveVisuals)
    {
        float duration = Mathf.Max(0.01f, arrivalPopDuration);
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            float popT = normalizedTime < 0.5f
                ? normalizedTime / 0.5f
                : 1f - ((normalizedTime - 0.5f) / 0.5f);

            float scaleMultiplier = Mathf.Lerp(1f, arrivalPopScaleMultiplier, popT);

            for (int i = 0; i < waveVisuals.Count; i++)
            {
                WaveVisual item = waveVisuals[i];
                if (item == null || item.instance == null) continue;

                item.instance.transform.localScale = item.targetScale * scaleMultiplier;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < waveVisuals.Count; i++)
        {
            WaveVisual item = waveVisuals[i];
            if (item == null || item.instance == null) continue;

            item.instance.transform.localScale = item.targetScale;
        }
    }

    #endregion

    #region Visual Spawning Helpers

    private GameObject CreatePrototypeSphere(CargoEntry entry, int slotIndex, Vector3 worldPosition, Quaternion worldRotation)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"DEBUG_CheckoutCargo_{slotIndex}_{entry.Category}";
        sphere.transform.SetParent(runtimeVisualRoot, true);
        sphere.transform.SetPositionAndRotation(worldPosition, worldRotation);

        CargoVisualDefinition visualDefinition = entry.SelectedCargoVisual;
        float multiplier = visualDefinition != null ? visualDefinition.DebugScaleMultiplier : 1f;
        sphere.transform.localScale = Vector3.one * prototypeSphereScale * multiplier;

        Renderer renderer = sphere.GetComponent<Renderer>();

        if (renderer != null)
        {
            if (prototypeSphereMaterial != null) renderer.sharedMaterial = prototypeSphereMaterial;

            if (materialPropertyBlock == null) materialPropertyBlock = new MaterialPropertyBlock();

            Color debugColor = GetDebugColor(entry);
            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor("_BaseColor", debugColor);
            materialPropertyBlock.SetColor("_Color", debugColor);
            renderer.SetPropertyBlock(materialPropertyBlock);
        }

        return sphere;
    }

    private void PrepareAsVisualOnly(GameObject visualInstance)
    {
        if (visualInstance == null) return;

        Collider[] colliders = visualInstance.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        Rigidbody[] bodies = visualInstance.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null) continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private Color GetDebugColor(CargoEntry entry)
    {
        if (entry != null && entry.SelectedCargoVisual != null) return entry.SelectedCargoVisual.DebugColor;
        if (entry != null && entry.SourceLoot != null) return entry.SourceLoot.WorldDebugColor;

        return Color.white;
    }

    #endregion

    #region Dynamic Wave Origin

    /// <summary>
    /// Returns the effective visual launch position for the NEXT checkout wave.
    ///
    /// The authored Wave Spawn Origin is the base. Once cargo has reached the
    /// repeating overflow portion of the pile, the launch point rises by the
    /// same overflow-layer step used by the checkout slots.
    /// </summary>
    private Vector3 GetCurrentWaveSpawnPosition()
    {
        Vector3 basePosition = waveSpawnOrigin != null ? waveSpawnOrigin.position : transform.position;

        if (!waveOriginFollowsStackHeight)
        {
            return basePosition + GetStackDirection() * waveOriginTopLayerOffset;
        }

        int topLayer = GetCurrentTopOverflowLayer();

        return basePosition +
               GetStackDirection() * (topLayer * overflowLayerHeight + waveOriginTopLayerOffset);
    }

    /// <summary>
    /// Returns 0 while the pile is still using only authored slots.
    /// Returns 1 for the first repeated overflow layer, 2 for the next, etc.
    ///
    /// displayedCargoCount points to the NEXT free slot, so the effective wave
    /// origin rises exactly when the incoming wave begins filling a higher layer.
    /// </summary>
    private int GetCurrentTopOverflowLayer()
    {
        if (!TryGetValidSlotLayout(out int validSlotCount)) return 0;
        if (displayedCargoCount < validSlotCount) return 0;

        int templateCount = Mathf.Clamp(overflowTemplateSlotCount, 1, validSlotCount);
        int overflowIndex = displayedCargoCount - validSlotCount;

        return overflowIndex / templateCount + 1;
    }

    #endregion

    #region Slot Calculation

    public void GetSlotWorldPose(int slotIndex, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        if (!TryGetValidSlotLayout(out int validSlotCount))
        {
            worldPosition = transform.position + GetStackDirection() * displayedCargoCount * overflowLayerHeight;
            worldRotation = transform.rotation;
            return;
        }

        if (slotIndex < validSlotCount)
        {
            Transform slot = authoredSlots[slotIndex];

            worldPosition = slot.position;
            worldRotation = slot.rotation;
            return;
        }

        int templateCount = Mathf.Clamp(overflowTemplateSlotCount, 1, validSlotCount);
        int templateStartIndex = validSlotCount - templateCount;

        int overflowIndex = slotIndex - validSlotCount;
        int templateOffset = overflowIndex % templateCount;
        int overflowLayer = overflowIndex / templateCount + 1;

        Transform templateSlot = authoredSlots[templateStartIndex + templateOffset];

        Vector3 layerOffset = GetStackDirection() * (overflowLayerHeight * overflowLayer);

        worldPosition = templateSlot.position + layerOffset;
        worldRotation = templateSlot.rotation;
    }

    private Vector3 GetStackDirection()
    {
        Vector3 direction = stackDirectionReference != null ? stackDirectionReference.up : Vector3.up;

        if (direction.sqrMagnitude < 0.0001f) return Vector3.up;

        return direction.normalized;
    }

    private bool TryGetValidSlotLayout(out int validSlotCount)
    {
        validSlotCount = 0;

        if (authoredSlots == null || authoredSlots.Length == 0) return false;

        for (int i = 0; i < authoredSlots.Length; i++)
        {
            if (authoredSlots[i] == null) break;
            validSlotCount++;
        }

        return validSlotCount > 0;
    }

    #endregion

    #region Cleanup

    private void EnsureRuntimeVisualRoot()
    {
        if (runtimeVisualRoot != null) return;

        Transform existing = transform.Find("Runtime Checkout Cargo Visuals");

        if (existing != null)
        {
            runtimeVisualRoot = existing;
            return;
        }

        GameObject root = new GameObject("Runtime Checkout Cargo Visuals");
        root.transform.SetParent(transform, false);

        runtimeVisualRoot = root.transform;
    }

    private void ClearSpawnedVisuals()
    {
        for (int i = spawnedVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = spawnedVisuals[i];
            if (visual != null) Destroy(visual);
        }

        spawnedVisuals.Clear();
    }

    #endregion

    #region Debug

    [ContextMenu("TEST - Clear Checkout Display")]
    private void DebugClearCheckoutDisplay()
    {
        ClearSession();
    }

    private void OnDrawGizmos()
    {
        DrawWaveOriginGizmo();

        Vector3 directionOrigin = stackDirectionReference != null ? stackDirectionReference.position : transform.position;
        Vector3 stackDirection = GetStackDirection();

        Gizmos.DrawLine(directionOrigin, directionOrigin + stackDirection * Mathf.Max(0.5f, overflowLayerHeight * 2f));
        Gizmos.DrawSphere(directionOrigin + stackDirection * Mathf.Max(0.5f, overflowLayerHeight * 2f), 0.05f);

        if (!TryGetValidSlotLayout(out int validSlotCount)) return;

        for (int i = 0; i < validSlotCount; i++)
        {
            Transform slot = authoredSlots[i];
            if (slot == null) continue;

            Gizmos.DrawWireSphere(slot.position, 0.06f);
            Gizmos.DrawLine(slot.position, slot.position + slot.forward * 0.15f);
        }

        int templateCount = Mathf.Clamp(overflowTemplateSlotCount, 1, validSlotCount);
        int templateStartIndex = validSlotCount - templateCount;

        for (int layer = 1; layer <= 2; layer++)
        {
            for (int i = 0; i < templateCount; i++)
            {
                Transform slot = authoredSlots[templateStartIndex + i];
                if (slot == null) continue;

                Vector3 previewPosition = slot.position + GetStackDirection() * (overflowLayerHeight * layer);
                Gizmos.DrawWireCube(previewPosition, Vector3.one * 0.08f);
            }
        }
    }

    private void DrawWaveOriginGizmo()
    {
        Vector3 baseOrigin = waveSpawnOrigin != null ? waveSpawnOrigin.position : transform.position;
        Vector3 effectiveOrigin = Application.isPlaying ? GetCurrentWaveSpawnPosition() : baseOrigin + GetStackDirection() * waveOriginTopLayerOffset;

        Gizmos.DrawWireSphere(baseOrigin, 0.12f);
        Gizmos.DrawLine(baseOrigin, effectiveOrigin);
        Gizmos.DrawWireSphere(effectiveOrigin, 0.09f);
        Gizmos.DrawLine(effectiveOrigin, effectiveOrigin + GetStackDirection() * 0.25f);
    }

    #endregion
}
