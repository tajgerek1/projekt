using System;
using NightWatch.Foundation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class BuildingGraffitiBootstrap : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private string targetBuildingName = "Building_u_prefab";
        [SerializeField] private Vector3 fallbackBuildingPosition = new Vector3(546.8404f, 71.96481f, 559.3304f);
        [SerializeField] private Vector3 fallbackBuildingEuler = new Vector3(0f, -90f, 0f);

        [Header("Graffiti Placement")]
        [SerializeField] private Vector3 localOffset = new Vector3(1.000002f, 1.8f, 6.783335f);
        [SerializeField] private Vector3 localEuler = Vector3.zero;
        [SerializeField] private Vector3 localScale = new Vector3(2.4f, 1.6f, 1f);

        [Header("Cleaning")]
        [SerializeField] private ToolType requiredTool = ToolType.Chainsaw;
        [SerializeField] [Min(0.1f)] private float cleanDurationSeconds = 10f;
        [SerializeField] private string taskId = "graffiti";
        [SerializeField] private string promptText = "ZMYJ GRAFFITI";

        private const string GraffitiObjectName = "RuntimeGraffitiTarget";
        private const int FallbackInteractableLayer = 6;
        private bool hasSpawned;

        private void Start()
        {
            TrySpawnGraffiti();
        }

        private void TrySpawnGraffiti()
        {
            if (hasSpawned)
            {
                return;
            }

            Transform building = FindTargetBuilding();
            if (building == null)
            {
                Debug.LogWarning("[BuildingGraffitiBootstrap] Could not find target building for runtime graffiti.", this);
                return;
            }

            if (building.Find(GraffitiObjectName) != null)
            {
                hasSpawned = true;
                return;
            }

            GameObject graffitiObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            graffitiObject.name = GraffitiObjectName;
            graffitiObject.layer = ResolveInteractableLayer();

            Transform graffitiTransform = graffitiObject.transform;
            graffitiTransform.SetParent(building, false);
            graffitiTransform.localPosition = localOffset;
            graffitiTransform.localEulerAngles = localEuler;
            graffitiTransform.localScale = localScale;
            EnsureGraffitiCollider(graffitiObject);

            MeshRenderer renderer = graffitiObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateGraffitiMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            GraffitiCleanupInteractable cleanupInteractable = graffitiObject.AddComponent<GraffitiCleanupInteractable>();
            cleanupInteractable.Configure(taskId, requiredTool, 1, promptText, cleanDurationSeconds, renderer);

            hasSpawned = true;
        }

        private Transform FindTargetBuilding()
        {
            Transform[] allTransforms = FindSceneTransforms();
            Transform bestByName = null;
            float bestByNameDistance = float.MaxValue;

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (candidate == null || !NameMatches(candidate.name, targetBuildingName))
                {
                    continue;
                }

                float sqrDistance = (candidate.position - fallbackBuildingPosition).sqrMagnitude;
                if (sqrDistance < bestByNameDistance)
                {
                    bestByNameDistance = sqrDistance;
                    bestByName = candidate;
                }
            }

            if (bestByName != null)
            {
                return bestByName;
            }

            GameObject fallbackAnchor = new GameObject("GraffitiFallbackAnchor");
            fallbackAnchor.transform.position = fallbackBuildingPosition;
            fallbackAnchor.transform.eulerAngles = fallbackBuildingEuler;
            return fallbackAnchor.transform;
        }

        private static bool NameMatches(string candidateName, string expectedBaseName)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(expectedBaseName))
            {
                return false;
            }

            if (candidateName.Equals(expectedBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return candidateName.StartsWith(expectedBaseName + " (", StringComparison.OrdinalIgnoreCase);
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

        private static void EnsureGraffitiCollider(GameObject graffitiObject)
        {
            if (graffitiObject == null)
            {
                return;
            }

            MeshCollider meshCollider = graffitiObject.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.enabled = false;
            }

            BoxCollider boxCollider = graffitiObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = graffitiObject.AddComponent<BoxCollider>();
            }

            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(1f, 1f, 0.08f);
            boxCollider.isTrigger = false;
        }

        private static Material CreateGraffitiMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new Material(shader);
            Texture2D texture = GenerateGraffitiTexture(256, 128);

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
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
            return material;
        }

        private static Texture2D GenerateGraffitiTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            Color transparent = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = transparent;
            }

            texture.SetPixels(pixels);

            Random.State previousState = Random.state;
            Random.InitState(20260424);

            Color[] palette = new[]
            {
                new Color(1f, 0.25f, 0.8f, 0.92f),
                new Color(0.2f, 0.95f, 0.45f, 0.88f),
                new Color(0.15f, 0.85f, 1f, 0.88f),
                new Color(1f, 0.65f, 0.2f, 0.9f)
            };

            for (int stroke = 0; stroke < 42; stroke++)
            {
                Color strokeColor = palette[Random.Range(0, palette.Length)];
                Vector2 from = new Vector2(Random.Range(10f, width - 10f), Random.Range(10f, height - 10f));
                int segments = Random.Range(3, 8);
                float maxStep = Random.Range(16f, 38f);

                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector2 to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * maxStep;
                    to.x = Mathf.Clamp(to.x, 4f, width - 4f);
                    to.y = Mathf.Clamp(to.y, 4f, height - 4f);

                    DrawLine(texture, from, to, strokeColor, Random.Range(2, 5));
                    from = to;
                }
            }

            for (int splat = 0; splat < 20; splat++)
            {
                Color splatColor = palette[Random.Range(0, palette.Length)];
                splatColor.a = Random.Range(0.5f, 0.85f);
                int centerX = Random.Range(8, width - 8);
                int centerY = Random.Range(8, height - 8);
                int radius = Random.Range(2, 6);
                DrawCircle(texture, centerX, centerY, radius, splatColor);
            }

            Random.state = previousState;
            texture.Apply();
            return texture;
        }

        private static void DrawLine(Texture2D texture, Vector2 from, Vector2 to, Color color, int thickness)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 1.5f));
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
                DrawCircle(texture, x, y, thickness, color);
            }
        }

        private static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int sqrRadius = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > sqrRadius)
                    {
                        continue;
                    }

                    BlendPixel(texture, centerX + x, centerY + y, color);
                }
            }
        }

        private static void BlendPixel(Texture2D texture, int x, int y, Color source)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            {
                return;
            }

            Color destination = texture.GetPixel(x, y);
            float sourceAlpha = Mathf.Clamp01(source.a);
            float inverseSourceAlpha = 1f - sourceAlpha;

            Color blended = new Color(
                source.r * sourceAlpha + destination.r * inverseSourceAlpha,
                source.g * sourceAlpha + destination.g * inverseSourceAlpha,
                source.b * sourceAlpha + destination.b * inverseSourceAlpha,
                sourceAlpha + destination.a * inverseSourceAlpha
            );

            texture.SetPixel(x, y, blended);
        }
    }

    public static class BuildingGraffitiBootstrapInstaller
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

            GameObject bootstrapObject = new GameObject("BuildingGraffitiBootstrap");
            bootstrapObject.AddComponent<BuildingGraffitiBootstrap>();
        }

        private static BuildingGraffitiBootstrap FindBootstrapInstance()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<BuildingGraffitiBootstrap>();
#else
            return UnityEngine.Object.FindObjectOfType<BuildingGraffitiBootstrap>();
#endif
        }
    }
}
