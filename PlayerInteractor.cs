using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightWatch.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InteractionPromptUI promptUI;

        [Header("Raycast")]
        [SerializeField] [Min(0.1f)] private float interactionDistance = 4f;
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;

        private bool missingReferencesLogged;

        private void Update()
        {
            if (!HasValidReferences())
            {
                return;
            }

            if (!TryGetLookedAtInteractable(out IInteractable interactable))
            {
                promptUI.HidePrompt();
                return;
            }

            string prompt = interactable.GetInteractionPrompt(this);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                promptUI.HidePrompt();
                return;
            }

            promptUI.ShowPrompt(prompt);

            if (WasInteractPressedThisFrame())
            {
                if (verboseLogs)
                {
                    Debug.Log($"[PlayerInteractor] Interacted with '{(interactable as Object)?.name ?? "Unknown"}'.", this);
                }

                interactable.Interact(this);
            }
        }

        private bool HasValidReferences()
        {
            if (playerCamera != null && promptUI != null)
            {
                return true;
            }

            if (!missingReferencesLogged)
            {
                missingReferencesLogged = true;
                Debug.LogError("[PlayerInteractor] Missing references. Assign Player Camera and Prompt UI in Inspector.", this);
            }

            return false;
        }

        private bool TryGetLookedAtInteractable(out IInteractable interactable)
        {
            interactable = null;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            MonoBehaviour[] candidates = hit.transform.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IInteractable candidate)
                {
                    interactable = candidate;
                    return true;
                }
            }

            return false;
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
            return Input.GetKeyDown(interactKey);
#else
            return false;
#endif
        }
    }
}
