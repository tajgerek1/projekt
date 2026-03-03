using System;
using System.Collections.Generic;
using UnityEngine;

namespace NocnaStraz
{
    [Serializable]
    public sealed class GameStats
    {
        private readonly Dictionary<StatType, int> _values = new();

        public event Action<StatType, int> OnChanged;

        public GameStats()
        {
            // Startowe wartości (MVP). Zakres 0–100.
            _values[StatType.Illumination] = 70;
            _values[StatType.Order] = 70;
            _values[StatType.Gossip] = 25;
            _values[StatType.Fatigue] = 20;
            _values[StatType.Tension] = 15;
        }

        public int Get(StatType stat) => _values.TryGetValue(stat, out var v) ? v : 0;

        public void Set(StatType stat, int value)
        {
            value = Mathf.Clamp(value, 0, 100);
            _values[stat] = value;
            OnChanged?.Invoke(stat, value);
        }

        public void Add(StatType stat, int delta)
        {
            Set(stat, Get(stat) + delta);
        }

        public void ApplyDeltas(IEnumerable<StatDelta> deltas)
        {
            if (deltas == null) return;
            foreach (var d in deltas) Add(d.Stat, d.Delta);
        }

        public static bool IsBetterHigh(StatType stat)
        {
            // W tej grze:
            // - oświetlenie i porządek: im wyżej tym lepiej
            // - plotki, zmęczenie, napięcie: im niżej tym lepiej (ale nadal trzymamy 0–100)
            return stat is StatType.Illumination or StatType.Order;
        }
    }
}
