using System;
using System.Collections.Generic;
using NightWatch.Foundation;
using UnityEngine;

namespace NightWatch.Tasks
{
    [DisallowMultipleComponent]
    public sealed class TaskManager : MonoBehaviour
    {
        [Header("Task Setup")]
        [SerializeField] private List<TaskDefinition> taskDefinitions = new List<TaskDefinition>();

        private readonly List<TaskRuntimeState> runtimeStates = new List<TaskRuntimeState>();
        private readonly List<TaskRuntimeState> allRuntimeStates = new List<TaskRuntimeState>();
        private readonly Dictionary<string, TaskRuntimeState> statesById = new Dictionary<string, TaskRuntimeState>(StringComparer.OrdinalIgnoreCase);

        public event Action OnTasksChanged;

        private void Awake()
        {
            BuildRuntimeStates();
        }

        public IReadOnlyList<TaskRuntimeState> GetAllTasks()
        {
            return runtimeStates;
        }

        public bool TryGetTaskState(string taskId, out TaskRuntimeState state)
        {
            state = null;

            if (string.IsNullOrWhiteSpace(taskId))
            {
                return false;
            }

            return statesById.TryGetValue(taskId, out state);
        }

        public bool IsTaskActive(string taskId)
        {
            return TryGetTaskState(taskId, out TaskRuntimeState state) && state != null && state.Active;
        }

        public void SelectActiveTasks(IEnumerable<string> activeTaskIds)
        {
            HashSet<string> selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (activeTaskIds != null)
            {
                foreach (string taskId in activeTaskIds)
                {
                    if (!string.IsNullOrWhiteSpace(taskId))
                    {
                        selectedIds.Add(taskId.Trim());
                    }
                }
            }

            for (int i = 0; i < allRuntimeStates.Count; i++)
            {
                TaskRuntimeState state = allRuntimeStates[i];
                if (state == null)
                {
                    continue;
                }

                state.ResetForNewNight(selectedIds.Contains(state.TaskId));
            }

            RefreshActiveStates();
            OnTasksChanged?.Invoke();
        }

        public bool ActivateTask(string taskId, bool resetProgress)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return false;
            }

            if (!statesById.TryGetValue(taskId, out TaskRuntimeState state) || state == null)
            {
                return false;
            }

            bool wasActive = state.Active;
            bool wasCompleted = state.Completed;
            int previousProgress = state.CurrentProgress;
            state.SetActive(true, resetProgress);
            RefreshActiveStates();

            if (!wasActive || state.Completed != wasCompleted || state.CurrentProgress != previousProgress)
            {
                OnTasksChanged?.Invoke();
            }

            return true;
        }

        public TaskRuntimeState EnsureRuntimeTask(
            string taskId,
            string displayName,
            int requiredCount,
            ToolType requiredTool,
            TaskType taskType)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return null;
            }

            string safeTaskId = taskId.Trim();
            if (statesById.TryGetValue(safeTaskId, out TaskRuntimeState existingState))
            {
                existingState.SetRequiredCount(requiredCount);
                return existingState;
            }

            TaskRuntimeState state = new TaskRuntimeState(
                safeTaskId,
                string.IsNullOrWhiteSpace(displayName) ? safeTaskId : displayName,
                requiredCount,
                requiredTool,
                taskType,
                false);

            allRuntimeStates.Add(state);
            statesById.Add(safeTaskId, state);
            RefreshActiveStates();
            OnTasksChanged?.Invoke();
            return state;
        }

        public void ReportProgress(string taskId, int amount)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                Debug.LogWarning("[TaskManager] ReportProgress received empty taskId.", this);
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"[TaskManager] ReportProgress amount must be > 0 for taskId '{taskId}'.", this);
                return;
            }

            if (!statesById.TryGetValue(taskId, out TaskRuntimeState state))
            {
                Debug.LogWarning($"[TaskManager] TaskId '{taskId}' was not found in runtime states.", this);
                return;
            }

            if (!state.Active)
            {
                return;
            }

            bool wasCompleted = state.Completed;
            int previousProgress = state.CurrentProgress;

            state.AddProgress(amount);

            if (state.CurrentProgress != previousProgress || state.Completed != wasCompleted)
            {
                OnTasksChanged?.Invoke();
            }
        }

        public void SetRequiredCount(string taskId, int requiredCount)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                Debug.LogWarning("[TaskManager] SetRequiredCount received empty taskId.", this);
                return;
            }

            if (!statesById.TryGetValue(taskId, out TaskRuntimeState state))
            {
                Debug.LogWarning($"[TaskManager] TaskId '{taskId}' was not found in runtime states.", this);
                return;
            }

            int previousRequiredCount = state.RequiredCount;
            bool wasCompleted = state.Completed;
            state.SetRequiredCount(requiredCount);

            if (state.RequiredCount != previousRequiredCount || state.Completed != wasCompleted)
            {
                OnTasksChanged?.Invoke();
            }
        }

        public int GetCompletedCount()
        {
            int completedCount = 0;

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                if (runtimeStates[i].Completed)
                {
                    completedCount++;
                }
            }

            return completedCount;
        }

        public int GetTotalCount()
        {
            return runtimeStates.Count;
        }

        public float GetCompletionRatio()
        {
            int total = GetTotalCount();
            if (total <= 0)
            {
                return 0f;
            }

            return GetCompletedCount() / (float)total;
        }

        public void ResetAllTasks()
        {
            for (int i = 0; i < allRuntimeStates.Count; i++)
            {
                allRuntimeStates[i]?.ResetForNewNight(true);
            }

            RefreshActiveStates();
            OnTasksChanged?.Invoke();
        }

        private void BuildRuntimeStates()
        {
            runtimeStates.Clear();
            allRuntimeStates.Clear();
            statesById.Clear();

            for (int i = 0; i < taskDefinitions.Count; i++)
            {
                TaskDefinition definition = taskDefinitions[i];
                if (definition == null)
                {
                    continue;
                }

                string taskId = definition.TaskId;
                if (string.IsNullOrWhiteSpace(taskId))
                {
                    Debug.LogWarning($"[TaskManager] TaskDefinition '{definition.name}' has empty TaskId and was skipped.", this);
                    continue;
                }

                if (statesById.ContainsKey(taskId))
                {
                    Debug.LogWarning($"[TaskManager] Duplicate TaskId '{taskId}' detected. Skipping duplicate definition.", this);
                    continue;
                }

                TaskRuntimeState state = new TaskRuntimeState(definition);
                allRuntimeStates.Add(state);
                statesById.Add(taskId, state);
            }

            RefreshActiveStates();
            OnTasksChanged?.Invoke();
        }

        private void RefreshActiveStates()
        {
            runtimeStates.Clear();

            for (int i = 0; i < allRuntimeStates.Count; i++)
            {
                TaskRuntimeState state = allRuntimeStates[i];
                if (state != null && state.Active)
                {
                    runtimeStates.Add(state);
                }
            }
        }
    }
}
