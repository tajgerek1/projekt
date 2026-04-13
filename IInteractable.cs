namespace NightWatch.Interaction
{
    public interface IInteractable
    {
        string GetInteractionPrompt(PlayerInteractor interactor);
        void Interact(PlayerInteractor interactor);
    }
}
