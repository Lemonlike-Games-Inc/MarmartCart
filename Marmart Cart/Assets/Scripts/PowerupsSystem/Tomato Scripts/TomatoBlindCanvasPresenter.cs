using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders authoritative TomatoBlindState inside the struck player's shared
/// fullscreen-effect layer. Viewport ownership and 2P/4P camera alignment live
/// in PowerupViewportCanvasSystem, so this presenter creates no Canvas.
/// </summary>
[DisallowMultipleComponent]
public class TomatoBlindCanvasPresenter : MonoBehaviour
{
    private const int MaxPlayerSlots = PowerupRuntimeSystem.MaxPlayerSlots;

    [Header("References")]
    [SerializeField] private TomatoBlindEffectSystem blindEffectSystem;
    [SerializeField] private PowerupViewportCanvasSystem viewportCanvasSystem;
    [SerializeField] private TomatoPresentationProfile presentationProfile;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int visibleViewportCount;

    private readonly Image[] viewportImages = new Image[MaxPlayerSlots];
    private readonly TomatoBlindState[] displayedStates =
        new TomatoBlindState[MaxPlayerSlots];
    private readonly bool[] hasDisplayedState = new bool[MaxPlayerSlots];
    private readonly float[] visualStartedAtTime = new float[MaxPlayerSlots];

    private TomatoBlindEffectSystem subscribedBlindEffectSystem;
    private PowerupViewportCanvasSystem subscribedViewportCanvasSystem;
    private bool missingBlindSystemLogged;
    private bool missingCanvasSystemLogged;
    private bool missingProfileLogged;
    private bool missingSpriteLogged;

