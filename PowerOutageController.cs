using System.Collections.Generic;
using NightWatch.Foundation;
using NightWatch.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class PowerOutageController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string targetSceneName = "SampleScene";

        [Header("Outage")]
        [SerializeField] [Range(0, 23)] private int outageHour = 1;
        [SerializeField] [Range(0, 59)] private int outageMinute = 0;
        [SerializeField] private string electricBoxName = "WOC_Ct_Items_ElectricBox_Pre";
        [SerializeField] private string electricityTaskId = "electricity";
        [SerializeField] private string electricityTaskDisplayName = "PRAD";
        [SerializeField] private GameObject electricBoxPrefab;
        [SerializeField] private Vector3 fallbackElectricBoxPosition = new Vector3(558.3621f, 71.96485f, 554.0212f);
        [SerializeField] private Vector3 fallbackElectricBoxEuler = new Vector3(0f, 180f, 0f);
        [SerializeField] private Vector3 fallbackElectricBoxScale = new Vector3(1.5f, 1f, 1f);

        private const string ElectricBoxPrefabPath = "Assets/WOC/Prefab/City/Items/WOC_Ct_Items_ElectricBox_Pre.prefab";
        private const int InteractableLayer = 6;

        private readonly List<StreetLightEntry> streetLights = new List<StreetLightEntry>();
        private readonly List<StreetLightRendererEntry> streetLightRenderers = new List<StreetLightRendererEntry>();
        private TimeManager timeManager;
        private TaskManager taskManager;
        private ElectricBoxInteractable electricBoxInteractable;
        private bool outageStarted;
        private bool repaired;

        public bool IsOutageActive => outageStarted && !repaired;
        public bool IsRepaired => repaired;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != targetSceneName)
            {
                enabled = false;
                return;
            }

            timeManager = FindFirstObjectByType<TimeManager>();
            taskManager = FindFirstObjectByType<TaskManager>();
            if (timeManager != null)
            {
                timeManager.OnTimeChanged += HandleTimeChanged;
            }

            RefreshStreetLights();
            SetupElectricBox();
        }

        private void OnDestroy()
        {
            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
            }
        }

        private void Update()
        {
            if (timeManager == null || outageStarted || repaired)
            {
                return;
            }

            if (HasReachedOutageTime(timeManager.FormattedTime))
            {
                StartOutage();
            }
        }

        public void RepairPower()
        {
            if (!IsOutageActive)
            {
                return;
            }

            repaired = true;
            SetStreetLightsEnabled(true);
        }

        public void ResetForNewNight()
        {
            outageStarted = false;
            repaired = false;
            RefreshStreetLights();
            SetStreetLightsEnabled(true);
            SetupElectricBox();
        }

        private void HandleTimeChanged(string formattedTime, float normalizedProgress)
        {
            _ = normalizedProgress;

            if (outageStarted || repaired || !HasReachedOutageTime(formattedTime))
            {
                return;
            }

            StartOutage();
        }

        private void StartOutage()
        {
            outageStarted = true;
            ActivateElectricityTask();
            RefreshStreetLights();
            SetStreetLightsEnabled(false);
            Debug.Log($"[PowerOutageController] Outage started. Disabled {streetLights.Count} Light components and dimmed {streetLightRenderers.Count} light renderers.", this);
        }

        private void ActivateElectricityTask()
        {
            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<TaskManager>();
            }

            if (taskManager == null)
            {
                return;
            }

            taskManager.EnsureRuntimeTask(electricityTaskId, electricityTaskDisplayName, 1, ToolType.Key, TaskType.Repair);
            taskManager.ActivateTask(electricityTaskId, true);
        }

        private bool HasReachedOutageTime(string formattedTime)
        {
            if (string.IsNullOrWhiteSpace(formattedTime))
            {
                return false;
            }

            string[] parts = formattedTime.Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int hour) ||
                !int.TryParse(parts[1], out int minute))
            {
                return false;
            }

            int current = hour * 60 + minute;
            int target = outageHour * 60 + outageMinute;
            return current >= target && hour < 6;
        }

        private void RefreshStreetLights()
        {
            streetLights.Clear();
            streetLightRenderers.Clear();

#if UNITY_2023_1_OR_NEWER
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            Light[] lights = FindObjectsOfType<Light>();
#endif

            for (int i = 0; i < lights.Length; i++)
            {
                Light lightSource = lights[i];
                if (!IsStreetLight(lightSource))
                {
                    continue;
                }

                streetLights.Add(new StreetLightEntry(lightSource, FindVisualRenderers(lightSource)));
            }

            HashSet<Renderer> seenRenderers = new HashSet<Renderer>();
#if UNITY_2023_1_OR_NEWER
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            Renderer[] renderers = FindObjectsOfType<Renderer>();
#endif
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || seenRenderers.Contains(renderer) || !IsStreetLightRenderer(renderer))
                {
                    continue;
                }

                seenRenderers.Add(renderer);
                streetLightRenderers.Add(new StreetLightRendererEntry(renderer));
            }
        }

        private static bool IsStreetLight(Light lightSource)
        {
            if (lightSource == null || lightSource.type == LightType.Directional)
            {
                return false;
            }

            string path = BuildTransformPath(lightSource.transform).ToLowerInvariant();
            if (IsExcludedLightPath(path))
            {
                return false;
            }

            return path.Contains("microavl") ||
                   path.Contains("street") ||
                   path.Contains("lamp") ||
                   path.Contains("latarnia") ||
                   path.Contains("woc_ct_items_light") ||
                   path.Contains("point light") ||
                   lightSource.intensity >= 20f;
        }

        private static bool IsStreetLightRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            string path = BuildTransformPath(renderer.transform).ToLowerInvariant();
            if (IsExcludedLightPath(path))
            {
                return false;
            }

            if (path.Contains("microavl") || path.Contains("woc_ct_items_light"))
            {
                return true;
            }

            bool streetLightPath =
                path.Contains("street_lamp") ||
                path.Contains("street lamp") ||
                path.Contains("latarnia") ||
                path.Contains("lamp_") ||
                path.Contains("/lamp") ||
                path.EndsWith("lamp");

            return streetLightPath && HasLightLikeMaterial(renderer);
        }

        private static bool HasLightLikeMaterial(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                string materialName = material.name.ToLowerInvariant();
                if (materialName.Contains("light") ||
                    materialName.Contains("glow") ||
                    materialName.Contains("lamp") ||
                    materialName.Contains("lantern"))
                {
                    return true;
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    Color emission = material.GetColor("_EmissionColor");
                    if (Mathf.Max(emission.r, emission.g, emission.b) > 0.05f)
                    {
                        return true;
                    }
                }

                if (material.HasProperty("_Intensity") && material.GetFloat("_Intensity") > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExcludedLightPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            return path.Contains("player") ||
                   path.Contains("camera") ||
                   path.Contains("flashlight") ||
                   path.Contains("hand") ||
                   path.Contains("_held") ||
                   path.Contains("electricbox") ||
                   path.Contains("electric box") ||
                   path.Contains("runtimegraffiti");
        }

        private static Renderer[] FindVisualRenderers(Light lightSource)
        {
            if (lightSource == null)
            {
                return System.Array.Empty<Renderer>();
            }

            Transform root = lightSource.transform;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                return renderers;
            }

            return System.Array.Empty<Renderer>();
        }

        private void SetStreetLightsEnabled(bool enabledState)
        {
            if (enabledState)
            {
                for (int i = 0; i < streetLightRenderers.Count; i++)
                {
                    streetLightRenderers[i]?.SetEnabled(true);
                }

                for (int i = 0; i < streetLights.Count; i++)
                {
                    streetLights[i]?.SetEnabled(true);
                }

                return;
            }

            for (int i = 0; i < streetLights.Count; i++)
            {
                streetLights[i]?.SetEnabled(false);
            }

            for (int i = 0; i < streetLightRenderers.Count; i++)
            {
                streetLightRenderers[i]?.SetEnabled(false);
            }
        }

        private void SetupElectricBox()
        {
            GameObject electricBox = FindElectricBox();
            if (electricBox == null)
            {
                electricBox = SpawnFallbackElectricBox();
            }

            if (electricBox == null)
            {
                Debug.LogWarning("[PowerOutageController] Electric box was not found and could not be spawned.", this);
                return;
            }

            electricBox.name = electricBoxName;
            SetLayerRecursively(electricBox.transform, InteractableLayer);
            EnsureCollider(electricBox);

            electricBoxInteractable = electricBox.GetComponent<ElectricBoxInteractable>();
            if (electricBoxInteractable == null)
            {
                electricBoxInteractable = electricBox.AddComponent<ElectricBoxInteractable>();
            }

            electricBoxInteractable.Configure(this);
        }

        private GameObject FindElectricBox()
        {
#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            Transform[] transforms = FindObjectsOfType<Transform>();
#endif

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                {
                    continue;
                }

                if (NameMatches(candidate.name, electricBoxName))
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private GameObject SpawnFallbackElectricBox()
        {
            GameObject prefab = ResolveElectricBoxPrefab();
            GameObject electricBox = prefab != null
                ? Instantiate(prefab, fallbackElectricBoxPosition, Quaternion.Euler(fallbackElectricBoxEuler))
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            electricBox.transform.SetPositionAndRotation(fallbackElectricBoxPosition, Quaternion.Euler(fallbackElectricBoxEuler));
            electricBox.transform.localScale = fallbackElectricBoxScale;
            return electricBox;
        }

        private GameObject ResolveElectricBoxPrefab()
        {
            if (electricBoxPrefab != null)
            {
                return electricBoxPrefab;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(ElectricBoxPrefabPath);
#else
            return null;
#endif
        }

        private static void EnsureCollider(GameObject target)
        {
            if (target.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.size = new Vector3(1.2f, 1.8f, 0.6f);
        }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            if (node == null)
            {
                return;
            }

            node.gameObject.layer = layer;

            for (int i = 0; i < node.childCount; i++)
            {
                SetLayerRecursively(node.GetChild(i), layer);
            }
        }

        private static bool NameMatches(string candidateName, string expectedBaseName)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(expectedBaseName))
            {
                return false;
            }

            if (candidateName.Equals(expectedBaseName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return candidateName.StartsWith(expectedBaseName + " (", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }

        private sealed class StreetLightEntry
        {
            private readonly Light lightSource;
            private readonly Transform lightTransform;
            private readonly bool originalLightEnabled;
            private readonly Vector3 originalWorldPosition;
            private readonly Quaternion originalWorldRotation;
            private readonly Vector3 originalLocalScale;

            public StreetLightEntry(Light newLightSource, Renderer[] newRenderers)
            {
                _ = newRenderers;
                lightSource = newLightSource;
                lightTransform = lightSource != null ? lightSource.transform : null;
                originalLightEnabled = lightSource != null && lightSource.enabled;
                originalWorldPosition = lightTransform != null ? lightTransform.position : Vector3.zero;
                originalWorldRotation = lightTransform != null ? lightTransform.rotation : Quaternion.identity;
                originalLocalScale = lightTransform != null ? lightTransform.localScale : Vector3.one;
            }

            public void SetEnabled(bool enabledState)
            {
                if (lightTransform != null && enabledState)
                {
                    lightTransform.SetPositionAndRotation(originalWorldPosition, originalWorldRotation);
                    lightTransform.localScale = originalLocalScale;
                }

                if (lightSource != null)
                {
                    lightSource.enabled = enabledState && originalLightEnabled;
                }
            }
        }

        private sealed class StreetLightRendererEntry
        {
            private sealed class MaterialState
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

            private readonly Renderer renderer;
            private readonly bool originalRendererEnabled;
            private readonly MaterialState[] materialStates;

            public StreetLightRendererEntry(Renderer newRenderer)
            {
                renderer = newRenderer;
                originalRendererEnabled = renderer != null && renderer.enabled;
                materialStates = CaptureMaterialStates(renderer);
            }

            public void SetEnabled(bool enabledState)
            {
                if (renderer == null)
                {
                    return;
                }

                RestoreMaterials(enabledState);
                renderer.enabled = originalRendererEnabled;
            }

            private void RestoreMaterials(bool enabledState)
            {
                for (int i = 0; i < materialStates.Length; i++)
                {
                    MaterialState state = materialStates[i];
                    if (state == null || state.material == null)
                    {
                        continue;
                    }

                    float multiplier = enabledState ? 1f : 0f;

                    if (state.hasEmissionColor)
                    {
                        state.material.SetColor("_EmissionColor", state.emissionColor * multiplier);
                    }

                    if (state.hasIntensity)
                    {
                        state.material.SetFloat("_Intensity", state.intensity * multiplier);
                    }
                }
            }

            private static MaterialState[] CaptureMaterialStates(Renderer renderer)
            {
                if (renderer == null)
                {
                    return System.Array.Empty<MaterialState>();
                }

                Material[] materials = renderer.materials;
                MaterialState[] states = new MaterialState[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    MaterialState state = new MaterialState
                    {
                        material = material,
                        hasColor = material.HasProperty("_Color"),
                        hasBaseColor = material.HasProperty("_BaseColor"),
                        hasEmissionColor = material.HasProperty("_EmissionColor"),
                        hasIntensity = material.HasProperty("_Intensity")
                    };

                    state.color = state.hasColor ? material.GetColor("_Color") : Color.white;
                    state.baseColor = state.hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
                    state.emissionColor = state.hasEmissionColor ? material.GetColor("_EmissionColor") : Color.black;
                    state.intensity = state.hasIntensity ? material.GetFloat("_Intensity") : 0f;
                    states[i] = state;
                }

                return states;
            }
        }
    }

    public static class PowerOutageControllerInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForActiveScene()
        {
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = mode;
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid() || scene.name != "SampleScene")
            {
                return;
            }

            if (FindControllerInstance() != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject("PowerOutageController");
            controllerObject.AddComponent<PowerOutageController>();
        }

        private static PowerOutageController FindControllerInstance()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<PowerOutageController>();
#else
            return UnityEngine.Object.FindObjectOfType<PowerOutageController>();
#endif
        }
    }
}
