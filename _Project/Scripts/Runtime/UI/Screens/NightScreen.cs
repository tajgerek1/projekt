using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class NightScreen : MonoBehaviour
    {
        private RectTransform _root;
        private HudUI _hud;
        private TaskListUI _tasks;
        private ToastUI _toast;

        private Text _currentTitle;
        private Text _currentDesc;
        private Button _startBtn;
        private Button _endBtn;
        private Text _timer;

        private NightGameManager _mgr;

        public ToastUI Toast => _toast;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var bg = UIFactory.Panel(parent, "NightScreen", new Vector2(0,0), new Vector2(1,1), Vector2.zero, Vector2.zero);
            _root = bg.GetComponent<RectTransform>();
            bg.GetComponent<Image>().color = new Color(0,0,0,0); // tło przezroczyste

            _hud = new HudUI(bg.transform);
            _hud.Bind(_mgr.Stats);

            _tasks = new TaskListUI(this, bg.transform);

            // Current task panel
            var cur = UIFactory.Panel(bg.transform, "CurrentTask", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.16f), Vector2.zero, Vector2.zero);
            cur.GetComponent<Image>().color = new Color(0,0,0,0.6f);

            _currentTitle = UIFactory.Text(cur.transform, "CTitle", "Wybierz zadanie…", 28, TextAnchor.UpperLeft);
            _currentTitle.rectTransform.anchorMin = new Vector2(0.02f, 0.52f);
            _currentTitle.rectTransform.anchorMax = new Vector2(0.70f, 0.98f);

            _currentDesc = UIFactory.Text(cur.transform, "CDesc", "", 22, TextAnchor.UpperLeft);
            _currentDesc.rectTransform.anchorMin = new Vector2(0.02f, 0.05f);
            _currentDesc.rectTransform.anchorMax = new Vector2(0.70f, 0.55f);

            _startBtn = UIFactory.Button(cur.transform, "Start", "START", 32);
            var sbRT = _startBtn.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(0.72f, 0.18f);
            sbRT.anchorMax = new Vector2(0.98f, 0.90f);
            sbRT.offsetMin = Vector2.zero;
            sbRT.offsetMax = Vector2.zero;
            _startBtn.onClick.AddListener(() =>
            {
                if (_mgr.Tasks.Current != null) _mgr.StartCurrentTask();
            });

            _timer = UIFactory.Text(bg.transform, "Timer", "", 22, TextAnchor.UpperRight);
            _timer.rectTransform.anchorMin = new Vector2(0.5f, 0.98f);
            _timer.rectTransform.anchorMax = new Vector2(0.98f, 1f);
            _timer.rectTransform.pivot = new Vector2(1, 1);
            _timer.rectTransform.sizeDelta = new Vector2(0, 50);
            _timer.rectTransform.anchoredPosition = new Vector2(-10, -8);

            _endBtn = UIFactory.Button(bg.transform, "EndNight", "SZYBKI ŚWIT (debug)", 22);
            var ebRT = _endBtn.GetComponent<RectTransform>();
            ebRT.anchorMin = new Vector2(0.02f, 0.98f);
            ebRT.anchorMax = new Vector2(0.35f, 1f);
            ebRT.pivot = new Vector2(0, 1);
            ebRT.sizeDelta = new Vector2(0, 50);
            ebRT.anchoredPosition = new Vector2(10, -8);
            _endBtn.onClick.AddListener(() => _mgr.ForceEndNight());

            _toast = new ToastUI(this, bg.transform);

            Bind();
            Refresh();

            Show();
        }

        private void Bind()
        {
            _mgr.Tasks.OnTaskListChanged += Refresh;
            _mgr.Tasks.OnCurrentTaskChanged += _ => RefreshCurrent();
            _mgr.Events.OnEventHappened += ev => _toast.Show($"EVENT: {ev.Title}\n{ev.Description}", 2.4f);
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_mgr == null || _root == null || !_root.gameObject.activeSelf) return;
            float left = Mathf.Max(0, _mgr.NightDurationSeconds - _mgr.TimeSinceNightStart);
            if (_timer != null)
                _timer.text = $"Do świtu: {left:0}s";
        }

        private void Refresh()
        {
            _tasks.Rebuild(_mgr.Tasks.Active, task =>
            {
                _mgr.Tasks.TrySetCurrent(task);
            });

            // jeśli nic nie wybrane, wybierz pierwsze
            if (_mgr.Tasks.Current == null)
            {
                var first = _mgr.Tasks.Active.FirstOrDefault(t => !t.IsCompleted);
                if (first != null) _mgr.Tasks.TrySetCurrent(first);
            }

            RefreshCurrent();
        }

        private void RefreshCurrent()
        {
            var cur = _mgr.Tasks.Current;
            if (cur == null)
            {
                _currentTitle.text = "Brak aktywnego zadania";
                _currentDesc.text = "To akurat podejrzane…";
                _startBtn.interactable = false;
                return;
            }

            _currentTitle.text = $"{cur.Def.Title}  (<{cur.Def.LocationHint}>)";
            _currentDesc.text = cur.Def.Description;
            _startBtn.interactable = true;
        }
    }
}
