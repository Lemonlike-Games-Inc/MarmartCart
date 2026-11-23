using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Ability Samples offered at the pick before a Round, and how likely each
/// tier is in each Round. Every card rolls its tier with that Round's weights,
/// then a Sample of that tier. The cards in one hand are always different
/// Samples; two players can be offered the same ones. Definition only.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Ability Samples/Sample Pool",
    fileName = "Ability Sample Pool"
)]
public sealed class AbilitySamplePool : ScriptableObject
{
    [Serializable]
    public sealed class RoundTierWeights
    {
        // Shown as the list element name in the Inspector; set from Round.
        [SerializeField, HideInInspector] private string label;

        [Tooltip("The Round these weights are for. A Round without an entry uses the closest earlier one.")]
        [Min(1)]
        [SerializeField] private int round;

        [Min(0f)] [SerializeField] private float silver;
        [Min(0f)] [SerializeField] private float gold;
        [Min(0f)] [SerializeField] private float prismatic;
        [Min(0f)] [SerializeField] private float unique;

        public RoundTierWeights() : this(2, 60f, 40f, 0f, 0f)
        {
        }

        public RoundTierWeights(int round, float silver, float gold, float prismatic, float unique)
        {
            this.round = round;
            this.silver = silver;
            this.gold = gold;
            this.prismatic = prismatic;
            this.unique = unique;
            RefreshLabel();
        }

        public int Round => round;

        public float GetWeight(AbilitySampleTier tier)
        {
            return tier switch
            {
                AbilitySampleTier.Silver => silver,
                AbilitySampleTier.Gold => gold,
                AbilitySampleTier.Prismatic => prismatic,
                AbilitySampleTier.Unique => unique,
                _ => 0f
            };
        }

        public void RefreshLabel()
        {
            label = $"Round {round}";
        }
    }

    // Add new tiers here too.
    private static readonly AbilitySampleTier[] Tiers =
    {
        AbilitySampleTier.Silver,
        AbilitySampleTier.Gold,
        AbilitySampleTier.Prismatic,
        AbilitySampleTier.Unique
    };

    [Tooltip("Every Sample that can be offered.")]
    [SerializeField] private List<AbilitySampleDefinition> samples = new List<AbilitySampleDefinition>();

    [Tooltip(
        "Chance of each tier per card, per Round. Relative weights: they do not need " +
        "to add up to 100. A tier with no Samples left in the pool is skipped."
    )]
    [SerializeField]
    private List<RoundTierWeights> roundWeights = new List<RoundTierWeights>
    {
        new RoundTierWeights(2, 60f, 40f, 0f, 0f),
        new RoundTierWeights(3, 30f, 70f, 0f, 0f)
    };

    [Tooltip("Pity rule: a hand never holds more than one Baseline Sample (the plain supplies).")]
    [SerializeField] private bool atMostOneBaselinePerHand;

    public IReadOnlyList<AbilitySampleDefinition> Samples => samples;

    /// <summary>
    /// Fills hand with different Samples for this Round, as many as the pool
    /// allows, and clears the rest. Returns how many were filled.
    /// candidates is scratch space, so a roll allocates nothing.
    /// </summary>
    public int RollHand(int round, AbilitySampleDefinition[] hand, List<AbilitySampleDefinition> candidates)
    {
        RoundTierWeights weights = GetWeights(round);
        bool hasBaseline = false;
        int filled = 0;

        for (int slot = 0; slot < hand.Length; slot++)
        {
            candidates.Clear();

            for (int i = 0; i < samples.Count; i++)
            {
                AbilitySampleDefinition sample = samples[i];

                if (sample == null || sample.Item == null) continue;
                if (Array.IndexOf(hand, sample, 0, filled) >= 0) continue;
                if (atMostOneBaselinePerHand && hasBaseline && sample.Kind == AbilitySampleKind.Baseline) continue;

                candidates.Add(sample);
            }

            if (candidates.Count == 0) break;

            AbilitySampleDefinition picked = PickByTier(candidates, weights);
            hand[filled++] = picked;
            hasBaseline |= picked.Kind == AbilitySampleKind.Baseline;
        }

        for (int i = filled; i < hand.Length; i++)
        {
            hand[i] = null;
        }

        return filled;
    }

    /// <summary>The weights for this Round: its own entry, else the closest earlier one, else the first.</summary>
    public RoundTierWeights GetWeights(int round)
    {
        RoundTierWeights best = null;

        for (int i = 0; i < roundWeights.Count; i++)
        {
            RoundTierWeights entry = roundWeights[i];
            if (entry == null || entry.Round > round) continue;
            if (best == null || entry.Round > best.Round) best = entry;
        }

        if (best == null && roundWeights.Count > 0) best = roundWeights[0];
        return best;
    }

    private static AbilitySampleDefinition PickByTier(List<AbilitySampleDefinition> candidates, RoundTierWeights weights)
    {
        float total = 0f;

        for (int t = 0; t < Tiers.Length; t++)
        {
            total += TierWeight(Tiers[t], candidates, weights);
        }

        // No weighted tier has a Sample left: any remaining Sample will do.
        if (total <= 0f) return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.value * total;
        AbilitySampleTier chosen = AbilitySampleTier.Silver;

        for (int t = 0; t < Tiers.Length; t++)
        {
            float weight = TierWeight(Tiers[t], candidates, weights);
            if (weight <= 0f) continue;

            chosen = Tiers[t];
            if (roll < weight) break;
            roll -= weight;
        }

        int count = CountTier(candidates, chosen);
        int pick = UnityEngine.Random.Range(0, count);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Tier != chosen) continue;
            if (pick-- == 0) return candidates[i];
        }

        return candidates[0];
    }

    private static float TierWeight(AbilitySampleTier tier, List<AbilitySampleDefinition> candidates, RoundTierWeights weights)
    {
        float weight = weights != null ? weights.GetWeight(tier) : 1f;
        return weight > 0f && CountTier(candidates, tier) > 0 ? weight : 0f;
    }

    private static int CountTier(List<AbilitySampleDefinition> candidates, AbilitySampleTier tier)
    {
        int count = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Tier == tier) count++;
        }

        return count;
    }

    private void OnValidate()
    {
        for (int i = 0; i < roundWeights.Count; i++)
        {
            roundWeights[i]?.RefreshLabel();
        }
    }
}
