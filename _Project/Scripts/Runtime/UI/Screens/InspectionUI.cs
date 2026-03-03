using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class InspectionUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _title;
        private Text _body;
        private Button _restart;

        private NightGameManager _mgr;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var panel = UIFactory.Panel(parent, "InspectionScreen",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f),
                Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            _title = UIFactory.Text(panel.transform, "Title", "PORANNA INSPEKCJA", 40, TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0, 1);
            _title.rectTransform.anchorMax = new Vector2(1, 1);
            _title.rectTransform.pivot = new Vector2(0.5f, 1);
            _title.rectTransform.sizeDelta = new Vector2(0, 90);
            _title.rectTransform.anchoredPosition = new Vector2(0, -10);

            _body = UIFactory.Text(panel.transform, "Body", "", 28, TextAnchor.UpperLeft);
            _body.rectTransform.anchorMin = new Vector2(0.08f, 0.22f);
            _body.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);

            _restart = UIFactory.Button(panel.transform, "Restart", "NOWA NOC", 34);
            var rt = _restart.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.08f);
            rt.anchorMax = new Vector2(0.8f, 0.18f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _restart.onClick.AddListener(() => _mgr.RestartRun());

            Hide();
        }

        public void ShowResult(string text)
        {
            _root.gameObject.SetActive(true);
            _body.text = text;
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
