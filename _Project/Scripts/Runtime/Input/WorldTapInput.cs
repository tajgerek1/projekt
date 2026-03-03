using UnityEngine;

namespace NocnaStraz
{
    public sealed class WorldTapInput : MonoBehaviour
    {
        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                var ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 500f))
                {
                    var inter = hit.collider.GetComponentInParent<Interactable>();
                    if (inter != null) inter.Tap();
                }
            }
        }
    }
}
