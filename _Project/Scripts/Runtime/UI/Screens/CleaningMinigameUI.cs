using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class CleaningMinigameUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _title;
        private Text _desc;
        private Text _counter;
        private Button _cancel;

        private UIDropZone _binZone;
        private RectTransform _itemsRoot;

        private NightGameManager _mgr;
        private bool _running;
        private float _t;

        private int _toClean;
        private int _cleaned;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var panel = UIFactory.Panel(parent, "CleaningScreen",
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            _title = UIFactory.Text(panel.transform, "Title", "MINIGRA: SPRZĄTANIE", 34, TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0, 1);
            _title.rectTransform.anchorMax = new Vector2(1, 1);
            _title.rectTransform.pivot = new Vector2(0.5f, 1);
            _title.rectTransform.sizeDelta = new Vector2(0, 80);
            _title.rectTransform.anchoredPosition = new Vector2(0, -10);

            _desc = UIFactory.Text(panel.transform, "Desc",
                "Przeciągnij śmieci/liście do kosza. Uwaga: sąsiadka 'patrzy' szybciej gdy rosną plotki.",
                24, TextAnchor.UpperCenter);
            _desc.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
            _desc.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);

            _counter = UIFactory.Text(panel.transform, "Counter", "", 28, TextAnchor.MiddleCenter);
            _counter.rectTransform.anchorMin = new Vector2(0.1f, 0.62f);
            _counter.rectTransform.anchorMax = new Vector2(0.9f, 0.72f);

            // Bin area
            var bin = UIFactory.Panel(panel.transform, "Bin",
                new Vector2(0.72f, 0.18f), new Vector2(0.92f, 0.55f),
                Vector2.zero, Vector2.zero);
            bin.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 0.18f);

            var binLabel = UIFactory.Text(bin.transform, "BinLabel", "KOSZ", 28, TextAnchor.MiddleCenter);

            _binZone = bin.AddComponent<UIDropZone>();
            _binZone.OnDropped += OnDroppedToBin;

            // Items root
            var items = new GameObject("Items");
            items.transform.SetParent(panel.transform, false);
            _itemsRoot = items.AddComponent<RectTransform>();
            _itemsRoot.anchorMin = new Vector2(0.08f, 0.18f);
            _itemsRoot.anchorMax = new Vector2(0.68f, 0.55f);
            _itemsRoot.offsetMin = Vector2.zero;
            _itemsRoot.offsetMax = Vector2.zero;

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

            _cleaned = 0;

            // Ilość zależy od napięcia (więcej chaosu)
            float tension01 = _mgr.Stats.Get(StatType.Tension) / 100f;
            _toClean = Mathf.RoundToInt(Mathf.Lerp(7, 11, tension01));

            _t = 0;

            RebuildItems();
            UpdateCounter();
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

            // Sąsiadka "patrzy": im wyższe plotki, tym krótszy limit.
            float gossip01 = _mgr.Stats.Get(StatType.Gossip) / 100f;
            float timeLimit = Mathf.Lerp(24f, 14f, gossip01);

            if (_t > timeLimit)
            {
                _running = false;
                _mgr.FinishMinigame(success: false, quality01: (float)_cleaned / _toClean);
            }
        }

        private void RebuildItems()
        {
            for (int i = _itemsRoot.childCount - 1; i >= 0; i--)
                Destroy(_itemsRoot.GetChild(i).gameObject);

            float w = Mathf.Max(520f, _itemsRoot.rect.width);
            float h = Mathf.Max(360f, _itemsRoot.rect.height);

            for (int i = 0; i < _toClean; i++)
            {
                var itemGO = new GameObject($"Item{i+1}");
                itemGO.transform.SetParent(_itemsRoot, false);
                var rt = itemGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(120, 120);
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    Random.Range(70f, w - 70f),
                    -Random.Range(70f, h - 70f)
                );

                var img = itemGO.AddComponent<Image>();
                img.color = new Color(0.8f, 0.8f, 0.9f, 0.25f);

                var drag = itemGO.AddComponent<UIDragItem>();
                drag.OnDroppedSomewhere += OnDragEnd;
            }
        }

        private void OnDragEnd(UIDragItem item)
        {
            // Jeśli nie wpadło do kosza, wróć na start.
            if (item != null) item.ResetToStart();
        }

        private void OnDroppedToBin(UIDragItem item)
        {
            if (!_running || item == null) return;

            Destroy(item.gameObject);
            _cleaned++;
            UpdateCounter();

            if (_cleaned >= _toClean)
            {
                _running = false;
                _mgr.FinishMinigame(success: true, quality01: 1f);
            }
        }

        private void UpdateCounter()
        {
            _counter.text = $"Uprzątnięte: {_cleaned}/{_toClean}";
        }
    }
}
