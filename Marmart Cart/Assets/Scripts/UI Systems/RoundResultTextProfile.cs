using UnityEngine;

/// <summary>
/// Every text of the Round and Match result banner (RoundResultView). Both
/// gameplay scenes use one asset, so a line edited here changes everywhere.
///
/// UniText's global preset handles &lt;b&gt;, &lt;i&gt;, &lt;color=#RRGGBB&gt; and
/// &lt;size=55%&gt; tags. A line break starts a second, usually smaller, line.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Meta/Round Result Text Profile",
    fileName = "RoundResultTextProfile"
)]
public sealed class RoundResultTextProfile : ScriptableObject
{
    [Header("Round Result (after each Round)")]
    [Tooltip("This view's player won alone. {0} = Round number.")]
    [SerializeField] private string youWinRound = "YOU WIN ROUND {0}!";

    [Tooltip("This view's player tied for the win. {0} = Round number.")]
    [SerializeField] private string youShareRound = "YOU SHARE ROUND {0}!";

    [Tooltip("One other player won. {0} = Round number, {1} = the winner.")]
    [SerializeField] private string otherWinsRound = "{1} WINS ROUND {0}";

    [Tooltip("Several other players tied for the win. {0} = Round number, {1} = the winners.")]
    [SerializeField] private string othersWinRound = "{1} WIN ROUND {0}";

    [Tooltip("Nobody banked anything. {0} = Round number.")]
    [TextArea(2, 4)]
    [SerializeField] private string noRoundWinner = "NO WINNER IN ROUND {0}\n<size=55%>Nobody made it through checkout!</size>";

    [Tooltip("Joins winner names, for example \"P1 & P3\".")]
    [SerializeField] private string winnerJoiner = " & ";

    [Header("Match Result (after Round 3, one line per rank)")]
    [Tooltip("Tied players share a rank (1, 1, 2), like the results podium.")]
    [TextArea(2, 4)]
    [SerializeField] private string winnerLine = "WINNER!\n<size=55%>Top cart of the whole Hypermarket!</size>";

    [Tooltip("1st place shared with other players.")]
    [TextArea(2, 4)]
    [SerializeField] private string tiedWinnerLine = "TIED FOR 1ST!\n<size=55%>Split the receipt, champs!</size>";

    [TextArea(2, 4)]
    [SerializeField] private string secondPlaceLine = "2ND PLACE!\n<size=55%>So close! One more checkout and it was yours.</size>";

    [TextArea(2, 4)]
    [SerializeField] private string thirdPlaceLine = "3RD PLACE!\n<size=55%>Solid haul. The wobbly wheel held on!</size>";

    [TextArea(2, 4)]
    [SerializeField] private string fourthPlaceLine = "4TH PLACE!\n<size=55%>Cleanup on aisle you. Rematch?</size>";

    [Tooltip("Nobody scored a single Match Point.")]
    [TextArea(2, 4)]
    [SerializeField] private string noMatchWinnerLine = "NO WINNER!\n<size=55%>The shelves win this time.</size>";

    [Header("Match Points Line")]
    [Tooltip("Under the Round result. {0} = the player entries.")]
    [SerializeField] private string roundPointsFormat = "MATCH POINTS   {0}";

    [Tooltip("Under the Match result. {0} = the player entries.")]
    [SerializeField] private string finalPointsFormat = "FINAL MATCH POINTS   {0}";

    [Tooltip("One player's entry. {0} = player label, {1} = Match Points.")]
    [SerializeField] private string pointsEntry = "{0} {1}";

    [SerializeField] private string pointsSeparator = "     ";

    [Header("Player Labels")]
    [Tooltip("Label for the view's own player. Other players use PlayerIdentityProfile labels (P1-P4).")]
    [SerializeField] private string youLabel = "YOU";

    [Tooltip("Colour player labels and winner names with the PlayerIdentityProfile colours.")]
    [SerializeField] private bool colorPlayerLabels = true;

    public string YouWinRound => youWinRound;
    public string YouShareRound => youShareRound;
    public string OtherWinsRound => otherWinsRound;
    public string OthersWinRound => othersWinRound;
    public string NoRoundWinner => noRoundWinner;
    public string WinnerJoiner => winnerJoiner;

    public string NoMatchWinnerLine => noMatchWinnerLine;

    public string RoundPointsFormat => roundPointsFormat;
    public string FinalPointsFormat => finalPointsFormat;
    public string PointsEntry => pointsEntry;
    public string PointsSeparator => pointsSeparator;

    public string YouLabel => youLabel;
    public bool ColorPlayerLabels => colorPlayerLabels;

    /// <summary>The Match line for a dense rank (1 = best). Ranks past 4 use the 4th line.</summary>
    public string GetRankLine(int rank, bool sharedFirst)
    {
        switch (rank)
        {
            case 1: return sharedFirst ? tiedWinnerLine : winnerLine;
            case 2: return secondPlaceLine;
            case 3: return thirdPlaceLine;
            default: return fourthPlaceLine;
        }
    }
}
