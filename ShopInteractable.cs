using NightWatch.Foundation;
using NightWatch.Ui;
using UnityEngine;

namespace NightWatch.World
{
    [DisallowMultipleComponent]
    public sealed class ShopInteractable : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private ShopScreenController shopScreenController;

        [Header("Prompt")]
        [SerializeField] private string promptText = "OTWORZ SKLEP";
        [SerializeField] private string openedPromptText = "SKLEP OTWARTY";

        public string GetPromptText(ToolType currentTool)
        {
            _ = currentTool;

            if (shopScreenController == null)
            {
                return "BRAK SKLEPU";
            }

            return shopScreenController.IsOpen ? openedPromptText : promptText;
        }

        public bool CanInteract(ToolType currentTool)
        {
            _ = currentTool;
            return shopScreenController != null && !shopScreenController.IsOpen;
        }

        public void Interact(ToolType currentTool)
        {
            _ = currentTool;

            if (!CanInteract(currentTool))
            {
                return;
            }

            shopScreenController.OpenShop();
        }
    }
}
