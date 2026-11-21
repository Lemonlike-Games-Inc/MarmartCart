using UnityEngine;

/// <summary>Used the moment LT is pressed, with no aim (Cola).</summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Uses/Instant",
    fileName = "Instant Use"
)]
public sealed class InstantItemUse : ItemUseDefinition
{
    public override bool RequiresAim => false;
}
