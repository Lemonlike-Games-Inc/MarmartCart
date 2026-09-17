using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-level, event-driven presenter for each player's one-slot power-up
/// inventory. It creates one widget under each shared viewport HUD root and
/// updates only when players register/unregister or inventory changes.
/// </summary>
[DisallowMultipleComponent]
public class PowerupHeldItemHudPresenter : MonoBehaviour
{
    [Serializable]
    private struct SlotDiagnostics
    {
        public int PlayerIndex;
        public bool PlayerBound;
        public bool HasStoredPowerup;
        public PowerupId StoredPowerup;
        public bool Visible;
    }

    private const int MaxPlayerSlots = PowerupRuntimeSystem.MaxPlayerSlots;

    [Header("References")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private PowerupViewportCanvasSystem viewportCanvasSystem;
    [SerializeField] private PowerupHudProfile hudProfile;

    [Header("Late Binding")]
    [Min(0.02f)]
    [SerializeField] private float bindingRetryInterval = 0.1f;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingIconAssignments = true;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private SlotDiagnostics[] slotDiagnostics =
        new SlotDiagnostics[MaxPlayerSlots];

    private readonly PlayerPowerupController[] boundControllers =
        new PlayerPowerupController[MaxPlayerSlots];
    private readonly RectTransform[] widgetRoots =
        new RectTransform[MaxPlayerSlots];
    private readonly RectTransform[] backgroundRects =
        new RectTransform[MaxPlayerSlots];
    private readonly Image[] backgroundImages =
        new Image[MaxPlayerSlots];
    private readonly Image[] iconImages =
        new Image[MaxPlayerSlots];
    private readonly bool[] hasStoredPowerup = new bool[MaxPlayerSlots];
    private readonly PowerupId[] storedPowerups =
        new PowerupId[MaxPlayerSlots];
    private readonly bool[] missingIconLogged = new bool[4];

    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private PowerupViewportCanvasSystem subscribedViewportCanvasSystem;
    private Coroutine bindingRoutine;
    private bool missingProfileLogged;

    private void Reset()
    {
        EnsureDiagnostics();
        ResolveReferences();
    }

    private void Awake()
    {
        EnsureDiagnostics();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureSubscriptions();
        SynchronizePlayers();
        EnsureWidgets();
        RefreshAllSlots();
        StartBindingIfNeeded();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureSubscriptions();
        SynchronizePlayers();
        EnsureWidgets();
        RefreshAllSlots();
        StartBindingIfNeeded();
    }

    private void OnDisable()
    {
        StopBinding();
        UnsubscribeFromSystems();
        UnbindAllPlayers();
        SetAllWidgetsActive(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < widgetRoots.Length; i++)
        {
            if (widgetRoots[i] != null)
            {
                Destroy(widgetRoots[i].gameObject);
            }
        }
    }

    private void OnValidate()
    {
        bindingRetryInterval = Mathf.Max(0.02f, bindingRetryInterval);
        EnsureDiagnostics();
    }

    private void HandlePlayerRegistered(
        int playerIndex,
        PlayerPowerupController controller)
    {
        BindPlayer(playerIndex, controller);
        RefreshSlot(playerIndex - 1);
    }

    private void HandlePlayerUnregistered(int playerIndex)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        UnbindPlayer(slotIndex);
        hasStoredPowerup[slotIndex] = false;
        RefreshSlot(slotIndex);
    }

    private void HandleStoredPowerupChanged(
        PlayerPowerupController controller,
        PowerupId? powerupId)
    {
        if (controller == null) return;

        int slotIndex = controller.PlayerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots ||
            boundControllers[slotIndex] != controller)
        {
            return;
        }

        hasStoredPowerup[slotIndex] = powerupId.HasValue;

        if (powerupId.HasValue)
        {
            storedPowerups[slotIndex] = powerupId.Value;
        }

