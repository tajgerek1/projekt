using System.Collections.Generic;
using NightWatch.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NightWatch.Ui
{
    [DisallowMultipleComponent]
    public sealed class TaskWallMarkerController : MonoBehaviour
    {
        [Header("Unlock")]
        [SerializeField] private ShopScreenController shopScreenController;
        [SerializeField] private string unlockItemId = "item_1";

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private RectTransform markerContainer;
        [SerializeField] private RectTransform markerPrefab;

        [Header("Targets")]
        [SerializeField] private TaskTarget[] trackedTargets = new TaskTarget[0];
        [SerializeField] private bool includeRuntimeTaskTargets = true;
        [SerializeField] private bool hideCompletedTaskTargets = true;

        [Header("Visual")]
        [SerializeField] [Min(0f)] private float edgePadding = 36f;
        [SerializeField] private Vector2 screenOffset = Vector2.zero;
        [SerializeField] private Color markerColor = new Color(1f, 0.92f, 0.15f, 1f);
        [SerializeField] [Min(0.1f)] private float markerScale = 1f;

        private readonly List<RectTransform> markerInstances = new List<RectTransform>();
        private readonly List<ITaskMarkerTarget> markerTargets = new List<ITaskMarkerTarget>();
        private bool hasLoggedMissingReferences;

        private void Awake()
        {
            TryAutoAssignReferences();
            includeRuntimeTaskTargets = true;
            BuildMarkers();
            SetContainerVisible(false);
        }

        private void OnValidate()
        {
            if (edgePadding < 0f)
            {
                edgePadding = 0f;
            }

            if (markerScale < 0.1f)
            {
                markerScale = 0.1f;
            }
        }

        private void Update()
        {
            if (!ValidateReferences())
            {
                SetContainerVisible(false);
                return;
            }

            bool unlocked = shopScreenController.IsPurchased(unlockItemId);
            SetContainerVisible(unlocked);
            if (!unlocked)
            {
                HideAllMarkers();
                return;
            }

            RefreshMarkerTargets();
            EnsureMarkerInstanceCount(markerTargets.Count);
            UpdateMarkers();
        }

        private bool ValidateReferences()
        {
            TryAutoAssignReferences();

            bool valid = playerCamera != null &&
                         markerContainer != null &&
                         markerPrefab != null &&
                         shopScreenController != null;

            if (valid)
            {
                return true;
            }

            if (!hasLoggedMissingReferences)
            {
                hasLoggedMissingReferences = true;
                Debug.LogError("[TaskWallMarkerController] Missing references. Assign ShopScreenController, PlayerCamera, MarkerContainer and MarkerPrefab.", this);
            }

            return false;
        }

        private void TryAutoAssignReferences()
        {
            if (shopScreenController == null)
            {
                shopScreenController = FindBestShopScreenController();
            }

            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<TaskManager>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (markerPrefab == null)
            {
                Transform markerPrefabTransform = FindSceneTransformByName("MarkerPrefab");
                markerPrefab = markerPrefabTransform != null ? markerPrefabTransform as RectTransform : null;
            }

            if (markerContainer == null)
            {
                Transform markerContainerTransform = FindSceneTransformByName("TaskMarkes");
                markerContainer = markerContainerTransform != null ? markerContainerTransform as RectTransform : null;
            }

            if (markerContainer == null && markerPrefab != null && markerPrefab.parent is RectTransform parentRect)
            {
                markerContainer = parentRect;
            }
        }

        private void BuildMarkers()
        {
            ClearRuntimeMarkers();
            RefreshMarkerTargets();

            if (markerContainer == null || markerPrefab == null)
            {
                return;
            }

            markerPrefab.gameObject.SetActive(false);
            EnsureMarkerInstanceCount(markerTargets.Count);
        }

        private void RefreshMarkerTargets()
        {
            markerTargets.Clear();

            for (int i = 0; i < trackedTargets.Length; i++)
            {
                AddMarkerTargetIfNeeded(trackedTargets[i]);
            }

            if (!includeRuntimeTaskTargets)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            MonoBehaviour[] sceneBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            MonoBehaviour[] sceneBehaviours = FindObjectsOfType<MonoBehaviour>();
#endif

            for (int i = 0; i < sceneBehaviours.Length; i++)
            {
                if (sceneBehaviours[i] is ITaskMarkerTarget markerTarget)
                {
                    AddMarkerTargetIfNeeded(markerTarget);
                }
            }
        }

        private void AddMarkerTargetIfNeeded(ITaskMarkerTarget target)
        {
            if (target == null || IsUnityObjectMissing(target))
            {
                return;
            }

            for (int i = 0; i < markerTargets.Count; i++)
            {
                if (ReferenceEquals(markerTargets[i], target))
                {
                    return;
                }

                if (markerTargets[i] is UnityEngine.Object existingObject &&
                    target is UnityEngine.Object targetObject &&
                    existingObject == targetObject)
                {
                    return;
                }
            }

            markerTargets.Add(target);
        }

        private void EnsureMarkerInstanceCount(int targetCount)
        {
            if (markerContainer == null || markerPrefab == null)
            {
                return;
            }

            markerPrefab.gameObject.SetActive(false);

            while (markerInstances.Count < targetCount)
            {
                RectTransform markerInstance = Instantiate(markerPrefab, markerContainer, false);
                markerInstance.gameObject.name = $"TaskMarker_{markerInstances.Count + 1}";
                markerInstance.gameObject.SetActive(false);
                markerInstance.localScale = Vector3.one * markerScale;

                Image markerImage = markerInstance.GetComponent<Image>();
                if (markerImage != null)
                {
                    markerImage.color = markerColor;
                }

                markerInstances.Add(markerInstance);
            }

            while (markerInstances.Count > targetCount)
            {
                int lastIndex = markerInstances.Count - 1;
                RectTransform markerInstance = markerInstances[lastIndex];
                markerInstances.RemoveAt(lastIndex);

                if (markerInstance != null)
                {
                    Destroy(markerInstance.gameObject);
                }
            }
        }

        private void UpdateMarkers()
        {
            int targetCount = Mathf.Min(markerTargets.Count, markerInstances.Count);
            Rect rect = markerContainer.rect;
            float minX = -rect.width * 0.5f + edgePadding;
            float maxX = rect.width * 0.5f - edgePadding;
            float minY = -rect.height * 0.5f + edgePadding;
            float maxY = rect.height * 0.5f - edgePadding;

            for (int i = 0; i < targetCount; i++)
            {
                ITaskMarkerTarget target = markerTargets[i];
                RectTransform marker = markerInstances[i];

                if (target == null || IsUnityObjectMissing(target) || marker == null || !ShouldShowTarget(target))
                {
                    if (marker != null)
                    {
                        marker.gameObject.SetActive(false);
                    }

                    continue;
                }

                Vector3 viewport = playerCamera.WorldToViewportPoint(target.GetMarkerWorldPosition());
                if (viewport.z <= 0f)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                float x = (viewport.x - 0.5f) * rect.width;
                float y = (viewport.y - 0.5f) * rect.height;

                x = Mathf.Clamp(x, minX, maxX);
                y = Mathf.Clamp(y, minY, maxY);

                marker.anchoredPosition = new Vector2(x, y) + screenOffset;
                marker.localScale = Vector3.one * markerScale;
                marker.gameObject.SetActive(true);
            }
        }

        private bool ShouldShowTarget(ITaskMarkerTarget target)
        {
            if (target.IsUsed)
            {
                return false;
            }

            if (taskManager != null)
            {
                if (!taskManager.TryGetTaskState(target.TaskId, out TaskRuntimeState state) || state == null || !state.Active)
                {
                    return false;
                }

                return !hideCompletedTaskTargets || !state.Completed;
            }

            if (!hideCompletedTaskTargets)
            {
                return true;
            }

            return true;
        }

        private static bool IsUnityObjectMissing(ITaskMarkerTarget target)
        {
            return target is UnityEngine.Object unityObject && unityObject == null;
        }

        private void HideAllMarkers()
        {
            for (int i = 0; i < markerInstances.Count; i++)
            {
                RectTransform marker = markerInstances[i];
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private void SetContainerVisible(bool isVisible)
        {
            if (markerContainer != null && markerContainer.gameObject.activeSelf != isVisible)
            {
                markerContainer.gameObject.SetActive(isVisible);
            }
        }

        private void ClearRuntimeMarkers()
        {
            for (int i = 0; i < markerInstances.Count; i++)
            {
                if (markerInstances[i] != null)
                {
                    Destroy(markerInstances[i].gameObject);
                }
            }

            markerInstances.Clear();
        }

        private static Transform FindSceneTransformByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded ||
                    !string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private ShopScreenController FindBestShopScreenController()
        {
            ShopScreenController activeShop = ShopScreenController.ActiveInstance;
            if (activeShop != null && activeShop.HasItem(unlockItemId))
            {
                return activeShop;
            }

#if UNITY_2023_1_OR_NEWER
            ShopScreenController[] controllers = FindObjectsByType<ShopScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            ShopScreenController[] controllers = FindObjectsOfType<ShopScreenController>(true);
#endif
            ShopScreenController firstUsableController = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                ShopScreenController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                if (firstUsableController == null && controller.isActiveAndEnabled)
                {
                    firstUsableController = controller;
                }

                if (controller.isActiveAndEnabled && controller.HasItem(unlockItemId))
                {
                    return controller;
                }
            }

            for (int i = 0; i < controllers.Length; i++)
            {
                ShopScreenController controller = controllers[i];
                if (controller != null && controller.HasItem(unlockItemId))
                {
                    return controller;
                }
            }

            return firstUsableController;
        }
    }

    public static class TaskWallMarkerControllerInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene")
            {
                return;
            }

            if (FindControllerInstance() != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject("TaskWallMarkerController");
            controllerObject.AddComponent<TaskWallMarkerController>();
        }

        private static TaskWallMarkerController FindControllerInstance()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<TaskWallMarkerController>();
#else
            return Object.FindObjectOfType<TaskWallMarkerController>();
#endif
        }
    }
}
