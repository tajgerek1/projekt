using System.Collections;
using System.Collections.Generic;
using NightWatch.Foundation;
using NightWatch.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class GraffitiCleanupInteractable : MonoBehaviour, IInteractable, ITaskMarkerTarget
    {
        [Header("Task")]
        [SerializeField] private string taskId = "graffiti";
        [SerializeField] private ToolType requiredTool = ToolType.Chainsaw;
        [SerializeField] [Min(1)] private int progressValue = 1;

        [Header("Prompt")]
        [SerializeField] private string promptText = "ZMYJ GRAFFITI";
        [SerializeField] private string invalidPromptTemplate = "WYMAGANY: {0}";
        [SerializeField] private string cleaningPromptTemplate = "ZMYWANIE... {0}%";
        [SerializeField] private string completedPromptText = "WYCZYSZCZONE";

        [Header("Cleaning")]
        [SerializeField] [Min(0.1f)] private float cleanDurationSeconds = 10f;
        [SerializeField] private Renderer[] fadeRenderers = new Renderer[0];

        [Header("Marker")]
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.2f, 0f);

        [Header("References")]
        [SerializeField] private TaskManager taskManager;

        private sealed class FadeEntry
        {
            public Material material;
            public Color color;
            public Color baseColor;
            public Color emissionColor;
            public float intensity;
            public bool hasColor;
            public bool hasBaseColor;
            public bool hasEmissionColor;
            public bool hasIntensity;
        }

        private readonly List<FadeEntry> fadeEntries = new List<FadeEntry>();
        private bool hasBeenUsed;
        private bool isCleaning;
        private bool fadePrepared;
        private float cleaningProgress01;
        private Coroutine cleaningRoutine;

        public string TaskId => taskId;
        public bool IsUsed => hasBeenUsed;

        private void Awake()
        {
            ResolveTaskManagerIfNeeded();
        }

        public void Configure(string newTaskId, ToolType newRequiredTool, int newProgressValue, string newPromptText, float newCleanDurationSeconds, Renderer newFadeRenderer)
        {
            taskId = string.IsNullOrWhiteSpace(newTaskId) ? taskId : newTaskId;
            requiredTool = newRequiredTool;
            progressValue = Mathf.Max(1, newProgressValue);
            cleanDurationSeconds = Mathf.Max(0.1f, newCleanDurationSeconds);
            fadeRenderers = newFadeRenderer != null ? new[] { newFadeRenderer } : System.Array.Empty<Renderer>();

            if (!string.IsNullOrWhiteSpace(newPromptText))
            {
                promptText = newPromptText;
            }

            fadePrepared = false;
            fadeEntries.Clear();
            PrepareFadeEntriesIfNeeded();
            ApplyFade(1f);
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

            if (isCleaning)
            {
                int percent = Mathf.RoundToInt(cleaningProgress01 * 100f);
                return string.Format(cleaningPromptTemplate, percent);
            }

            if (currentTool == requiredTool)
            {
                return promptText;
            }

            return string.Format(invalidPromptTemplate, requiredTool);
        }

        public bool CanInteract(ToolType currentTool)
        {
            return IsTaskActive() && !hasBeenUsed && !isCleaning && currentTool == requiredTool;
        }

        public void Interact(ToolType currentTool)
        {
            if (!CanInteract(currentTool))
            {
                return;
            }

            if (cleaningRoutine != null)
            {
                StopCoroutine(cleaningRoutine);
            }

            cleaningRoutine = StartCoroutine(RunCleaning());
        }

        public Vector3 GetMarkerWorldPosition()
        {
            return transform.position + markerOffset;
        }

        private IEnumerator RunCleaning()
        {
            ResolveTaskManagerIfNeeded();
            isCleaning = true;
            cleaningProgress01 = 0f;
            PrepareFadeEntriesIfNeeded();

            float duration = Mathf.Max(0.1f, cleanDurationSeconds);
            NightWatch.Items.ToolUseAnimationEvents.PlayGraffitiScrub(transform, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cleaningProgress01 = Mathf.Clamp01(elapsed / duration);
                ApplyFade(1f - cleaningProgress01);
                yield return null;
            }

            CompleteCleaning();
        }

        private void CompleteCleaning()
        {
            cleaningRoutine = null;
            hasBeenUsed = true;
            isCleaning = false;
            cleaningProgress01 = 1f;
            ApplyFade(0f);

            SetRenderersEnabled(false);
            SetCollidersEnabled(false);

            if (taskManager != null)
            {
                taskManager.ReportProgress(taskId, progressValue);
            }
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

        private void PrepareFadeEntriesIfNeeded()
        {
            if (fadePrepared)
            {
                return;
            }

            fadeEntries.Clear();
            Renderer[] renderers = fadeRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
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

                    ConfigureTransparentMaterial(material);
                    FadeEntry entry = new FadeEntry
                    {
                        material = material,
                        hasColor = material.HasProperty("_Color"),
                        hasBaseColor = material.HasProperty("_BaseColor"),
                        hasEmissionColor = material.HasProperty("_EmissionColor"),
                        hasIntensity = material.HasProperty("_Intensity")
                    };

                    entry.color = entry.hasColor ? material.GetColor("_Color") : Color.white;
                    entry.baseColor = entry.hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
                    entry.emissionColor = entry.hasEmissionColor ? material.GetColor("_EmissionColor") : Color.black;
                    entry.intensity = entry.hasIntensity ? material.GetFloat("_Intensity") : 0f;
                    fadeEntries.Add(entry);
                }
            }

            fadePrepared = true;
        }

        private void ApplyFade(float alphaMultiplier)
        {
            float alpha = Mathf.Clamp01(alphaMultiplier);

            for (int i = 0; i < fadeEntries.Count; i++)
            {
                FadeEntry entry = fadeEntries[i];
                if (entry == null || entry.material == null)
                {
                    continue;
                }

                if (entry.hasColor)
                {
                    Color color = entry.color;
                    color.a *= alpha;
                    entry.material.SetColor("_Color", color);
                }

                if (entry.hasBaseColor)
                {
                    Color color = entry.baseColor;
                    color.a *= alpha;
                    entry.material.SetColor("_BaseColor", color);
                }

                if (entry.hasEmissionColor)
                {
                    entry.material.SetColor("_EmissionColor", entry.emissionColor * alpha);
                }

                if (entry.hasIntensity)
                {
                    entry.material.SetFloat("_Intensity", entry.intensity * alpha);
                }
            }
        }

        private void SetRenderersEnabled(bool enabledState)
        {
            Renderer[] renderers = fadeRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabledState;
                }
            }
        }

        private void SetCollidersEnabled(bool enabledState)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabledState;
                }
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void OnValidate()
        {
            if (progressValue < 1)
            {
                progressValue = 1;
            }

            if (cleanDurationSeconds < 0.1f)
            {
                cleanDurationSeconds = 0.1f;
            }
        }
    }
}
