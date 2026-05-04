using System.Collections.Generic;
using System.Collections;
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
    public sealed class SpilledTrashCleanupBootstrap : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string targetSceneName = "SampleScene";

        [Header("Trash Can")]
        [SerializeField] private string normalTrashCanName = "WOC_Ct_Items_GarbageCan_02_Pre";
        [SerializeField] private GameObject normalTrashCanPrefab;
        [SerializeField] private Vector3 overturnedPositionOffset = new Vector3(0f, 0.435f, 0f);
        [SerializeField] private Vector3 overturnedEulerOffset = new Vector3(0f, 30f, 90f);
        [SerializeField] [Min(0.05f)] private float holdToLiftSeconds = 5f;
        [SerializeField] [Range(0.05f, 1f)] private float trashCanSelectionRatio = 0.5f;

        [Header("Trash")]
        [SerializeField] private GameObject[] trashPrefabs = new GameObject[0];
        [SerializeField] private Vector3 trashScale = Vector3.one;

        [Header("Task")]
        [SerializeField] private string taskId = "trash";
        [SerializeField] private ToolType requiredTool = ToolType.TrashBag;
        [SerializeField] private string collectPromptText = "ZBIERZ SMIECI";
        [SerializeField] private string liftCanPromptText = "PODNIES KOSZ";

        private const string RootObjectName = "RuntimeSpilledTrashCleanup";
        private const string NormalTrashCanPrefabPath = "Assets/WOC/Prefab/City/Items/WOC_Ct_Items_GarbageCan_02_Pre.prefab";
        private const int InteractableLayer = 6;

        private static readonly string[] TrashPrefabPaths =
        {
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Bottles/Crushed/Prefab_CoffeeCup_Crushed.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Bottles/Crushed/Prefab_SodaCan_Crushed1.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Bottles/Crushed/Prefab_SodaCan_Crushed2.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Bottles/Crushed/Prefab_WaterBottle_S_Crushed.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Bottles/Prefab_SodaCup.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/FooDContainer/Prefab_SaiceCup.prefab",
            "Assets/URP_WasteOvergrowth_SA/Prefabs/Trashbag/Prefab_Trashbag_Spilled.prefab"
        };

        private static readonly Vector3[] TrashOffsets =
        {
            new Vector3(0.95f, 0.02f, -0.75f),
            new Vector3(1.45f, 0.02f, -0.05f),
            new Vector3(0.65f, 0.02f, 0.65f),
            new Vector3(-0.25f, 0.02f, 0.85f),
            new Vector3(-0.95f, 0.02f, 0.2f),
            new Vector3(-0.65f, 0.02f, -0.75f),
            new Vector3(0.2f, 0.02f, -1.25f)
        };

        private static readonly Vector3[] TrashEulerAngles =
        {
            new Vector3(0f, 25f, 82f),
            new Vector3(8f, 210f, 78f),
            new Vector3(0f, 120f, 90f),
            new Vector3(0f, 280f, 76f),
            new Vector3(10f, 340f, 86f),
            new Vector3(0f, 165f, 92f),
            new Vector3(0f, 65f, 0f)
        };

        private bool hasSpawned;

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != targetSceneName)
            {
                yield break;
            }

            yield return null;
            SpawnCleanupIfNeeded();
        }

        private void SpawnCleanupIfNeeded()
        {
            if (hasSpawned || GameObject.Find(RootObjectName) != null)
            {
                hasSpawned = true;
                return;
            }

            if (!IsTrashTaskActive())
            {
                hasSpawned = true;
                return;
            }

            GameObject[] normalTrashCans = FindNormalTrashCans();
            normalTrashCans = SelectRandomSubset(normalTrashCans, trashCanSelectionRatio);
            if (normalTrashCans.Length == 0)
            {
                Debug.LogWarning("[SpilledTrashCleanupBootstrap] No normal trash cans found to overturn.", this);
                hasSpawned = true;
                return;
            }

            GameObject root = new GameObject(RootObjectName);
            GameObject[] resolvedTrashPrefabs = ResolveTrashPrefabs();
            GameObject resolvedTrashCanPrefab = ResolveNormalTrashCanPrefab();
            List<GameObject> availableOverturnedTrashCans = FindExistingOverturnedTrashCans();

            SetTrashTaskRequiredCount(normalTrashCans.Length * (TrashOffsets.Length + 1));

            for (int i = 0; i < normalTrashCans.Length; i++)
            {
                GameObject normalTrashCan = normalTrashCans[i];
                if (normalTrashCan == null)
                {
                    continue;
                }

                Transform group = new GameObject($"SpilledTrashCleanup_{i + 1}").transform;
                group.SetParent(root.transform, false);

                GameObject overturnedTrashCan = SpawnOverturnedTrashCan(group, normalTrashCan, resolvedTrashCanPrefab, availableOverturnedTrashCans);
                Transform trashOrigin = overturnedTrashCan != null ? overturnedTrashCan.transform : normalTrashCan.transform;
                SpawnTrash(group, trashOrigin.position, trashOrigin.eulerAngles, resolvedTrashPrefabs, i);
                normalTrashCan.SetActive(false);
            }

            hasSpawned = true;
        }

        private GameObject[] FindNormalTrashCans()
        {
            List<GameObject> trashCans = new List<GameObject>();
            Transform[] sceneTransforms = FindSceneTransforms();

            for (int i = 0; i < sceneTransforms.Length; i++)
            {
                Transform candidate = sceneTransforms[i];
                if (candidate == null || candidate == transform)
                {
                    continue;
                }

                string candidateName = candidate.gameObject.name;
                if (!NameMatches(candidateName, normalTrashCanName) || candidateName.Contains("_overturn"))
                {
                    continue;
                }

                trashCans.Add(candidate.gameObject);
            }

            return trashCans.ToArray();
        }

        private static GameObject[] SelectRandomSubset(GameObject[] source, float ratio)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<GameObject>();
            }

            List<GameObject> candidates = new List<GameObject>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    candidates.Add(source[i]);
                }
            }

            Shuffle(candidates);
            int targetCount = Mathf.Clamp(Mathf.CeilToInt(candidates.Count * Mathf.Clamp01(ratio)), 1, candidates.Count);
            if (targetCount >= candidates.Count)
            {
                return candidates.ToArray();
            }

            candidates.RemoveRange(targetCount, candidates.Count - targetCount);
            return candidates.ToArray();
        }

        private static void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                T temporaryValue = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temporaryValue;
            }
        }

        private List<GameObject> FindExistingOverturnedTrashCans()
        {
            List<GameObject> trashCans = new List<GameObject>();
            Transform[] sceneTransforms = FindSceneTransforms();

            for (int i = 0; i < sceneTransforms.Length; i++)
            {
                Transform candidate = sceneTransforms[i];
                if (candidate == null || candidate == transform)
                {
                    continue;
                }

                string candidateName = candidate.gameObject.name;
                if (!candidateName.Contains("_overturn") || !candidateName.StartsWith(normalTrashCanName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                trashCans.Add(candidate.gameObject);
            }

            return trashCans;
        }

        private GameObject SpawnOverturnedTrashCan(
            Transform parent,
            GameObject normalTrashCan,
            GameObject trashCanPrefab,
            List<GameObject> availableOverturnedTrashCans)
        {
            Transform normalTransform = normalTrashCan.transform;
            Vector3 uprightEuler = normalTransform.eulerAngles;
            Vector3 overturnedEuler = uprightEuler + overturnedEulerOffset;
            Vector3 overturnedPosition = normalTransform.position + overturnedPositionOffset;

            GameObject overturnedTrashCan = TakeClosestOverturnedTrashCan(normalTransform.position, availableOverturnedTrashCans);
            if (overturnedTrashCan != null)
            {
                overturnedTrashCan.transform.SetParent(parent, true);
                overturnedTrashCan.SetActive(true);
            }
            else
            {
                GameObject sourcePrefab = trashCanPrefab != null ? trashCanPrefab : normalTrashCan;
                overturnedTrashCan = Instantiate(sourcePrefab, overturnedPosition, Quaternion.Euler(overturnedEuler), parent);
                overturnedTrashCan.name = $"{normalTrashCan.name}_overturn";
                overturnedTrashCan.transform.localScale = normalTransform.localScale;
            }

            SetLayerRecursively(overturnedTrashCan.transform, InteractableLayer);
            EnsureTrashCanCollider(overturnedTrashCan);

            SpilledTrashCanInteractable interactable = overturnedTrashCan.GetComponent<SpilledTrashCanInteractable>();
            if (interactable == null)
            {
                interactable = overturnedTrashCan.AddComponent<SpilledTrashCanInteractable>();
            }

            interactable.Configure(
                taskId,
                requiredTool,
                1,
                liftCanPromptText,
                normalTransform.position,
                uprightEuler,
                holdToLiftSeconds,
                normalTrashCan);

            return overturnedTrashCan;
        }

        private void SpawnTrash(Transform parent, Vector3 trashCanPosition, Vector3 trashCanEuler, GameObject[] resolvedTrashPrefabs, int groupIndex)
        {
            Quaternion spreadRotation = Quaternion.Euler(0f, trashCanEuler.y + overturnedEulerOffset.y, 0f);

            for (int i = 0; i < TrashOffsets.Length; i++)
            {
                Vector3 position = trashCanPosition + spreadRotation * TrashOffsets[i];
                Quaternion rotation = Quaternion.Euler(TrashEulerAngles[i] + new Vector3(0f, trashCanEuler.y, 0f));
                GameObject prefab = resolvedTrashPrefabs.Length > 0 ? resolvedTrashPrefabs[i % resolvedTrashPrefabs.Length] : null;

                GameObject trash = prefab != null
                    ? Instantiate(prefab, position, rotation, parent)
                    : CreateFallbackTrash(parent, position, rotation, i);

                trash.name = $"SpilledTrash_{groupIndex + 1}_Item_{i + 1}";
                trash.transform.SetPositionAndRotation(position, rotation);
                trash.transform.localScale = Vector3.Scale(trash.transform.localScale, trashScale);

                SetLayerRecursively(trash.transform, InteractableLayer);
                EnsureTrashCollider(trash);
                ConfigureTrashTaskTarget(trash);
            }
        }

        private void ConfigureTrashTaskTarget(GameObject trash)
        {
            TaskTarget taskTarget = trash.GetComponent<TaskTarget>();
            if (taskTarget == null)
            {
                taskTarget = trash.AddComponent<TaskTarget>();
            }

            taskTarget.ConfigureTask(taskId, requiredTool, 1, collectPromptText);
            taskTarget.SetAllowAnyTool(true);
            taskTarget.SetDisableOnUse(true);
        }

        private void SetTrashTaskRequiredCount(int requiredCount)
        {
            TaskManager taskManager = FindFirstObjectByType<TaskManager>();
            if (taskManager != null)
            {
                taskManager.SetRequiredCount(taskId, requiredCount);
            }
        }

        private bool IsTrashTaskActive()
        {
            TaskManager taskManager = FindFirstObjectByType<TaskManager>();
            return taskManager == null || taskManager.IsTaskActive(taskId);
        }

        private GameObject ResolveNormalTrashCanPrefab()
        {
            if (normalTrashCanPrefab != null)
            {
                return normalTrashCanPrefab;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(NormalTrashCanPrefabPath);
#else
            return null;
#endif
        }

        private GameObject[] ResolveTrashPrefabs()
        {
            if (trashPrefabs != null && trashPrefabs.Length > 0)
            {
                return trashPrefabs;
            }

#if UNITY_EDITOR
            List<GameObject> loadedPrefabs = new List<GameObject>();
            for (int i = 0; i < TrashPrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrashPrefabPaths[i]);
                if (prefab != null)
                {
                    loadedPrefabs.Add(prefab);
                }
            }

            return loadedPrefabs.ToArray();
#else
            return System.Array.Empty<GameObject>();
#endif
        }

        private static GameObject CreateFallbackTrash(Transform parent, Vector3 position, Quaternion rotation, int index)
        {
            PrimitiveType primitiveType = index % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Capsule;
            GameObject fallback = GameObject.CreatePrimitive(primitiveType);
            fallback.transform.SetParent(parent, false);
            fallback.transform.SetPositionAndRotation(position, rotation);
            fallback.transform.localScale = new Vector3(0.25f, 0.08f, 0.18f);
            return fallback;
        }

        private static GameObject TakeClosestOverturnedTrashCan(Vector3 normalPosition, List<GameObject> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            int closestIndex = -1;
            float closestSqrDistance = 100f;

            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - normalPosition).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }

            if (closestIndex < 0)
            {
                return null;
            }

            GameObject closest = candidates[closestIndex];
            candidates.RemoveAt(closestIndex);
            return closest;
        }

        private static void EnsureTrashCanCollider(GameObject trashCan)
        {
            CapsuleCollider collider = trashCan.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = trashCan.AddComponent<CapsuleCollider>();
            }

            collider.isTrigger = false;
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.6f;
            collider.height = 1f;
            collider.direction = 1;
        }

        private static void EnsureTrashCollider(GameObject trash)
        {
            SphereCollider rootCollider = trash.GetComponent<SphereCollider>();
            if (rootCollider != null)
            {
                rootCollider.isTrigger = false;
                rootCollider.center = new Vector3(0f, 0.2f, 0f);
                rootCollider.radius = Mathf.Max(rootCollider.radius, 0.45f);
                return;
            }

            rootCollider = trash.AddComponent<SphereCollider>();
            rootCollider.isTrigger = false;
            rootCollider.center = new Vector3(0f, 0.2f, 0f);
            rootCollider.radius = 0.45f;
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

        private static Transform[] FindSceneTransforms()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<Transform>();
#endif
        }
    }

    public static class SpilledTrashCleanupBootstrapInstaller
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

            if (FindBootstrapInstance() != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("SpilledTrashCleanupBootstrap");
            bootstrapObject.AddComponent<SpilledTrashCleanupBootstrap>();
        }

        private static SpilledTrashCleanupBootstrap FindBootstrapInstance()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<SpilledTrashCleanupBootstrap>();
#else
            return UnityEngine.Object.FindObjectOfType<SpilledTrashCleanupBootstrap>();
#endif
        }
    }
}
