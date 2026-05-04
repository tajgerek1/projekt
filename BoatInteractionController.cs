using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public class BoatInteractionController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactDistance = 6f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private KeyCode legacyInteractKey = KeyCode.E;

        private FirstPersonController firstPersonController;
        private BoatVehicle currentBoat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_2023_1_OR_NEWER
            FirstPersonController[] players = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            FirstPersonController[] players = Object.FindObjectsOfType<FirstPersonController>();
#endif
            for (int i = 0; i < players.Length; i++)
            {
                FirstPersonController player = players[i];
                if (player != null && player.GetComponent<BoatInteractionController>() == null)
                {
                    player.gameObject.AddComponent<BoatInteractionController>();
                }
            }
        }

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (firstPersonController == null || !WasInteractPressedThisFrame())
            {
                return;
            }

            if (currentBoat != null)
            {
                currentBoat.Exit();
                currentBoat = null;
                return;
            }

            BoatVehicle boat = FindBoatInView();
            if (boat == null)
            {
                boat = FindNearestBoatAround();
            }

            if (boat != null && boat.TryEnter(firstPersonController))
            {
                currentBoat = boat;
            }
        }

        private BoatVehicle FindBoatInView()
        {
            if (playerCamera == null)
            {
                return null;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactionMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            Transform candidate = FindBoatCandidateRoot(hit.transform);
            return EnsureBoatVehicle(candidate);
        }

        private BoatVehicle FindNearestBoatAround()
        {
#if UNITY_2023_1_OR_NEWER
            BoatVehicle[] boats = Object.FindObjectsByType<BoatVehicle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            BoatVehicle[] boats = Object.FindObjectsOfType<BoatVehicle>();
#endif
            BoatVehicle nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < boats.Length; i++)
            {
                BoatVehicle boat = boats[i];
                if (boat == null || !boat.CanEnter(transform))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, boat.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = boat;
                }
            }

            if (nearest != null)
            {
                return nearest;
            }

#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            Transform[] transforms = Object.FindObjectsOfType<Transform>();
#endif
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                {
                    continue;
                }

                string lowerName = t.name.ToLowerInvariant();
                if (!lowerName.Contains("boat"))
                {
                    continue;
                }

                if (Vector3.Distance(transform.position, t.position) > interactDistance)
                {
                    continue;
                }

                BoatVehicle boat = EnsureBoatVehicle(t);
                if (boat != null && boat.CanEnter(transform))
                {
                    return boat;
                }
            }

            return null;
        }

        private static Transform FindBoatCandidateRoot(Transform start)
        {
            Transform current = start;
            while (current != null)
            {
                if (current.GetComponent<BoatVehicle>() != null)
                {
                    return current;
                }

                string lower = current.name.ToLowerInvariant();
                if (lower.Contains("boat"))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static BoatVehicle EnsureBoatVehicle(Transform candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            BoatVehicle vehicle = candidate.GetComponent<BoatVehicle>();
            if (vehicle != null)
            {
                return vehicle;
            }

            return candidate.gameObject.AddComponent<BoatVehicle>();
        }

        private bool WasInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyInteractKey);
#else
            return false;
#endif
        }
    }
}
