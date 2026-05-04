using System.Collections;
using System.Collections.Generic;
using NightWatch.Foundation;
using UnityEngine;

namespace NightWatch.Tasks
{
    public interface ITaskMarkerTarget
    {
        string TaskId { get; }
        bool IsUsed { get; }
        Vector3 GetMarkerWorldPosition();
    }

    [DisallowMultipleComponent]
    public sealed class TaskTarget : MonoBehaviour, IInteractable, ITaskMarkerTarget
    {
        [Header("Task")]
        [SerializeField] private string taskId;
        [SerializeField] private ToolType requiredTool = ToolType.TrashBag;
        [SerializeField] private bool allowAnyTool;
        [SerializeField] [Min(1)] private int progressValue = 1;
        [SerializeField] private string promptText = "UZYJ";
        [SerializeField] private string invalidPromptTemplate = "WYMAGANY: {0}";
        [SerializeField] [Min(0f)] private float interactionDurationSeconds = 0f;
        [SerializeField] private string cleaningPromptTemplate = "ZMYWANIE... {0}%";
        [SerializeField] private Renderer[] fadeRenderers = new Renderer[0];
        [SerializeField] private bool disableCollidersWhileCleaning = true;

        [Header("Behavior")]
        [SerializeField] private bool disableOnUse = true;
        [SerializeField] private ParticleSystem[] particlesToStop;
        [SerializeField] private Behaviour[] behavioursToDisable;
        [SerializeField] private GameObject[] objectsToDisable;
        [SerializeField] private GameObject[] objectsToEnable;

        [Header("Marker")]
        [SerializeField] private Transform markerAnchor;
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.6f, 0f);

        [Header("References")]
        [SerializeField] private TaskManager taskManager;

        private const string CampfireTaskId = "campfire";
        private const float CampfireFireVisualSearchRadius = 8f;
        private const float CampfireExtinguishDurationSeconds = 1.8f;

        private sealed class FadeEntry
        {
            public Material material;
            public string colorProperty;
            public Color initialColor;
        }

        private readonly List<FadeEntry> fadeEntries = new List<FadeEntry>();
        private bool hasBeenUsed;
        private bool isCleaning;
        private bool hasLoggedMissingTaskManager;
        private float cleaningProgress01;
        private bool hasPreparedFadeEntries;

        public string TaskId => taskId;
        public bool IsUsed => hasBeenUsed;

        private void Awake()
        {
            ResolveTaskManagerIfNeeded();
        }

        public string GetPromptText(ToolType currentTool)
        {
            if (!IsTaskActive())
            {
                return string.Empty;
            }

            if (hasBeenUsed)
            {
                return "WYKONANO";
            }

            if (isCleaning)
            {
                int percent = Mathf.RoundToInt(cleaningProgress01 * 100f);
                return IsCampfireTask() ? $"GASZENIE... {percent}%" : string.Format(cleaningPromptTemplate, percent);
            }

            if (allowAnyTool || currentTool == requiredTool)
            {
                return promptText;
            }

            return string.Format(invalidPromptTemplate, requiredTool);
        }

        public bool CanInteract(ToolType currentTool)
        {
            return IsTaskActive() && !hasBeenUsed && !isCleaning && (allowAnyTool || currentTool == requiredTool);
        }

