using System;
using System.Collections.Generic;
using System.Linq;

namespace NocnaStraz
{
    public sealed class TaskManager
    {
        private readonly Random _rng = new();
        private readonly List<TaskDefinition> _taskPool;
        private readonly List<TaskInstance> _active = new();

        public IReadOnlyList<TaskInstance> Active => _active;
        public TaskInstance Current { get; private set; }

        public event Action OnTaskListChanged;
        public event Action<TaskInstance> OnCurrentTaskChanged;

        public TaskManager(GameStats stats)
        {
            _taskPool = TaskDatabase.CreateDefaultTasks();
            // Startowo: 3 zadania.
            for (int i = 0; i < 3; i++)
                EnqueueRandom(stats);
        }

        public void EnqueueRandom(GameStats stats)
        {
            var candidates = _taskPool.Where(t => t.CanAppear(stats)).ToList();
            if (candidates.Count == 0) candidates = _taskPool.ToList();

            var picked = WeightedRandom.Pick(candidates, t => t.Weight, _rng);
            _active.Add(new TaskInstance(picked));
            OnTaskListChanged?.Invoke();
        }

        public bool TrySetCurrent(TaskInstance inst)
        {
            if (inst == null || inst.IsCompleted) return false;
            Current = inst;
            OnCurrentTaskChanged?.Invoke(Current);
            return true;
        }

        public void CompleteCurrent(GameStats stats, bool success)
        {
            if (Current == null) return;

            Current.IsCompleted = true;
            stats.ApplyDeltas(success ? Current.Def.SuccessDeltas : Current.Def.FailDeltas);

            Current = null;
            OnTaskListChanged?.Invoke();
            OnCurrentTaskChanged?.Invoke(null);
        }

        public void RemoveCompleted()
        {
            _active.RemoveAll(t => t.IsCompleted);
            OnTaskListChanged?.Invoke();
        }
    }
}
