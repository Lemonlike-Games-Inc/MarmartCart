using UnityEngine;

/// <summary>
/// Owns temporary material-based cart feedback.
///
/// IMPORTANT:
/// QuickOutline appends its runtime OutlineMask / OutlineFill materials after
/// the cart's normal material slots. This manager preserves those appended
/// materials whenever it swaps between Base, Ghost, and Vulnerable visuals.
///
/// Supported visual states:
///
/// Ghost:
/// - Uses the assigned ghost material.
/// - Alpha fades/blinks continuously.
/// - Blink accelerates as the ghost timer approaches zero.
///
/// Vulnerable:
/// - Alternates between the cart's normal/team materials and the assigned
///   vulnerable material.
/// - Uses a constant hard-flash rate.
/// - Intended to use a bright white / emissive vulnerable material.
/// </summary>
[DisallowMultipleComponent]
public class CartMaterialManager : MonoBehaviour
{
    private enum VisualMode
    {
        None,
        Ghost,
        Vulnerable
    }

    #region References

    [Header("References")]
    [SerializeField] private Renderer cartRenderer;

    [Header("Effect Materials")]
    [SerializeField] private Material ghostMaterial;

    [Tooltip("Recommended: bright white / emissive material for the Vulnerable hard flash.")]
    [SerializeField] private Material vulnerableMaterial;

    #endregion

    #region Ghost Blink

    [Header("Ghost Blink")]
    [Tooltip("Remaining ghost time at which the blink begins accelerating toward Max Blink Rate.")]
    [Min(0.01f)]
    [SerializeField] private float ghostBlinkAccelerationWindow = 1f;

    [Min(0f)]
    [SerializeField] private float ghostMinBlinkRate = 2f;

    [Min(0f)]
    [SerializeField] private float ghostMaxBlinkRate = 10f;

    #endregion

    #region Vulnerable Flash

    [Header("Vulnerable Flash")]
    [Tooltip("Number of full Team Color <-> White flash cycles per second.")]
    [Min(0.01f)]
    [SerializeField] private float vulnerableFlashRate = 4f;

    #endregion

    #region Runtime

    private VisualMode currentMode = VisualMode.None;

    // Only the cart's original material slots. QuickOutline materials are NOT
    // stored here.
    private Material[] baseMaterials;
    private int baseMaterialSlotCount;

    // Effect materials only cover the original cart material slots.
    private Material[] ghostMaterialInstances;
    private Material[] vulnerableMaterialInstances;

    // Runtime arrays used while an effect is active:
    //
    // [cart materials...] + [QuickOutline / other appended materials...]
    //
    // These are prepared once when the effect starts, then reused while
    // Vulnerable flashes instead of allocating every flash.
    private Material[] baseMaterialsWithExtras;
    private Material[] ghostMaterialsWithExtras;
    private Material[] vulnerableMaterialsWithExtras;

    private int[] ghostColorPropertyIds;
    private float[] ghostBaseAlphas;

    private float ghostTimeRemaining;
    private float blinkTimer;

    private bool vulnerableShowingEffect;

