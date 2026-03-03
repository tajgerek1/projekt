using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class UIDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action<UIDragItem> OnDroppedSomewhere;

        private RectTransform _rt;
        private Canvas _canvas;
        private CanvasGroup _cg;
        private Vector2 _startPos;
        private Transform _startParent;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _cg = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _startPos = _rt.anchoredPosition;
            _startParent = _rt.parent;
            _rt.SetParent(_canvas.transform, true); // na wierzch
            _cg.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, eventData.position, eventData.pressEventCamera, out var localPoint);
            _rt.anchoredPosition = localPoint;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _cg.blocksRaycasts = true;
            OnDroppedSomewhere?.Invoke(this);
        }

        public void ResetToStart()
        {
            _rt.SetParent(_startParent, true);
            _rt.anchoredPosition = _startPos;
        }
    }

    public sealed class UIDropZone : MonoBehaviour, IDropHandler
    {
        public System.Action<UIDragItem> OnDropped;

        public void OnDrop(PointerEventData eventData)
        {
            var item = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<UIDragItem>() : null;
            if (item != null) OnDropped?.Invoke(item);
        }
    }
}
