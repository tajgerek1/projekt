using UnityEngine;

namespace NocnaStraz
{
    /// <summary>
    /// Prosty spawner środowiska z assetów użytkownika (WOC) wrzuconych do Resources.
    /// Nie modyfikuje sceny na stałe – buduje dekoracje w runtime, żeby projekt był lekki i "out of the box".
    /// </summary>
    public sealed class EnvironmentSpawner : MonoBehaviour
    {
        [Header("Area")]
        [SerializeField] private float areaRadius = 18f;

        [Header("Counts")]
        [SerializeField] private int treeCount = 14;
        [SerializeField] private int dwarfTreeCount = 10;
        [SerializeField] private int grassCount = 40;
        [SerializeField] private int parkElementCount = 6;
        [SerializeField] private int lightCount = 6;

        private static bool _spawned;

        private void Awake()
        {
            if (_spawned) { Destroy(gameObject); return; }
            _spawned = true;

            SpawnAll();
        }

        private void SpawnAll()
        {
            var root = new GameObject("[WOC] Environment").transform;
            root.SetParent(transform, false);

            // Ground hint - a simple plane under the scene if none exists.
            EnsureGround(root);

            // Trees
            SpawnMany(root, new[]
            {
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_05_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_03_01_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_01_02_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_01_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_02_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_03_Mo",
            }, treeCount, 0.9f, 1.3f, ring: true);

            // Dwarf trees / bushes
            SpawnMany(root, new[]
            {
                "WOC/City/Plant/WOC_Ct_Plant_DwarfTree/Model/WOC_Ct_Plant_DwarfTree_Mo",
            }, dwarfTreeCount, 0.8f, 1.2f, ring: false);

            // Grass clumps
            SpawnMany(root, new[]
            {
                "WOC/City/Plant/WOC_Ct_Plant_Grass_01/Model/WOC_Ct_Plant_Grass_01_Mo",
            }, grassCount, 0.7f, 1.2f, ring: false);

            // Park elements (ławki / elementy małej architektury)
            SpawnMany(root, new[]
            {
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_01_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_02_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_03_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_04_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_05_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_06_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_07_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_08_Mo",
            }, parkElementCount, 0.9f, 1.1f, ring: false, centerBias: true);

            // Lights along a simple path line
            SpawnLights(root);

            // Dedicated kids playground area
            SpawnPlayground(root);
        }

        private void EnsureGround(Transform root)
        {
            // If there's already a ground in the scene, leave it.
            if (GameObject.Find("Ground") != null) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
        }

        private void SpawnLights(Transform root)
        {
            var paths = new[]
            {
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_01_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_02_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_03_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_04_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_05_Mo",
            };

            for (int i = 0; i < lightCount; i++)
            {
                var go = LoadAny(paths);
                if (go == null) break;

                var inst = Instantiate(go, root);
                inst.name = $"Light_{i:00}";
                float t = (lightCount <= 1) ? 0.5f : (i / (float)(lightCount - 1));
                float x = Mathf.Lerp(-10f, 10f, t);
                float z = -2.0f + Mathf.Sin(t * Mathf.PI) * 0.8f;
                inst.transform.position = new Vector3(x, 0f, z);
                inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Add actual light source (soft glow)
                var light = inst.GetComponentInChildren<Light>();
                if (light == null)
                {
                    var lgo = new GameObject("PointLight");
                    lgo.transform.SetParent(inst.transform, false);
                    lgo.transform.localPosition = new Vector3(0f, 3.2f, 0f);

                    light = lgo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 10f;
                    light.intensity = 2.2f;
                }
            }
        }



private void SpawnPlayground(Transform root)
{
    var pgRoot = new GameObject("[WOC] Playground").transform;
    pgRoot.SetParent(root, false);

    // Place it to the side of the main path
    Vector3 center = new Vector3(12f, 0f, 12f);
    pgRoot.position = center;

    // Simple sand pad
    var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
    pad.name = "PlaygroundPad";
    pad.transform.SetParent(pgRoot, false);
    pad.transform.localPosition = new Vector3(0f, -0.45f, 0f);
    pad.transform.localScale = new Vector3(10f, 0.9f, 10f);
    var padMr = pad.GetComponent<MeshRenderer>();
    if (padMr != null)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.95f, 0.88f, 0.70f);
        padMr.sharedMaterial = mat;
    }

    // Use park elements as playground props (swing/slide/etc. depending on the pack)
    var props = new[]
    {
        "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_06_Mo",
        "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_07_Mo",
        "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_08_Mo",
    };

    Vector3[] offsets =
    {
        new Vector3(-2.5f, 0f, -1.5f),
        new Vector3( 2.2f, 0f, -1.0f),
        new Vector3( 0.2f, 0f,  2.3f),
    };

    for (int i = 0; i < props.Length; i++)
    {
        var go = Resources.Load<GameObject>(props[i]);
        if (go == null) continue;

        var inst = Instantiate(go, pgRoot);
        inst.name = $"PlaygroundProp_{i:00}_{go.name}";
        inst.transform.localPosition = offsets[i];
        inst.transform.localRotation = Quaternion.Euler(0f, 180f + (i * 35f), 0f);
        inst.transform.localScale = Vector3.one * 1.05f;
    }

    // A couple of benches nearby for vibe
    var benchPaths = new[]
    {
        "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_01_Mo",
        "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_02_Mo",
    };

    for (int i = 0; i < 2; i++)
    {
        var go = LoadAny(benchPaths);
        if (go == null) break;
        var inst = Instantiate(go, pgRoot);
        inst.name = $"PlaygroundBench_{i:00}";
        inst.transform.localPosition = new Vector3(-4.2f + i * 8.4f, 0f, 4.3f);
        inst.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        inst.transform.localScale = Vector3.one;
    }
}
        private void SpawnMany(Transform root, string[] resourcePaths, int count, float minScale, float maxScale, bool ring, bool centerBias = false)
        {
            for (int i = 0; i < count; i++)
            {
                var go = LoadAny(resourcePaths);
                if (go == null) return;

                var inst = Instantiate(go, root);
                inst.name = $"{go.name}_{i:00}";

                var pos = ring
                    ? RandomOnRing(areaRadius * 0.7f, areaRadius)
                    : RandomInDisk(areaRadius * (centerBias ? 0.45f : 0.9f));

                inst.transform.position = new Vector3(pos.x, 0f, pos.y);
                inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = Random.Range(minScale, maxScale);
                inst.transform.localScale = Vector3.one * s;
            }
        }

        private static GameObject LoadAny(string[] resourcePaths)
        {
            if (resourcePaths == null || resourcePaths.Length == 0) return null;
            var path = resourcePaths[Random.Range(0, resourcePaths.Length)];
            var go = Resources.Load<GameObject>(path);
            return go;
        }

        private static Vector2 RandomInDisk(float radius)
        {
            var p = Random.insideUnitCircle * radius;
            return p;
        }

        private static Vector2 RandomOnRing(float minR, float maxR)
        {
            float r = Random.Range(minR, maxR);
            float a = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
    }
}
