using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NocnaStraz
{
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }
        public event Action<bool> OnHoldChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            OnHoldChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
            OnHoldChanged?.Invoke(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // gdy palec wyjedzie poza przycisk
            if (IsHeld)
            {
                IsHeld = false;
                OnHoldChanged?.Invoke(false);
            }
        }
    }
}
