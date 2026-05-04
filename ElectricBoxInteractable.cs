using NightWatch.Foundation;
using NightWatch.Tasks;
using UnityEngine;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class ElectricBoxInteractable : MonoBehaviour, IInteractable, ITaskMarkerTarget
    {
        [Header("Task")]
        [SerializeField] private string taskId = "electricity";
        [SerializeField] private ToolType requiredTool = ToolType.Key;

        [Header("Prompt")]
        [SerializeField] private string repairPromptText = "NAPRAW SKRZYNKE";
        [SerializeField] private string waitingPromptText = "SKRZYNKA DZIALA";
        [SerializeField] private string repairedPromptText = "PRAD NAPRAWIONY";
        [SerializeField] private string invalidPromptTemplate = "WYMAGANY: {0}";

        [Header("Marker")]
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.6f, 0f);

        [Header("References")]
        [SerializeField] private TaskManager taskManager;

        private PowerOutageController outageController;

        public string TaskId => taskId;
        public bool IsUsed => outageController == null || !outageController.IsOutageActive || outageController.IsRepaired;

        public void Configure(PowerOutageController newOutageController)
        {
            outageController = newOutageController;
        }

        public string GetPromptText(ToolType currentTool)
        {
            if (outageController == null || !outageController.IsOutageActive)
            {
                return outageController != null && outageController.IsRepaired ? repairedPromptText : waitingPromptText;
            }

            if (currentTool == requiredTool)
            {
                return repairPromptText;
            }

            return string.Format(invalidPromptTemplate, requiredTool);
        }

        public bool CanInteract(ToolType currentTool)
        {
            return outageController != null && outageController.IsOutageActive && currentTool == requiredTool;
        }

        public void Interact(ToolType currentTool)
        {
            if (!CanInteract(currentTool))
            {
                return;
            }

            outageController.RepairPower();
            ResolveTaskManagerIfNeeded();
            if (taskManager != null)
            {
                taskManager.ReportProgress(taskId, 1);
            }
        }

        public Vector3 GetMarkerWorldPosition()
        {
            return transform.position + markerOffset;
        }

        private void ResolveTaskManagerIfNeeded()
        {
            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<TaskManager>();
            }
        }
    }
}
