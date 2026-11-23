using System;
using UnityEngine;

/// <summary>
/// Per-player Ability: holds this Round's Sample and runs its charge. When the
/// charge is ready, its item goes into the hand's ability slot; the cooldown
/// starts when that item is used. The charge counts game time only while the
/// match is playing. An Upgrade Sample's passive is applied to the hand while
/// the Sample is held. Runtime state lives here, never on the Sample asset.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAbilityController : MonoBehaviour
{
    public enum ChargeState
    {
        NoSample,
        Charging,
        Ready
    }

    [Header("References")]
    [SerializeField] private PlayerItemHand hand;

    [Header("Debug")]
    [Tooltip(
        "Sample given when the scene loads. A pick before the Round (from Round 2) " +
        "replaces it. Clear it before the booth build."
    )]
    [SerializeField] private AbilitySampleDefinition debugStartingSample;

    [SerializeField] private bool logStateChanges = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private AbilitySampleDefinition sample;
    [SerializeField] private ChargeState state;
    [SerializeField] private float remainingChargeSeconds;
    [SerializeField] private float chargeDurationSeconds;

    private PowerupRuntimeSystem runtimeSystem;
    private PlayerItemHand subscribedHand;
    private PlayerItemHand passiveHand;
    private int passiveHandle;

    public AbilitySampleDefinition Sample => sample;
    public ChargeState State => state;
    public float RemainingChargeSeconds => remainingChargeSeconds;

    /// <summary>0 when a charge starts, 1 when it is ready.</summary>
    public float ChargeProgress01 =>
        state == ChargeState.Ready ? 1f
        : state == ChargeState.Charging && chargeDurationSeconds > 0f
            ? 1f - Mathf.Clamp01(remainingChargeSeconds / chargeDurationSeconds)
            : 0f;

    public event Action<PlayerAbilityController> OnSampleChanged;
    public event Action<PlayerAbilityController> OnChargeReady;
    public event Action<PlayerAbilityController> OnChargeStarted;

    private void Reset()
    {
        hand = GetComponent<PlayerItemHand>();
    }

    private void Awake()
    {
        if (hand == null) hand = GetComponent<PlayerItemHand>();
    }

    private void OnEnable()
    {
        SubscribeHand();
        if (sample != null) ApplyPassive();
    }

    private void Start()
    {
        runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();

        if (sample == null && debugStartingSample != null)
        {
            SetSample(debugStartingSample);
        }
    }

    private void OnDisable()
    {
        RemovePassive();
        UnsubscribeHand();
    }

    private void Update()
    {
        if (state != ChargeState.Charging) return;

        // Without a runtime system (isolated test scene) the charge always counts.
        if (runtimeSystem != null && !runtimeSystem.MatchPlaying) return;

        remainingChargeSeconds -= Time.deltaTime;
        if (remainingChargeSeconds > 0f) return;

        remainingChargeSeconds = 0f;
        state = ChargeState.Ready;

        if (hand == null || !hand.TryLoadAbilityItem(sample.Item))
        {
            Debug.LogError($"[Ability] P{PlayerIndex} could not load {sample.Item.name} into the hand.", this);
            return;
        }

        if (logStateChanges)
        {
            Debug.Log($"[Ability] P{PlayerIndex} {sample.DisplayName} charge ready ({sample.Item.name}).", this);
        }

        OnChargeReady?.Invoke(this);
    }

    /// <summary>
    /// Gives this player a Sample for the Round, or none. Replaces any earlier
    /// Sample and its unused charge; the first charge starts again.
    /// </summary>
    public void SetSample(AbilitySampleDefinition newSample)
    {
        RemovePassive();
        if (hand != null) hand.ClearAbilityItem();

        sample = newSample;

        if (sample == null || sample.Item == null)
        {
            if (sample != null)
            {
                Debug.LogError($"[Ability] Sample {sample.name} has no item.", this);
            }

            sample = null;
            state = ChargeState.NoSample;
            remainingChargeSeconds = 0f;
            chargeDurationSeconds = 0f;
        }
        else
        {
            if (logStateChanges)
            {
                Debug.Log($"[Ability] P{PlayerIndex} Sample set: {sample.DisplayName}.", this);
            }

            ApplyPassive();
            StartCharge(sample.FirstChargeSeconds);
        }

        OnSampleChanged?.Invoke(this);
    }

    private void StartCharge(float seconds)
    {
        state = ChargeState.Charging;
        chargeDurationSeconds = Mathf.Max(0f, seconds);
        remainingChargeSeconds = chargeDurationSeconds;

        if (logStateChanges)
        {
            Debug.Log($"[Ability] P{PlayerIndex} {sample.DisplayName} charging: {chargeDurationSeconds:0.#} s.", this);
        }

        OnChargeStarted?.Invoke(this);
    }

    private void HandleAbilityItemUsed(PlayerItemHand usedHand, ItemDefinition item)
    {
        if (usedHand != hand || state != ChargeState.Ready || sample == null) return;

        if (logStateChanges)
        {
            Debug.Log($"[Ability] P{PlayerIndex} used {item.name}; cooldown starts.", this);
        }

        StartCharge(sample.CooldownSeconds);
    }

    // Passive: this player's map pickups of the Sample's base item give the
    // Sample's item instead, for as long as the Sample is held.
    private void ApplyPassive()
    {
        RemovePassive();

        if (hand == null || sample == null || sample.UpgradesMapItem == null) return;

        passiveHandle = hand.AddMapItemReplacement(sample.UpgradesMapItem, sample.Item);
        if (passiveHandle == 0) return;

        passiveHand = hand;

        if (logStateChanges)
        {
            Debug.Log(
                $"[Ability] P{PlayerIndex} passive on: {sample.UpgradesMapItem.name} " +
                $"pickups give {sample.Item.name}.",
                this
            );
        }
    }

    private void RemovePassive()
    {
        if (passiveHand != null) passiveHand.RemoveMapItemReplacement(passiveHandle);

        passiveHand = null;
        passiveHandle = 0;
    }

    private int PlayerIndex => hand != null ? hand.PlayerIndex : 0;

    private void SubscribeHand()
    {
        if (hand == null || subscribedHand == hand) return;

        UnsubscribeHand();
        subscribedHand = hand;
        subscribedHand.OnAbilityItemUsed += HandleAbilityItemUsed;
    }

    private void UnsubscribeHand()
    {
        if (subscribedHand == null) return;

        subscribedHand.OnAbilityItemUsed -= HandleAbilityItemUsed;
        subscribedHand = null;
    }
}
