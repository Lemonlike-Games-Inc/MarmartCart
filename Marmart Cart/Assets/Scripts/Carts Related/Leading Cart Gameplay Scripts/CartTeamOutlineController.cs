using UnityEngine;

/// <summary>
/// Team-colored wrapper around the QuickOutline plugin.
///
/// This script does not modify QuickOutline.
/// It only assigns team settings and enables/disables existing Outline components.
///
/// Loose follower prefab:
/// - Startup Player Id = 0
/// - ChainedCartManager calls SetTeam(...) when collected.
///
/// Leading cart P1-P4 variants:
/// - Startup Player Id can optionally be set to 1, 2, 3, or 4.
/// - This is useful when the controller lives on an untagged visual child.
/// </summary>
[DisallowMultipleComponent]
public class CartTeamOutlineController : MonoBehaviour
{
    private const int MaxSupportedPlayers = 4;

    #region References

    [Header("QuickOutline Targets")]
    [Tooltip("QuickOutline components controlled by this cart. If empty, cached once from children in Awake.")]
    [SerializeField] private Outline[] outlineTargets;

    #endregion

    #region Startup

    [Header("Startup Team")]
    [Tooltip("0 = do nothing on Start. 1-4 = immediately apply that player's outline. Useful for P1-P4 leading cart variants.")]
    [Range(0, MaxSupportedPlayers)]
    [SerializeField] private int startupPlayerId;

    #endregion

    #region Outline Settings

    [Header("Outline Settings")]
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineVisible;

    [Range(0f, 10f)]
    [SerializeField] private float outlineWidth = 2f;

    [Header("Team Outline Colors")]
    [Tooltip("P1, P2, P3, P4. These may be brighter than the actual plastic team colors for readability.")]
    [SerializeField]
    private Color[] teamOutlineColors = new Color[MaxSupportedPlayers]
    {
        new Color(0.15f, 0.45f, 1f, 1f),
        new Color(1f, 0.18f, 0.18f, 1f),
        new Color(0.15f, 0.9f, 0.25f, 1f),
        new Color(1f, 0.85f, 0.1f, 1f)
    };

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private int currentPlayerId;
    [SerializeField] private bool outlineActive;

    public int CurrentPlayerId => currentPlayerId;
    public bool OutlineActive => outlineActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheOutlineTargets();
        SetOutlineComponentsEnabled(false);
    }

    private void Start()
    {
        // IMPORTANT:
        // Do nothing when Startup Player Id is 0.
        // A newly spawned follower may already have been assigned by
        // ChainedCartManager between Awake() and Start().
        if (startupPlayerId > 0) SetTeam(startupPlayerId);
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Clamp(outlineWidth, 0f, 10f);

        if (!Application.isPlaying || !outlineActive || currentPlayerId <= 0) return;

        ApplyCurrentSettings();
    }

    #endregion

    #region Team State

    /// <summary>
    /// Assigns a player team. Player IDs are 1-4.
    /// </summary>
    public void SetTeam(int playerId)
    {
        int colorIndex = playerId - 1;

        if (colorIndex < 0 || teamOutlineColors == null || colorIndex >= teamOutlineColors.Length)
        {
            ClearTeam();
            return;
        }

        currentPlayerId = playerId;
        outlineActive = true;

        CacheOutlineTargets();
        ApplyCurrentSettings();

        // Re-enable instead of only setting enabled=true.
        // QuickOutline's OnEnable is what appends its two outline materials.
        // This makes one-time collection/team changes robust if another script
        // previously replaced the renderer's material array.
        RefreshOutlineComponents();
    }

    public void ClearTeam()
    {
        currentPlayerId = 0;
        outlineActive = false;
        SetOutlineComponentsEnabled(false);
    }

    #endregion

    #region Runtime Tuning API

    public void SetOutlineWidth(float width)
    {
        outlineWidth = Mathf.Clamp(width, 0f, 10f);

        if (outlineActive) ApplyCurrentSettings();
    }

    public void SetOutlineMode(Outline.Mode mode)
    {
        outlineMode = mode;

        if (outlineActive) ApplyCurrentSettings();
    }

    public void SetTeamOutlineColor(int playerId, Color color)
    {
        int colorIndex = playerId - 1;

        if (teamOutlineColors == null || colorIndex < 0 || colorIndex >= teamOutlineColors.Length) return;

        teamOutlineColors[colorIndex] = color;

        if (outlineActive && currentPlayerId == playerId) ApplyCurrentSettings();
    }

    public void ApplyCurrentSettings()
    {
        if (!outlineActive || currentPlayerId <= 0) return;

        int colorIndex = currentPlayerId - 1;

        if (teamOutlineColors == null || colorIndex < 0 || colorIndex >= teamOutlineColors.Length) return;

        CacheOutlineTargets();

        Color outlineColor = teamOutlineColors[colorIndex];

        for (int i = 0; i < outlineTargets.Length; i++)
        {
            Outline outline = outlineTargets[i];
            if (outline == null) continue;

            outline.OutlineMode = outlineMode;
            outline.OutlineColor = outlineColor;
            outline.OutlineWidth = outlineWidth;
        }
    }

    /// <summary>
    /// Re-runs QuickOutline OnDisable/OnEnable once so its outline material slots
    /// are guaranteed to be appended again. Intended for state/team changes,
    /// not per-frame use.
    /// </summary>
    public void RefreshOutlineComponents()
    {
        CacheOutlineTargets();

        if (outlineTargets == null) return;

        for (int i = 0; i < outlineTargets.Length; i++)
        {
            Outline outline = outlineTargets[i];
            if (outline == null) continue;

            if (outline.enabled) outline.enabled = false;
            outline.enabled = true;
        }
    }

    #endregion

    #region Inspector Testing

    [ContextMenu("Preview Player 1 Outline")]
    private void PreviewPlayer1() => SetTeam(1);

    [ContextMenu("Preview Player 2 Outline")]
    private void PreviewPlayer2() => SetTeam(2);

    [ContextMenu("Preview Player 3 Outline")]
    private void PreviewPlayer3() => SetTeam(3);

    [ContextMenu("Preview Player 4 Outline")]
    private void PreviewPlayer4() => SetTeam(4);

    [ContextMenu("Refresh Active Outline")]
    private void RefreshActiveOutline()
    {
        if (!outlineActive) return;

        ApplyCurrentSettings();
        RefreshOutlineComponents();
    }

    [ContextMenu("Clear Outline")]
    private void ClearOutline() => ClearTeam();

    #endregion

    #region Helpers

    private void CacheOutlineTargets()
    {
        if (outlineTargets != null && outlineTargets.Length > 0) return;

        outlineTargets = GetComponentsInChildren<Outline>(true);
    }

    private void SetOutlineComponentsEnabled(bool enabledState)
    {
        CacheOutlineTargets();

        if (outlineTargets == null) return;

        for (int i = 0; i < outlineTargets.Length; i++)
        {
            if (outlineTargets[i] != null) outlineTargets[i].enabled = enabledState;
        }
    }

    #endregion
}
