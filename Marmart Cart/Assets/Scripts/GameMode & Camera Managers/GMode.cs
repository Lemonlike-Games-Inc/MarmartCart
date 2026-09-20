using System;
using UnityEngine;

public enum GameMode
{
    duel2P,
    freeForAll4P,
    teamBattle4P
}

/// <summary>
/// Persistent match-session mode state.
///
/// Lifetime rule:
/// - A tutorial/gameplay scene may create GMode.
/// - The first GMode becomes the singleton and survives scene loads.
/// - It persists through Tutorial -> Gameplay -> Results/etc.
/// - Returning to Main Menu ends the match session and explicitly destroys it.
///
/// Main Menu itself does not need a GMode.
/// </summary>
[DisallowMultipleComponent]
public class GMode : MonoBehaviour
{
    public static GMode Instance { get; private set; }

    [Header("Current Match Mode")]
    public GameMode CurrentMode = GameMode.duel2P;

    public event Action<GameMode> OnModeChanged;

    public bool IsTwoPlayer => CurrentMode == GameMode.duel2P;
    public bool IsFreeForAll => CurrentMode == GameMode.freeForAll4P;
    public bool IsTeamBattle => CurrentMode == GameMode.teamBattle4P;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public int PlayerCount()
    {
        return CurrentMode switch
        {
            GameMode.duel2P => 2,
            GameMode.freeForAll4P => 4,
            GameMode.teamBattle4P => 4,
            _ => 0
        };
    }

    public void SetMode(GameMode newMode)
    {
        if (CurrentMode == newMode) return;

        CurrentMode = newMode;
        OnModeChanged?.Invoke(CurrentMode);
    }

    /// <summary>
    /// Ends the current persistent match session.
    ///
    /// Instance is cleared immediately because Destroy itself happens
    /// at the end of the frame.
    /// </summary>
    public static void DestroyPersistentInstance()
    {
        if (Instance == null) return;

        GMode oldInstance = Instance;

        Instance = null;

        if (oldInstance != null)
        {
            Destroy(oldInstance.gameObject);
        }
    }
}