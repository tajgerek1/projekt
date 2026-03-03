using System;
using System.Collections.Generic;
using UnityEngine;

namespace NocnaStraz
{
    public sealed class RandomEventSystem
    {
        private readonly System.Random _rng = new();
        private readonly List<RandomEventDefinition> _events;

        private float _nextEventAt;

        public event Action<RandomEventDefinition> OnEventHappened;

        public RandomEventSystem()
        {
            _events = TaskDatabase.CreateDefaultEvents();
            ScheduleNext(0f);
        }

        public void Tick(GameStats stats, TaskManager tasks, float timeSinceStart)
        {
            if (timeSinceStart < _nextEventAt) return;

            // Im wyższe napięcie, tym częściej eventy.
            float tension01 = stats.Get(StatType.Tension) / 100f;
            float bias = Mathf.Lerp(0.6f, 1.6f, tension01);

            var ev = WeightedRandom.Pick(_events, e => e.Weight * bias, _rng);
            ApplyEvent(ev, stats, tasks);

            OnEventHappened?.Invoke(ev);
            ScheduleNext(tension01);
        }

        private void ScheduleNext(float tension01)
        {
            // 20–45 sekund, skraca się wraz z napięciem.
            float min = Mathf.Lerp(18f, 10f, tension01);
            float max = Mathf.Lerp(45f, 25f, tension01);
            _nextEventAt += UnityEngine.Random.Range(min, max);
        }

        private void ApplyEvent(RandomEventDefinition ev, GameStats stats, TaskManager tasks)
        {
            stats.ApplyDeltas(ev.InstantDeltas);

            switch (ev.Kind)
            {
                case RandomEventKind.Blackout:
                    // Dorzucamy dodatkową naprawę.
                    tasks.EnqueueRandom(stats);
                    tasks.EnqueueRandom(stats);
                    break;
                case RandomEventKind.BusyPark:
                    // Dorzucamy sprzątanie.
                    tasks.EnqueueRandom(stats);
                    break;
                case RandomEventKind.NeighbourInterrupt:
                    // Dorzucamy dialog.
                    tasks.EnqueueRandom(stats);
                    break;
                case RandomEventKind.WeirdShadow:
                    // Niewinny chaos -> lekko psuje porządek.
                    stats.Add(StatType.Order, -6);
                    break;
                case RandomEventKind.SuspiciousPackage:
                    // Daje mini-konflikt – plotki i napięcie już podbite, dodatkowo lekkie zmęczenie.
                    stats.Add(StatType.Fatigue, +4);
                    break;
            }
        }
    }
}
