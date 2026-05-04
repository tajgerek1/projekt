using StarterAssets;
using UnityEngine;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public class BoatVehicle : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Transform seatPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float interactDistance = 5.5f;

        [Header("Speed")]
        [SerializeField] private float forwardSpeed = 8f;
        [SerializeField] private float reverseSpeed = 4.5f;
        [SerializeField] private float turnSpeed = 95f;
        [SerializeField] private float landSpeedMultiplier = 0.03f;
        [SerializeField] private float landTurnMultiplier = 0.15f;

        [Header("Water / Terrain")]
        [SerializeField] private float waterProbeTolerance = 8f;
        [SerializeField] private float buoyancyOffset = 0.15f;
        [SerializeField] private float buoyancyLerp = 7f;
        [SerializeField] private float minWaterDepthAboveTerrain = 0.12f;
        [SerializeField] private float terrainClearance = 0.06f;

        private FirstPersonController driverController;
        private StarterAssetsInputs driverInputs;
        private CharacterController driverCharacterController;
        private Transform driverTransform;
        private Transform originalDriverParent;

        private Rigidbody rb;
        private Collider ownCollider;

        public bool IsOccupied => driverController != null;

        private void Reset()
        {
            EnsureAnchors();
        }

        private void Awake()
        {
            EnsureAnchors();
            EnsurePhysics();
        }

        private void OnDisable()
        {
            if (IsOccupied)
            {
                Exit();
            }
        }

        private void FixedUpdate()
        {
            if (!IsOccupied || rb == null || driverInputs == null)
            {
                return;
            }

            Vector2 move = driverInputs.move;
            float forwardInput = Mathf.Clamp(move.y, -1f, 1f);
            float turnInput = Mathf.Clamp(move.x, -1f, 1f);

            bool onWater = TryGetNavigableWaterSurfaceY(out float waterSurfaceY);
            float speedMultiplier = onWater ? 1f : landSpeedMultiplier;
            float turnMultiplier = onWater ? 1f : landTurnMultiplier;

            float maxSpeed = forwardInput >= 0f ? forwardSpeed : reverseSpeed;
            Vector3 displacement = transform.forward * (forwardInput * maxSpeed * speedMultiplier * Time.fixedDeltaTime);
            Vector3 newPosition = rb.position + displacement;

            float terrainY = SampleTerrainHeight(newPosition);

            if (onWater)
            {
                float targetY = waterSurfaceY + buoyancyOffset;
                newPosition.y = Mathf.Lerp(rb.position.y, targetY, Time.fixedDeltaTime * buoyancyLerp);
                if (!float.IsNegativeInfinity(terrainY))
                {
                    newPosition.y = Mathf.Max(newPosition.y, terrainY + terrainClearance);
                }
            }
            else if (!float.IsNegativeInfinity(terrainY))
            {
                newPosition.y = Mathf.Max(rb.position.y, terrainY + terrainClearance);
            }

            rb.MovePosition(newPosition);

            float yaw = turnInput * turnSpeed * turnMultiplier * Time.fixedDeltaTime;
            if (Mathf.Abs(yaw) > 0.0001f)
            {
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));
            }
        }

        public bool CanEnter(Transform interactor)
        {
            if (IsOccupied || interactor == null)
            {
                return false;
            }

            Vector3 target = seatPoint != null ? seatPoint.position : transform.position;
            return Vector3.Distance(interactor.position, target) <= interactDistance;
        }

        public bool TryEnter(FirstPersonController playerController)
        {
            if (playerController == null || IsOccupied || !CanEnter(playerController.transform))
            {
                return false;
            }

            driverController = playerController;
            driverInputs = playerController.GetComponent<StarterAssetsInputs>();
            driverCharacterController = playerController.GetComponent<CharacterController>();
            driverTransform = playerController.transform;
            originalDriverParent = driverTransform.parent;

            if (driverCharacterController != null)
            {
                driverCharacterController.enabled = false;
            }

            driverController.SetMovementLocked(true);

            if (seatPoint != null)
            {
                driverTransform.SetParent(seatPoint, true);
                driverTransform.position = seatPoint.position;
            }
            else
            {
                driverTransform.SetParent(transform, true);
                driverTransform.position = transform.position + transform.up * 1.15f;
            }

            Vector3 euler = driverTransform.eulerAngles;
            driverTransform.rotation = Quaternion.Euler(euler.x, transform.eulerAngles.y, euler.z);
            return true;
        }

        public void Exit()
        {
            if (!IsOccupied)
            {
                return;
            }

            driverTransform.SetParent(originalDriverParent, true);

            Vector3 exitPosition = exitPoint != null
                ? exitPoint.position
                : transform.TransformPoint(new Vector3(1.35f, 0.1f, -1.2f));

            if (driverCharacterController != null)
            {
                driverCharacterController.enabled = true;
                driverCharacterController.Move(exitPosition - driverTransform.position);
            }
            else
            {
                driverTransform.position = exitPosition;
            }

            driverController.SetMovementLocked(false);

            driverController = null;
            driverInputs = null;
            driverCharacterController = null;
            driverTransform = null;
            originalDriverParent = null;
        }

        private void EnsurePhysics()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.useGravity = false;
            rb.linearDamping = 1.8f;
            rb.angularDamping = 3.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            ownCollider = GetComponent<Collider>();
        }

        private void EnsureAnchors()
        {
            if (seatPoint == null)
            {
                seatPoint = GetOrCreateAnchor("_BoatSeat", new Vector3(0f, 1.1f, 0f));
            }

            if (exitPoint == null)
            {
                exitPoint = GetOrCreateAnchor("_BoatExit", new Vector3(1.5f, 0.05f, -1.1f));
            }
        }

        private Transform GetOrCreateAnchor(string objectName, Vector3 localPosition)
        {
            Transform existing = transform.Find(objectName);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(objectName);
            Transform t = go.transform;
            t.SetParent(transform, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        private bool TryGetNavigableWaterSurfaceY(out float waterSurfaceY)
        {
            Vector3 probe = transform.position + Vector3.up * 1.5f;
            if (!WaterSurfaceArea.TryGetClosestSurfaceY(probe, waterProbeTolerance, out waterSurfaceY, ownCollider))
            {
                return false;
            }

            float terrainY = SampleTerrainHeight(probe);
            if (!float.IsNegativeInfinity(terrainY) && waterSurfaceY < terrainY + minWaterDepthAboveTerrain)
            {
                return false;
            }

            return true;
        }

        private static float SampleTerrainHeight(Vector3 worldPosition)
        {
            Terrain t = Terrain.activeTerrain;
            if (t == null)
            {
                return float.NegativeInfinity;
            }

            return t.SampleHeight(worldPosition) + t.transform.position.y;
        }
    }
}
