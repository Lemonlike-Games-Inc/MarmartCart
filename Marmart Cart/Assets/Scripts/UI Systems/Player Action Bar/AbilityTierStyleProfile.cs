using UnityEngine;

/// <summary>
/// Card colours and motion of each Ability Sample tier, shared by every card
/// that shows a Sample (the Player Action Bar and the Ability pick), so they
/// always match. Style only; nothing writes here at runtime.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/UI/Ability Tier Styles",
    fileName = "Ability Tier Styles"
)]
public sealed class AbilityTierStyleProfile : ScriptableObject
{
    [SerializeField]
    private ActionBarCardStyle silver = new ActionBarCardStyle(
        new Color(0.78f, 0.81f, 0.86f), new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    [SerializeField]
    private ActionBarCardStyle gold = new ActionBarCardStyle(
        new Color(1f, 0.78f, 0.2f), new Color(0.44f, 0.3f, 0.08f), Color.clear, ActionBarCardMotion.Shine);

    [SerializeField]
    private ActionBarCardStyle prismatic = new ActionBarCardStyle(
        new Color(0.67f, 0.41f, 0.74f), new Color(0.24f, 0.19f, 0.38f), Color.clear, ActionBarCardMotion.Prismatic);

    [SerializeField]
    private ActionBarCardStyle unique = new ActionBarCardStyle(
        new Color(0.74f, 0.04f, 0.12f), new Color(0.24f, 0.03f, 0.05f), new Color(0.88f, 0.7f, 0.33f), ActionBarCardMotion.Pulse);

    public ActionBarCardStyle Get(AbilitySampleTier tier)
    {
        return tier switch
        {
            AbilitySampleTier.Gold => gold,
            AbilitySampleTier.Prismatic => prismatic,
            AbilitySampleTier.Unique => unique,
            _ => silver
        };
    }
}
