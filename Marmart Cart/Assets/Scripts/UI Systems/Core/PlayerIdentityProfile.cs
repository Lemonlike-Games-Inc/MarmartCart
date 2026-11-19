using UnityEngine;

/// <summary>
/// Each player's visual identity, shared by every UI that shows a player:
/// colour and short label. See Docs/Design/ui-architecture.md.
/// Player indices are 1-based.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/Player Identity Profile",
    fileName = "PlayerIdentityProfile"
)]
public sealed class PlayerIdentityProfile : ScriptableObject
{
    public const int MaxPlayers = 4;

    [SerializeField] private Color[] playerColors =
    {
        new Color(0.22f, 0.62f, 1f, 1f),
        new Color(1f, 0.27f, 0.22f, 1f),
        new Color(0.28f, 0.90f, 0.43f, 1f),
        new Color(1f, 0.72f, 0.16f, 1f)
    };

    [SerializeField] private string[] playerLabels = { "P1", "P2", "P3", "P4" };

    public Color GetColor(int playerIndex)
    {
        int slot = playerIndex - 1;
        return slot >= 0 && slot < playerColors.Length ? playerColors[slot] : Color.white;
    }

    public string GetLabel(int playerIndex)
    {
        int slot = playerIndex - 1;
        return slot >= 0 && slot < playerLabels.Length ? playerLabels[slot] : string.Empty;
    }

    private void OnValidate()
    {
        if (playerColors == null || playerColors.Length != MaxPlayers)
            System.Array.Resize(ref playerColors, MaxPlayers);

        if (playerLabels == null || playerLabels.Length != MaxPlayers)
            System.Array.Resize(ref playerLabels, MaxPlayers);
    }
}
