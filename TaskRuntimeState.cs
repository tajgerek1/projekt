using UnityEngine;

namespace NightWatch.Tasks
{
    [System.Serializable]
    public sealed class TaskRuntimeState
    {
        [SerializeField] private TaskDefinition definition;
        [SerializeField] private string runtimeTaskId;
        [SerializeField] private string runtimeDisplayName;
        [SerializeField] private NightWatch.Foundation.ToolType runtimeRequiredTool;
        [SerializeField] private TaskType runtimeTaskType;
        [SerializeField] private int currentProgress;
        [SerializeField] private int requiredCount;
        [SerializeField] private bool completed;
        [SerializeField] private bool failed;
        [SerializeField] private bool active = true;

        public TaskDefinition Definition => definition;
        public string TaskId => definition != null ? definition.TaskId : runtimeTaskId;
        public string DisplayName => definition != null ? definition.DisplayName : runtimeDisplayName;
        public NightWatch.Foundation.ToolType RequiredTool => definition != null ? definition.RequiredTool : runtimeRequiredTool;
        public TaskType TaskType => definition != null ? definition.TaskType : runtimeTaskType;
        public int CurrentProgress => currentProgress;
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public bool Completed => completed;
        public bool Failed => failed;
        public bool Active => active;

        public TaskRuntimeState(TaskDefinition taskDefinition, bool startsActive = true)
        {
            definition = taskDefinition;
            currentProgress = 0;
            requiredCount = definition != null ? Mathf.Max(1, definition.RequiredCount) : 1;
            completed = false;
            failed = false;
            active = startsActive;
        }

        public TaskRuntimeState(
            string taskId,
            string displayName,
            int newRequiredCount,
            NightWatch.Foundation.ToolType requiredTool,
            TaskType taskType,
            bool startsActive = false)
        {
            definition = null;
            runtimeTaskId = taskId;
            runtimeDisplayName = displayName;
            runtimeRequiredTool = requiredTool;
            runtimeTaskType = taskType;
            currentProgress = 0;
            requiredCount = Mathf.Max(1, newRequiredCount);
            completed = false;
            failed = false;
            active = startsActive;
        }

        public void SetActive(bool isActive, bool resetProgress)
        {
            active = isActive;

            if (resetProgress)
            {
                currentProgress = 0;
                completed = false;
                failed = false;
            }
        }

        public void SetRequiredCount(int count)
        {
            requiredCount = Mathf.Max(1, count);
            currentProgress = Mathf.Clamp(currentProgress, 0, RequiredCount);

            if (currentProgress >= RequiredCount)
            {
                completed = true;
            }
        }

        public void AddProgress(int amount)
        {
            if (!active || completed || failed || amount <= 0)
            {
                return;
            }

            currentProgress = Mathf.Clamp(currentProgress + amount, 0, RequiredCount);

            if (currentProgress >= RequiredCount)
            {
                completed = true;
            }
        }

        public void MarkFailed()
        {
            if (!active || completed)
            {
                return;
            }

            failed = true;
        }

        public void ResetForNewNight()
        {
            ResetForNewNight(active);
        }

        public void ResetForNewNight(bool isActive)
        {
            currentProgress = 0;
            completed = false;
            failed = false;
            active = isActive;
        }
    }
}
