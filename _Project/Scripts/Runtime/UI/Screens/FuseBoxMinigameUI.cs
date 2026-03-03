using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class FuseBoxMinigameUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _title;
        private Text _desc;
        private Text _patternText;
        private Text _timerText;

        private Button _apply;
        private Button _cancel;

        private readonly List<Button> _toggles = new();
        private bool[] _state;
        private bool[] _target;

        private NightGameManager _mgr;
        private bool _running;
        private float _t;
        private float _timeLimit;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var panel = UIFactory.Panel(parent, "FuseScreen",
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            _title = UIFactory.Text(panel.transform, "Title", "MINIGRA: BEZPIECZNIKI", 34, TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0, 1);
            _title.rectTransform.anchorMax = new Vector2(1, 1);
            _title.rectTransform.pivot = new Vector2(0.5f, 1);
            _title.rectTransform.sizeDelta = new Vector2(0, 80);
            _title.rectTransform.anchoredPosition = new Vector2(0, -10);

            _desc = UIFactory.Text(panel.transform, "Desc",
                "Ustaw przełączniki w układzie jak na wzorze (1 = WŁ, 0 = WYŁ).",
                24, TextAnchor.UpperCenter);
            _desc.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
            _desc.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);

            _patternText = UIFactory.Text(panel.transform, "Pattern", "", 34, TextAnchor.MiddleCenter);
            _patternText.rectTransform.anchorMin = new Vector2(0.1f, 0.60f);
            _patternText.rectTransform.anchorMax = new Vector2(0.9f, 0.72f);

            _timerText = UIFactory.Text(panel.transform, "Timer", "", 24, TextAnchor.MiddleCenter);
            _timerText.rectTransform.anchorMin = new Vector2(0.1f, 0.54f);
            _timerText.rectTransform.anchorMax = new Vector2(0.9f, 0.60f);

            // Toggle grid
            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(panel.transform, false);
            var gridRT = gridGO.AddComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0.14f, 0.25f);
            gridRT.anchorMax = new Vector2(0.86f, 0.52f);
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;

            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 120);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            for (int i = 0; i < 6; i++)
            {
                var b = UIFactory.Button(gridGO.transform, $"T{i}", "0", 40);
                var idx = i;
                b.onClick.AddListener(() => Toggle(idx));
                _toggles.Add(b);
            }

            _apply = UIFactory.Button(panel.transform, "Apply", "ZATWIERDŹ", 30);
            var applyRT = _apply.GetComponent<RectTransform>();
            applyRT.anchorMin = new Vector2(0.18f, 0.12f);
            applyRT.anchorMax = new Vector2(0.82f, 0.22f);
            applyRT.offsetMin = Vector2.zero;
            applyRT.offsetMax = Vector2.zero;
            _apply.onClick.AddListener(Check);

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

            _state = new bool[6];
            _target = new bool[6];

            // Losowy wzór
            for (int i = 0; i < 6; i++)
            {
                _target[i] = Random.value > 0.5f;
                _state[i] = false;
            }

            // Zmęczenie skraca czas
            float fatigue01 = _mgr.Stats.Get(StatType.Fatigue) / 100f;
            _timeLimit = Mathf.Lerp(20f, 12f, fatigue01);

            _t = 0;
            Refresh();
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
            float left = Mathf.Max(0, _timeLimit - _t);
            _timerText.text = $"Czas: {left:0.0}s";

            if (_t >= _timeLimit)
            {
                _running = false;
                _mgr.FinishMinigame(success: false, quality01: 0f);
            }
        }

        private void Toggle(int idx)
        {
            if (!_running) return;
            _state[idx] = !_state[idx];
            Refresh();
        }

        private void Refresh()
        {
            string target = "";
            string current = "";
            for (int i = 0; i < 6; i++)
            {
                target += _target[i] ? "1" : "0";
                current += _state[i] ? "1" : "0";

                var label = _toggles[i].GetComponentInChildren<Text>();
                label.text = _state[i] ? "1" : "0";
                _toggles[i].GetComponent<Image>().color = _state[i]
                    ? new Color(0.2f, 0.7f, 1f, 0.22f)
                    : new Color(1f, 1f, 1f, 0.10f);
            }
            _patternText.text = $"WZÓR: {target}   |   TWOJE: {current}";
        }

        private void Check()
        {
            if (!_running) return;

            for (int i = 0; i < 6; i++)
            {
                if (_state[i] != _target[i])
                {
                    _running = false;
                    _mgr.FinishMinigame(success: false, quality01: 0f);
                    return;
                }
            }

            _running = false;
            _mgr.FinishMinigame(success: true, quality01: 1f);
        }
    }
}
