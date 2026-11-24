using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a Round's Policies come from. The fixed Policy Schedule is one source;
/// a later random Policy pool (tier weights per Round, like the Ability Sample
/// Pool) is another. MarmartPolicyDirector only asks for the list.
/// Definition only: no runtime state on the asset.
/// </summary>
public abstract class PolicyRoundSource : ScriptableObject
{
    /// <summary>Adds this Round's Policies to <paramref name="results"/> (cleared first). Round is 1-based.</summary>
    public abstract void GetPolicies(int round, List<PolicyDefinition> results);
}
