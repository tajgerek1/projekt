using UnityEngine;

namespace NocnaStraz
{
    public enum InteractableKind
    {
        Lamp,
        Bin,
        Trash
    }

    public sealed class Interactable : MonoBehaviour
    {
        public InteractableKind Kind;
        public string Id;
        public bool Flag; // np. przepełniony kosz

        public void Tap()
        {
            NightGameManager.Instance?.OnWorldTapped(this);
        }
    }
}
