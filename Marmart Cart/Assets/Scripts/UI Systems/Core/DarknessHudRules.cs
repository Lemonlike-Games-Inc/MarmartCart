using System;
using UnityEngine;

/// <summary>HUD parts whose look in the dark (PlayerViewDarkness) each darkness request sets.</summary>
public enum DarknessHudElement
{
    RivalPointers,
    TimerAndLeaderboard,
    PlayerRings,
    ActionBar,
    PolicyBanners
}

/// <summary>How a HUD part looks while its viewer is in the dark.</summary>
public enum DarknessHudMode
{
    /// <summary>Under the darkness, like the world: visible only inside the lit circle.</summary>
    Covered = 0,

    /// <summary>Gone while dark, also inside the lit circle.</summary>
    Hidden = 1,

    /// <summary>Drawn on top of the darkness.</summary>
    OnTop = 2
}

/// <summary>
/// Per-request HUD settings for PlayerViewDarkness. When two requests darken the
/// same player, the stricter setting wins per part: Hidden, then Covered, then On Top.
/// Canvas parts (action bar, Policy banners) always draw above the darkness, so for
/// them Covered and Hidden both fade them out; they sit outside the lit circle anyway.
/// </summary>
[Serializable]
public sealed class DarknessHudRules
{
    public static readonly DarknessHudRules Default = new DarknessHudRules();

    [Tooltip("The arrows around the player's own cart that point at rivals.")]
    public DarknessHudMode rivalPointers = DarknessHudMode.Hidden;

    [Tooltip("The timer and the live leaderboard in the view's corner. They draw together.")]
    public DarknessHudMode timerAndLeaderboard = DarknessHudMode.Covered;

    [Tooltip("The hype and cargo rings around the player's own cart, with their warning icons and prompts.")]
    public DarknessHudMode playerRings = DarknessHudMode.Covered;

    [Tooltip("Canvas UI. Covered and Hidden both fade it out.")]
    public DarknessHudMode actionBar = DarknessHudMode.Covered;

    [Tooltip("Canvas UI. Covered and Hidden both fade them out.")]
    public DarknessHudMode policyBanners = DarknessHudMode.Covered;

    public DarknessHudMode Get(DarknessHudElement element)
    {
        switch (element)
        {
            case DarknessHudElement.RivalPointers: return rivalPointers;
            case DarknessHudElement.TimerAndLeaderboard: return timerAndLeaderboard;
            case DarknessHudElement.PlayerRings: return playerRings;
            case DarknessHudElement.ActionBar: return actionBar;
            case DarknessHudElement.PolicyBanners: return policyBanners;
            default: return DarknessHudMode.Covered;
        }
    }

    /// <summary>The stricter of two settings: Hidden, then Covered, then On Top.</summary>
    public static DarknessHudMode Stricter(DarknessHudMode a, DarknessHudMode b)
    {
        return Rank(a) >= Rank(b) ? a : b;
    }

    private static int Rank(DarknessHudMode mode)
    {
        switch (mode)
        {
            case DarknessHudMode.Hidden: return 2;
            case DarknessHudMode.Covered: return 1;
            default: return 0;
        }
    }
}
