using System;
using System.Collections.Generic;

namespace NocnaStraz
{
    [Serializable]
    public struct StatDelta
    {
        public StatType Stat;
        public int Delta;

        public StatDelta(StatType stat, int delta)
        {
            Stat = stat;
            Delta = delta;
        }
    }

    public enum TaskKind
    {
        RepairLamp,
        CleanPath,
        FuseBox,
        NeighbourDialogue,
        QuickTapBins
    }

    [Serializable]
    public sealed class TaskDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public string LocationHint;
        public TaskKind Kind;

        public float Weight = 1f;

        // Efekty na paski.
        public List<StatDelta> SuccessDeltas = new();
        public List<StatDelta> FailDeltas = new();

        // Prosty warunek pojawienia się (MVP).
        public StatType? RequiredStat;
        public int RequiredBelow = -1;

        public bool CanAppear(GameStats stats)
        {
            if (RequiredStat == null || RequiredBelow < 0) return true;
            return stats.Get(RequiredStat.Value) < RequiredBelow;
        }
    }

    [Serializable]
    public sealed class TaskInstance
    {
        public TaskDefinition Def;
        public bool IsCompleted;

        public TaskInstance(TaskDefinition def)
        {
            Def = def;
        }
    }

    public enum RandomEventKind
    {
        Blackout,
        SuspiciousPackage,
        NeighbourInterrupt,
        WeirdShadow,
        BusyPark
    }

    [Serializable]
    public sealed class RandomEventDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public RandomEventKind Kind;
        public float Weight = 1f;

        public List<StatDelta> InstantDeltas = new();
    }
}
