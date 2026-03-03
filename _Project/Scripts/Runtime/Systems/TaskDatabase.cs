using System.Collections.Generic;

namespace NocnaStraz
{
    /// <summary>
    /// W MVP trzymamy dane w kodzie (zgodnie z backlogiem: ScriptableObject lub klasy).
    /// Łatwo przenieść to do ScriptableObjectów później.
    /// </summary>
    public static class TaskDatabase
    {
        public static List<TaskDefinition> CreateDefaultTasks()
        {
            return new List<TaskDefinition>
            {
                new TaskDefinition
                {
                    Id = "repair_lamp",
                    Title = "Napraw latarnię",
                    Description = "Latarnia mruga jakby opowiadała dowcipy. Zrób 'hold + timing' i przywróć światło.",
                    LocationHint = "Latarnia przy alejce",
                    Kind = TaskKind.RepairLamp,
                    Weight = 1.6f,
                    RequiredStat = StatType.Illumination,
                    RequiredBelow = 85,
                    SuccessDeltas = new List<StatDelta>
                    {
                        new(StatType.Illumination, +18),
                        new(StatType.Fatigue, +6),
                        new(StatType.Tension, -3),
                    },
                    FailDeltas = new List<StatDelta>
                    {
                        new(StatType.Illumination, -12),
                        new(StatType.Tension, +10),
                        new(StatType.Gossip, +4),
                    }
                },
                new TaskDefinition
                {
                    Id = "clean_path",
                    Title = "Posprzątaj ścieżkę",
                    Description = "Liście i śmieci udają, że są elementem sztuki współczesnej. Wciągnij je do kosza (drag & drop).",
                    LocationHint = "Główna ścieżka",
                    Kind = TaskKind.CleanPath,
                    Weight = 1.4f,
                    RequiredStat = StatType.Order,
                    RequiredBelow = 90,
                    SuccessDeltas = new List<StatDelta>
                    {
                        new(StatType.Order, +18),
                        new(StatType.Fatigue, +5),
                        new(StatType.Gossip, -4),
                    },
                    FailDeltas = new List<StatDelta>
                    {
                        new(StatType.Order, -10),
                        new(StatType.Gossip, +8),
                        new(StatType.Tension, +4),
                    }
                },
                new TaskDefinition
                {
                    Id = "fusebox",
                    Title = "Awaria bezpieczników",
                    Description = "Ktoś uznał, że bezpieczniki są jak puzzle. Ustaw przełączniki w dobrym układzie.",
                    LocationHint = "Skrzynka serwisowa",
                    Kind = TaskKind.FuseBox,
                    Weight = 1.0f,
                    RequiredStat = StatType.Illumination,
                    RequiredBelow = 70,
                    SuccessDeltas = new List<StatDelta>
                    {
                        new(StatType.Illumination, +12),
                        new(StatType.Fatigue, +8),
                        new(StatType.Tension, -2),
                    },
                    FailDeltas = new List<StatDelta>
                    {
                        new(StatType.Illumination, -15),
                        new(StatType.Tension, +12),
                    }
                },
                new TaskDefinition
                {
                    Id = "neighbour_dialogue",
                    Title = "Sąsiadka Plotkara zaczepia",
                    Description = "Musisz odpowiedzieć tak, żeby było miło… i żeby nie poszło na grupkę osiedlową.",
                    LocationHint = "Przy ogrodzeniu",
                    Kind = TaskKind.NeighbourDialogue,
                    Weight = 0.9f,
                    SuccessDeltas = new List<StatDelta>
                    {
                        new(StatType.Gossip, -10),
                        new(StatType.Tension, -6),
                    },
                    FailDeltas = new List<StatDelta>
                    {
                        new(StatType.Gossip, +12),
                        new(StatType.Tension, +6),
                    }
                },
                new TaskDefinition
                {
                    Id = "bins_check",
                    Title = "Kontrola koszy",
                    Description = "Szybki obchód: kliknij 3 kosze. Jeśli są przepełnione, porządek leci w dół.",
                    LocationHint = "Wzdłuż alejki",
                    Kind = TaskKind.QuickTapBins,
                    Weight = 1.2f,
                    SuccessDeltas = new List<StatDelta>
                    {
                        new(StatType.Order, +8),
                        new(StatType.Fatigue, +2),
                    },
                    FailDeltas = new List<StatDelta>
                    {
                        new(StatType.Order, -8),
                        new(StatType.Gossip, +6),
                    }
                }
            };
        }

        public static List<RandomEventDefinition> CreateDefaultEvents()
        {
            return new List<RandomEventDefinition>
            {
                new RandomEventDefinition
                {
                    Id = "blackout",
                    Title = "Blackout!",
                    Description = "Kilka lamp gaśnie naraz. Park wygląda jakby udawał, że go nie ma.",
                    Kind = RandomEventKind.Blackout,
                    Weight = 1.2f,
                    InstantDeltas = new List<StatDelta>
                    {
                        new(StatType.Illumination, -20),
                        new(StatType.Tension, +10),
                    }
                },
                new RandomEventDefinition
                {
                    Id = "package",
                    Title = "Paczka przy ławce",
                    Description = "Jest paczka. Nie, nie taka z ciastkami. Co robisz?",
                    Kind = RandomEventKind.SuspiciousPackage,
                    Weight = 0.8f,
                    InstantDeltas = new List<StatDelta>
                    {
                        new(StatType.Tension, +6),
                        new(StatType.Gossip, +4),
                    }
                },
                new RandomEventDefinition
                {
                    Id = "neighbour_interrupt",
                    Title = "Sąsiadka zaczepia (z zaskoczenia)",
                    Description = "Pojawia się jak reklama po 3 sekundach filmu. Trudno ją pominąć.",
                    Kind = RandomEventKind.NeighbourInterrupt,
                    Weight = 1.0f,
                    InstantDeltas = new List<StatDelta>
                    {
                        new(StatType.Gossip, +6),
                        new(StatType.Tension, +4),
                    }
                },
                new RandomEventDefinition
                {
                    Id = "weird_shadow",
                    Title = "Dziwny cień",
                    Description = "Nie jest horrorem. Po prostu… cień ma swoje zdanie.",
                    Kind = RandomEventKind.WeirdShadow,
                    Weight = 0.9f,
                    InstantDeltas = new List<StatDelta>
                    {
                        new(StatType.Tension, +12),
                    }
                },
                new RandomEventDefinition
                {
                    Id = "busy_park",
                    Title = "Wzmożony ruch ludzi",
                    Description = "Więcej ludzi = więcej śmieci. Matematyka ulicy.",
                    Kind = RandomEventKind.BusyPark,
                    Weight = 1.1f,
                    InstantDeltas = new List<StatDelta>
                    {
                        new(StatType.Order, -10),
                        new(StatType.Gossip, +3),
                    }
                }
            };
        }
    }
}
