using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NocnaStraz
{
    public static class UIFactory
    {
        public static Canvas EnsureCanvas(string name = "[UI] Canvas")
        {
            var existing = Object.FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;

            var canvasGO = new GameObject(name);
            Object.DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var es = new GameObject("[UI] EventSystem");
            Object.DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static Font BuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static GameObject Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.55f);

            return go;
        }

        public static Text Text(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.font = BuiltinFont();
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.text = text;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return t;
        }

        public static Button Button(Transform parent, string name, string label, int fontSize = 28)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Runtime-created GameObject has Transform by default; UI requires RectTransform.
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.12f);

            var btn = go.AddComponent<Button>();

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);

            // Explicit RectTransform for safety (some runtimes won't auto-convert).
            var txtRT = txtGO.AddComponent<RectTransform>();
            var t = txtGO.AddComponent<Text>();
            t.font = BuiltinFont();
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;

            txtRT.anchorMin = new Vector2(0, 0);
            txtRT.anchorMax = new Vector2(1, 1);
            txtRT.offsetMin = new Vector2(10, 6);
            txtRT.offsetMax = new Vector2(-10, -6);

            return btn;
        }

        // Uwaga: nazwa metody nie może kolidować z typem UnityEngine.UI.Slider,
        // bo wtedy w tym pliku "Slider.Direction" zaczyna rozwiązywać się do metody.
        public static Slider CreateSlider(Transform parent, string name)
        {
            // Prosty slider: background + fill
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 26);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.08f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0);
            faRT.anchorMax = new Vector2(1, 1);
            faRT.offsetMin = new Vector2(4, 4);
            faRT.offsetMax = new Vector2(-4, -4);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0, 0);
            fillRT.anchorMax = new Vector2(1, 1);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.6f, 0.9f, 1f, 0.7f);

            var s = go.AddComponent<Slider>();
            s.minValue = 0;
            s.maxValue = 100;
            s.wholeNumbers = true;
            s.fillRect = fillRT;
            s.targetGraphic = fillImg;
            s.direction = Slider.Direction.LeftToRight;

            // Slider bez handle
            s.handleRect = null;

            return s;
        }
    }
}
