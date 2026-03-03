using System.Collections.Generic;
using UnityEngine;

namespace NocnaStraz
{
    /// <summary>
    /// ParkWorld:
    /// - v1.1.1 i wcześniej: budował mapę parku w runtime.
    /// - v1.1.2: mapa/propsy są ustawiane ręcznie w scenie, a ParkWorld tylko:
    ///     * zbiera Interactable (Lamp/Bin/Trash),
    ///     * steruje światłami lamp pod statystykę "Oświetlenie".
    /// </summary>
    public sealed class ParkWorld : MonoBehaviour
    {
        private readonly List<Light> _lampLights = new();
        private readonly List<Interactable> _bins = new();
        private readonly List<Interactable> _trash = new();

        private Material _groundMat;
        private Material _pathMat;
        private Material _curbMat;
        private Material _sandMat;

        // WOC-ish materials (optional)
        private Material _woodMat;

        public IReadOnlyList<Interactable> Bins => _bins;
        public IReadOnlyList<Interactable> Trash => _trash;

        // Layout (metry)
        private const float GroundScale = 12.0f; // Unity plane ma 10x10 => 12 daje ~120x120
        private const float PathWidth = 4.8f;
        private const float PathLength = 68f;
        private const float PathY = 0.05f;
        private const float PathHalf = PathLength * 0.5f;

        // Connector leads to playground gate.
        private const float ConnectorWidth = 3.4f;

        // Playground layout (matches reference images: fenced rectangle, lamps around, central tree circle)
        private static readonly Vector3 PlaygroundCenter = new(36f, 0f, 18f);
        private const float PlaygroundSizeX = 28f;
        private const float PlaygroundSizeZ = 20f;
        private static float PlaygroundHalfX => PlaygroundSizeX * 0.5f;
        private static float PlaygroundHalfZ => PlaygroundSizeZ * 0.5f;
        private static float PlaygroundGateZ => PlaygroundCenter.z - PlaygroundHalfZ - 0.2f;
        private static float ConnectorZ => PlaygroundGateZ;

        /// <summary>
        /// v1.1.2: skanuje aktualną scenę i zbiera obiekty oznaczone Interactable.
        /// Dzięki temu możesz ręcznie ustawić modele (WOC lub inne) i tylko dodać komponent Interactable.
        /// </summary>
        public void ScanScene()
        {
            _bins.Clear();
            _trash.Clear();
            _lampLights.Clear();

            var interactables = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var i in interactables)
            {
                if (i == null) continue;
                if (string.IsNullOrEmpty(i.Id)) i.Id = i.gameObject.name;

                switch (i.Kind)
                {
                    case InteractableKind.Bin:
                        _bins.Add(i);
                        break;
                    case InteractableKind.Trash:
                        _trash.Add(i);
                        break;
                    case InteractableKind.Lamp:
                    {
                        // Spróbuj znaleźć światło pod lampą
                        var l = i.GetComponentInChildren<Light>();
                        if (l != null) _lampLights.Add(l);
                        break;
                    }
                }
            }

