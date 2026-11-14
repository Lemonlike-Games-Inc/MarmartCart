using System.Globalization;
using UnityEngine;

namespace LightSide
{
    /// <summary>Nice-number stepping shared by rulers, grids and axis ticks.</summary>
    public static class AxisScale
    {
        private static readonly float[] multipliers = { 1f, 2f, 5f, 10f };

        /// <summary>
        /// The smallest 1-2-5-decade step no finer than <paramref name="rawStep"/>, in the caller's units.
        /// Values at or below zero — a transient of unresolved layout — are treated as 1e-6.
        /// </summary>
        public static float NiceStep(float rawStep)
        {
            rawStep = Mathf.Max(rawStep, 1e-6f);
            var magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rawStep)));
            foreach (var multiplier in multipliers)
                if (magnitude * multiplier >= rawStep)
                    return magnitude * multiplier;
            return magnitude * 10f;
        }

        /// <summary>
        /// Formats a tick at <paramref name="value"/> for an axis stepping by <paramref name="step"/>: just
        /// enough decimals for that step and no more, so a label never claims precision the axis has not got.
        /// Written with a decimal point whatever the machine's locale, matching the numeric fields it sits with.
        /// </summary>
        public static string Format(float value, float step)
        {
            step = Mathf.Max(step, 1e-6f);
            if (Mathf.Abs(value) < step * 0.5f) value = 0f;
            var decimals = Mathf.Clamp(Mathf.CeilToInt(-Mathf.Log10(step)), 0, 6);
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }
    }
}