        public void Interact(ToolType currentTool)
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
                    Debug.LogError($"[TaskTarget] Missing TaskManager reference on '{name}'.", this);
                }

                return;
            }

            if (IsCampfireTask())
            {
                StartCoroutine(RunCampfireExtinguishInteraction());
                return;
            }

            if (interactionDurationSeconds > 0f)
            {
                StartCoroutine(RunCleaningInteraction());
                return;
            }

            CompleteInteraction();
        }

        public void ConfigureTask(string newTaskId, ToolType newRequiredTool, int newProgressValue, string newPromptText)
        {
            taskId = string.IsNullOrWhiteSpace(newTaskId) ? taskId : newTaskId;
            requiredTool = newRequiredTool;
            progressValue = Mathf.Max(1, newProgressValue);

            if (!string.IsNullOrWhiteSpace(newPromptText))
            {
                promptText = newPromptText;
            }
        }

        public void ConfigureCleaning(float durationSeconds, Renderer[] newFadeRenderers)
        {
            interactionDurationSeconds = Mathf.Max(0f, durationSeconds);
            fadeRenderers = newFadeRenderers ?? System.Array.Empty<Renderer>();
            hasPreparedFadeEntries = false;
            fadeEntries.Clear();
        }

        public void SetDisableOnUse(bool shouldDisableOnUse)
        {
            disableOnUse = shouldDisableOnUse;
        }

        public void SetAllowAnyTool(bool shouldAllowAnyTool)
        {
            allowAnyTool = shouldAllowAnyTool;
        }

        private IEnumerator RunCleaningInteraction()
        {
            isCleaning = true;
            cleaningProgress01 = 0f;
            PrepareFadeEntriesIfNeeded();
            SetInteractionCollidersEnabled(false);

            float duration = Mathf.Max(0.01f, interactionDurationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cleaningProgress01 = Mathf.Clamp01(elapsed / duration);
                ApplyFade(1f - cleaningProgress01);
                yield return null;
            }

            ApplyFade(0f);
            cleaningProgress01 = 1f;
            isCleaning = false;
            SetInteractionCollidersEnabled(true);
            CompleteInteraction();
        }

        private IEnumerator RunCampfireExtinguishInteraction()
        {
            isCleaning = true;
            cleaningProgress01 = 0f;
            SetInteractionCollidersEnabled(false);
            NightWatch.Items.ToolUseAnimationEvents.PlayBucketPour(transform.position + Vector3.up * 0.35f, CampfireExtinguishDurationSeconds);

            float elapsed = 0f;
            while (elapsed < CampfireExtinguishDurationSeconds)
            {
                elapsed += Time.deltaTime;
                cleaningProgress01 = Mathf.Clamp01(elapsed / CampfireExtinguishDurationSeconds);
                yield return null;
            }

            cleaningProgress01 = 1f;
            isCleaning = false;
            SetInteractionCollidersEnabled(true);
            CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            taskManager.ReportProgress(taskId, progressValue);
            DisableCampfireFireVisualIfNeeded();
            StopParticles(particlesToStop);
            SetBehavioursEnabled(behavioursToDisable, false);
            SetObjectsActive(objectsToDisable, false);
            SetObjectsActive(objectsToEnable, true);

            hasBeenUsed = true;
            isCleaning = false;
            cleaningProgress01 = 1f;

            if (disableOnUse)
            {
                gameObject.SetActive(false);
            }
        }

        private void DisableCampfireFireVisualIfNeeded()
        {
            if (!IsCampfireTask())
            {
                return;
            }

            Transform fireVisual = FindNearestCampfireFireVisual();
            if (fireVisual == null)
            {
                Debug.LogWarning($"[TaskTarget] Campfire '{name}' was extinguished, but no nearby VFX_FullOpaqueFire visual was found.", this);
                return;
            }

            DisableFireVisual(fireVisual.gameObject);
        }

        private Transform FindNearestCampfireFireVisual()
        {
            Transform[] transforms = FindSceneTransforms();
            Transform best = null;
            float bestSqrDistance = CampfireFireVisualSearchRadius * CampfireFireVisualSearchRadius;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == transform || candidate.GetComponent<TaskTarget>() != null)
                {
                    continue;
                }

                if (!IsCampfireFireVisual(candidate))
                {
                    continue;
                }

                float sqrDistance = (candidate.position - transform.position).sqrMagnitude;
                if (sqrDistance <= bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsCampfireFireVisual(Transform candidate)
        {
            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("vfx_fullopaquefire") || lowerName.Contains("fullopaquefire"))
            {
                return true;
            }

            MonoBehaviour[] behaviours = candidate.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().Name.Contains("VFX_FireController"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DisableFireVisual(GameObject fireVisual)
        {
            if (fireVisual == null)
            {
                return;
            }

            ParticleSystem[] particles = fireVisual.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particles[i].Clear(true);
                }
            }

            Light[] lights = fireVisual.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].enabled = false;
                }
            }

            Renderer[] renderers = fireVisual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }

            fireVisual.SetActive(false);
        }

        private bool IsCampfireTask()
        {
            return string.Equals(taskId, CampfireTaskId, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTaskActive()
        {
            ResolveTaskManagerIfNeeded();
            return taskManager == null ||
                   (taskManager.TryGetTaskState(taskId, out TaskRuntimeState state) && state != null && state.Active);
        }

        private void ResolveTaskManagerIfNeeded()
        {
            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<TaskManager>();
            }
        }

        public Vector3 GetMarkerWorldPosition()
        {
            Transform anchor = markerAnchor != null ? markerAnchor : transform;
            return anchor.position + markerOffset;
        }

        private void PrepareFadeEntriesIfNeeded()
        {
            if (hasPreparedFadeEntries)
            {
                return;
            }

            fadeEntries.Clear();

            Renderer[] renderersToFade = fadeRenderers;
            if (renderersToFade == null || renderersToFade.Length == 0)
            {
                renderersToFade = GetComponentsInChildren<Renderer>(true);
            }

            for (int rendererIndex = 0; rendererIndex < renderersToFade.Length; rendererIndex++)
            {
                Renderer renderer = renderersToFade[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    string colorProperty = GetColorPropertyName(material);
                    if (string.IsNullOrEmpty(colorProperty))
                    {
                        continue;
                    }

                    FadeEntry entry = new FadeEntry
                    {
                        material = material,
                        colorProperty = colorProperty,
                        initialColor = material.GetColor(colorProperty)
                    };
                    fadeEntries.Add(entry);
                }
            }

            hasPreparedFadeEntries = true;
        }

        private void ApplyFade(float alphaMultiplier)
        {
            float clampedAlpha = Mathf.Clamp01(alphaMultiplier);

            for (int i = 0; i < fadeEntries.Count; i++)
            {
                FadeEntry entry = fadeEntries[i];
                if (entry == null || entry.material == null || string.IsNullOrEmpty(entry.colorProperty))
                {
                    continue;
                }

                Color color = entry.initialColor;
                color.a = entry.initialColor.a * clampedAlpha;
                entry.material.SetColor(entry.colorProperty, color);
            }
        }

        private void SetInteractionCollidersEnabled(bool isEnabled)
        {
            if (!disableCollidersWhileCleaning)
            {
                return;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = isEnabled;
                }
            }
        }

        private static string GetColorPropertyName(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return "_BaseColor";
            }

            if (material.HasProperty("_Color"))
            {
                return "_Color";
            }

            return string.Empty;
        }

        private static void StopParticles(ParticleSystem[] targets)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static void SetBehavioursEnabled(Behaviour[] targets, bool enabledState)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].enabled = enabledState;
                }
            }
        }

        private static void SetObjectsActive(GameObject[] targets, bool activeState)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetActive(activeState);
                }
            }
        }

        private static Transform[] FindSceneTransforms()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<Transform>();
#endif
        }

        private void OnValidate()
        {
            if (progressValue < 1)
            {
                progressValue = 1;
            }

            if (interactionDurationSeconds < 0f)
            {
                interactionDurationSeconds = 0f;
            }
        }
    }
}
