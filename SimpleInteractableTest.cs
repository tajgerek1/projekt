using UnityEngine;

namespace NightWatch.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SimpleInteractableTest : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "E - Test";
        [SerializeField] private string logMessage = "SimpleInteractableTest: Interaction OK.";

        public string GetInteractionPrompt(PlayerInteractor interactor)
        {
            _ = interactor;
            return promptText;
        }

        public void Interact(PlayerInteractor interactor)
        {
            _ = interactor;
            Debug.Log(logMessage, this);
        }
    }
}
