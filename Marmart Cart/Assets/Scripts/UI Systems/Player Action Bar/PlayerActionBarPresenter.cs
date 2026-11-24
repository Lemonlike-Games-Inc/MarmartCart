using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Scene-level presenter of the Player Action Bar, the row of cards at the
/// bottom of each player's view (map Powerup, Ability, later Emergency).
/// It puts each player's bar prefab (from PlayerActionBarProfile) into that
/// player's HUD layer of ViewportCanvasSystem, binds the player's
/// PlayerItemHand and PlayerAbilityController, and tells the bar what to show.
/// Event driven, except the Ability charge, which it pushes each frame while
/// the charge runs.
/// </summary>
[DisallowMultipleComponent]
public class PlayerActionBarPresenter : MonoBehaviour
{
    private const int MaxPlayerSlots = PowerupRuntimeSystem.MaxPlayerSlots;

    [Header("References")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private ViewportCanvasSystem viewportCanvasSystem;

    [FormerlySerializedAs("hudProfile")]
    [SerializeField] private PlayerActionBarProfile profile;

    [Header("Late Binding")]
    [Min(0.02f)]
    [SerializeField] private float bindingRetryInterval = 0.1f;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingIconAssignments = true;

    private readonly PlayerItemHand[] hands = new PlayerItemHand[MaxPlayerSlots];
    private readonly PlayerAbilityController[] abilities = new PlayerAbilityController[MaxPlayerSlots];
    private readonly PlayerActionBarView[] bars = new PlayerActionBarView[MaxPlayerSlots];
    private readonly bool[] abilityCharging = new bool[MaxPlayerSlots];
    private readonly bool[] missingPrefabLogged = new bool[MaxPlayerSlots];
    private readonly List<ItemDefinition> missingIconLogged = new List<ItemDefinition>(4);

    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private ViewportCanvasSystem subscribedViewportCanvasSystem;
    private Coroutine bindingRoutine;
    private bool missingProfileLogged;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        PlayerHudVisibility.Changed += HandleHudVisibilityChanged;
        BindEverything();
        StartBindingIfNeeded();
    }

    private void Start()
    {
        BindEverything();
        StartBindingIfNeeded();
    }

