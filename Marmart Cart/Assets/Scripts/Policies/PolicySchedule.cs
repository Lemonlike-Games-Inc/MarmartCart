using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fixed Policies per Round: Element 0 = Round 1, 1 = Round 2, 2 = Round 3.
/// A Round can have none, one, or several Policies; they all run together.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Policies/Policy Schedule",
    fileName = "Policy Schedule"
)]
public sealed class PolicySchedule : PolicyRoundSource
{
    [Serializable]
    public sealed class RoundPolicies
    {
        // Shown as the list element name in the Inspector.
        [SerializeField, HideInInspector] private string label;

        [SerializeField] private List<PolicyDefinition> policies = new List<PolicyDefinition>();

        public IReadOnlyList<PolicyDefinition> Policies => policies;

        public void SetLabel(int round)
        {
            label = $"Round {round}";
        }
    }

    [SerializeField] private RoundPolicies[] rounds = new RoundPolicies[MatchSession.RoundsPerMatch];

    public override void GetPolicies(int round, List<PolicyDefinition> results)
    {
        results.Clear();

        int slot = round - 1;
        if (rounds == null || slot < 0 || slot >= rounds.Length || rounds[slot] == null) return;

        IReadOnlyList<PolicyDefinition> policies = rounds[slot].Policies;

        for (int i = 0; i < policies.Count; i++)
        {
            PolicyDefinition policy = policies[i];
            if (policy != null && !results.Contains(policy)) results.Add(policy);
        }
    }

    private void OnValidate()
    {
        if (rounds == null || rounds.Length != MatchSession.RoundsPerMatch)
            Array.Resize(ref rounds, MatchSession.RoundsPerMatch);

        for (int i = 0; i < rounds.Length; i++)
        {
            if (rounds[i] == null) rounds[i] = new RoundPolicies();
            rounds[i].SetLabel(i + 1);
        }
    }
}
