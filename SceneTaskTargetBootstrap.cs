using System.Collections;
using NightWatch.Foundation;
using NightWatch.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class SceneTaskTargetBootstrap : MonoBehaviour
    {
        [SerializeField] private string targetSceneName = "SampleScene";
        [SerializeField] private string campfireNameFragment = "Campfire";
        [SerializeField] private string hydrantNameFragment = "FireHydrant";
        [SerializeField] [Range(0.05f, 1f)] private float hydrantSelectionRatio = 0.34f;

        private const int FallbackInteractableLayer = 6;

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != targetSceneName)
            {
                yield break;
            }

            yield return null;
            ConfigureCampfireTarget();
            ConfigureHydrantTarget();
        }

        private void ConfigureCampfireTarget()
        {
            Transform campfire = FindFirstSceneTransformContaining(campfireNameFragment);
            if (campfire == null)
            {
                return;
            }

            TaskTarget target = EnsureTaskTarget(campfire.gameObject);
            target.ConfigureTask("campfire", ToolType.Bucket, 1, "ZGAS OGNISKO");
            target.SetDisableOnUse(false);
            EnsureInteractableSetup(campfire.gameObject, new Vector3(0f, 0.9f, 0f), 1.35f);
        }

        private void ConfigureHydrantTarget()
        {
            TaskManager taskManager = FindFirstObjectByType<TaskManager>();
            if (taskManager != null && !taskManager.IsTaskActive("hydrant"))
            {
                return;
            }

            Transform[] selectedHydrants = SelectRandomSubset(FindSceneTransformsContaining(hydrantNameFragment), hydrantSelectionRatio);
            if (selectedHydrants.Length == 0)
            {
                return;
            }

            if (taskManager != null)
            {
                taskManager.SetRequiredCount("hydrant", selectedHydrants.Length);
            }

            for (int i = 0; i < selectedHydrants.Length; i++)
            {
                Transform hydrant = selectedHydrants[i];
                if (hydrant == null)
                {
                    continue;
                }

                TaskTarget target = EnsureTaskTarget(hydrant.gameObject);
                target.ConfigureTask("hydrant", ToolType.Key, 1, "NAPRAW HYDRANT");
                target.SetDisableOnUse(false);
                EnsureInteractableSetup(hydrant.gameObject, new Vector3(0f, 0.9f, 0f), 0.9f);
            }
        }

        private static TaskTarget EnsureTaskTarget(GameObject targetObject)
        {
            TaskTarget taskTarget = targetObject.GetComponent<TaskTarget>();
            if (taskTarget == null)
            {
                taskTarget = targetObject.AddComponent<TaskTarget>();
            }

            return taskTarget;
        }

        private static void EnsureInteractableSetup(GameObject targetObject, Vector3 colliderCenter, float colliderRadius)
        {
            SetLayerRecursively(targetObject.transform, ResolveInteractableLayer());

            if (targetObject.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            SphereCollider collider = targetObject.AddComponent<SphereCollider>();
            collider.center = colliderCenter;
            collider.radius = Mathf.Max(0.1f, colliderRadius);
            collider.isTrigger = false;
        }

        private static Transform FindFirstSceneTransformContaining(string nameFragment)
        {
            if (string.IsNullOrWhiteSpace(nameFragment))
            {
                return null;
            }

            Transform[] transforms = FindSceneTransforms();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform[] FindSceneTransformsContaining(string nameFragment)
        {
            if (string.IsNullOrWhiteSpace(nameFragment))
            {
                return System.Array.Empty<Transform>();
            }

            System.Collections.Generic.List<Transform> results = new System.Collections.Generic.List<Transform>();
            Transform[] transforms = FindSceneTransforms();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) < 0 ||
                    HasMatchingParent(candidate, nameFragment))
                {
                    continue;
                }

                results.Add(candidate);
            }

            return results.ToArray();
        }

        private static bool HasMatchingParent(Transform candidate, string nameFragment)
        {
            Transform parent = candidate.parent;
            while (parent != null)
            {
                if (parent.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static Transform[] SelectRandomSubset(Transform[] source, float ratio)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<Transform>();
            }

            System.Collections.Generic.List<Transform> candidates = new System.Collections.Generic.List<Transform>();
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

        private static void Shuffle<T>(System.Collections.Generic.IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                T temporaryValue = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temporaryValue;
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

        private static int ResolveInteractableLayer()
        {
            int layer = LayerMask.NameToLayer("Interactable");
            return layer >= 0 ? layer : FallbackInteractableLayer;
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
    }

    public static class SceneTaskTargetBootstrapInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene")
            {
                return;
            }

            if (FindBootstrapInstance() != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("SceneTaskTargetBootstrap");
            bootstrapObject.AddComponent<SceneTaskTargetBootstrap>();
        }

        private static SceneTaskTargetBootstrap FindBootstrapInstance()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<SceneTaskTargetBootstrap>();
#else
            return Object.FindObjectOfType<SceneTaskTargetBootstrap>();
#endif
        }
    }
}
