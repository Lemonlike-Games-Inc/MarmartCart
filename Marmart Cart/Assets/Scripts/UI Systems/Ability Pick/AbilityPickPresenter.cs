using UnityEngine;

/// <summary>
/// Shows the Ability pick: one AbilityPickView per active player, in that
/// player's Banner layer of ViewportCanvasSystem. Presentation only:
/// AbilitySamplePickController owns the pick and its timing. On its events
/// this fills the cards (icon, name, description, cooldown, tier look) and
/// shows each player's state; each second it updates the timer.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityPickPresenter : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private AbilitySamplePickController pickController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [SerializeField] private AbilityPickProfile profile;

    [Tooltip("Item icons, for a Sample without its own Card Icon.")]
    [SerializeField] private PlayerActionBarProfile itemIcons;

    [Tooltip("Player label and colour on the picked card's stamp.")]
    [SerializeField] private PlayerIdentityProfile playerIdentity;

    private readonly AbilityPickView[] views = new AbilityPickView[MaxViews];

    private bool showing;
    private int shownTimerSeconds = -1;

    #region Unity

    private void Awake()
    {
        if (pickController == null)
            pickController = FindFirstObjectByType<AbilitySamplePickController>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (profile == null || profile.ViewPrefab == null)
            Debug.LogError("[UI] AbilityPickPresenter needs an AbilityPickProfile with a view prefab. The pick will not show.", this);
    }

    private void OnEnable()
    {
        if (pickController != null)
        {
            pickController.CardsDealt += HandleCardsDealt;
            pickController.CardRevealed += HandleCardRevealed;
            pickController.ChoosingStarted += HandleChoosingStarted;
            pickController.HighlightChanged += HandlePlayerChanged;
            pickController.CardPicked += HandleCardPicked;
            pickController.AllPicked += HandleAllPicked;
            pickController.PickEnded += HandlePickEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged += HandleLayoutChanged;
    }

    private void OnDisable()
    {
        if (pickController != null)
        {
            pickController.CardsDealt -= HandleCardsDealt;
            pickController.CardRevealed -= HandleCardRevealed;
            pickController.ChoosingStarted -= HandleChoosingStarted;
            pickController.HighlightChanged -= HandlePlayerChanged;
            pickController.CardPicked -= HandleCardPicked;
            pickController.AllPicked -= HandleAllPicked;
            pickController.PickEnded -= HandlePickEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged -= HandleLayoutChanged;

        showing = false;
        HideAll();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null) Destroy(views[i].gameObject);
        }
    }

    private void Update()
    {
        if (!showing) return;

        int seconds = CurrentTimerSeconds();
        if (seconds == shownTimerSeconds) return;

        shownTimerSeconds = seconds;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf) views[i].SetTimer(seconds);
        }
    }

    #endregion

    #region Pick Events

    private void HandleCardsDealt()
    {
        showing = true;
        EnsureViews();
        FillCards();
        RefreshAll(false);

        Debug.Log($"[UI] Ability pick shown for {pickController.PlayerCount} players.", this);
    }

    private void HandleCardRevealed(int cardIndex)
    {
        RefreshAll(true);
    }

    private void HandleChoosingStarted()
    {
        RefreshAll(true);
    }

    private void HandlePlayerChanged(int playerIndex, int cardIndex)
    {
        Refresh(playerIndex - 1, true);
    }

    private void HandleCardPicked(int playerIndex, int cardIndex, bool auto)
    {
        Refresh(playerIndex - 1, true);
    }

    private void HandleAllPicked()
    {
        RefreshAll(true);
    }

    private void HandlePickEnded()
    {
        if (!showing) return;

        showing = false;
        HideAll();
        Debug.Log("[UI] Ability pick hidden.", this);
    }

    private void HandleLayoutChanged()
    {
        if (!showing) return;

        EnsureViews();
        FillCards();
        RefreshAll(false);
    }

    #endregion

    #region Views

    private void EnsureViews()
    {
        if (viewportCanvas == null || profile == null || profile.ViewPrefab == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            if (views[i] != null) continue;

            if (!viewportCanvas.TryGetPlayerLayer(i + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
                continue;

            AbilityPickView view = Instantiate(profile.ViewPrefab, layer, false);
            view.name = $"P{i + 1} Ability Pick";

            // Fill the player's view, whatever the prefab preview left on the root.
            RectTransform rect = view.RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            view.gameObject.SetActive(false);
            views[i] = view;
        }
    }

    private void FillCards()
    {
        for (int slot = 0; slot < MaxViews; slot++)
        {
            AbilityPickView view = views[slot];
            if (view == null) continue;

            for (int card = 0; card < AbilityPickView.CardCount; card++)
            {
                AbilitySampleDefinition sample = pickController.GetOffer(slot + 1, card);
                view.SetCard(card, sample, GetIcon(sample));
            }
        }
    }

    private void RefreshAll(bool animate)
    {
        for (int i = 0; i < MaxViews; i++)
        {
            Refresh(i, animate);
        }
    }

    private void Refresh(int slot, bool animate)
    {
        if (slot < 0 || slot >= MaxViews) return;

        AbilityPickView view = views[slot];
        if (view == null) return;

        int playerIndex = slot + 1;

        bool visible =
            showing &&
            playerIndex <= pickController.PlayerCount &&
            viewportCanvas != null &&
            viewportCanvas.IsPlayerViewActive(playerIndex);

        bool wasVisible = view.gameObject.activeSelf;
        if (wasVisible != visible) view.gameObject.SetActive(visible);
        if (!visible) return;

        view.ApplyLayout(profile.GetLayout(viewportCanvas.ActivePlayerCount));

        AbilitySamplePickController.PickPhase phase = pickController.Phase;
        bool allPicked = phase == AbilitySamplePickController.PickPhase.AllPicked;

        AbilityPickDisplay display = new AbilityPickDisplay
        {
            RevealedCount = pickController.RevealedCount,
            Choosing = phase == AbilitySamplePickController.PickPhase.Choosing || allPicked,
            Highlight = pickController.GetHighlight(playerIndex),
            Locked = pickController.IsLocked(playerIndex),
            AllPicked = allPicked,
            ShowTimer = !allPicked,
            TimerSeconds = CurrentTimerSeconds(),
            PlayerLabel = playerIdentity != null ? playerIdentity.GetLabel(playerIndex) : $"P{playerIndex}",
            PlayerColor = playerIdentity != null ? playerIdentity.GetColor(playerIndex) : Color.white
        };

        view.Display(display, animate && wasVisible);
    }

    private void HideAll()
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf) views[i].gameObject.SetActive(false);
        }
    }

    private Sprite GetIcon(AbilitySampleDefinition sample)
    {
        if (sample == null) return null;
        if (sample.CardIcon != null) return sample.CardIcon;
        return itemIcons != null ? itemIcons.GetIcon(sample.Item) : null;
    }

    private int CurrentTimerSeconds()
    {
        return pickController != null ? Mathf.CeilToInt(pickController.RemainingPickSeconds) : 0;
    }

    #endregion
}
