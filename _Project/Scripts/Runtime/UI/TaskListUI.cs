using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class TaskListUI
    {
        private readonly RectTransform _root;
        private readonly RectTransform _content;
        private readonly MonoBehaviour _runner;

        private readonly List<Button> _buttons = new();

        public TaskListUI(MonoBehaviour runner, Transform parent)
        {
            _runner = runner;
            var panel = UIFactory.Panel(parent, "Tasks", new Vector2(0.02f, 0.16f), new Vector2(0.98f, 0.78f), Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            var title = new GameObject("Title");
            title.transform.SetParent(panel.transform, false);
            var titleRT = title.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.sizeDelta = new Vector2(0, 60);
            titleRT.anchoredPosition = new Vector2(0, 0);
            var t = title.AddComponent<Text>();
            t.font = UIFactory.BuiltinFont();
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "ZADANIA NA TĘ NOC";

            // ScrollView
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(14, 14);
            scrollRT.offsetMax = new Vector2(-14, -74);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = new Vector2(0, 0);
            vpRT.anchorMax = new Vector2(1, 1);
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(1,1,1,0.02f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _content = content.AddComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 0);

            var v = content.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 10;
            v.padding = new RectOffset(0, 0, 0, 0);

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRT;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
        }

        public void Rebuild(IReadOnlyList<TaskInstance> tasks, Action<TaskInstance> onClicked)
        {
            for (int i = 0; i < _content.childCount; i++)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _buttons.Clear();

            foreach (var task in tasks)
            {
                if (task.IsCompleted) continue;

                var btn = UIFactory.Button(_content, task.Def.Id, $"{task.Def.Title}\n< {task.Def.LocationHint} >", 24);
                var rt = btn.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 110);

                btn.onClick.AddListener(() => onClicked?.Invoke(task));
                _buttons.Add(btn);
            }
        }
    }
}
