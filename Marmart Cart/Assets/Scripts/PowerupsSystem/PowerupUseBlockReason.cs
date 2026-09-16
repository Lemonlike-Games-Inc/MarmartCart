using System;

/// <summary>
/// Independent reasons that prevent a stored power-up from being aimed/used.
/// Removing one reason must never clear another active restriction.
///
/// Stall is intentionally absent: stalled players are allowed to aim and use
/// power-ups while Drift and normal Hype Speed Up remain restricted elsewhere.
/// </summary>
[Flags]
public enum PowerupUseBlockReason
{
    None = 0,
    MatchInactive = 1 << 0,
    Checkout = 1 << 1,
    Frozen = 1 << 2,
    ActiveEffectLock = 1 << 3,
    ExternallyDisabled = 1 << 4
}