            // Jeśli nie ma żadnej lampy oznaczonej InteractableKind.Lamp, a w scenie są point-lighty,
            // to sterujemy nimi jako fallback.
            if (_lampLights.Count == 0)
            {
                var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l == null) continue;
                    if (l.type == LightType.Point) _lampLights.Add(l);
                }
            }
        }

        public void Build()
        {
            CreateMaterials();
            LoadOptionalWocMaterials();

            var envRoot = new GameObject("[Env] Layout").transform;
            envRoot.SetParent(transform, false);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(envRoot, false);
            ground.transform.localScale = new Vector3(GroundScale, 1, GroundScale);
            ground.GetComponent<Renderer>().sharedMaterial = _groundMat;

            // Main Path
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "MainPath";
            path.transform.SetParent(envRoot, false);
            path.transform.localScale = new Vector3(PathWidth, 0.10f, PathLength);
            path.transform.localPosition = new Vector3(0, PathY, 0);
            path.GetComponent<Renderer>().sharedMaterial = _pathMat;

            // Curbs (main)
            CreateCurb(envRoot, new Vector3(-(PathWidth * 0.5f + 0.25f), 0.08f, 0f), new Vector3(0.28f, 0.16f, PathLength + 0.6f));
            CreateCurb(envRoot, new Vector3(+(PathWidth * 0.5f + 0.25f), 0.08f, 0f), new Vector3(0.28f, 0.16f, PathLength + 0.6f));

            // Connector to playground (horizontal)
            // Connector to playground (horizontal) -> ends at gate (south side, centered)
            float connLen = PlaygroundCenter.x;
            var connector = GameObject.CreatePrimitive(PrimitiveType.Cube);
            connector.name = "PlaygroundConnector";
            connector.transform.SetParent(envRoot, false);
            connector.transform.localPosition = new Vector3(connLen * 0.5f, PathY, ConnectorZ);
            connector.transform.localScale = new Vector3(connLen, 0.10f, ConnectorWidth);
            connector.GetComponent<Renderer>().sharedMaterial = _pathMat;

            // Connector curbs
            CreateCurb(envRoot, new Vector3(connLen * 0.5f, 0.08f, ConnectorZ - (ConnectorWidth * 0.5f + 0.25f)), new Vector3(connLen + 0.6f, 0.16f, 0.28f));
            CreateCurb(envRoot, new Vector3(connLen * 0.5f, 0.08f, ConnectorZ + (ConnectorWidth * 0.5f + 0.25f)), new Vector3(connLen + 0.6f, 0.16f, 0.28f));

            // Little plaza at intersection
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaza.name = "PathPlaza";
            plaza.transform.SetParent(envRoot, false);
            plaza.transform.localPosition = new Vector3(0f, PathY, ConnectorZ);
            plaza.transform.localScale = new Vector3(PathWidth + 1.2f, 0.10f, ConnectorWidth + 1.2f);
            plaza.GetComponent<Renderer>().sharedMaterial = _pathMat;

            // Plants and background props (away from paths/playground)
            SpawnPlants(envRoot);

            // Street furniture
            SpawnLamps(envRoot);
            SpawnBenches(envRoot);
            SpawnBins(envRoot);

            // Playground
            SpawnPlayground(envRoot);

            // Trash to cleaning minigame
            SpawnTrash(6);
        }

        public void SpawnTrash(int count)
        {
            for (int i = _trash.Count - 1; i >= 0; i--)
            {
                if (_trash[i] != null) Destroy(_trash[i].gameObject);
            }
            _trash.Clear();

            for (int i = 0; i < count; i++)
            {
                var tr = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tr.name = $"Trash{i + 1}";
                tr.transform.SetParent(transform, false);
                tr.transform.localScale = Vector3.one * 0.35f;
                tr.transform.localPosition = new Vector3(Random.Range(-1.6f, 1.6f), 0.25f, Random.Range(-20f, 20f));
                tr.GetComponent<Renderer>().material.color = new Color(0.7f, 0.7f, 0.75f);

                var inter = tr.AddComponent<Interactable>();
                inter.Kind = InteractableKind.Trash;
                inter.Id = tr.name;

                _trash.Add(inter);
            }
        }

        public void ApplyIllumination(int illumination)
        {
            float i01 = illumination / 100f;
            float intensity = Mathf.Lerp(0.12f, 3.1f, i01);
            foreach (var l in _lampLights)
            {
                if (l == null) continue;
                l.intensity = intensity;
                l.enabled = illumination > 8;
            }
        }

        public void RandomizeBins()
        {
            foreach (var b in _bins)
            {
                if (b == null) continue;
                b.Flag = Random.value < 0.3f;

                var marker = b.transform.Find("OverflowMarker");
                if (marker != null)
                    marker.gameObject.SetActive(b.Flag);
            }
        }

        private void CreateMaterials()
        {
            var std = Shader.Find("Standard");
            _groundMat = new Material(std) { color = new Color(0.06f, 0.08f, 0.07f) };
            _pathMat = new Material(std) { color = new Color(0.15f, 0.15f, 0.17f) };
            _curbMat = new Material(std) { color = new Color(0.23f, 0.23f, 0.26f) };
            _sandMat = new Material(std) { color = new Color(0.92f, 0.86f, 0.70f) };
        }

        private void LoadOptionalWocMaterials()
        {
            // Wood-ish material from WOC ParkElement (if present). Fallback to a plain Standard.
            _woodMat = Resources.Load<Material>("WOC/City/Items/WOC_Ct_Items_ParkElement/Material/WOC_Ct_Items_ParkElement_01_Ma");
            if (_woodMat == null)
            {
                var std = Shader.Find("Standard");
                _woodMat = new Material(std) { color = new Color(0.45f, 0.33f, 0.20f) };
            }
        }

        private void CreateCurb(Transform parent, Vector3 pos, Vector3 scale)
        {
            var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Curb";
            curb.transform.SetParent(parent, false);
            curb.transform.localPosition = pos;
            curb.transform.localScale = scale;
            curb.GetComponent<Renderer>().sharedMaterial = _curbMat;
        }

        private void SpawnLamps(Transform parent)
        {
            // Main path: 7 pairs
            const int pairs = 7;
            float startZ = -PathHalf + 6.0f;
            float endZ = PathHalf - 6.0f;

            for (int i = 0; i < pairs; i++)
            {
                float t = pairs <= 1 ? 0.5f : i / (float)(pairs - 1);
                float z = Mathf.Lerp(startZ, endZ, t);

                var lampL = CreateLamp($"Lamp_L{i + 1}", new Vector3(-(PathWidth * 0.5f + 1.2f), 0, z), styleIndex: i);
                lampL.transform.SetParent(parent, false);

                var lampR = CreateLamp($"Lamp_R{i + 1}", new Vector3(+(PathWidth * 0.5f + 1.2f), 0, z), styleIndex: i + 1);
                lampR.transform.SetParent(parent, false);
            }

            // Connector: 2 lamps
            var c1 = CreateLamp("Lamp_Conn1", new Vector3(PlaygroundCenter.x * 0.33f, 0, ConnectorZ - 2.1f), styleIndex: 20);
            c1.transform.SetParent(parent, false);
            var c2 = CreateLamp("Lamp_Conn2", new Vector3(PlaygroundCenter.x * 0.66f, 0, ConnectorZ + 2.1f), styleIndex: 21);
            c2.transform.SetParent(parent, false);
        }

        private void SpawnBenches(Transform parent)
        {
            // Real benches built from primitives (no more “mini swings as benches”).
            float[] zs = { -22f, -14f, -6f, 2f, 10f, 18f, 26f };

            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];

                // Left side benches
                CreateBenchPrimitive(parent, $"Bench_L{i + 1}", new Vector3(-(PathWidth * 0.5f + 2.6f), 0, z), Quaternion.Euler(0, 90, 0));

                // Right side benches (every other to keep it airy)
                if (i % 2 == 0)
                    CreateBenchPrimitive(parent, $"Bench_R{i + 1}", new Vector3(+(PathWidth * 0.5f + 2.6f), 0, z + 1.8f), Quaternion.Euler(0, -90, 0));
            }

            // Couple benches near connector junction
            CreateBenchPrimitive(parent, "Bench_Plaza_1", new Vector3(-3.3f, 0, ConnectorZ + 2.2f), Quaternion.Euler(0, 25, 0));
            CreateBenchPrimitive(parent, "Bench_Plaza_2", new Vector3(3.1f, 0, ConnectorZ - 2.2f), Quaternion.Euler(0, -155, 0));
        }

        private void SpawnBins(Transform parent)
        {
            // 4 bins along the main path + 1 near playground entrance
            Vector3[] positions =
            {
                new Vector3(+(PathWidth * 0.5f + 1.95f), 0, -18f),
                new Vector3(-(PathWidth * 0.5f + 1.95f), 0, -2f),
                new Vector3(+(PathWidth * 0.5f + 1.95f), 0,  14f),
                new Vector3(-(PathWidth * 0.5f + 1.95f), 0,  26f),
                new Vector3(PlaygroundCenter.x - 6.0f, 0, ConnectorZ + 1.6f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var bin = CreateBin($"Bin{i + 1}", positions[i]);
                bin.transform.SetParent(parent, false);
                _bins.Add(bin.GetComponent<Interactable>());
            }
        }

        private void SpawnPlayground(Transform parent)
        {
            // Layout based on the provided reference images.
            // Fenced rectangle, lamps around, symmetric conifers, central tree circle.

            var pgRoot = new GameObject("PlacZabaw").transform;
            pgRoot.SetParent(parent, false);
            pgRoot.localPosition = PlaygroundCenter;

            // Sidewalk pad (outside fence)
            var sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sidewalk.name = "Playground_Sidewalk";
            sidewalk.transform.SetParent(pgRoot, false);
            sidewalk.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            sidewalk.transform.localScale = new Vector3(PlaygroundSizeX + 4.0f, 0.05f, PlaygroundSizeZ + 4.0f);
            sidewalk.GetComponent<Renderer>().sharedMaterial = _curbMat;
            Destroy(sidewalk.GetComponent<Collider>());

            // Sand base (inside fence)
            var sand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sand.name = "Playground_Sand";
            sand.transform.SetParent(pgRoot, false);
            sand.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sand.transform.localScale = new Vector3(PlaygroundSizeX, 0.04f, PlaygroundSizeZ);
            sand.GetComponent<Renderer>().sharedMaterial = _sandMat;
            Destroy(sand.GetComponent<Collider>());

            // Fence (pickets + rails) with entrance gap on SOUTH side centered (towards connector path)
            CreatePlaygroundPicketFence(pgRoot, PlaygroundSizeX, PlaygroundSizeZ, entranceGapWidth: 3.6f);

            // Perimeter lamps (around sidewalk, outside fence)
            SpawnPlaygroundPerimeterLamps(pgRoot);

            // Central circular island (grass + curb + big tree + benches)
            CreatePlaygroundCenterIsland(pgRoot);

            // Conifers inside perimeter
            SpawnPlaygroundConifers(pgRoot);

            // Equipment + benches
            SpawnPlaygroundEquipment(pgRoot);

            // Small bin near entrance (clickable)
            var bin = CreateBin("PlaygroundBin", new Vector3(+PlaygroundHalfX - 1.8f, 0f, -PlaygroundHalfZ + 1.6f));
            bin.transform.SetParent(pgRoot, false);
            _bins.Add(bin.GetComponent<Interactable>());

            // Zone trigger (future: missions)
            var zone = pgRoot.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 1.0f, 0f);
            zone.size = new Vector3(PlaygroundSizeX + 2.0f, 2.5f, PlaygroundSizeZ + 2.0f);
        }

        private void CreatePlaygroundPicketFence(Transform pgRoot, float sizeX, float sizeZ, float entranceGapWidth)
        {
            float hx = sizeX * 0.5f;
            float hz = sizeZ * 0.5f;
            float fenceY = 0.60f;

            // Pickets
            const float picketW = 0.10f;
            const float picketD = 0.06f;
            const float picketH = 1.20f;
            const float spacing = 0.32f;

            // Rails
            const float railH = 0.08f;
            const float railD = 0.10f;
            const float railY = 0.85f;

            bool IsInEntranceGapSouth(float x) => Mathf.Abs(x) < (entranceGapWidth * 0.5f);

            void Picket(string name, Vector3 pos, Quaternion rot)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = name;
                p.transform.SetParent(pgRoot, false);
                p.transform.localPosition = pos;
                p.transform.localRotation = rot;
                p.transform.localScale = new Vector3(picketW, picketH, picketD);
                ApplySharedMaterial(p, _woodMat);
                Destroy(p.GetComponent<Collider>());
            }

            void Rail(string name, Vector3 pos, Vector3 scale)
            {
                var r0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                r0.name = name;
                r0.transform.SetParent(pgRoot, false);
                r0.transform.localPosition = pos;
                r0.transform.localScale = scale;
                ApplySharedMaterial(r0, _woodMat);
                Destroy(r0.GetComponent<Collider>());
            }

            // North side pickets
            for (float x = -hx; x <= hx; x += spacing)
                Picket($"Fence_N_{x:0.00}", new Vector3(x, fenceY, +hz), Quaternion.identity);

            // South side pickets (skip entrance gap)
            for (float x = -hx; x <= hx; x += spacing)
            {
                if (IsInEntranceGapSouth(x)) continue;
                Picket($"Fence_S_{x:0.00}", new Vector3(x, fenceY, -hz), Quaternion.identity);
            }

            // West/East sides pickets
            for (float z = -hz; z <= hz; z += spacing)
            {
                Picket($"Fence_W_{z:0.00}", new Vector3(-hx, fenceY, z), Quaternion.Euler(0, 90, 0));
                Picket($"Fence_E_{z:0.00}", new Vector3(+hx, fenceY, z), Quaternion.Euler(0, 90, 0));
            }

            // Rails (top-ish)
            Rail("FenceRail_N", new Vector3(0f, railY, +hz), new Vector3(sizeX + 0.6f, railH, railD));
            Rail("FenceRail_W", new Vector3(-hx, railY, 0f), new Vector3(railD, railH, sizeZ + 0.6f));
            Rail("FenceRail_E", new Vector3(+hx, railY, 0f), new Vector3(railD, railH, sizeZ + 0.6f));

            // South rail split to leave entrance gap
            float gap = entranceGapWidth + 0.4f;
            float seg = (sizeX - gap) * 0.5f;
            Rail("FenceRail_S_1", new Vector3(-(gap * 0.5f + seg * 0.5f), railY, -hz), new Vector3(seg, railH, railD));
            Rail("FenceRail_S_2", new Vector3(+(gap * 0.5f + seg * 0.5f), railY, -hz), new Vector3(seg, railH, railD));

            // Gate posts
            var gatePostL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gatePostL.name = "FenceGatePost_L";
            gatePostL.transform.SetParent(pgRoot, false);
            gatePostL.transform.localPosition = new Vector3(-entranceGapWidth * 0.5f, fenceY, -hz);
            gatePostL.transform.localScale = new Vector3(0.18f, 1.35f, 0.18f);
            ApplySharedMaterial(gatePostL, _woodMat);
            Destroy(gatePostL.GetComponent<Collider>());

            var gatePostR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gatePostR.name = "FenceGatePost_R";
            gatePostR.transform.SetParent(pgRoot, false);
            gatePostR.transform.localPosition = new Vector3(+entranceGapWidth * 0.5f, fenceY, -hz);
            gatePostR.transform.localScale = new Vector3(0.18f, 1.35f, 0.18f);
            ApplySharedMaterial(gatePostR, _woodMat);
            Destroy(gatePostR.GetComponent<Collider>());

            // Interior gate patch
            var gatePath = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gatePath.name = "PlaygroundGatePatch";
            gatePath.transform.SetParent(pgRoot, false);
            gatePath.transform.localPosition = new Vector3(0f, 0.03f, -hz + 1.2f);
            gatePath.transform.localScale = new Vector3(entranceGapWidth + 0.6f, 0.06f, 2.4f);
            gatePath.GetComponent<Renderer>().sharedMaterial = _pathMat;
            Destroy(gatePath.GetComponent<Collider>());
        }

        private void SpawnPlaygroundPerimeterLamps(Transform pgRoot)
        {
            // Lampy dookoła placu zabaw (jak na referencji): równy rozstaw wzdłuż chodnika.
            float hx = PlaygroundHalfX;
            float hz = PlaygroundHalfZ;
            float outX = hx + 1.9f;
            float outZ = hz + 1.9f;

            var points = new List<Vector3>();

            // South + North (z narożnikami)
            const int xCount = 5;
            for (int i = 0; i < xCount; i++)
            {
                float t01 = xCount <= 1 ? 0.5f : i / (float)(xCount - 1);
                float x = Mathf.Lerp(-outX, outX, t01);
                points.Add(new Vector3(x, 0, -outZ));
                points.Add(new Vector3(x, 0, +outZ));
            }

            // West + East (bez narożników, żeby nie dublować)
            const int zCount = 4;
            for (int j = 1; j < zCount - 1; j++)
            {
                float t01 = j / (float)(zCount - 1);
                float z = Mathf.Lerp(-outZ, outZ, t01);
                points.Add(new Vector3(-outX, 0, z));
                points.Add(new Vector3(+outX, 0, z));
            }

            for (int i = 0; i < points.Count; i++)
            {
                var lamp = CreateLamp($"PG_Lamp_{i + 1:00}", points[i], styleIndex: 100 + i);
                lamp.transform.SetParent(pgRoot, false);
            }
        }

