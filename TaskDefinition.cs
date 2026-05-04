using NightWatch.Foundation;
using UnityEngine;

namespace NightWatch.Tasks
{
    public enum TaskType
    {
        Collect = 0,
        Repair = 1,
        Extinguish = 2,
        Clean = 3
    }

    [CreateAssetMenu(
        fileName = "TaskDefinition",
        menuName = "NightWatch/Tasks/Task Definition")]
    public sealed class TaskDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string taskId;
        [SerializeField] private string displayName;

        [Header("Requirements")]
        [SerializeField] [Min(1)] private int requiredCount = 1;
        [SerializeField] private ToolType requiredTool = ToolType.Flashlight;
        [SerializeField] private TaskType taskType = TaskType.Collect;

        [Header("Optional")]
        [SerializeField] [TextArea(2, 4)] private string description;
        [SerializeField] [Min(0f)] private float rewardWeight = 1f;

        public string TaskId => taskId;
        public string DisplayName => displayName;
        public int RequiredCount => requiredCount;
        public ToolType RequiredTool => requiredTool;
        public TaskType TaskType => taskType;
        public string Description => description;
        public float RewardWeight => rewardWeight;

        private void OnValidate()
        {
            if (requiredCount < 1)
            {
                requiredCount = 1;
            }
        }
    }
}
