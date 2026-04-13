using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Optional helper for underwater movement in FirstPersonController.
    /// Add this component on water objects to provide an explicit water surface Y.
    /// If no explicit water areas exist, it falls back to renderers with water-like names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterSurfaceArea : MonoBehaviour
    {
        [Tooltip("Additional offset applied to detected water surface height.")]
        [SerializeField] private float surfaceOffset;

        private static readonly List<WaterSurfaceArea> Areas = new List<WaterSurfaceArea>(32);
        private static readonly List<Renderer> FallbackWaterRenderers = new List<Renderer>(64);
        private static readonly RaycastHit[] UpCastHits = new RaycastHit[16];
        private static float nextFallbackScanTime;

        private Renderer cachedRenderer;
        private Collider cachedCollider;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            cachedCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (!Areas.Contains(this))
            {
                Areas.Add(this);
            }
        }

        private void OnDisable()
        {
            Areas.Remove(this);
        }

        /// <summary>
        /// Finds nearest water surface to a probe point (within vertical tolerance).
        /// </summary>
        public static bool TryGetClosestSurfaceY(Vector3 probePosition, float tolerance, out float surfaceY, Collider ignoredProbeCollider = null)
        {
            surfaceY = 0f;
            bool found = false;
            float bestDistance = float.MaxValue;

            for (int i = Areas.Count - 1; i >= 0; i--)
            {
                WaterSurfaceArea area = Areas[i];
                if (area == null)
                {
                    Areas.RemoveAt(i);
                    continue;
                }

                if (!area.isActiveAndEnabled)
                {
                    continue;
                }

                if (!area.TryGetSurfaceY(out float y))
                {
                    continue;
                }

                if (!area.IsProbeWithinWaterXZ(probePosition))
                {
                    continue;
                }

                if (IsSurfaceBlockedFromProbe(probePosition, y, area.cachedCollider, ignoredProbeCollider))
                {
                    continue;
                }

                float dist = Mathf.Abs(probePosition.y - y);
                if (dist <= tolerance && dist < bestDistance)
                {
                    found = true;
                    bestDistance = dist;
                    surfaceY = y;
                }
            }

            if (found)
            {
                return true;
            }

            RefreshFallbackRenderersIfNeeded();
            for (int i = FallbackWaterRenderers.Count - 1; i >= 0; i--)
            {
                Renderer r = FallbackWaterRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    FallbackWaterRenderers.RemoveAt(i);
                    continue;
                }

                float y = r.bounds.max.y;

                if (!IsProbeWithinRendererXZ(r, probePosition))
                {
                    continue;
                }

                if (IsSurfaceBlockedFromProbe(probePosition, y, null, ignoredProbeCollider))
                {
                    continue;
                }

                float dist = Mathf.Abs(probePosition.y - y);
                if (dist <= tolerance && dist < bestDistance)
                {
                    found = true;
                    bestDistance = dist;
                    surfaceY = y;
                }
            }

            return found;
        }

        private bool TryGetSurfaceY(out float y)
        {
            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }

            if (cachedCollider != null && cachedCollider.enabled)
            {
                y = cachedCollider.bounds.max.y + surfaceOffset;
                return true;
            }

            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer != null && cachedRenderer.enabled)
            {
                y = cachedRenderer.bounds.max.y + surfaceOffset;
                return true;
            }

            y = transform.position.y + surfaceOffset;
            return true;
        }

        private bool IsProbeWithinWaterXZ(Vector3 probePosition)
        {
            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }

            if (cachedCollider != null && cachedCollider.enabled)
            {
                return IsProbeWithinBoundsXZ(cachedCollider.bounds, probePosition, 0.35f);
            }

            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer != null && cachedRenderer.enabled)
            {
                return IsProbeWithinBoundsXZ(cachedRenderer.bounds, probePosition, 0.35f);
            }

            // Without bounds info we cannot safely determine horizontal membership.
            // In that case, reject this area to avoid "global water" side effects.
            return false;
        }

        private static bool IsProbeWithinRendererXZ(Renderer r, Vector3 probePosition)
        {
            return IsProbeWithinBoundsXZ(r.bounds, probePosition, 0.35f);
        }

        private static bool IsProbeWithinBoundsXZ(Bounds bounds, Vector3 probePosition, float edgePadding)
        {
            float minX = bounds.min.x - edgePadding;
            float maxX = bounds.max.x + edgePadding;
            float minZ = bounds.min.z - edgePadding;
            float maxZ = bounds.max.z + edgePadding;
            return probePosition.x >= minX && probePosition.x <= maxX &&
                   probePosition.z >= minZ && probePosition.z <= maxZ;
        }

        private static bool IsSurfaceBlockedFromProbe(Vector3 probePosition, float surfaceY, Collider ignoredWaterCollider, Collider ignoredProbeCollider)
        {
            float verticalDistance = surfaceY - probePosition.y;
            if (verticalDistance <= 0.02f)
            {
                return false;
            }

            Vector3 origin = probePosition + Vector3.up * 0.02f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.up,
                UpCastHits,
                verticalDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = UpCastHits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (ignoredWaterCollider != null && hitCollider == ignoredWaterCollider)
                {
                    continue;
                }

                if (ignoredProbeCollider != null && hitCollider == ignoredProbeCollider)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void RefreshFallbackRenderersIfNeeded()
        {
            if (Time.unscaledTime < nextFallbackScanTime)
            {
                return;
            }

            nextFallbackScanTime = Time.unscaledTime + 2.5f;
            FallbackWaterRenderers.Clear();

            Renderer[] renderers;
#if UNITY_2023_1_OR_NEWER
            renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            renderers = Object.FindObjectsOfType<Renderer>();
#endif
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                string name = r.gameObject.name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string lower = name.ToLowerInvariant();
                if (lower.Contains("water") || lower.Contains("lake") || lower.Contains("river") || lower.Contains("ocean"))
                {
                    FallbackWaterRenderers.Add(r);
                }
            }
        }
    }
}
