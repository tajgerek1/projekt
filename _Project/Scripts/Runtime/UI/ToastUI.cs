using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class ToastUI
    {
        private readonly RectTransform _root;
        private readonly Text _text;
        private readonly MonoBehaviour _runner;
        private Coroutine _co;

        public ToastUI(MonoBehaviour runner, Transform parent)
        {
            _runner = runner;
            var panel = UIFactory.Panel(parent, "Toast", new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.12f), Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();
            _root.gameObject.SetActive(false);

            _text = UIFactory.Text(panel.transform, "ToastText", "", 28, TextAnchor.MiddleCenter);
        }

        public void Show(string msg, float seconds = 2.2f)
        {
            _text.text = msg;
            _root.gameObject.SetActive(true);

            if (_co != null) _runner.StopCoroutine(_co);
            _co = _runner.StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float s)
        {
            yield return new WaitForSeconds(s);
            _root.gameObject.SetActive(false);
        }
    }
}