    public bool IsGhostVisualActive => currentMode == VisualMode.Ghost;
    public bool IsVulnerableVisualActive => currentMode == VisualMode.Vulnerable;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartRenderer == null)
        {
            Debug.LogError("[CartMaterialManager] Cart Renderer is not assigned.", this);
            enabled = false;
            return;
        }

        CaptureInitialBaseMaterials();

        if (ghostMaterial != null) CreateGhostInstances();
        if (vulnerableMaterial != null) CreateVulnerableInstances();
    }

    private void Update()
    {
        switch (currentMode)
        {
            case VisualMode.Ghost:
                UpdateGhostVisual();
                break;

            case VisualMode.Vulnerable:
                UpdateVulnerableVisual();
                break;
        }
    }

    private void OnDestroy()
    {
        DestroyMaterialInstances(ghostMaterialInstances);
        DestroyMaterialInstances(vulnerableMaterialInstances);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Refreshes only the cart's normal material slots.
    ///
    /// Any extra materials currently appended by QuickOutline are deliberately
    /// ignored so they never become part of the stored base cart material set.
    /// </summary>
    public void RefreshBaseMaterials()
    {
        if (currentMode != VisualMode.None || cartRenderer == null || baseMaterialSlotCount <= 0) return;

        Material[] currentMaterials = cartRenderer.sharedMaterials;

        if (currentMaterials == null || currentMaterials.Length < baseMaterialSlotCount) return;

        if (baseMaterials == null || baseMaterials.Length != baseMaterialSlotCount)
        {
            baseMaterials = new Material[baseMaterialSlotCount];
        }

        for (int i = 0; i < baseMaterialSlotCount; i++)
        {
            baseMaterials[i] = currentMaterials[i];
        }
    }

    public void SetGhostMode(float duration)
    {
        if (cartRenderer == null || ghostMaterialInstances == null) return;

        // Vulnerability is a gameplay-critical chain state and should remain
        // visually readable until gameplay explicitly clears it.
        if (currentMode == VisualMode.Vulnerable) return;

        ghostTimeRemaining = Mathf.Max(0f, duration);
        blinkTimer = 0f;

        if (ghostTimeRemaining <= 0f)
        {
            ClearVisualMode();
            return;
        }

        PrepareEffectMaterialSets();

        currentMode = VisualMode.Ghost;
        ApplyMaterialSet(ghostMaterialsWithExtras);

        ApplyGhostAlpha(1f);
    }

    public void SetVulnerableMode(bool vulnerable)
    {
        if (!vulnerable)
        {
            if (currentMode == VisualMode.Vulnerable) ClearVisualMode();
            return;
        }

        if (cartRenderer == null || vulnerableMaterialInstances == null) return;

        ghostTimeRemaining = 0f;
        blinkTimer = 0f;

        PrepareEffectMaterialSets();

        currentMode = VisualMode.Vulnerable;

        // Start on the bright Vulnerable frame so the state change is
        // immediately obvious.
        vulnerableShowingEffect = true;
        ApplyMaterialSet(vulnerableMaterialsWithExtras);
    }

    public void ClearVisualMode()
    {
        currentMode = VisualMode.None;

        ghostTimeRemaining = 0f;
        blinkTimer = 0f;
        vulnerableShowingEffect = false;

        if (baseMaterialsWithExtras != null)
        {
            ApplyMaterialSet(baseMaterialsWithExtras);
        }
        else
        {
            // No active effect snapshot exists. Preserve whatever extra
            // QuickOutline materials are currently appended.
            PrepareEffectMaterialSets();
            ApplyMaterialSet(baseMaterialsWithExtras);
        }

        ClearPreparedEffectMaterialSets();
    }

    #endregion

    #region Ghost Visual

    private void UpdateGhostVisual()
    {
        ghostTimeRemaining -= Time.deltaTime;
        blinkTimer += Time.deltaTime;

        float normalizedRemaining = ghostBlinkAccelerationWindow > 0f
            ? Mathf.Clamp01(ghostTimeRemaining / ghostBlinkAccelerationWindow)
            : 0f;

        float blinkRate = Mathf.Lerp(ghostMinBlinkRate, ghostMaxBlinkRate, 1f - normalizedRemaining);
        float blinkAlpha = Mathf.Abs(Mathf.Sin(blinkTimer * blinkRate));

        ApplyGhostAlpha(blinkAlpha);

        if (ghostTimeRemaining <= 0f) ClearVisualMode();
    }

    private void ApplyGhostAlpha(float alphaMultiplier)
    {
        if (ghostMaterialInstances == null || ghostColorPropertyIds == null || ghostBaseAlphas == null) return;

        int count = Mathf.Min(
            ghostMaterialInstances.Length,
            Mathf.Min(ghostColorPropertyIds.Length, ghostBaseAlphas.Length)
        );

        for (int i = 0; i < count; i++)
        {
            Material material = ghostMaterialInstances[i];
            int colorPropertyId = ghostColorPropertyIds[i];

            if (material == null || colorPropertyId == -1) continue;

            Color color = material.GetColor(colorPropertyId);
            color.a = ghostBaseAlphas[i] * Mathf.Clamp01(alphaMultiplier);
            material.SetColor(colorPropertyId, color);
        }
    }

    #endregion

    #region Vulnerable Visual

    private void UpdateVulnerableVisual()
    {
        blinkTimer += Time.deltaTime;

        bool shouldShowEffect =
            Mathf.Sin(blinkTimer * vulnerableFlashRate * Mathf.PI * 2f) >= 0f;

        if (shouldShowEffect == vulnerableShowingEffect) return;

        vulnerableShowingEffect = shouldShowEffect;

        if (vulnerableShowingEffect)
        {
            ApplyMaterialSet(vulnerableMaterialsWithExtras);
        }
        else
        {
            ApplyMaterialSet(baseMaterialsWithExtras);
        }
    }

    #endregion

    #region Material Setup

    private void CaptureInitialBaseMaterials()
    {
        Material[] initialMaterials = cartRenderer.sharedMaterials;

        baseMaterialSlotCount = initialMaterials != null ? initialMaterials.Length : 0;

        if (baseMaterialSlotCount <= 0)
        {
            Debug.LogError("[CartMaterialManager] Cart Renderer has no base materials.", this);
            baseMaterials = new Material[0];
            return;
        }

        baseMaterials = new Material[baseMaterialSlotCount];

        for (int i = 0; i < baseMaterialSlotCount; i++)
        {
            baseMaterials[i] = initialMaterials[i];
        }
    }

    /// <summary>
    /// Captures all material slots appended after the cart's original slots
    /// (QuickOutline normally contributes OutlineMask + OutlineFill) and builds
    /// Base/Ghost/Vulnerable arrays that all preserve those extras.
    /// </summary>
    private void PrepareEffectMaterialSets()
    {
        if (cartRenderer == null || baseMaterials == null) return;

        Material[] currentMaterials = cartRenderer.sharedMaterials;

        int currentCount = currentMaterials != null ? currentMaterials.Length : 0;
        int extraCount = Mathf.Max(0, currentCount - baseMaterialSlotCount);

        baseMaterialsWithExtras = BuildMaterialSet(baseMaterials, currentMaterials, extraCount);

        if (ghostMaterialInstances != null)
        {
            ghostMaterialsWithExtras = BuildMaterialSet(ghostMaterialInstances, currentMaterials, extraCount);
        }

        if (vulnerableMaterialInstances != null)
        {
            vulnerableMaterialsWithExtras = BuildMaterialSet(vulnerableMaterialInstances, currentMaterials, extraCount);
        }
    }

    private Material[] BuildMaterialSet(Material[] cartMaterials, Material[] currentMaterials, int extraCount)
    {
        if (cartMaterials == null) return null;

        Material[] combined = new Material[baseMaterialSlotCount + extraCount];

        for (int i = 0; i < baseMaterialSlotCount; i++)
        {
            combined[i] = i < cartMaterials.Length ? cartMaterials[i] : null;
        }

        for (int i = 0; i < extraCount; i++)
        {
            combined[baseMaterialSlotCount + i] = currentMaterials[baseMaterialSlotCount + i];
        }

        return combined;
    }

    private void ApplyMaterialSet(Material[] materials)
    {
        if (cartRenderer == null || materials == null) return;

        // Use sharedMaterials here because every material reference in these
        // runtime arrays is already the exact instance we want. This avoids
        // Unity creating extra renderer material instances on every flash.
        cartRenderer.sharedMaterials = materials;
    }

    private void ClearPreparedEffectMaterialSets()
    {
        baseMaterialsWithExtras = null;
        ghostMaterialsWithExtras = null;
        vulnerableMaterialsWithExtras = null;
    }

    private void CreateGhostInstances()
    {
        int materialCount = Mathf.Max(1, baseMaterialSlotCount);

        ghostMaterialInstances = new Material[materialCount];
        ghostColorPropertyIds = new int[materialCount];
        ghostBaseAlphas = new float[materialCount];

        for (int i = 0; i < materialCount; i++)
        {
            Material instance = new Material(ghostMaterial);

            ghostMaterialInstances[i] = instance;
            ghostColorPropertyIds[i] = GetColorPropertyId(instance);

            if (ghostColorPropertyIds[i] != -1)
            {
                ghostBaseAlphas[i] = instance.GetColor(ghostColorPropertyIds[i]).a;
            }
            else
            {
                ghostBaseAlphas[i] = 1f;
            }
        }
    }

    private void CreateVulnerableInstances()
    {
        int materialCount = Mathf.Max(1, baseMaterialSlotCount);

        vulnerableMaterialInstances = new Material[materialCount];

        for (int i = 0; i < materialCount; i++)
        {
            vulnerableMaterialInstances[i] = new Material(vulnerableMaterial);
        }
    }

    private int GetColorPropertyId(Material material)
    {
        if (material == null) return -1;

        int baseColorId = Shader.PropertyToID("_BaseColor");
        if (material.HasProperty(baseColorId)) return baseColorId;

        int colorId = Shader.PropertyToID("_Color");
        if (material.HasProperty(colorId)) return colorId;

        return -1;
    }

    private void DestroyMaterialInstances(Material[] materials)
    {
        if (materials == null) return;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null) Destroy(materials[i]);
        }
    }

    #endregion
}
