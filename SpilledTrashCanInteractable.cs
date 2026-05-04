using NightWatch.Foundation;
using NightWatch.Tasks;
using UnityEngine;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class SpilledTrashCanInteractable : MonoBehaviour, IHoldInteractable, ITaskMarkerTarget
    {
        [Header("Task")]
        [SerializeField] private string taskId = "trash";
        [SerializeField] private ToolType requiredTool = ToolType.TrashBag;
        [SerializeField] private bool allowAnyTool = true;
        [SerializeField] [Min(1)] private int progressValue = 1;

        [Header("Prompt")]
        [SerializeField] private string promptText = "PODNIES KOSZ";
        [SerializeField] private string invalidPromptTemplate = "WYMAGANY: {0}";
        [SerializeField] private string liftingPromptTemplate = "PODNOSZENIE KOSZA... {0}%";
        [SerializeField] private string completedPromptText = "KOSZ PODNIESIONY";

        [Header("Animation")]
        [SerializeField] [Min(0.05f)] private float holdDurationSeconds = 5f;
        [SerializeField] [Min(0f)] private float liftArcHeight = 0.35f;
        [SerializeField] private Vector3 uprightEuler = Vector3.zero;

        [Header("References")]
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private GameObject normalTrashCan;
        [SerializeField] private Vector3 uprightPosition;

        [Header("Marker")]
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.4f, 0f);

        private bool hasBeenUsed;
        private bool isHolding;
        private bool hasLoggedMissingTaskManager;
        private float holdProgress01;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Quaternion targetRotation;

        public string TaskId => taskId;
        public bool IsUsed => hasBeenUsed;

        public void Configure(
            string newTaskId,
            ToolType newRequiredTool,
            int newProgressValue,
            string newPromptText,
            Vector3 newUprightPosition,
            Vector3 newUprightEuler,
            float newHoldDurationSeconds,
            GameObject newNormalTrashCan)
        {
            taskId = string.IsNullOrWhiteSpace(newTaskId) ? taskId : newTaskId;
            requiredTool = newRequiredTool;
            progressValue = Mathf.Max(1, newProgressValue);
            uprightPosition = newUprightPosition;
            uprightEuler = newUprightEuler;
            holdDurationSeconds = Mathf.Max(0.05f, newHoldDurationSeconds);
            normalTrashCan = newNormalTrashCan;

            if (!string.IsNullOrWhiteSpace(newPromptText))
            {
                promptText = newPromptText;
            }
        }

        private void Awake()
        {
            ResolveTaskManagerIfNeeded();
            CacheLiftStartTransform();
        }

        public string GetPromptText(ToolType currentTool)
        {
            if (!IsTaskActive())
            {
                return string.Empty;
            }

            if (hasBeenUsed)
            {
                return completedPromptText;
            }

            if (isHolding)
            {
                int percent = Mathf.RoundToInt(holdProgress01 * 100f);
                return string.Format(liftingPromptTemplate, percent);
            }

            if (allowAnyTool || currentTool == requiredTool)
            {
                return promptText;
            }

            return string.Format(invalidPromptTemplate, requiredTool);
        }

        public bool CanInteract(ToolType currentTool)
        {
            return IsTaskActive() && !hasBeenUsed && (allowAnyTool || currentTool == requiredTool);
        }

        public void Interact(ToolType currentTool)
        {
            BeginHold(currentTool);
        }

        public void BeginHold(ToolType currentTool)
        {
            if (!CanInteract(currentTool))
            {
                return;
            }

            ResolveTaskManagerIfNeeded();
            if (taskManager == null)
            {
                if (!hasLoggedMissingTaskManager)
                {
                    hasLoggedMissingTaskManager = true;
                    Debug.LogError($"[SpilledTrashCanInteractable] Missing TaskManager reference on '{name}'.", this);
                }

                return;
            }

            isHolding = true;
            CacheLiftStartTransform();
            ApplyLiftPose(holdProgress01);
        }

        public void UpdateHold(ToolType currentTool, float deltaTime)
        {
            if (!CanInteract(currentTool))
            {
                CancelHold();
                return;
            }

            if (!isHolding)
            {
                BeginHold(currentTool);
            }

            if (!isHolding)
            {
                return;
            }

            holdProgress01 = Mathf.Clamp01(holdProgress01 + deltaTime / Mathf.Max(0.05f, holdDurationSeconds));
            ApplyLiftPose(holdProgress01);

            if (holdProgress01 >= 1f)
            {
                CompleteHold();
            }
        }

        public void CancelHold()
        {
            if (hasBeenUsed)
            {
                return;
            }

            isHolding = false;
            holdProgress01 = 0f;
            ApplyLiftPose(0f);
        }

        private void CompleteHold()
        {
            if (hasBeenUsed)
            {
                return;
            }

            hasBeenUsed = true;
            isHolding = false;
            holdProgress01 = 1f;
            ApplyLiftPose(1f);
            taskManager.ReportProgress(taskId, progressValue);

            if (normalTrashCan != null)
            {
                normalTrashCan.transform.SetPositionAndRotation(uprightPosition, Quaternion.Euler(uprightEuler));
                normalTrashCan.SetActive(true);
            }

            Destroy(gameObject);
        }

        private void CacheLiftStartTransform()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            targetRotation = Quaternion.Euler(uprightEuler);
        }

        private void ApplyLiftPose(float progress01)
        {
            float t = Mathf.Clamp01(progress01);
            float eased = t * t * (3f - 2f * t);
            float arc = Mathf.Sin(eased * Mathf.PI) * liftArcHeight;

            transform.position = Vector3.Lerp(startPosition, uprightPosition, eased) + Vector3.up * arc;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
        }

        private void ResolveTaskManagerIfNeeded()
        {
            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<TaskManager>();
            }
        }

        private bool IsTaskActive()
        {
            ResolveTaskManagerIfNeeded();
            return taskManager == null ||
                   (taskManager.TryGetTaskState(taskId, out TaskRuntimeState state) && state != null && state.Active);
        }

        public Vector3 GetMarkerWorldPosition()
        {
            return transform.position + markerOffset;
        }

        private void OnValidate()
        {
            if (progressValue < 1)
            {
                progressValue = 1;
            }

            if (holdDurationSeconds < 0.05f)
            {
                holdDurationSeconds = 0.05f;
            }

            if (liftArcHeight < 0f)
            {
                liftArcHeight = 0f;
            }
        }
    }
}