    public int VisibleViewportCount => visibleViewportCount;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureImages();
        HideAllViewports();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureSubscriptions();
        EnsureImages();
        SynchronizeAllStates();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureSubscriptions();
        EnsureImages();
        SynchronizeAllStates();
    }

    private void Update()
    {
        if (blindEffectSystem == null || viewportCanvasSystem == null)
        {
            ResolveReferences();
            EnsureSubscriptions();
            EnsureImages();
        }

        UpdateViewports();
    }

    private void OnDisable()
    {
        Unsubscribe();
        HideAllViewports();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < viewportImages.Length; i++)
        {
            if (viewportImages[i] != null)
            {
                Destroy(viewportImages[i].gameObject);
            }
        }
    }

    private void HandleBlindStateChanged(
        int playerIndex,
        TomatoBlindState state)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        bool beginsNewVisual =
            !hasDisplayedState[slotIndex] ||
            !displayedStates[slotIndex].Active ||
            displayedStates[slotIndex].EffectInstanceId !=
                state.EffectInstanceId;

        displayedStates[slotIndex] = state;
        hasDisplayedState[slotIndex] = state.Active;

        if (beginsNewVisual)
        {
            visualStartedAtTime[slotIndex] = Time.time;
        }
    }

    private void HandleBlindStateEnded(
        int playerIndex,
        TomatoBlindState state,
        PowerupEffectEndReason endReason)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        displayedStates[slotIndex] = state;
        hasDisplayedState[slotIndex] = false;
        SetImageVisible(slotIndex, false, 0f);
    }

    private void HandleViewportLayoutRefreshed()
    {
        EnsureImages();
        UpdateViewports();
    }

    private void UpdateViewports()
    {
        visibleViewportCount = 0;

        if (presentationProfile == null)
        {
            LogMissingProfileOnce();
            HideAllViewports();
            return;
        }

        missingProfileLogged = false;

        if (presentationProfile.BlindOverlaySprite == null)
        {
            if (logMissingReferences && !missingSpriteLogged)
            {
                missingSpriteLogged = true;
                Debug.LogWarning(
                    "[TomatoBlindCanvasPresenter] Assign the blind Sprite in " +
                    "TomatoPresentationProfile to display Tomato blindness.",
                    this
                );
            }

            HideAllViewports();
            return;
        }

        missingSpriteLogged = false;

        if (viewportCanvasSystem == null)
        {
            HideAllViewports();
            return;
        }

        EnsureImages();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            bool viewportCanRender =
                viewportCanvasSystem.IsViewportActive(i + 1);

            if (!viewportCanRender ||
                !hasDisplayedState[i] ||
                !displayedStates[i].Active)
            {
                SetImageVisible(i, false, 0f);
                continue;
            }

            TomatoBlindState state = displayedStates[i];
            float remainingSeconds = state.GetRemainingSeconds(Time.time);

            if (remainingSeconds <= 0f)
            {
                SetImageVisible(i, false, 0f);
                continue;
            }

            float opacity = CalculateOpacity(
                i,
                state,
                remainingSeconds
            );

            SetImageVisible(i, opacity > 0.0001f, opacity);

            if (opacity > 0.0001f) visibleViewportCount++;
        }
    }

    private float CalculateOpacity(
        int slotIndex,
        TomatoBlindState state,
        float remainingSeconds)
    {
        float stackOpacity =
            presentationProfile.FirstHitOpacity +
            Mathf.Max(0, state.SplashCount - 1) *
            presentationProfile.AdditionalHitOpacity;

        stackOpacity = Mathf.Min(
            stackOpacity,
            presentationProfile.MaximumBlindOpacity
        );

        float fadeIn = presentationProfile.BlindFadeInSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(
                (Time.time - visualStartedAtTime[slotIndex]) /
                presentationProfile.BlindFadeInSeconds
            );

        float fadeOut = presentationProfile.BlindFadeOutSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(
                remainingSeconds /
                presentationProfile.BlindFadeOutSeconds
            );

        return Mathf.Clamp01(stackOpacity * Mathf.Min(fadeIn, fadeOut));
    }

    private void SetImageVisible(
        int slotIndex,
        bool visible,
        float opacity)
    {
        Image image = viewportImages[slotIndex];
        if (image == null) return;

        ApplyProfileToImage(image);

        Color tint = presentationProfile != null
            ? presentationProfile.BlindOverlayTint
            : Color.white;

        tint.a *= Mathf.Clamp01(opacity);
        image.color = tint;
        image.enabled = visible;
    }

    private void EnsureImages()
    {
        if (viewportCanvasSystem == null) return;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (viewportImages[i] != null) continue;

            if (!viewportCanvasSystem.TryGetLayerRoot(
                    i + 1,
                    PowerupViewportCanvasSystem.ViewportLayer.FullscreenEffect,
                    out RectTransform effectRoot
                ))
            {
                continue;
            }

            GameObject imageObject = new GameObject(
                $"P{i + 1} Tomato Blind",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            RectTransform imageRect =
                imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(effectRoot, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.localScale = Vector3.one;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.enabled = false;
            ApplyProfileToImage(image);
            viewportImages[i] = image;
        }
    }

    private void ApplyProfileToImage(Image image)
    {
        if (image == null || presentationProfile == null) return;

        image.sprite = presentationProfile.BlindOverlaySprite;
        image.material = presentationProfile.BlindOverlayMaterial;
        image.preserveAspect = presentationProfile.PreserveBlindSpriteAspect;
    }

    private void SynchronizeAllStates()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            int playerIndex = i + 1;

            if (blindEffectSystem != null &&
                blindEffectSystem.TryGetState(
                    playerIndex,
                    out TomatoBlindState state
                ))
            {
                HandleBlindStateChanged(playerIndex, state);
            }
            else
            {
                hasDisplayedState[i] = false;
                SetImageVisible(i, false, 0f);
            }
        }
    }

    private void HideAllViewports()
    {
        visibleViewportCount = 0;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            SetImageVisible(i, false, 0f);
        }
    }

    private void ResolveReferences()
    {
        if (blindEffectSystem == null)
        {
            blindEffectSystem =
                FindFirstObjectByType<TomatoBlindEffectSystem>();
        }

        if (viewportCanvasSystem == null)
        {
            viewportCanvasSystem =
                FindFirstObjectByType<PowerupViewportCanvasSystem>();
        }

        if (logMissingReferences &&
            blindEffectSystem == null &&
            !missingBlindSystemLogged)
        {
            missingBlindSystemLogged = true;
            Debug.LogWarning(
                "[TomatoBlindCanvasPresenter] TomatoBlindEffectSystem was not " +
                "found; waiting for the gameplay effect service.",
                this
            );
        }

        if (logMissingReferences &&
            viewportCanvasSystem == null &&
            !missingCanvasSystemLogged)
        {
            missingCanvasSystemLogged = true;
            Debug.LogWarning(
                "[TomatoBlindCanvasPresenter] Add one " +
                "PowerupViewportCanvasSystem to the scene.",
                this
            );
        }

        if (blindEffectSystem != null) missingBlindSystemLogged = false;
        if (viewportCanvasSystem != null) missingCanvasSystemLogged = false;
    }

    private void EnsureSubscriptions()
    {
        if (subscribedBlindEffectSystem != blindEffectSystem)
        {
            if (subscribedBlindEffectSystem != null)
            {
                subscribedBlindEffectSystem.OnBlindStateChanged -=
                    HandleBlindStateChanged;
                subscribedBlindEffectSystem.OnBlindStateEnded -=
                    HandleBlindStateEnded;
            }

            subscribedBlindEffectSystem = blindEffectSystem;

            if (subscribedBlindEffectSystem != null)
            {
                subscribedBlindEffectSystem.OnBlindStateChanged +=
                    HandleBlindStateChanged;
                subscribedBlindEffectSystem.OnBlindStateEnded +=
                    HandleBlindStateEnded;
            }
        }

        if (subscribedViewportCanvasSystem != viewportCanvasSystem)
        {
            if (subscribedViewportCanvasSystem != null)
            {
                subscribedViewportCanvasSystem.OnViewportLayoutRefreshed -=
                    HandleViewportLayoutRefreshed;
            }

            subscribedViewportCanvasSystem = viewportCanvasSystem;

            if (subscribedViewportCanvasSystem != null)
            {
                subscribedViewportCanvasSystem.OnViewportLayoutRefreshed +=
                    HandleViewportLayoutRefreshed;
            }
        }
    }

    private void Unsubscribe()
    {
        if (subscribedBlindEffectSystem != null)
        {
            subscribedBlindEffectSystem.OnBlindStateChanged -=
                HandleBlindStateChanged;
            subscribedBlindEffectSystem.OnBlindStateEnded -=
                HandleBlindStateEnded;
        }

        if (subscribedViewportCanvasSystem != null)
        {
            subscribedViewportCanvasSystem.OnViewportLayoutRefreshed -=
                HandleViewportLayoutRefreshed;
        }

        subscribedBlindEffectSystem = null;
        subscribedViewportCanvasSystem = null;
    }

    private void LogMissingProfileOnce()
    {
        if (!logMissingReferences || missingProfileLogged) return;

        missingProfileLogged = true;
        Debug.LogWarning(
            "[TomatoBlindCanvasPresenter] Assign a " +
            "TomatoPresentationProfile.",
            this
        );
    }
}