    private void OnDisable()
    {
        PlayerHudVisibility.Changed -= HandleHudVisibilityChanged;
        StopBinding();
        UnsubscribeFromSystems();
        UnbindAllPlayers();
        SetAllBarsActive(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null) Destroy(bars[i].gameObject);
        }
    }

    private void OnValidate()
    {
        bindingRetryInterval = Mathf.Max(0.02f, bindingRetryInterval);
    }

    private void Update()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (!abilityCharging[i]) continue;

            PlayerAbilityController ability = abilities[i];
            PlayerActionBarView bar = bars[i];
            if (ability == null || bar == null) continue;

            bar.SetAbilityCharge(ability.ChargeProgress01, ability.RemainingChargeSeconds);
        }
    }

    private void BindEverything()
    {
        ResolveReferences();
        EnsureSubscriptions();
        SynchronizePlayers();
        EnsureBars();
        RefreshAll(false);
    }

    private void HandlePlayerRegistered(int playerIndex, PlayerItemHand hand)
    {
        BindPlayer(playerIndex, hand);
        Refresh(playerIndex - 1, false);
    }

    private void HandlePlayerUnregistered(int playerIndex)
    {
        int slot = playerIndex - 1;
        if (slot < 0 || slot >= MaxPlayerSlots) return;

        UnbindPlayer(slot);
        Refresh(slot, false);
    }

    private void HandleMapItemChanged(PlayerItemHand hand, ItemDefinition item)
    {
        RefreshHand(hand, false);
    }

    private void HandleAbilityItemChanged(PlayerItemHand hand, ItemDefinition item)
    {
        RefreshHand(hand, item != null);
    }

    private void HandleEquippedSlotChanged(PlayerItemHand hand, HandSlotId slot)
    {
        RefreshHand(hand, false);
    }

    private void HandleAbilityChanged(PlayerAbilityController ability)
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (abilities[i] != ability) continue;

            Refresh(i, true);
            return;
        }
    }

    private void HandleViewportLayoutChanged()
    {
        EnsureBars();
        RefreshAll(false);
    }

    private void RefreshHand(PlayerItemHand hand, bool abilityBecameReady)
    {
        if (hand == null) return;

        int slot = hand.PlayerIndex - 1;
        if (slot < 0 || slot >= MaxPlayerSlots || hands[slot] != hand) return;

        Refresh(slot, true);

        PlayerActionBarView bar = bars[slot];

        if (abilityBecameReady && bar != null && bar.gameObject.activeInHierarchy)
        {
            bar.PlayAbilityReadyPop();
        }
    }

    private void HandleHudVisibilityChanged()
    {
        RefreshAll(false);
    }

    private void RefreshAll(bool animate)
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            Refresh(i, animate);
        }
    }

    private void Refresh(int slot, bool animate)
    {
        if (slot < 0 || slot >= MaxPlayerSlots) return;

        PlayerActionBarView bar = bars[slot];

        if (bar == null)
        {
            abilityCharging[slot] = false;
            return;
        }

        PlayerItemHand hand = hands[slot];
        PlayerAbilityController ability = abilities[slot];
        AbilitySampleDefinition sample = ability != null ? ability.Sample : null;
        ItemDefinition mapItem = hand != null && hand.MapSlot.IsLoaded ? hand.MapSlot.Item : null;

        bool visible =
            hand != null &&
            profile != null &&
            viewportCanvasSystem != null &&
            viewportCanvasSystem.IsPlayerViewActive(slot + 1) &&
            PlayerHudVisibility.IsShown(slot + 1) &&
            (mapItem != null || sample != null);

        bool wasVisible = bar.gameObject.activeSelf;

        abilityCharging[slot] =
            visible &&
            sample != null &&
            ability.State == PlayerAbilityController.ChargeState.Charging;

        if (wasVisible != visible) bar.gameObject.SetActive(visible);
        if (!visible) return;

        ApplyLayout(bar);

        bar.SetIcons(GetIcon(mapItem), sample != null ? GetIcon(sample.Item) : null);

        ItemDefinition equippedItem = hand.EquippedItem;

        for (int i = 0; i < PlayerActionBarProfile.PromptSlotCount; i++)
        {
            bar.SetPrompt(i, profile.GetPromptSprite(equippedItem, i));
        }

        ActionBarDisplay display = new ActionBarDisplay
        {
            HasPowerup = mapItem != null,
            HasAbility = sample != null,
            AbilityLoaded = hand.AbilitySlot.IsLoaded,
            AbilityCharging = abilityCharging[slot],
            AbilityTier = sample != null ? sample.Tier : AbilitySampleTier.Silver,
            Equipped = hand.EquippedSlotId
        };

        bar.Display(display, animate && wasVisible);

        if (abilityCharging[slot])
        {
            bar.SetAbilityCharge(ability.ChargeProgress01, ability.RemainingChargeSeconds);
        }
    }

    private void ApplyLayout(PlayerActionBarView bar)
    {
        profile.GetLayout(
            viewportCanvasSystem.ActivePlayerCount,
            out Vector2 anchor,
            out Vector2 offsetPixels,
            out float scale
        );

        RectTransform rect = bar.RectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = offsetPixels;
        rect.localScale = new Vector3(scale, scale, 1f);
    }

    private Sprite GetIcon(ItemDefinition item)
    {
        if (item == null) return null;

        Sprite icon = profile.GetIcon(item);

        if (icon == null && logMissingIconAssignments && !missingIconLogged.Contains(item))
        {
            missingIconLogged.Add(item);
            Debug.LogWarning(
                $"[UI] No action bar icon is assigned for {item.name} in {profile.name}.",
                this
            );
        }

        return icon;
    }

    private void EnsureBars()
    {
        if (viewportCanvasSystem == null) return;

        if (profile == null)
        {
            if (!missingProfileLogged)
            {
                missingProfileLogged = true;
                Debug.LogWarning("[UI] PlayerActionBarPresenter needs a PlayerActionBarProfile.", this);
            }

            return;
        }

        missingProfileLogged = false;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (bars[i] != null) continue;

            if (!viewportCanvasSystem.TryGetPlayerLayer(
                    i + 1,
                    ViewportCanvasSystem.PlayerLayer.Hud,
                    out RectTransform hudRoot
                ))
            {
                continue;
            }

            PlayerActionBarView prefab = profile.GetBarPrefab(i + 1);

            if (prefab == null)
            {
                if (!missingPrefabLogged[i])
                {
                    missingPrefabLogged[i] = true;
                    Debug.LogWarning($"[UI] {profile.name} has no action bar prefab for P{i + 1}.", this);
                }

                continue;
            }

            PlayerActionBarView bar = Instantiate(prefab, hudRoot, false);
            bar.name = $"P{i + 1} Action Bar";
            bar.gameObject.SetActive(false);
            bars[i] = bar;
        }
    }

    private void SynchronizePlayers()
    {
        if (runtimeSystem == null) return;

        for (int playerIndex = 1; playerIndex <= MaxPlayerSlots; playerIndex++)
        {
            if (runtimeSystem.TryGetPlayer(playerIndex, out PlayerItemHand hand))
            {
                BindPlayer(playerIndex, hand);
            }
            else
            {
                UnbindPlayer(playerIndex - 1);
            }
        }
    }

    private void BindPlayer(int playerIndex, PlayerItemHand hand)
    {
        int slot = playerIndex - 1;
        if (slot < 0 || slot >= MaxPlayerSlots || hand == null || hands[slot] == hand) return;

        UnbindPlayer(slot);

        hands[slot] = hand;
        hand.OnMapItemChanged += HandleMapItemChanged;
        hand.OnAbilityItemChanged += HandleAbilityItemChanged;
        hand.OnEquippedSlotChanged += HandleEquippedSlotChanged;

        PlayerAbilityController ability = hand.GetComponent<PlayerAbilityController>();
        abilities[slot] = ability;

        if (ability != null)
        {
            ability.OnSampleChanged += HandleAbilityChanged;
            ability.OnChargeStarted += HandleAbilityChanged;
            ability.OnChargeReady += HandleAbilityChanged;
        }
    }

    private void UnbindPlayer(int slot)
    {
        if (slot < 0 || slot >= MaxPlayerSlots) return;

        PlayerItemHand hand = hands[slot];

        if (hand != null)
        {
            hand.OnMapItemChanged -= HandleMapItemChanged;
            hand.OnAbilityItemChanged -= HandleAbilityItemChanged;
            hand.OnEquippedSlotChanged -= HandleEquippedSlotChanged;
        }

        PlayerAbilityController ability = abilities[slot];

        if (ability != null)
        {
            ability.OnSampleChanged -= HandleAbilityChanged;
            ability.OnChargeStarted -= HandleAbilityChanged;
            ability.OnChargeReady -= HandleAbilityChanged;
        }

        hands[slot] = null;
        abilities[slot] = null;
        abilityCharging[slot] = false;
    }

    private void UnbindAllPlayers()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            UnbindPlayer(i);
        }
    }

    private void SetAllBarsActive(bool active)
    {
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null) bars[i].gameObject.SetActive(active);
        }
    }

    private void ResolveReferences()
    {
        if (runtimeSystem == null) runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        if (viewportCanvasSystem == null) viewportCanvasSystem = FindFirstObjectByType<ViewportCanvasSystem>();
    }

    private void EnsureSubscriptions()
    {
        if (subscribedRuntimeSystem != runtimeSystem)
        {
            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnPlayerRegistered -= HandlePlayerRegistered;
                subscribedRuntimeSystem.OnPlayerUnregistered -= HandlePlayerUnregistered;
            }

            subscribedRuntimeSystem = runtimeSystem;

            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnPlayerRegistered += HandlePlayerRegistered;
                subscribedRuntimeSystem.OnPlayerUnregistered += HandlePlayerUnregistered;
            }
        }

        if (subscribedViewportCanvasSystem != viewportCanvasSystem)
        {
            if (subscribedViewportCanvasSystem != null)
            {
                subscribedViewportCanvasSystem.LayoutChanged -= HandleViewportLayoutChanged;
            }

            subscribedViewportCanvasSystem = viewportCanvasSystem;

            if (subscribedViewportCanvasSystem != null)
            {
                subscribedViewportCanvasSystem.LayoutChanged += HandleViewportLayoutChanged;
            }
        }
    }

    private void UnsubscribeFromSystems()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnPlayerRegistered -= HandlePlayerRegistered;
            subscribedRuntimeSystem.OnPlayerUnregistered -= HandlePlayerUnregistered;
        }

        if (subscribedViewportCanvasSystem != null)
        {
            subscribedViewportCanvasSystem.LayoutChanged -= HandleViewportLayoutChanged;
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
        while (isActiveAndEnabled && (runtimeSystem == null || viewportCanvasSystem == null))
        {
            BindEverything();

            if (runtimeSystem != null && viewportCanvasSystem != null) break;

            yield return new WaitForSecondsRealtime(bindingRetryInterval);
        }

        bindingRoutine = null;
    }
}
