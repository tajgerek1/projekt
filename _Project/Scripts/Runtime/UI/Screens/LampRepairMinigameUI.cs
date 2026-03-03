using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class LampRepairMinigameUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _title;
        private Text _desc;
        private Slider _progress;
        private RectTransform _bar;
        private RectTransform _needle;
        private RectTransform _zone;
        private HoldButton _hold;
        private Button _cancel;

        private NightGameManager _mgr;

        private float _t;
        private float _zoneCenter;
        private float _zoneHalf;
        private float _progressValue;
        private bool _running;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var panel = UIFactory.Panel(parent, "LampRepairScreen",
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            _title = UIFactory.Text(panel.transform, "Title", "MINIGRA: NAPRAWA LATARNI", 34, TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0, 1);
            _title.rectTransform.anchorMax = new Vector2(1, 1);
            _title.rectTransform.pivot = new Vector2(0.5f, 1);
            _title.rectTransform.sizeDelta = new Vector2(0, 80);
            _title.rectTransform.anchoredPosition = new Vector2(0, -10);

            _desc = UIFactory.Text(panel.transform, "Desc",
                "Trzymaj przycisk, gdy wskazówka jest w zielonej strefie. Zapełnij pasek do 100.",
                26, TextAnchor.UpperCenter);
            _desc.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
            _desc.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);
            _desc.rectTransform.offsetMin = Vector2.zero;
            _desc.rectTransform.offsetMax = Vector2.zero;

            // Bar area
            var barGO = UIFactory.Panel(panel.transform, "Bar", new Vector2(0.1f, 0.46f), new Vector2(0.9f, 0.58f), Vector2.zero, Vector2.zero);
            var barImg = barGO.GetComponent<Image>();
            barImg.color = new Color(1, 1, 1, 0.06f);
            _bar = barGO.GetComponent<RectTransform>();

            // Green zone
            var zoneGO = new GameObject("Zone");
            zoneGO.transform.SetParent(barGO.transform, false);
            _zone = zoneGO.AddComponent<RectTransform>();
            _zone.anchorMin = new Vector2(0, 0);
            _zone.anchorMax = new Vector2(0, 1);
            _zone.pivot = new Vector2(0.5f, 0.5f);
            _zone.sizeDelta = new Vector2(140, 0);
            var zoneImg = zoneGO.AddComponent<Image>();
            zoneImg.color = new Color(0.1f, 0.9f, 0.2f, 0.25f);

            // Needle
            var needleGO = new GameObject("Needle");
            needleGO.transform.SetParent(barGO.transform, false);
            _needle = needleGO.AddComponent<RectTransform>();
            _needle.anchorMin = new Vector2(0, 0);
            _needle.anchorMax = new Vector2(0, 1);
            _needle.pivot = new Vector2(0.5f, 0.5f);
            _needle.sizeDelta = new Vector2(10, 0);
            var needleImg = needleGO.AddComponent<Image>();
            needleImg.color = new Color(1, 1, 1, 0.9f);

            // Progress
            var progGO = new GameObject("Progress");
            progGO.transform.SetParent(panel.transform, false);
            var progRT = progGO.AddComponent<RectTransform>();
            progRT.anchorMin = new Vector2(0.1f, 0.33f);
            progRT.anchorMax = new Vector2(0.9f, 0.41f);
            progRT.offsetMin = Vector2.zero;
            progRT.offsetMax = Vector2.zero;
            _progress = UIFactory.CreateSlider(progGO.transform, "Slider");
            _progress.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            _progress.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            _progress.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            _progress.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Hold button
            var holdBtn = UIFactory.Button(panel.transform, "HoldBtn", "TRZYMAJ", 34);
            var holdRT = holdBtn.GetComponent<RectTransform>();
            holdRT.anchorMin = new Vector2(0.18f, 0.12f);
            holdRT.anchorMax = new Vector2(0.82f, 0.28f);
            holdRT.offsetMin = Vector2.zero;
            holdRT.offsetMax = Vector2.zero;

            _hold = holdBtn.gameObject.AddComponent<HoldButton>();
            // Kliknięcie też niech działa (na PC)
            holdBtn.onClick.AddListener(() => { });

            // Cancel
            _cancel = UIFactory.Button(panel.transform, "Cancel", "WRÓĆ", 26);
            var cancelRT = _cancel.GetComponent<RectTransform>();
            cancelRT.anchorMin = new Vector2(0.78f, 0.9f);
            cancelRT.anchorMax = new Vector2(0.98f, 0.98f);
            cancelRT.offsetMin = Vector2.zero;
            cancelRT.offsetMax = Vector2.zero;
            _cancel.onClick.AddListener(() =>
            {
                if (_running) _mgr.CancelMinigame();
            });

            Hide();
        }

        public void ShowForTask(TaskInstance task)
        {
            _running = true;
            _root.gameObject.SetActive(true);

            _progressValue = 0;
            _progress.value = 0;

            // Difficulty: im większe zmęczenie, tym mniejsza strefa.
            float fatigue01 = _mgr.Stats.Get(StatType.Fatigue) / 100f;
            _zoneHalf = Mathf.Lerp(0.18f, 0.08f, fatigue01);
            _zoneCenter = Random.Range(0.25f, 0.75f);

            PlaceZone();

            _t = 0;
        }

        public void Hide()
        {
            _running = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_running) return;

            _t += Time.deltaTime;

            // Needle movement
            float needle01 = Mathf.PingPong(_t * 0.85f, 1f);
            PlaceNeedle(needle01);

            bool inZone = Mathf.Abs(needle01 - _zoneCenter) <= _zoneHalf;

            if (_hold.IsHeld)
            {
                _progressValue += (inZone ? 28f : -18f) * Time.deltaTime;
            }
            else
            {
                _progressValue -= 6f * Time.deltaTime;
            }

            _progressValue = Mathf.Clamp(_progressValue, 0, 100);
            _progress.value = _progressValue;

            if (_progressValue >= 100f)
            {
                _running = false;
                _mgr.FinishMinigame(success: true, quality01: 1f);
            }

            // Timeout w zależności od napięcia (większe napięcie = krócej)
            float tension01 = _mgr.Stats.Get(StatType.Tension) / 100f;
            float timeLimit = Mathf.Lerp(18f, 10f, tension01);
            if (_t > timeLimit)
            {
                _running = false;
                _mgr.FinishMinigame(success: false, quality01: _progressValue / 100f);
            }
        }

        private void PlaceZone()
        {
            float w = _bar.rect.width;
            float x = Mathf.Lerp(0, w, _zoneCenter);
            float zoneW = w * (_zoneHalf * 2f);
            _zone.sizeDelta = new Vector2(Mathf.Max(40, zoneW), 0);
            _zone.anchoredPosition = new Vector2(x, 0);
        }

        private void PlaceNeedle(float needle01)
        {
            float w = _bar.rect.width;
            float x = Mathf.Lerp(0, w, needle01);
            _needle.anchoredPosition = new Vector2(x, 0);
        }
    }
}
