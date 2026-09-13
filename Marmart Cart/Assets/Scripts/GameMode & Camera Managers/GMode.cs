using System;
using UnityEngine;

public enum GameMode
{
    duel2P,
    freeForAll4P,
    teamBattle4P
}

/// <summary>
/// Persistent source of truth for the currently selected game mode.
///
/// Normal game flow should call SetMode() before loading the gameplay scene.
/// For Play Mode testing, changing CurrentMode directly in the Inspector also
/// raises OnModeChanged so scene systems can reconfigure live.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GMode : MonoBehaviour
{
    public static GMode Instance { get; private set; }

    [Header("Current Match Mode")]
    [Tooltip(
        "Authoritative match mode. Normal flow should use SetMode(). " +
        "While testing in Play Mode, changing this Inspector value also notifies listeners."
    )]
    public GameMode CurrentMode = GameMode.duel2P;

    public event Action<GameMode> OnModeChanged;

    public bool IsTwoPlayer => CurrentMode == GameMode.duel2P;
    public bool IsFreeForAll => CurrentMode == GameMode.freeForAll4P;
    public bool IsTeamBattle => CurrentMode == GameMode.teamBattle4P;

    private GameMode lastNotifiedMode;
    private bool hasLastNotifiedMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // The scene controller will read the initial value during its own Awake.
        // This cache is only for detecting later runtime changes.
        lastNotifiedMode = CurrentMode;
        hasLastNotifiedMode = true;
    }

    /// <summary>
    /// Preferred runtime API for changing the authoritative game mode.
    /// </summary>
    public void SetMode(GameMode mode)
    {
        CurrentMode = mode;
        NotifyModeChangedIfNeeded();
    }

    /// <summary>
    /// Compatibility API used by existing gameplay code.
    /// </summary>
    public int PlayerCount()
    {
        return CurrentMode switch
        {
            GameMode.duel2P => 2,
            GameMode.freeForAll4P => 4,
            GameMode.teamBattle4P => 4,
            _ => 2
        };
    }

    private void NotifyModeChangedIfNeeded()
    {
        if (!hasLastNotifiedMode)
        {
            lastNotifiedMode = CurrentMode;
            hasLastNotifiedMode = true;
            return;
        }

        if (lastNotifiedMode == CurrentMode) return;

        lastNotifiedMode = CurrentMode;
        OnModeChanged?.Invoke(CurrentMode);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspector edits do not go through SetMode(), so during Play Mode we
    /// detect the serialized field change here and notify on the editor's next
    /// safe callback. This is editor/testing support only; builds use SetMode().
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= HandleInspectorModeChangedDelayed;
        UnityEditor.EditorApplication.delayCall += HandleInspectorModeChangedDelayed;
    }

    private void HandleInspectorModeChangedDelayed()
    {
        if (this == null) return;
        if (!Application.isPlaying) return;
        if (Instance != this) return;

        NotifyModeChangedIfNeeded();
    }
#endif
}
