using System;
using UnityEngine;

/// <summary>
/// Reusable pooled-Ice material driver. It resolves one Renderer, creates one
/// per-instance material, restores the requested Base Map alpha and authored
/// emission on reuse, then disables emission and fades alpha to zero.
///
/// If no references are assigned, the resolver treats the component root's
/// first child as Visual Root, then looks first under Visual Root's first
/// child. This matches the authored Ice hierarchy while retaining fallbacks
/// for simpler prefabs.
/// </summary>
[DisallowMultipleComponent]
public class IceVisualFadeController : MonoBehaviour
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId =
        Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    [Header("Optional Authoring Overrides")]
    [Tooltip(
        "Leave empty to use this object's first child as Visual Root."
    )]
    [SerializeField] private Transform visualRoot;

    [Tooltip(
        "Leave empty to find the first Renderer under the first child of " +
        "Visual Root, with safe fallbacks for simpler hierarchies."
    )]
    [SerializeField] private Renderer targetRenderer;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool materialResolved;
    [SerializeField] private bool fading;
    [SerializeField] private float currentAlpha;

    private Material runtimeMaterial;
    private int colorPropertyId = -1;
    private Color authoredBaseColor = Color.white;
    private Color authoredEmissionColor = Color.black;
    private bool authoredEmissionEnabled;
    private MaterialGlobalIlluminationFlags authoredGlobalIlluminationFlags;
    private bool authoredMaterialStateCaptured;

    private float fadeStartedAtTime;
    private float fadeDurationSeconds;
    private float fadeStartAlpha;
    private Action fadeCompleted;

    public bool IsFading => fading;
    public bool HasUsableMaterial => EnsureMaterial();
    public Renderer TargetRenderer
    {
        get
        {
            ResolveRenderer();
            return targetRenderer;
        }
    }

    private void Awake()
    {
        ResolveRenderer();
    }

    private void Update()
    {
        if (!fading) return;

        float progress = fadeDurationSeconds <= 0.0001f
            ? 1f
            : Mathf.Clamp01(
                (Time.time - fadeStartedAtTime) / fadeDurationSeconds
            );

        SetAlpha(Mathf.Lerp(fadeStartAlpha, 0f, progress));

        if (progress < 1f) return;

        fading = false;
        Action callback = fadeCompleted;
        fadeCompleted = null;
        callback?.Invoke();
    }

    /// <summary>
    /// Cancels a pending fade and restores the pool-safe authored state.
    /// Returns false when no compatible material can be resolved.
    /// </summary>
    public bool PrepareForUse(int startAlphaByte)
    {
        CancelFadeWithoutCallback();

        if (!EnsureMaterial()) return false;

        RestoreEmission();
        SetAlpha(Mathf.Clamp(startAlphaByte, 0, 255) / 255f);
        return true;
    }

    /// <summary>
    /// Disables emission immediately, fades Base Map alpha using scaled game
    /// time, then invokes the pool-return callback exactly once.
    /// </summary>
    public bool BeginFade(float durationSeconds, Action onCompleted)
    {
        CancelFadeWithoutCallback();

        if (!EnsureMaterial())
        {
            onCompleted?.Invoke();
            return false;
        }

        DisableEmission();
        fadeStartAlpha = GetCurrentAlpha();
        fadeDurationSeconds = Mathf.Max(0f, durationSeconds);
        fadeStartedAtTime = Time.time;
        fadeCompleted = onCompleted;

        if (fadeDurationSeconds <= 0.0001f)
        {
            SetAlpha(0f);
            Action callback = fadeCompleted;
            fadeCompleted = null;
            callback?.Invoke();
            return true;
        }

        fading = true;
        return true;
    }

    /// <summary>
    /// Used immediately before a pooled object is disabled. No completion
    /// callback is invoked during this reset.
    /// </summary>
    public void ResetForPool(int startAlphaByte)
    {
        CancelFadeWithoutCallback();

        if (!EnsureMaterial()) return;

        RestoreEmission();
        SetAlpha(Mathf.Clamp(startAlphaByte, 0, 255) / 255f);
    }

    private void CancelFadeWithoutCallback()
    {
        fading = false;
        fadeCompleted = null;
        fadeStartedAtTime = 0f;
        fadeDurationSeconds = 0f;
        fadeStartAlpha = 0f;
    }

    private bool EnsureMaterial()
    {
        if (runtimeMaterial != null && colorPropertyId >= 0)
        {
            materialResolved = true;
            return true;
        }

        ResolveRenderer();

        if (targetRenderer == null)
        {
            materialResolved = false;
            return false;
        }

        runtimeMaterial = targetRenderer.material;

        if (runtimeMaterial == null)
        {
            materialResolved = false;
            return false;
        }

        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            colorPropertyId = BaseColorId;
        }
        else if (runtimeMaterial.HasProperty(LegacyColorId))
        {
            colorPropertyId = LegacyColorId;
        }
        else
        {
            materialResolved = false;
            return false;
        }

        if (!authoredMaterialStateCaptured)
        {
            authoredBaseColor = runtimeMaterial.GetColor(colorPropertyId);
            authoredEmissionEnabled =
                runtimeMaterial.IsKeywordEnabled("_EMISSION");
            authoredGlobalIlluminationFlags =
                runtimeMaterial.globalIlluminationFlags;

            if (runtimeMaterial.HasProperty(EmissionColorId))
            {
                authoredEmissionColor =
                    runtimeMaterial.GetColor(EmissionColorId);
            }

            authoredMaterialStateCaptured = true;
        }

        materialResolved = true;
        return true;
    }

    private void ResolveRenderer()
    {
        if (targetRenderer != null) return;

        if (visualRoot == null)
        {
            visualRoot = transform.childCount > 0
                ? transform.GetChild(0)
                : transform;
        }

        if (visualRoot.childCount > 0)
        {
            Transform firstVisualChild = visualRoot.GetChild(0);
            targetRenderer =
                firstVisualChild.GetComponent<Renderer>() ??
                firstVisualChild.GetComponentInChildren<Renderer>(true);
        }

        if (targetRenderer == null)
        {
            targetRenderer =
                visualRoot.GetComponent<Renderer>() ??
                visualRoot.GetComponentInChildren<Renderer>(true);
        }

        if (targetRenderer == null && visualRoot != transform)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private float GetCurrentAlpha()
    {
        if (!EnsureMaterial()) return 0f;
        return runtimeMaterial.GetColor(colorPropertyId).a;
    }

    private void SetAlpha(float alpha)
    {
        if (!EnsureMaterial()) return;

        Color color = authoredBaseColor;
        color.a = Mathf.Clamp01(alpha);
        runtimeMaterial.SetColor(colorPropertyId, color);
        currentAlpha = color.a;
    }

    private void DisableEmission()
    {
        if (!EnsureMaterial()) return;

        if (runtimeMaterial.HasProperty(EmissionColorId))
        {
            runtimeMaterial.SetColor(EmissionColorId, Color.black);
        }

        runtimeMaterial.DisableKeyword("_EMISSION");
        runtimeMaterial.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private void RestoreEmission()
    {
        if (!EnsureMaterial()) return;

        if (runtimeMaterial.HasProperty(EmissionColorId))
        {
            runtimeMaterial.SetColor(
                EmissionColorId,
                authoredEmissionColor
            );
        }

        if (authoredEmissionEnabled)
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            runtimeMaterial.DisableKeyword("_EMISSION");
        }

        runtimeMaterial.globalIlluminationFlags =
            authoredGlobalIlluminationFlags;
    }
}
