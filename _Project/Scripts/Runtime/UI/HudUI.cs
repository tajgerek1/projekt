using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class HudUI
    {
        private readonly Dictionary<StatType, Slider> _sliders = new();
        private readonly Dictionary<StatType, Text> _labels = new();

        public HudUI(Transform parent)
        {
            var hud = UIFactory.Panel(parent, "HUD", new Vector2(0.02f, 0.78f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            var layout = hud.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(14, 14, 14, 14);

            AddBar(hud.transform, StatType.Illumination, "OŚWIETLENIE");
            AddBar(hud.transform, StatType.Order, "PORZĄDEK");
            AddBar(hud.transform, StatType.Gossip, "PLOTKI");
            AddBar(hud.transform, StatType.Fatigue, "ZMĘCZENIE");
            AddBar(hud.transform, StatType.Tension, "NAPIĘCIE");
        }

        private void AddBar(Transform parent, StatType stat, string label)
        {
            var row = new GameObject(stat + "_Row");
            row.transform.SetParent(parent, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childForceExpandWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = false;
            h.spacing = 10;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(250, 28);
            var t = labelGO.AddComponent<Text>();
            t.font = UIFactory.BuiltinFont();
            t.fontSize = 22;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.white;
            t.text = label;

            var slider = UIFactory.CreateSlider(row.transform, "Slider");
            var srt = slider.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(650, 26);

            var valGO = new GameObject("Value");
            valGO.transform.SetParent(row.transform, false);
            var valRT = valGO.AddComponent<RectTransform>();
            valRT.sizeDelta = new Vector2(90, 28);
            var vt = valGO.AddComponent<Text>();
            vt.font = UIFactory.BuiltinFont();
            vt.fontSize = 22;
            vt.alignment = TextAnchor.MiddleRight;
            vt.color = Color.white;
            vt.text = "0";

            _sliders[stat] = slider;
            _labels[stat] = vt;
        }

        public void Bind(GameStats stats)
        {
            foreach (var kv in _sliders)
            {
                var v = stats.Get(kv.Key);
                kv.Value.value = v;
                _labels[kv.Key].text = v.ToString();
            }

            stats.OnChanged += (stat, value) =>
            {
                if (_sliders.TryGetValue(stat, out var s)) s.value = value;
                if (_labels.TryGetValue(stat, out var l)) l.text = value.ToString();
            };
        }
    }
}