        RefreshSlot(slotIndex);
    }

    private void HandleViewportLayoutRefreshed()
    {
        EnsureWidgets();
        RefreshAllSlots();
    }

    private void SynchronizePlayers()
    {
        if (runtimeSystem == null) return;

        for (int playerIndex = 1;
             playerIndex <= MaxPlayerSlots;
             playerIndex++)
        {
            if (runtimeSystem.TryGetPlayer(
                    playerIndex,
                    out PlayerPowerupController controller
                ))
            {
                BindPlayer(playerIndex, controller);
            }
            else
            {
                UnbindPlayer(playerIndex - 1);
                hasStoredPowerup[playerIndex - 1] = false;
            }
        }
    }

    private void BindPlayer(
        int playerIndex,
        PlayerPowerupController controller)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots ||
            controller == null)
        {
            return;
        }

        if (boundControllers[slotIndex] != controller)
        {
            UnbindPlayer(slotIndex);
            boundControllers[slotIndex] = controller;
            controller.OnStoredPowerupChanged +=
                HandleStoredPowerupChanged;
        }

        hasStoredPowerup[slotIndex] = controller.HasStoredPowerup;
        storedPowerups[slotIndex] = controller.StoredPowerup;
    }

    private void UnbindPlayer(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        PlayerPowerupController controller =
            boundControllers[slotIndex];

        if (controller != null)
        {
            controller.OnStoredPowerupChanged -=
                HandleStoredPowerupChanged;
        }

        boundControllers[slotIndex] = null;
    }

    private void UnbindAllPlayers()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            UnbindPlayer(i);
        }
    }

    private void EnsureWidgets()
    {
        if (viewportCanvasSystem == null) return;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (widgetRoots[i] != null) continue;

            if (!viewportCanvasSystem.TryGetLayerRoot(
                    i + 1,
                    PowerupViewportCanvasSystem.ViewportLayer.Hud,
                    out RectTransform hudRoot
                ))
            {
                continue;
            }

            CreateWidget(i, hudRoot);
        }
    }

    private void CreateWidget(int slotIndex, RectTransform hudRoot)
    {
        GameObject widgetObject = new GameObject(
            $"P{slotIndex + 1} Held Power-up",
            typeof(RectTransform)
        );

        RectTransform widgetRoot =
            widgetObject.GetComponent<RectTransform>();
        widgetRoot.SetParent(hudRoot, false);

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();
        backgroundRect.SetParent(widgetRoot, false);
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);

        Image background = backgroundObject.GetComponent<Image>();
        background.raycastTarget = false;
        background.type = Image.Type.Simple;

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(widgetRoot, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;

        Image icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.type = Image.Type.Simple;

        widgetRoots[slotIndex] = widgetRoot;
        backgroundRects[slotIndex] = backgroundRect;
        backgroundImages[slotIndex] = background;
        iconImages[slotIndex] = icon;
    }

    private void RefreshAllSlots()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            RefreshSlot(i);
        }
    }

    private void RefreshSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        EnsureDiagnostics();

        SlotDiagnostics diagnostics = slotDiagnostics[slotIndex];
        diagnostics.PlayerIndex = slotIndex + 1;
        diagnostics.PlayerBound = boundControllers[slotIndex] != null;
        diagnostics.HasStoredPowerup = hasStoredPowerup[slotIndex];
        diagnostics.StoredPowerup = storedPowerups[slotIndex];

        RectTransform widgetRoot = widgetRoots[slotIndex];

        if (widgetRoot == null ||
            viewportCanvasSystem == null ||
            hudProfile == null)
        {
            if (widgetRoot != null) widgetRoot.gameObject.SetActive(false);
            diagnostics.Visible = false;
            slotDiagnostics[slotIndex] = diagnostics;

            if (hudProfile == null && !missingProfileLogged)
            {
                missingProfileLogged = true;
                Debug.LogWarning(
                    "[PowerupHeldItemHudPresenter] Assign a PowerupHudProfile.",
                    this
                );
            }

            return;
        }

        missingProfileLogged = false;
        ApplyLayout(slotIndex);

        Sprite iconSprite = hasStoredPowerup[slotIndex]
            ? hudProfile.GetIcon(storedPowerups[slotIndex])
            : hudProfile.EmptySlotIcon;

        bool shouldShow =
            viewportCanvasSystem.IsViewportActive(slotIndex + 1) &&
            (
                hasStoredPowerup[slotIndex] ||
                hudProfile.ShowSlotWhenEmpty
            );

        Image background = backgroundImages[slotIndex];
        Image icon = iconImages[slotIndex];
        Sprite backgroundSprite =
            hudProfile.GetBackgroundSprite(slotIndex + 1);

        if (background != null)
        {
            background.sprite = backgroundSprite;
            background.color = hudProfile.SlotBackgroundTint;
            background.enabled = shouldShow && backgroundSprite != null;
        }

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.color = hudProfile.IconTint;
            icon.preserveAspect = hudProfile.PreserveIconAspect;
            icon.enabled = shouldShow && iconSprite != null;
        }

        bool hasVisibleGraphic =
            background != null && background.enabled ||
            icon != null && icon.enabled;

        widgetRoot.gameObject.SetActive(shouldShow && hasVisibleGraphic);
        diagnostics.Visible = shouldShow && hasVisibleGraphic;
        slotDiagnostics[slotIndex] = diagnostics;

        if (hasStoredPowerup[slotIndex] && iconSprite == null)
        {
            LogMissingIconOnce(storedPowerups[slotIndex]);
        }
    }

    private void ApplyLayout(int slotIndex)
    {
        RectTransform widgetRoot = widgetRoots[slotIndex];
        RectTransform backgroundRect = backgroundRects[slotIndex];
        Image iconImage = iconImages[slotIndex];

        if (widgetRoot == null ||
            backgroundRect == null ||
            iconImage == null ||
            hudProfile == null)
        {
            return;
        }

        hudProfile.GetLayout(
            viewportCanvasSystem.ActivePlayerCount,
            out Vector2 anchor,
            out Vector2 backgroundPositionPixels,
            out Vector2 iconPositionPixels,
            out Vector2 backgroundSizePixels,
            out Vector2 iconSizePixels
        );

        widgetRoot.anchorMin = anchor;
        widgetRoot.anchorMax = anchor;
        widgetRoot.pivot = new Vector2(0.5f, 0.5f);
        widgetRoot.anchoredPosition = Vector2.zero;
        widgetRoot.sizeDelta = Vector2.zero;
        widgetRoot.localScale = Vector3.one;

        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = backgroundPositionPixels;
        backgroundRect.sizeDelta = backgroundSizePixels;
        backgroundRect.localScale = Vector3.one;

        RectTransform iconRect = iconImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = iconPositionPixels;
        iconRect.sizeDelta = iconSizePixels;
        iconRect.localScale = Vector3.one;
    }

    private void SetAllWidgetsActive(bool active)
    {
        for (int i = 0; i < widgetRoots.Length; i++)
        {
            if (widgetRoots[i] != null)
            {
                widgetRoots[i].gameObject.SetActive(active);
            }
        }
    }

    private void ResolveReferences()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }

        if (viewportCanvasSystem == null)
        {
            viewportCanvasSystem =
                FindFirstObjectByType<PowerupViewportCanvasSystem>();
        }
    }

    private void EnsureSubscriptions()
    {
        if (subscribedRuntimeSystem != runtimeSystem)
        {
            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnPlayerRegistered -=
                    HandlePlayerRegistered;
                subscribedRuntimeSystem.OnPlayerUnregistered -=
                    HandlePlayerUnregistered;
            }

            subscribedRuntimeSystem = runtimeSystem;

            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnPlayerRegistered +=
                    HandlePlayerRegistered;
                subscribedRuntimeSystem.OnPlayerUnregistered +=
                    HandlePlayerUnregistered;
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

    private void UnsubscribeFromSystems()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnPlayerRegistered -=
                HandlePlayerRegistered;
            subscribedRuntimeSystem.OnPlayerUnregistered -=
                HandlePlayerUnregistered;
        }

        if (subscribedViewportCanvasSystem != null)
        {
            subscribedViewportCanvasSystem.OnViewportLayoutRefreshed -=
                HandleViewportLayoutRefreshed;
        }

        subscribedRuntimeSystem = null;
        subscribedViewportCanvasSystem = null;
    }

    private void StartBindingIfNeeded()
    {
        if ((runtimeSystem != null && viewportCanvasSystem != null) ||
            bindingRoutine != null ||
            !isActiveAndEnabled)
        {
            return;
        }

        bindingRoutine = StartCoroutine(BindReferencesRoutine());
    }

    private void StopBinding()
    {
        if (bindingRoutine == null) return;

        StopCoroutine(bindingRoutine);
        bindingRoutine = null;
    }

    private IEnumerator BindReferencesRoutine()
    {
        while (isActiveAndEnabled &&
               (runtimeSystem == null || viewportCanvasSystem == null))
        {
            ResolveReferences();
            EnsureSubscriptions();
            SynchronizePlayers();
            EnsureWidgets();
            RefreshAllSlots();

            if (runtimeSystem != null && viewportCanvasSystem != null)
            {
                break;
            }

            yield return new WaitForSecondsRealtime(bindingRetryInterval);
        }

        bindingRoutine = null;
    }

    private void EnsureDiagnostics()
    {
        if (slotDiagnostics == null ||
            slotDiagnostics.Length != MaxPlayerSlots)
        {
            Array.Resize(ref slotDiagnostics, MaxPlayerSlots);
        }

        for (int i = 0; i < slotDiagnostics.Length; i++)
        {
            SlotDiagnostics diagnostics = slotDiagnostics[i];
            diagnostics.PlayerIndex = i + 1;
            slotDiagnostics[i] = diagnostics;
        }
    }

    private void LogMissingIconOnce(PowerupId powerupId)
    {
        if (!logMissingIconAssignments) return;

        int index = (int)powerupId;

        if (index < 0 || index >= missingIconLogged.Length ||
            missingIconLogged[index])
        {
            return;
        }

        missingIconLogged[index] = true;
        Debug.LogWarning(
            $"[PowerupHeldItemHudPresenter] No HUD Sprite is assigned for " +
            $"{powerupId} in PowerupHudProfile.",
            this
        );
    }
}
