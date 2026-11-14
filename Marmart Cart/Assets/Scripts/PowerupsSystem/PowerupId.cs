/// <summary>
/// Stable identities for the complete planned Marmart Carts power-up roster.
/// There are deliberately no rarity/tier variants in the replacement system.
/// </summary>
public enum PowerupId
{
    Tomato = 0,
    IceCube = 1,
    ColaMentos = 2,
    StrongFan = 3
}

/// <summary>
/// Small explicit design rules for the fixed four-power-up scope.
/// Keep effect implementation out of this class.
/// </summary>
public static class PowerupIdRules
{
    public static bool IsDefined(PowerupId powerupId)
    {
        return powerupId == PowerupId.Tomato ||
               powerupId == PowerupId.IceCube ||
               powerupId == PowerupId.ColaMentos ||
               powerupId == PowerupId.StrongFan;
    }

    /// <summary>
    /// Whether the STORED item needs projectile targeting before its initial
    /// activation. Strong Fan is activated immediately and will read Aim only
    /// during its future active-duration state.
    /// </summary>
    public static bool RequiresProjectileAim(PowerupId powerupId)
    {
        return powerupId == PowerupId.Tomato ||
               powerupId == PowerupId.IceCube;
    }
}
