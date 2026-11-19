using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial-only owner of per-player ControlsOverlayUI instances.
/// Builds each active player's hints on that player's HUD layer of
/// ViewportCanvasSystem, which matches and clips the player's viewport.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialControlHintsManager : MonoBehaviour
{
    public const int MaxPlayers = 4;

    [Header("Overlay")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Tooltip(
        "Prefab containing ControlsOverlayUI and rows ordered: " +
        "Movement, Drift, Speedup, Reverse, Checkout, Aim, Activate Powerup."
    )]
    [SerializeField] private GameObject controlHintsPrefab;

    [Header("Scene Systems")]
    [SerializeField] private PlayerInputManager playerInputManager;
    [SerializeField] private tutorialGatesController tutorialGates;

    [Header("Runtime Cart Binding")]
    [Min(0.01f)]
    [SerializeField] private float bindingRetryInterval = 0.05f;

    [Tooltip("0 = wait forever.")]
    [Min(0f)]
    [SerializeField] private float bindingTimeout = 10f;

    [SerializeField] private bool logSuccessfulBindings;

    [Header("Runtime - Read Only")]
    [SerializeField] private int activePlayerCount;
    [SerializeField] private int boundPlayerCount;
    [SerializeField] private bool overlaysBuilt;

    private readonly ControlsOverlayUI[] playerOverlays =
        new ControlsOverlayUI[MaxPlayers];

    private Coroutine setupRoutine;
    private Coroutine bindingRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGates();

        if (overlaysBuilt)
        {
            ApplyCurrentGateVisibility();
            StartBindingIfNeeded();
        }
    }

    private void Start()
    {
        if (setupRoutine == null && !overlaysBuilt)
            setupRoutine = StartCoroutine(SetupWhenSceneModeReady());
    }

    private void OnDisable()
    {
        UnsubscribeFromGates();
        StopRoutines();
        UnbindAll();
    }

    private void OnDestroy()
    {
        DestroyGeneratedOverlays();
    }

    private void OnValidate()
    {
        bindingRetryInterval = Mathf.Max(0.01f, bindingRetryInterval);
        bindingTimeout = Mathf.Max(0f, bindingTimeout);
    }

    private IEnumerator SetupWhenSceneModeReady()
    {
        ResolveReferences();

        while (viewportCanvas == null || !viewportCanvas.IsReady)
        {
            yield return null;
            ResolveReferences();
        }

        activePlayerCount = viewportCanvas.ActivePlayerCount;
        BuildOverlays(activePlayerCount);
        ApplyCurrentGateVisibility();

        setupRoutine = null;
        StartBindingIfNeeded();
    }

    private void BuildOverlays(int playerCount)
    {
        if (overlaysBuilt) return;

        ResolveReferences();

        if (viewportCanvas == null)
        {
            Debug.LogError(
                "[TutorialControlHintsManager] ViewportCanvasSystem is missing.",
                this
            );
            return;
        }

        if (controlHintsPrefab == null)
        {
            Debug.LogError(
                "[TutorialControlHintsManager] Control Hints Prefab is missing.",
                this
            );
            return;
        }

        for (int playerIndex = 1; playerIndex <= playerCount; playerIndex++)
        {
            if (!viewportCanvas.TryGetPlayerLayer(
                    playerIndex,
                    ViewportCanvasSystem.PlayerLayer.Hud,
                    out RectTransform hudLayer))
            {
                continue;
            }

            ControlsOverlayUI overlay = Instantiate(
                controlHintsPrefab,
                hudLayer,
                false
            ).GetComponent<ControlsOverlayUI>();

            overlay.name = $"Control Hints P{playerIndex}";
            overlay.ConfigurePlayer(playerIndex);
            overlay.ApplyInitialVisibility();
            overlay.EnforceRowOrder();

            playerOverlays[playerIndex - 1] = overlay;
        }

        overlaysBuilt = true;
    }

    private void StartBindingIfNeeded()
    {
        if (!isActiveAndEnabled ||
            !overlaysBuilt ||
            bindingRoutine != null ||
            boundPlayerCount >= activePlayerCount)
        {
            return;
        }

        bindingRoutine = StartCoroutine(BindRuntimeCarts());
    }

    private IEnumerator BindRuntimeCarts()
    {
        float startedAt = Time.realtimeSinceStartup;
        WaitForSecondsRealtime wait =
            new WaitForSecondsRealtime(bindingRetryInterval);

        while (boundPlayerCount < activePlayerCount)
        {
            boundPlayerCount = 0;

            for (int playerIndex = 1;
                 playerIndex <= activePlayerCount;
                 playerIndex++)
            {
                ControlsOverlayUI overlay =
                    playerOverlays[playerIndex - 1];

                if (overlay == null) continue;

                if (overlay.BoundCart == null)
                {
                    CartControlScript cart =
                        ResolvePlayerCart(playerIndex);

                    if (cart != null)
                    {
                        overlay.BindToCart(cart);

                        if (logSuccessfulBindings)
                        {
                            Debug.Log(
                                $"[TutorialControlHintsManager] " +
                                $"P{playerIndex} bound to '{cart.name}'.",
                                cart
                            );
                        }
                    }
                }

                if (overlay.BoundCart != null)
                    boundPlayerCount++;
            }

            if (boundPlayerCount >= activePlayerCount)
                break;

            if (bindingTimeout > 0f &&
                Time.realtimeSinceStartup - startedAt >= bindingTimeout)
            {
                ReportMissingBindings();
                bindingRoutine = null;
                yield break;
            }

            yield return wait;
        }

        bindingRoutine = null;
    }

    private CartControlScript ResolvePlayerCart(int playerIndex)
    {
        if (playerInputManager == null) return null;

        // Preferred path: returned after the runtime cart exists and input
        // binding has completed.
        CartControlScript bound =
            playerInputManager.GetBoundCartControl(playerIndex);

        if (bound != null) return bound;

        // Startup fallback while PlayerInputManager is still doing its own
        // runtime-cart retry loop.
        GameObject playerRoot =
            playerInputManager.GetPlayerRoot(playerIndex);

        if (playerRoot == null || !playerRoot.activeInHierarchy)
            return null;

        return playerRoot.GetComponentInChildren<CartControlScript>(true);
    }

    private void ReportMissingBindings()
    {
        for (int playerIndex = 1;
             playerIndex <= activePlayerCount;
             playerIndex++)
        {
            ControlsOverlayUI overlay =
                playerOverlays[playerIndex - 1];

            if (overlay != null && overlay.BoundCart != null)
                continue;

            Debug.LogError(
                $"[TutorialControlHintsManager] Timed out waiting for " +
                $"Player {playerIndex}'s runtime CartControlScript.",
                this
            );
        }
    }

    private void SubscribeToGates()
    {
        if (tutorialGates == null)
            tutorialGates = FindFirstObjectByType<tutorialGatesController>();

        if (tutorialGates == null) return;

        tutorialGates.OnCheckoutHintsUnlocked -= HandleCheckoutHintsUnlocked;
        tutorialGates.OnCheckoutHintsUnlocked += HandleCheckoutHintsUnlocked;

        tutorialGates.OnAdvancedControlHintsUnlocked -= HandleAdvancedHintsUnlocked;
        tutorialGates.OnAdvancedControlHintsUnlocked += HandleAdvancedHintsUnlocked;
    }

    private void UnsubscribeFromGates()
    {
        if (tutorialGates == null) return;

        tutorialGates.OnCheckoutHintsUnlocked -= HandleCheckoutHintsUnlocked;
        tutorialGates.OnAdvancedControlHintsUnlocked -= HandleAdvancedHintsUnlocked;
    }

    private void ApplyCurrentGateVisibility()
    {
        bool checkoutVisible =
            tutorialGates != null &&
            tutorialGates.CheckoutHintsUnlocked;

        bool advancedVisible =
            tutorialGates != null &&
            tutorialGates.AdvancedControlHintsUnlocked;

        for (int i = 0; i < playerOverlays.Length; i++)
        {
            ControlsOverlayUI overlay = playerOverlays[i];
            if (overlay == null) continue;

            overlay.SetBaseHintsIntroduced(true);
            overlay.SetCheckoutIntroduced(checkoutVisible);
            overlay.SetAdvancedHintsIntroduced(advancedVisible);
        }
    }

    private void HandleCheckoutHintsUnlocked()
    {
        for (int i = 0; i < playerOverlays.Length; i++)
            playerOverlays[i]?.SetCheckoutIntroduced(true);
    }

    private void HandleAdvancedHintsUnlocked()
    {
        for (int i = 0; i < playerOverlays.Length; i++)
            playerOverlays[i]?.SetAdvancedHintsIntroduced(true);
    }

    private void ResolveReferences()
    {
        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (playerInputManager == null)
            playerInputManager =
                FindFirstObjectByType<PlayerInputManager>();

        if (tutorialGates == null)
            tutorialGates =
                FindFirstObjectByType<tutorialGatesController>();
    }

    private void StopRoutines()
    {
        if (setupRoutine != null)
        {
            StopCoroutine(setupRoutine);
            setupRoutine = null;
        }

        if (bindingRoutine != null)
        {
            StopCoroutine(bindingRoutine);
            bindingRoutine = null;
        }
    }

    private void UnbindAll()
    {
        boundPlayerCount = 0;

        for (int i = 0; i < playerOverlays.Length; i++)
            playerOverlays[i]?.Unbind();
    }

    private void DestroyGeneratedOverlays()
    {
        for (int i = 0; i < playerOverlays.Length; i++)
        {
            if (playerOverlays[i] != null)
                Destroy(playerOverlays[i].gameObject);

            playerOverlays[i] = null;
        }

        overlaysBuilt = false;
        boundPlayerCount = 0;
    }
}