private void CreatePlaygroundCenterIsland(Transform pgRoot)
        {
            const float radius = 4.4f;
            const float ringRadius = 4.85f;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Playground_CenterRing";
            ring.transform.SetParent(pgRoot, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(ringRadius, 0.06f, ringRadius);
            ring.GetComponent<Renderer>().sharedMaterial = _curbMat;
            Destroy(ring.GetComponent<Collider>());

            var grass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            grass.name = "Playground_CenterGrass";
            grass.transform.SetParent(pgRoot, false);
            grass.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            grass.transform.localScale = new Vector3(radius, 0.055f, radius);
            var gr = grass.GetComponent<Renderer>();
            if (gr != null)
            {
                var std = Shader.Find("Standard");
                var gm = new Material(std) { color = new Color(0.14f, 0.42f, 0.18f) };
                gr.sharedMaterial = gm;
            }
            Destroy(grass.GetComponent<Collider>());

            // Big tree in the center (use WOC tree, scaled)
            string[] treeModels =
            {
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_05_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_03_01_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_01_02_Mo",
            };

            var treeRoot = new GameObject("Playground_CenterTree");
            treeRoot.transform.SetParent(pgRoot, false);
            treeRoot.transform.localPosition = Vector3.zero;
            treeRoot.transform.localRotation = Quaternion.identity;

            foreach (var t in treeModels)
            {
                var v = TryCreateVisual(treeRoot.transform, t, targetHeight: 7.5f);
                if (v != null) break;
            }

            // Benches around the center (like reference: several benches around the ring)
            CreateBenchPrimitive(pgRoot, "Playground_CenterBench_S", new Vector3(0f, 0f, -2.65f), Quaternion.Euler(0, 0, 0));
            CreateBenchPrimitive(pgRoot, "Playground_CenterBench_N", new Vector3(0f, 0f, +2.65f), Quaternion.Euler(0, 180, 0));
            CreateBenchPrimitive(pgRoot, "Playground_CenterBench_W", new Vector3(-2.65f, 0f, 0f), Quaternion.Euler(0, 90, 0));
            CreateBenchPrimitive(pgRoot, "Playground_CenterBench_E", new Vector3(+2.65f, 0f, 0f), Quaternion.Euler(0, -90, 0));
}

        private void SpawnPlaygroundConifers(Transform pgRoot)
        {
            string[] coniferModels =
            {
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_03_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_02_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_01_Mo",
            };

            float hx = PlaygroundHalfX - 1.8f;
            float hz = PlaygroundHalfZ - 1.8f;

            Vector3[] spots =
            {
                new Vector3(-hx, 0, -hz),
                new Vector3(+hx, 0, -hz),
                new Vector3(-hx, 0, +hz),
                new Vector3(+hx, 0, +hz),

                new Vector3(-hx, 0, -hz * 0.2f),
                new Vector3(+hx, 0, -hz * 0.2f),
                new Vector3(-hx, 0, +hz * 0.2f),
                new Vector3(+hx, 0, +hz * 0.2f),

                new Vector3(-hx * 0.35f, 0, +hz),
                new Vector3(+hx * 0.35f, 0, +hz),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var root = new GameObject($"PG_Conifer_{i + 1:00}");
                root.transform.SetParent(pgRoot, false);
                root.transform.localPosition = spots[i];
                root.transform.localRotation = Quaternion.Euler(0, (i * 35f) % 360f, 0);

                var p = coniferModels[i % coniferModels.Length];
                TryCreateVisual(root.transform, p, targetHeight: 4.8f);
            }

            // Small orange accent trees outside corners
            string accent = "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_05_Mo";
            Vector3[] accents =
            {
                new Vector3(-PlaygroundHalfX - 0.9f, 0, -PlaygroundHalfZ - 0.6f),
                new Vector3(+PlaygroundHalfX + 0.9f, 0, -PlaygroundHalfZ - 0.6f),
                new Vector3(-PlaygroundHalfX - 0.9f, 0, +PlaygroundHalfZ + 0.6f),
                new Vector3(+PlaygroundHalfX + 0.9f, 0, +PlaygroundHalfZ + 0.6f),
            };

            for (int i = 0; i < accents.Length; i++)
            {
                var r = new GameObject($"PG_AccentTree_{i + 1:00}");
                r.transform.SetParent(pgRoot, false);
                r.transform.localPosition = accents[i];
                r.transform.localRotation = Quaternion.Euler(0, (i * 90f), 0);
                TryCreateVisual(r.transform, accent, targetHeight: 2.8f);
            }
        }

        private void SpawnPlaygroundEquipment(Transform pgRoot)
        {
            // Układ 1:1 (na tyle, na ile pozwalają dostępne modele WOC):
            // - sprzęty w czterech „strefach” jak na referencji,
            // - kilka małych przeszkód/łuków na środku,
            // - ławki przy ogrodzeniu.

            // LEFT cluster (drabinki/małpi gaj)
            CreateDecorSingle(pgRoot, "PG_Equip_Left_1", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_06_Mo",
                new Vector3(-9.2f, 0f, 2.4f), Quaternion.Euler(0, 90, 0), targetHeight: 3.4f);

            CreateDecorSingle(pgRoot, "PG_Equip_Left_2", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_04_Mo",
                new Vector3(-8.6f, 0f, -4.8f), Quaternion.Euler(0, 180, 0), targetHeight: 2.9f);

            // RIGHT cluster (huśtawki + drugi zestaw)
            CreateDecorSingle(pgRoot, "PG_Equip_Right_Swings", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_07_Mo",
                new Vector3(+9.6f, 0f, -1.6f), Quaternion.Euler(0, -90, 0), targetHeight: 3.8f);

            CreateDecorSingle(pgRoot, "PG_Equip_Right_2", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_08_Mo",
                new Vector3(+8.2f, 0f, +4.9f), Quaternion.Euler(0, -90, 0), targetHeight: 3.2f);

            // BACK (zestawy przy górnym płocie)
            CreateDecorSingle(pgRoot, "PG_Equip_Back_1", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_05_Mo",
                new Vector3(-2.8f, 0f, +7.8f), Quaternion.Euler(0, 0, 0), targetHeight: 3.0f);

            CreateDecorSingle(pgRoot, "PG_Equip_Back_2", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_03_Mo",
                new Vector3(+4.8f, 0f, +7.9f), Quaternion.Euler(0, -25, 0), targetHeight: 2.7f);

            // FRONT (małe przeszkody przy wejściu)
            CreateDecorSingle(pgRoot, "PG_Equip_Front_1", "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_02_Mo",
                new Vector3(-5.7f, 0f, -8.0f), Quaternion.Euler(0, 20, 0), targetHeight: 1.6f);

            // Decorative mini arches (kolorowe „przeszkody” jak na referencji)
            for (int i = 0; i < 4; i++)
            {
                var arch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arch.name = $"PG_MiniArch_{i + 1}";
                arch.transform.SetParent(pgRoot, false);
                arch.transform.localScale = new Vector3(0.42f, 0.14f, 0.42f);
                arch.transform.localPosition = new Vector3(-3.9f + i * 2.6f, 0.14f, -1.9f);
                arch.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var r = arch.GetComponent<Renderer>();
                if (r != null)
                {
                    var std = Shader.Find("Standard");
                    var col = i switch
                    {
                        0 => new Color(0.86f, 0.25f, 0.25f),
                        1 => new Color(0.25f, 0.56f, 0.92f),
                        2 => new Color(0.95f, 0.78f, 0.20f),
                        _ => new Color(0.30f, 0.80f, 0.38f),
                    };
                    r.sharedMaterial = new Material(std) { color = col };
                }
                Destroy(arch.GetComponent<Collider>());
            }

            // Benches along fence (parents area)
            CreateBenchPrimitive(pgRoot, "PG_Bench_West", new Vector3(-PlaygroundHalfX + 2.2f, 0f, -1.6f), Quaternion.Euler(0, 90, 0));
            CreateBenchPrimitive(pgRoot, "PG_Bench_East", new Vector3(+PlaygroundHalfX - 2.2f, 0f, +1.2f), Quaternion.Euler(0, -90, 0));
            CreateBenchPrimitive(pgRoot, "PG_Bench_North", new Vector3(-1.4f, 0f, +PlaygroundHalfZ - 2.1f), Quaternion.Euler(0, 180, 0));
            CreateBenchPrimitive(pgRoot, "PG_Bench_South", new Vector3(+2.0f, 0f, -PlaygroundHalfZ + 2.1f), Quaternion.Euler(0, 0, 0));
        }

private void SpawnPlants(Transform parent)
        {
            // Drzewa na obrzeżu
            string[] treeModels =
            {
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_05_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_03_01_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Tree_01_02_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_01_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_02_Mo",
                "WOC/City/Plant/WOC_Ct_Plant_Tree/Model/WOC_Ct_Plant_Treelawn_03_Mo",
            };

            for (int i = 0; i < 22; i++)
            {
                var p = SampleParkPosition(outsideMinR: 22f, outsideMaxR: 44f);
                CreateDecorRandom(parent, $"Tree_{i + 1}", treeModels,
                    new Vector3(p.x, 0, p.y), Quaternion.Euler(0, Random.Range(0, 360f), 0), targetHeight: Random.Range(4.8f, 7.0f));
            }

            // Krzaki
            string[] bushModels =
            {
                "WOC/City/Plant/WOC_Ct_Plant_DwarfTree/Model/WOC_Ct_Plant_DwarfTree_Mo",
            };
            for (int i = 0; i < 28; i++)
            {
                var p = SampleParkPosition(outsideMinR: 10f, outsideMaxR: 36f);
                CreateDecorRandom(parent, $"Bush_{i + 1}", bushModels,
                    new Vector3(p.x, 0, p.y), Quaternion.Euler(0, Random.Range(0, 360f), 0), targetHeight: Random.Range(1.3f, 2.3f));
            }

            // Trawa
            string[] grassModels =
            {
                "WOC/City/Plant/WOC_Ct_Plant_Grass_01/Model/WOC_Ct_Plant_Grass_01_Mo",
            };
            for (int i = 0; i < 100; i++)
            {
                var p = SampleParkPosition(outsideMinR: 6f, outsideMaxR: 40f);
                CreateDecorRandom(parent, $"Grass_{i + 1}", grassModels,
                    new Vector3(p.x, 0, p.y), Quaternion.Euler(0, Random.Range(0, 360f), 0), targetHeight: Random.Range(0.35f, 0.70f));
            }
        }

        private Vector2 SampleParkPosition(float outsideMinR, float outsideMaxR)
        {
            for (int tries = 0; tries < 120; tries++)
            {
                float r = Random.Range(outsideMinR, outsideMaxR);
                float a = Random.Range(0f, Mathf.PI * 2f);
                var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

                // Main path exclusion
                if (Mathf.Abs(p.x) < (PathWidth * 0.5f + 2.6f) && Mathf.Abs(p.y) < (PathHalf + 2.5f))
                    continue;

                // Connector exclusion (rectangle)
                if (p.x > -2f && p.x < (PlaygroundCenter.x + 4f) && Mathf.Abs(p.y - ConnectorZ) < (ConnectorWidth * 0.5f + 2.6f))
                    continue;

                // Playground exclusion
                if (Mathf.Abs(p.x - PlaygroundCenter.x) < (PlaygroundHalfX + 6f) && Mathf.Abs(p.y - PlaygroundCenter.z) < (PlaygroundHalfZ + 6f))
                    continue;

                return p;
            }

            return new Vector2(Random.Range(-40f, 40f), Random.Range(-40f, 40f));
        }

        private GameObject CreateLamp(string id, Vector3 pos, int styleIndex)
        {
            var root = new GameObject(id);
            root.transform.localPosition = pos;

            string[] models =
            {
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_02_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_01_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_03_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_04_Mo",
                "WOC/City/Items/WOC_Ct_Items_Light/Model/WOC_Ct_Items_Light_05_Mo",
            };

            GameObject visual = null;
            for (int k = 0; k < models.Length; k++)
            {
                var p = models[(styleIndex + k) % models.Length];
                visual = TryCreateVisual(root.transform, p, targetHeight: 4.8f);
                if (visual != null) break;
            }

            if (visual == null)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(root.transform, false);
                pole.transform.localScale = new Vector3(0.18f, 2.2f, 0.18f);
                pole.transform.localPosition = new Vector3(0, 2.2f, 0);
                pole.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.1f);

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root.transform, false);
                head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                head.transform.localPosition = new Vector3(0, 4.55f, 0);
                head.GetComponent<Renderer>().material.color = new Color(0.85f, 0.85f, 0.65f);
            }

            // Light source
            var lightGO = new GameObject("Light");
            lightGO.transform.SetParent(root.transform, false);

            Vector3 lightLocal = new Vector3(0, 4.6f, 0);
            if (TryGetBounds(root, out var b))
            {
                float y = b.max.y - root.transform.position.y - 0.25f;
                var c = root.transform.InverseTransformPoint(b.center);
                lightLocal = new Vector3(c.x, Mathf.Max(2.2f, y), c.z);
            }
            lightGO.transform.localPosition = lightLocal;

            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 11f;
            l.intensity = 2.2f;
            l.color = new Color(1f, 0.95f, 0.8f);
            _lampLights.Add(l);

            var inter = root.AddComponent<Interactable>();
            inter.Kind = InteractableKind.Lamp;
            inter.Id = id;

            var col = root.AddComponent<CapsuleCollider>();
            if (TryGetBounds(root, out var bounds))
            {
                float height = Mathf.Clamp(bounds.size.y, 2.2f, 6.0f);
                col.height = height;
                col.radius = Mathf.Clamp(bounds.size.x * 0.25f, 0.25f, 0.55f);
                col.center = new Vector3(0, height * 0.5f, 0);
            }
            else
            {
                col.center = new Vector3(0, 2.2f, 0);
                col.height = 4.8f;
                col.radius = 0.35f;
            }

            return root;
        }

        private GameObject CreateBin(string id, Vector3 pos)
        {
            var root = new GameObject(id);
            root.transform.localPosition = pos;

            // Prefer real garbage can assets (added in v1.0.9). Fallback to ParkElement.
            string[] binModels =
            {
                "WOC/City/Items/WOC_Ct_Items_GarbageCan/Model/WOC_Ct_Items_GarbageCan_01_Mo",
                "WOC/City/Items/WOC_Ct_Items_GarbageCan/Model/WOC_Ct_Items_GarbageCan_02_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_02_Mo",
                "WOC/City/Items/WOC_Ct_Items_ParkElement/Model/WOC_Ct_Items_ParkElement_04_Mo",
            };

            GameObject visual = null;
            for (int i = 0; i < binModels.Length; i++)
            {
                visual = TryCreateVisual(root.transform, binModels[i], targetHeight: 1.25f);
                if (visual != null) break;
            }

            if (visual == null)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                body.transform.localPosition = new Vector3(0, 0.45f, 0);
                body.GetComponent<Renderer>().material.color = new Color(0.15f, 0.18f, 0.2f);
            }

            var inter = root.AddComponent<Interactable>();
            inter.Kind = InteractableKind.Bin;
            inter.Id = id;

            var col = root.AddComponent<BoxCollider>();
            if (TryGetBounds(root, out var b))
            {
                var center = root.transform.InverseTransformPoint(b.center);
                col.center = new Vector3(center.x, center.y, center.z);
                col.size = new Vector3(
                    Mathf.Clamp(b.size.x, 0.7f, 1.6f),
                    Mathf.Clamp(b.size.y, 0.9f, 1.9f),
                    Mathf.Clamp(b.size.z, 0.7f, 1.6f));
            }
            else
            {
                col.center = new Vector3(0, 0.45f, 0);
                col.size = new Vector3(0.9f, 1.1f, 0.9f);
            }

            // Overflow marker
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "OverflowMarker";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = Vector3.one * 0.25f;
            float markerY = 1.2f;
            if (TryGetBounds(root, out var b2))
                markerY = Mathf.Max(0.9f, (b2.size.y * 0.5f) + 0.6f);
            marker.transform.localPosition = new Vector3(0f, markerY, 0f);
            var mr = marker.GetComponent<Renderer>();
            if (mr != null) mr.material.color = new Color(0.95f, 0.25f, 0.25f);
            var mc = marker.GetComponent<Collider>();
            if (mc != null) Destroy(mc);
            marker.SetActive(false);

            return root;
        }

        private void CreateBenchPrimitive(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = rot;

            // Dimensions (meters)
            const float seatW = 1.9f;
            const float seatD = 0.55f;
            const float seatH = 0.08f;
            const float seatY = 0.52f;

            const float backH = 0.55f;
            const float backT = 0.08f;
            const float backY = 0.85f;
            const float backZ = -0.22f;

            const float legW = 0.10f;
            const float legH = 0.52f;

            // Seat
            var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Seat";
            seat.transform.SetParent(root.transform, false);
            seat.transform.localScale = new Vector3(seatW, seatH, seatD);
            seat.transform.localPosition = new Vector3(0f, seatY, 0f);
            ApplySharedMaterial(seat, _woodMat);
            Destroy(seat.GetComponent<Collider>());

            // Back
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Back";
            back.transform.SetParent(root.transform, false);
            back.transform.localScale = new Vector3(seatW, backH, backT);
            back.transform.localPosition = new Vector3(0f, backY, backZ);
            ApplySharedMaterial(back, _woodMat);
            Destroy(back.GetComponent<Collider>());

            // Legs
            void Leg(string n, float x, float z)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = n;
                leg.transform.SetParent(root.transform, false);
                leg.transform.localScale = new Vector3(legW, legH, legW);
                leg.transform.localPosition = new Vector3(x, legH * 0.5f, z);
                ApplySharedMaterial(leg, _woodMat);
                Destroy(leg.GetComponent<Collider>());
            }

            float lx = seatW * 0.5f - 0.18f;
            float lz = seatD * 0.5f - 0.12f;
            Leg("Leg_FL", +lx, +lz);
            Leg("Leg_FR", -lx, +lz);
            Leg("Leg_BL", +lx, -lz);
            Leg("Leg_BR", -lx, -lz);

            // Single collider for player blocking
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, -0.05f);
            col.size = new Vector3(seatW, 1.05f, seatD + 0.25f);
        }

        private static void ApplySharedMaterial(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private void CreateDecorSingle(Transform parent, string name, string modelPath, Vector3 pos, Quaternion rot, float targetHeight)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = rot;

            TryCreateVisual(root.transform, modelPath, targetHeight);
        }

        private void CreateDecorRandom(Transform parent, string name, string[] modelPaths, Vector3 pos, Quaternion rot, float targetHeight)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = rot;

            if (modelPaths == null || modelPaths.Length == 0) return;
            var p = modelPaths[Random.Range(0, modelPaths.Length)];
            TryCreateVisual(root.transform, p, targetHeight);
        }

        private GameObject TryCreateVisual(Transform parent, string resourcePath, float targetHeight)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;

            var inst = Instantiate(prefab, parent);
            inst.name = "Visual";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            NormalizeByHeight(inst, targetHeight);
            PlaceOnGround(inst, parent.position.y);
            FixBrokenMaterials(inst);

            return inst;
        }

        private static void FixBrokenMaterials(GameObject go)
        {
            var std = Shader.Find("Standard");
            if (std == null) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    // If shader is missing / error shader OR pipeline-specific (URP/HDRP) in a built-in project, force Standard.
                    var sn = m.shader != null ? m.shader.name : "";
                    if (m.shader == null || sn == "Hidden/InternalErrorShader" || sn.Contains("Universal Render Pipeline") || sn.Contains("HDRP"))
                    {
                        m.shader = std;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private static void NormalizeByHeight(GameObject go, float targetHeight)
        {
            if (!TryGetBounds(go, out var b)) return;
            float h = b.size.y;
            if (h <= 0.0001f) return;
            float s = targetHeight / h;
            s = Mathf.Clamp(s, 0.2f, 6.0f);
            go.transform.localScale *= s;
        }

        private static void PlaceOnGround(GameObject go, float groundY)
        {
            if (!TryGetBounds(go, out var b)) return;
            float delta = groundY - b.min.y;
            go.transform.position += Vector3.up * delta;
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                bounds.Encapsulate(rs[i].bounds);
            return true;
        }
    }
}
