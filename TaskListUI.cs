using TMPro;
using UnityEngine;

namespace NightWatch.Tasks
{
    [DisallowMultipleComponent]
    public sealed class TaskListUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private TextMeshProUGUI[] taskSlots = new TextMeshProUGUI[4];

        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color completedColor = new Color(0.45f, 1f, 0.45f, 1f);
        [SerializeField] private Color failedColor = new Color(1f, 0.35f, 0.35f, 1f);

        private void OnEnable()
        {
            if (taskManager == null)
            {
                Debug.LogError("[TaskListUI] TaskManager reference is missing.", this);
                return;
            }

            taskManager.OnTasksChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (taskManager != null)
            {
                taskManager.OnTasksChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (taskManager == null || taskSlots == null)
            {
                return;
            }

            var tasks = taskManager.GetAllTasks();

            for (int slotIndex = 0; slotIndex < taskSlots.Length; slotIndex++)
            {
                TextMeshProUGUI slot = taskSlots[slotIndex];
                if (slot == null)
                {
                    continue;
                }

                if (slotIndex >= tasks.Count || tasks[slotIndex] == null)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);

                TaskRuntimeState state = tasks[slotIndex];
                slot.text = BuildTaskLine(state);
                slot.color = GetTaskColor(state);
            }
        }

        private string BuildTaskLine(TaskRuntimeState state)
        {
            string title = string.IsNullOrWhiteSpace(state.DisplayName)
                ? state.TaskId
                : state.DisplayName;

            if (state.Completed)
            {
                return $"{title} \u2713";
            }

            if (state.Failed)
            {
                return $"{title} \u2717";
            }

            if (state.RequiredCount <= 1)
            {
                return $"{title} !";
            }

            return $"{title} {state.CurrentProgress}/{state.RequiredCount}";
        }

        private Color GetTaskColor(TaskRuntimeState state)
        {
            if (state.Completed)
            {
                return completedColor;
            }

            if (state.Failed)
            {
                return failedColor;
            }

            return activeColor;
        }
    }
}
