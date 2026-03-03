using System;
using System.Collections.Generic;

namespace NocnaStraz
{
    public static class WeightedRandom
    {
        public static T Pick<T>(IReadOnlyList<T> items, Func<T, float> weight, Random rng)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("No items to pick.");
            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += Math.Max(0f, weight(items[i]));

            if (total <= 0.0001f)
                return items[rng.Next(items.Count)];

            double roll = rng.NextDouble() * total;
            double acc = 0;
            for (int i = 0; i < items.Count; i++)
            {
                acc += Math.Max(0f, weight(items[i]));
                if (roll <= acc) return items[i];
            }
            return items[^1];
        }
    }
}
