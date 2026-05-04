using System;
using System.Collections;
using NightWatch.Foundation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightWatch.Items
{
    [DisallowMultipleComponent]
    public sealed class EquippedToolController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform handAnchor;
        [SerializeField] private ToolSelectionController toolSelectionController;

        [Header("Tool Prefabs (1-6)")]
        [SerializeField] private GameObject flashlightPrefab;
        [SerializeField] private GameObject wrenchPrefab;
        [SerializeField] private GameObject bucketPrefab;
        [SerializeField] private GameObject sawPrefab;
        [SerializeField] private GameObject shotgunPrefab;
        [SerializeField] private GameObject trashBagPrefab;

        [Header("View Transform")]
        [SerializeField] private Vector3 localPosition = Vector3.zero;
        [SerializeField] private Vector3 localRotation = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;

        [Header("Per Tool Rotation Override")]
        [SerializeField] private bool useKeyRotationOverride = true;
        [SerializeField] private Vector3 keyLocalRotation = new Vector3(90f, 0f, 0f);

        [Header("Flashlight Beam")]
        [SerializeField] private bool createRuntimeFlashlightBeam = true;
        [SerializeField] private Vector3 flashlightBeamLocalPosition = new Vector3(0f, -0.05f, 0.25f);
        [SerializeField] private Vector3 flashlightBeamLocalRotation = Vector3.zero;
        [SerializeField] [Min(0f)] private float flashlightIntensity = 12f;
        [SerializeField] [Min(0.1f)] private float flashlightRange = 35f;
        [SerializeField] [Range(1f, 179f)] private float flashlightSpotAngle = 42f;
        [SerializeField] [Range(1f, 179f)] private float flashlightInnerSpotAngle = 18f;
        [SerializeField] private Color flashlightColor = new Color(1f, 0.94f, 0.82f, 1f);

        [Header("Use Animations")]
        [SerializeField] private Vector3 bucketPourMouthLocalPosition = new Vector3(0f, -0.12f, 0.28f);
        [SerializeField] [Min(0.1f)] private float bucketTiltAngle = 78f;
        [SerializeField] [Min(0.01f)] private float waterStreamWidth = 0.035f;
        [SerializeField] private Color waterStreamColor = new Color(0.45f, 0.82f, 1f, 0.75f);
        [SerializeField] [Min(0.1f)] private float graffitiRollerWidth = 0.45f;
        [SerializeField] [Min(0.01f)] private float graffitiRollerRadius = 0.07f;
        [SerializeField] private Color graffitiRollerColor = new Color(0.86f, 0.88f, 0.82f, 1f);
        [SerializeField] private Color graffitiRollerHandleColor = new Color(0.2f, 0.16f, 0.12f, 1f);

        [Header("Runtime (Debug)")]
        [SerializeField] private GameObject currentToolVisual;
        [SerializeField] private GameObject currentEquippedItem;
        [SerializeField] private Light currentFlashlightBeam;

        private const int IgnoreRaycastLayer = 2;

        private const string FlashlightGuid = "06619cc7e974f4744b8b03e00f5ce26f";
        private const string WrenchGuid = "76ac2c4e2124c6847bd2e2bded3417e9";
        private const string BucketGuid = "c3d61c4a466475540943084710bc35ed";
        private const string SawGuid = "ad2a7bc7e0fd6f64fa0a7335ebd6fe7d";
        private const string ShotgunGuid = "e18f5e87dbe3c814aace8fa485861c9a";
        private const string TrashBagGuid = "9cd1746789b311d4c9e0add4969236f1";

        private Coroutine toolUseAnimationRoutine;
        private GameObject activeWaterStream;
        private GameObject activeGraffitiRoller;
        private Vector3 animationOriginalLocalPosition;
        private Quaternion animationOriginalLocalRotation;
        private Vector3 animationOriginalLocalScale;
        private bool animationOriginalActiveSelf;
        private bool hasAnimationOriginalTransform;

        private void Awake()
        {
            if (toolSelectionController == null)
            {
                toolSelectionController = GetComponentInParent<ToolSelectionController>();
            }
        }

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            toolSelectionController.OnToolChanged += HandleToolChanged;
            ToolUseAnimationEvents.BucketPourRequested += HandleBucketPourRequested;
            ToolUseAnimationEvents.GraffitiScrubRequested += HandleGraffitiScrubRequested;
            HandleToolChanged(toolSelectionController.CurrentTool);
        }

        private void OnDisable()
        {
            if (toolSelectionController != null)
            {
                toolSelectionController.OnToolChanged -= HandleToolChanged;
            }

            ToolUseAnimationEvents.BucketPourRequested -= HandleBucketPourRequested;
            ToolUseAnimationEvents.GraffitiScrubRequested -= HandleGraffitiScrubRequested;
            ClearCurrentToolVisual();
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (handAnchor == null)
            {
                Debug.LogError("[EquippedToolController] Missing HandAnchor reference.", this);
                valid = false;
            }

            if (toolSelectionController == null)
            {
                Debug.LogError("[EquippedToolController] Missing ToolSelectionController reference.", this);
                valid = false;
            }

            return valid;
        }

        private void HandleToolChanged(ToolType tool)
        {
            ShowTool(tool);
        }

        private void ShowTool(ToolType tool)
        {
            ClearCurrentToolVisual();

            GameObject prefabReference = GetPrefabReferenceForTool(tool);
            prefabReference = ResolvePrefabReference(tool, prefabReference);
            currentEquippedItem = prefabReference;

            if (!IsPrefabUsable(prefabReference))
            {
                Debug.LogWarning($"[EquippedToolController] No prefab assigned for tool: {tool}.", this);
                return;
            }

            if (!TryInstantiateTool(prefabReference, out UnityEngine.Object spawnedObject, out string instantiateError))
            {
#if UNITY_EDITOR
                GameObject fallbackPrefab = ResolvePrefabReference(tool, null);
                if (fallbackPrefab != null &&
                    fallbackPrefab != prefabReference &&
                    TryInstantiateTool(fallbackPrefab, out spawnedObject, out instantiateError))
                {
                    prefabReference = fallbackPrefab;
                }
                else
                {
                    Debug.LogError(
                        $"[EquippedToolController] Failed to instantiate tool '{tool}' from reference type '{prefabReference.GetType().Name}'. Asset: '{prefabReference.name}'. {instantiateError}",
                        this);
                    return;
                }
#else
                Debug.LogError(
                    $"[EquippedToolController] Failed to instantiate tool '{tool}' from reference type '{prefabReference.GetType().Name}'. Asset: '{prefabReference.name}'. {instantiateError}",
                    this);
                return;
#endif
            }

            if (spawnedObject is GameObject spawnedGameObject)
            {
                currentToolVisual = spawnedGameObject;
            }
            else if (spawnedObject is Component spawnedComponent)
            {
                currentToolVisual = spawnedComponent.gameObject;
            }
            else
            {
                Debug.LogError(
                    $"[EquippedToolController] Spawned object for '{tool}' is not GameObject/Component. Spawned type: '{spawnedObject.GetType().Name}'.",
                    this);
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }

                return;
            }

            currentToolVisual.name = $"{prefabReference.name}_Held";
            currentToolVisual.transform.localPosition = localPosition;
            currentToolVisual.transform.localRotation = Quaternion.Euler(localRotation);
            currentToolVisual.transform.localScale = localScale;

            if (tool == ToolType.Key && useKeyRotationOverride)
            {
                currentToolVisual.transform.localRotation = Quaternion.Euler(keyLocalRotation);
            }

            DisableHeldPhysicsAndInteraction(currentToolVisual);

            if (tool == ToolType.Flashlight)
            {
                CreateFlashlightBeam();
            }
        }

        private static bool IsPrefabUsable(GameObject prefab)
        {
            return prefab != null && !string.IsNullOrWhiteSpace(prefab.name);
        }

        private bool TryInstantiateTool(GameObject prefab, out UnityEngine.Object spawnedObject, out string error)
        {
            spawnedObject = null;
            error = string.Empty;

            if (!IsPrefabUsable(prefab))
            {
                error = "Prefab reference is null or invalid.";
                return false;
            }

            try
            {
                spawnedObject = Instantiate(prefab, handAnchor, false);
                return true;
            }
            catch (System.Exception exception)
            {
                error = $"Exception: {exception}";
                return false;
            }
        }

        private GameObject ResolvePrefabReference(ToolType tool, GameObject configuredReference)
        {
            if (IsPrefabUsable(configuredReference))
            {
                return configuredReference;
            }

#if UNITY_EDITOR
            string guid = GetFallbackGuidForTool(tool);
            if (string.IsNullOrEmpty(guid))
            {
                return configuredReference;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return configuredReference;
            }

            GameObject fallbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (IsPrefabUsable(fallbackPrefab))
            {
                Debug.LogWarning(
                    $"[EquippedToolController] Recovered missing prefab for '{tool}' from asset path '{assetPath}'. Reassign this field in Inspector to remove warning.",
                    this);
                return fallbackPrefab;
            }
#endif

            return configuredReference;
        }

        private static string GetFallbackGuidForTool(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Flashlight:
                    return FlashlightGuid;
                case ToolType.Key:
                    return WrenchGuid;
                case ToolType.Bucket:
                    return BucketGuid;
                case ToolType.Chainsaw:
                    return SawGuid;
                case ToolType.Shotgun:
                    return ShotgunGuid;
                case ToolType.TrashBag:
                    return TrashBagGuid;
                default:
                    return null;
            }
        }

        private GameObject GetPrefabReferenceForTool(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Flashlight:
                    return flashlightPrefab;
                case ToolType.Key:
                    return wrenchPrefab;
                case ToolType.Bucket:
                    return bucketPrefab;
                case ToolType.Chainsaw:
                    return sawPrefab;
                case ToolType.Shotgun:
                    return shotgunPrefab;
                case ToolType.TrashBag:
                    return trashBagPrefab;
                default:
                    return null;
            }
        }

        private void ClearCurrentToolVisual()
        {
            StopToolUseAnimation();
            ClearFlashlightBeam();

            if (currentToolVisual == null)
            {
                return;
            }

            Destroy(currentToolVisual);
            currentToolVisual = null;
        }

        private void CreateFlashlightBeam()
        {
            if (!createRuntimeFlashlightBeam)
            {
                return;
            }

            ClearFlashlightBeam();

            Transform beamAnchor = ResolveFlashlightBeamAnchor();
            GameObject beamObject = new GameObject("RuntimeFlashlightBeam");
            beamObject.transform.SetParent(beamAnchor, false);
            beamObject.transform.localPosition = flashlightBeamLocalPosition;
            beamObject.transform.localRotation = Quaternion.Euler(flashlightBeamLocalRotation);
            beamObject.transform.localScale = Vector3.one;
            beamObject.layer = IgnoreRaycastLayer;

            currentFlashlightBeam = beamObject.AddComponent<Light>();
            currentFlashlightBeam.type = LightType.Spot;
            currentFlashlightBeam.color = flashlightColor;
            currentFlashlightBeam.intensity = flashlightIntensity;
            currentFlashlightBeam.range = flashlightRange;
            currentFlashlightBeam.spotAngle = flashlightSpotAngle;
            currentFlashlightBeam.innerSpotAngle = Mathf.Min(flashlightInnerSpotAngle, flashlightSpotAngle);
            currentFlashlightBeam.shadows = LightShadows.Soft;
            currentFlashlightBeam.renderMode = LightRenderMode.ForcePixel;
        }

        private void ClearFlashlightBeam()
        {
            if (currentFlashlightBeam == null)
            {
                return;
            }

            Destroy(currentFlashlightBeam.gameObject);
            currentFlashlightBeam = null;
        }

        private Transform ResolveFlashlightBeamAnchor()
        {
            Camera viewCamera = handAnchor != null ? handAnchor.GetComponentInParent<Camera>() : null;
            return viewCamera != null ? viewCamera.transform : handAnchor;
        }

        private void HandleBucketPourRequested(Vector3 targetWorldPosition, float durationSeconds)
        {
            if (!CanAnimateTool(ToolType.Bucket))
            {
                return;
            }

            StartToolUseAnimation(AnimateBucketPour(targetWorldPosition, Mathf.Max(0.3f, durationSeconds)));
        }

        private void HandleGraffitiScrubRequested(Transform graffitiTransform, float durationSeconds)
        {
            if (graffitiTransform == null || !CanAnimateTool(ToolType.Chainsaw))
            {
                return;
            }

            StartToolUseAnimation(AnimateGraffitiScrub(graffitiTransform, Mathf.Max(0.3f, durationSeconds)));
        }

        private bool CanAnimateTool(ToolType expectedTool)
        {
            return isActiveAndEnabled &&
                   currentToolVisual != null &&
                   toolSelectionController != null &&
                   toolSelectionController.CurrentTool == expectedTool;
        }

        private void StartToolUseAnimation(IEnumerator animationRoutine)
        {
            StopToolUseAnimation();
            toolUseAnimationRoutine = StartCoroutine(animationRoutine);
        }

        private void StopToolUseAnimation()
        {
            if (toolUseAnimationRoutine != null)
            {
                StopCoroutine(toolUseAnimationRoutine);
                toolUseAnimationRoutine = null;
            }

            RestoreToolAnimationTransform();
            DestroyAnimationObject(ref activeWaterStream);
            DestroyAnimationObject(ref activeGraffitiRoller);
        }

        private IEnumerator AnimateBucketPour(Vector3 targetWorldPosition, float durationSeconds)
        {
            CacheToolAnimationTransform();
            GameObject waterEffect = EnsureWaterPourEffect();
            float elapsed = 0f;

            while (elapsed < durationSeconds && currentToolVisual != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / durationSeconds);
                float pourIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.28f));
                float pourOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.78f) / 0.22f));
                float pourWeight = Mathf.Clamp01(pourIn * (1f - pourOut));

                ApplyBucketPourPose(targetWorldPosition, pourWeight);

                bool shouldShowWater = progress > 0.18f && progress < 0.9f && pourWeight > 0.05f;
                SetWaterPourEffectActive(waterEffect, shouldShowWater);
                if (shouldShowWater)
                {
                    Vector3 streamStart = currentToolVisual.transform.TransformPoint(bucketPourMouthLocalPosition);
                    UpdateWaterPourEffect(waterEffect, streamStart, targetWorldPosition, pourWeight, elapsed);
                }

                yield return null;
            }

            SetWaterPourEffectActive(waterEffect, false);
            RestoreToolAnimationTransform();
            DestroyAnimationObject(ref activeWaterStream);
            toolUseAnimationRoutine = null;
        }

        private IEnumerator AnimateGraffitiScrub(Transform graffitiTransform, float durationSeconds)
        {
            CacheToolAnimationTransform();
            currentToolVisual.SetActive(false);
            EnsureGraffitiRoller();
            float elapsed = 0f;

            while (elapsed < durationSeconds && currentToolVisual != null && graffitiTransform != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / durationSeconds);

                UpdateGraffitiRoller(graffitiTransform, progress);
                yield return null;
            }

            RestoreToolAnimationTransform();
            DestroyAnimationObject(ref activeGraffitiRoller);
            toolUseAnimationRoutine = null;
        }

        private void CacheToolAnimationTransform()
        {
            if (currentToolVisual == null)
            {
                return;
            }

            Transform toolTransform = currentToolVisual.transform;
            animationOriginalLocalPosition = toolTransform.localPosition;
            animationOriginalLocalRotation = toolTransform.localRotation;
            animationOriginalLocalScale = toolTransform.localScale;
            animationOriginalActiveSelf = currentToolVisual.activeSelf;
            hasAnimationOriginalTransform = true;
        }

        private void RestoreToolAnimationTransform()
        {
            if (!hasAnimationOriginalTransform || currentToolVisual == null)
            {
                hasAnimationOriginalTransform = false;
                return;
            }

            Transform toolTransform = currentToolVisual.transform;
            toolTransform.localPosition = animationOriginalLocalPosition;
            toolTransform.localRotation = animationOriginalLocalRotation;
            toolTransform.localScale = animationOriginalLocalScale;
            currentToolVisual.SetActive(animationOriginalActiveSelf);
            hasAnimationOriginalTransform = false;
        }

        private void ApplyBucketPourPose(Vector3 targetWorldPosition, float pourWeight)
        {
            if (currentToolVisual == null)
            {
                return;
            }

            Transform toolTransform = currentToolVisual.transform;
            Transform parent = toolTransform.parent != null ? toolTransform.parent : handAnchor;
            Vector3 worldDirection = targetWorldPosition - toolTransform.position;
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                worldDirection = parent != null ? parent.forward : Vector3.forward;
            }

            Vector3 localDirection = parent != null
                ? parent.InverseTransformDirection(worldDirection.normalized)
                : worldDirection.normalized;

            Vector3 flatLocalDirection = Vector3.ProjectOnPlane(localDirection, Vector3.up);
            if (flatLocalDirection.sqrMagnitude < 0.0001f)
            {
                flatLocalDirection = Vector3.forward;
            }

            flatLocalDirection.Normalize();

            float tilt01 = Mathf.Clamp01(pourWeight);
            float tiltRadians = bucketTiltAngle * Mathf.Deg2Rad * tilt01;
            Vector3 tippedUp = Vector3.Slerp(Vector3.up, flatLocalDirection, Mathf.Sin(tiltRadians));
            if (tippedUp.sqrMagnitude < 0.0001f)
            {
                tippedUp = Vector3.up;
            }

            Quaternion directionTilt = Quaternion.FromToRotation(Vector3.up, tippedUp.normalized);
            Quaternion pourRoll = Quaternion.AngleAxis(10f * tilt01, Vector3.forward);
            toolTransform.localRotation = directionTilt * animationOriginalLocalRotation * pourRoll;
            toolTransform.localPosition =
                animationOriginalLocalPosition + (flatLocalDirection * 0.1f + Vector3.down * 0.05f) * tilt01;
        }

        private GameObject EnsureWaterPourEffect()
        {
            if (activeWaterStream == null)
            {
                activeWaterStream = new GameObject("RuntimeBucketWaterPour");
                activeWaterStream.layer = IgnoreRaycastLayer;
                for (int i = 0; i < 6; i++)
                {
                    GameObject strand = new GameObject($"WaterStrand_{i}");
                    strand.transform.SetParent(activeWaterStream.transform, false);
                    strand.layer = IgnoreRaycastLayer;

                    LineRenderer lineRenderer = strand.AddComponent<LineRenderer>();
                    lineRenderer.useWorldSpace = true;
                    lineRenderer.positionCount = 8;
                    lineRenderer.numCapVertices = 3;
                    lineRenderer.numCornerVertices = 3;
                    lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lineRenderer.receiveShadows = false;
                    lineRenderer.textureMode = LineTextureMode.Stretch;
                    lineRenderer.material = CreateUnlitMaterial(waterStreamColor);
                    lineRenderer.enabled = false;
                }

                GameObject splash = new GameObject("WaterSplash");
                splash.transform.SetParent(activeWaterStream.transform, false);
                splash.layer = IgnoreRaycastLayer;

                ParticleSystem splashParticles = splash.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = splashParticles.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                main.startColor = waterStreamColor;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 120;

                ParticleSystem.EmissionModule emission = splashParticles.emission;
                emission.rateOverTime = 0f;

                ParticleSystem.ShapeModule shape = splashParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.14f;

                ParticleSystemRenderer particleRenderer = splash.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null)
                {
                    particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                    particleRenderer.material = CreateUnlitMaterial(waterStreamColor);
                    particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    particleRenderer.receiveShadows = false;
                }
            }

            return activeWaterStream;
        }

        private void SetWaterPourEffectActive(GameObject effect, bool isActive)
        {
            if (effect == null)
            {
                return;
            }

            LineRenderer[] lineRenderers = effect.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                if (lineRenderers[i] != null)
                {
                    lineRenderers[i].enabled = isActive;
                }
            }

            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particles = particleSystems[i];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = isActive ? 70f : 0f;

                if (isActive && !particles.isPlaying)
                {
                    particles.Play(true);
                }
                else if (!isActive)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void UpdateWaterPourEffect(GameObject effect, Vector3 startPosition, Vector3 targetPosition, float alphaMultiplier, float elapsedSeconds)
        {
            if (effect == null)
            {
                return;
            }

            Vector3 toTarget = targetPosition - startPosition;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = Vector3.down;
            }

            Vector3 forward = toTarget.normalized;
            Vector3 side = Vector3.Cross(forward, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                Camera viewCamera = handAnchor != null ? handAnchor.GetComponentInParent<Camera>() : null;
                side = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            }

            side.Normalize();

            LineRenderer[] strands = effect.GetComponentsInChildren<LineRenderer>(true);
            for (int strandIndex = 0; strandIndex < strands.Length; strandIndex++)
            {
                LineRenderer strand = strands[strandIndex];
                if (strand == null)
                {
                    continue;
                }

                const int segmentCount = 8;
                strand.positionCount = segmentCount;
                float strandCenter = (strandIndex - (strands.Length - 1) * 0.5f) * 0.018f;
                float strandWidth = waterStreamWidth * Mathf.Lerp(0.32f, 0.52f, (strandIndex + 1f) / Mathf.Max(1f, strands.Length));
                strand.startWidth = strandWidth * alphaMultiplier;
                strand.endWidth = strandWidth * 0.38f * alphaMultiplier;

                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    float t = segmentIndex / (float)(segmentCount - 1);
                    float noise = Mathf.Sin(elapsedSeconds * 22f + strandIndex * 2.7f + segmentIndex * 1.9f) * 0.015f;
                    Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);
                    position += Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.18f);
                    position += side * ((strandCenter + noise) * (1f - t * 0.45f));
                    strand.SetPosition(segmentIndex, position);
                }

                Color color = waterStreamColor;
                color.a *= Mathf.Clamp01(alphaMultiplier) * Mathf.Lerp(0.55f, 0.9f, strandIndex / Mathf.Max(1f, strands.Length - 1f));
                SetMaterialColor(strand.material, color);
            }

            ParticleSystem splashParticles = effect.GetComponentInChildren<ParticleSystem>(true);
            if (splashParticles != null)
            {
                splashParticles.transform.position = targetPosition + Vector3.up * 0.04f;
                ParticleSystem.MainModule main = splashParticles.main;
                Color splashColor = waterStreamColor;
                splashColor.a *= Mathf.Clamp01(alphaMultiplier);
                main.startColor = splashColor;
            }
        }

        private void EnsureGraffitiRoller()
        {
            if (activeGraffitiRoller != null)
            {
                return;
            }

            activeGraffitiRoller = new GameObject("RuntimeGraffitiRoller");
            activeGraffitiRoller.layer = IgnoreRaycastLayer;

            GameObject roller = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roller.name = "RollerHead";
            roller.transform.SetParent(activeGraffitiRoller.transform, false);
            roller.transform.localScale = new Vector3(graffitiRollerRadius, graffitiRollerWidth * 0.5f, graffitiRollerRadius);
            DestroyColliderIfPresent(roller);
            SetRendererMaterial(roller, graffitiRollerColor);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "RollerHandle";
            handle.transform.SetParent(activeGraffitiRoller.transform, false);
            handle.transform.localPosition = new Vector3(0f, -graffitiRollerWidth * 0.55f, -0.22f);
            handle.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            DestroyColliderIfPresent(handle);
            SetRendererMaterial(handle, graffitiRollerHandleColor);
        }

        private void UpdateGraffitiRoller(Transform graffitiTransform, float progress)
        {
            if (activeGraffitiRoller == null || graffitiTransform == null)
            {
                return;
            }

            const int rowCount = 5;
            float scaledProgress = Mathf.Clamp01(progress) * rowCount;
            int row = Mathf.Min(rowCount - 1, Mathf.FloorToInt(scaledProgress));
            float rowProgress = Mathf.Repeat(scaledProgress, 1f);
            if (row % 2 == 1)
            {
                rowProgress = 1f - rowProgress;
            }

            float localX = Mathf.Lerp(-0.42f, 0.42f, rowProgress);
            float localY = Mathf.Lerp(0.34f, -0.34f, row / (float)(rowCount - 1));
            localY += Mathf.Sin(progress * Mathf.PI * 10f) * 0.025f;

            Vector3 normal = GetGraffitiVisibleNormal(graffitiTransform);
            Vector3 worldPosition = graffitiTransform.TransformPoint(new Vector3(localX, localY, 0f)) + normal * 0.055f;
            activeGraffitiRoller.transform.SetPositionAndRotation(worldPosition, Quaternion.LookRotation(normal, graffitiTransform.right));
        }

        private Vector3 GetGraffitiVisibleNormal(Transform graffitiTransform)
        {
            Vector3 normal = graffitiTransform.forward;
            Camera viewCamera = handAnchor != null ? handAnchor.GetComponentInParent<Camera>() : null;
            if (viewCamera != null && Vector3.Dot(normal, viewCamera.transform.position - graffitiTransform.position) < 0f)
            {
                normal = -normal;
            }

            return normal;
        }

        private static void DestroyAnimationObject(ref GameObject target)
        {
            if (target != null)
            {
                Destroy(target);
                target = null;
            }
        }

        private static void DestroyColliderIfPresent(GameObject target)
        {
            Collider colliderComponent = target != null ? target.GetComponent<Collider>() : null;
            if (colliderComponent != null)
            {
                Destroy(colliderComponent);
            }
        }

        private static void SetRendererMaterial(GameObject target, Color color)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.material = CreateUnlitMaterial(color);
            }
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            SetMaterialColor(material, color);
            ConfigureTransparencyIfNeeded(material, color);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void ConfigureTransparencyIfNeeded(Material material, Color color)
        {
            if (material == null || color.a >= 0.99f)
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
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_OFF");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void DisableHeldPhysicsAndInteraction(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Collider colliderComponent in root.GetComponentsInChildren<Collider>(true))
            {
                colliderComponent.enabled = false;
            }

            foreach (Rigidbody rigidbodyComponent in root.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbodyComponent.isKinematic = true;
                rigidbodyComponent.detectCollisions = false;
            }

            SetLayerRecursively(root.transform, IgnoreRaycastLayer);
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

    public static class ToolUseAnimationEvents
    {
        public static event Action<Vector3, float> BucketPourRequested;
        public static event Action<Transform, float> GraffitiScrubRequested;

        public static void PlayBucketPour(Vector3 targetWorldPosition, float durationSeconds)
        {
            BucketPourRequested?.Invoke(targetWorldPosition, durationSeconds);
        }

        public static void PlayGraffitiScrub(Transform graffitiTransform, float durationSeconds)
        {
            GraffitiScrubRequested?.Invoke(graffitiTransform, durationSeconds);
        }
    }
}
