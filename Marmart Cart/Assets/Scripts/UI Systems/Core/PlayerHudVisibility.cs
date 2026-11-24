using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-local answer to "is this player's HUD shown?", for the player HUD:
/// the Shapes drawers (timer, leaderboard, hype ring, cargo bars, pointers,
/// markers, session guide, arena arrows) and the Player Action Bar. Other
/// systems hide it per player through add/remove handles.
///
/// Hide During Pre-Round: in a scene with Rounds (a MetaRoundController), the
/// HUD stays hidden from load until the match starts ("GO"). Scenes without
/// Rounds (tutorial) are never hidden by it. Without this component the HUD
/// always shows.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHudVisibility : MonoBehaviour
{
    public const int MaxPlayers = PlayerViewService.MaxViews;

    /// <summary>Player mask for every player.</summary>
    public const int AllPlayers = (1 << MaxPlayers) - 1;

    [Tooltip("Hide the HUD from scene load until the match starts, in scenes with Rounds.")]
    [SerializeField] private bool hideDuringPreRound = true;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [Header("Runtime - Read Only")]
    [Tooltip("Bit 0 = P1. A set bit hides that player's HUD.")]
    [SerializeField] private int hiddenMask;

    private struct HideRequest
    {
        public int Handle;
        public int PlayerMask;
    }

    private readonly List<HideRequest> requests = new List<HideRequest>(4);
    private int lastHandle;
    private int preRoundHandle;

    public static PlayerHudVisibility Instance { get; private set; }

    /// <summary>Some player's HUD was hidden or shown again.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Changed = null;
    }

    /// <summary>True when this player's HUD should draw. Always true without an instance.</summary>
    public static bool IsShown(int playerIndex)
    {
        PlayerHudVisibility instance = Instance;
        if (instance == null || playerIndex < 1 || playerIndex > MaxPlayers) return true;

        return (instance.hiddenMask & (1 << (playerIndex - 1))) == 0;
    }

    /// <summary>IsShown for the player whose view this camera draws. True for other cameras.</summary>
    public static bool IsShownForCamera(Camera cam)
    {
        if (Instance == null || !HudShapes.TryGetViewPlayer(cam, out int playerIndex)) return true;
        return IsShown(playerIndex);
    }

    public static int MaskFor(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= MaxPlayers ? 1 << (playerIndex - 1) : 0;
    }

    /// <summary>Hides the HUD of every player in the mask until removed. Returns a handle; 0 means nothing was added.</summary>
    public int AddHide(int playerMask)
    {
        playerMask &= AllPlayers;
        if (playerMask == 0) return 0;

        lastHandle++;
        if (lastHandle <= 0) lastHandle = 1;

        requests.Add(new HideRequest { Handle = lastHandle, PlayerMask = playerMask });
        Recalculate();
        return lastHandle;
    }

    public void RemoveHide(int handle)
    {
        if (handle == 0) return;

        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (requests[i].Handle != handle) continue;

            requests.RemoveAt(i);
            Recalculate();
            return;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[UI] More than one PlayerHudVisibility in the scene; the newest one is used.", this);

        Instance = this;

        if (gameTimeManager == null) gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        if (hideDuringPreRound && gameTimeManager != null && FindFirstObjectByType<MetaRoundController>() != null)
        {
            preRoundHandle = AddHide(AllPlayers);
            Debug.Log("[UI] Player HUD hidden for the pre-round.", this);
        }
    }

    private void OnEnable()
    {
        if (gameTimeManager != null) gameTimeManager.OnMatchStarted += HandleMatchStarted;
    }

    private void OnDisable()
    {
        if (gameTimeManager != null) gameTimeManager.OnMatchStarted -= HandleMatchStarted;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HandleMatchStarted()
    {
        if (preRoundHandle == 0) return;

        RemoveHide(preRoundHandle);
        preRoundHandle = 0;
        Debug.Log("[UI] Player HUD shown: match started.", this);
    }

    private void Recalculate()
    {
        int mask = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            mask |= requests[i].PlayerMask;
        }

        if (mask == hiddenMask) return;

        hiddenMask = mask;
        Changed?.Invoke();
    }
}
