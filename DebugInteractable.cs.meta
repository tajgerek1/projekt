using UnityEngine;

namespace NightWatch.Foundation
{
    /// <summary>
    /// Test-only helper for validating Stage 2 interaction flow.
    /// Replace with gameplay components (for example TaskTarget) in later stages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "UZYJ";
        [SerializeField] private bool disableObjectOnInteract;
        [SerializeField] private bool changeColorOnInteract = true;
        [SerializeField] private Color interactedColor = Color.green;

        private Renderer cachedRenderer;

        private void Awake()
        {
            cachedRenderer = GetComponentInChildren<Renderer>();
        }

        public string GetPromptText(ToolType currentTool)
        {
            _ = currentTool;
            return promptText;
        }

        public bool CanInteract(ToolType currentTool)
        {
            _ = currentTool;
            return true;
        }

        public void Interact(ToolType currentTool)
        {
            Debug.Log($"[DebugInteractable] Interacted with '{name}' using tool: {currentTool}.", this);

            if (changeColorOnInteract && cachedRenderer != null)
            {
                cachedRenderer.material.color = interactedColor;
            }

            if (disableObjectOnInteract)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
