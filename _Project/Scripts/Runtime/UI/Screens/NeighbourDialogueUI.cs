using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NocnaStraz
{
    public sealed class NeighbourDialogueUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _title;
        private Text _line;
        private readonly List<Button> _choices = new();

        private Button _cancel;

        private NightGameManager _mgr;
        private bool _running;

        private int _goodIndex;

        public void Build(NightGameManager mgr, Transform parent)
        {
            _mgr = mgr;

            var panel = UIFactory.Panel(parent, "DialogueScreen",
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);
            _root = panel.GetComponent<RectTransform>();

            _title = UIFactory.Text(panel.transform, "Title", "DIALOG: SĄSIADKA PLOTKARA", 34, TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0, 1);
            _title.rectTransform.anchorMax = new Vector2(1, 1);
            _title.rectTransform.pivot = new Vector2(0.5f, 1);
            _title.rectTransform.sizeDelta = new Vector2(0, 80);
            _title.rectTransform.anchoredPosition = new Vector2(0, -10);

            _line = UIFactory.Text(panel.transform, "Line", "", 28, TextAnchor.MiddleCenter);
            _line.rectTransform.anchorMin = new Vector2(0.08f, 0.60f);
            _line.rectTransform.anchorMax = new Vector2(0.92f, 0.78f);

            // Choices container
            var box = new GameObject("Choices");
            box.transform.SetParent(panel.transform, false);
            var rt = box.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.12f, 0.20f);
            rt.anchorMax = new Vector2(0.88f, 0.58f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < 3; i++)
            {
                var b = UIFactory.Button(box.transform, $"Choice{i}", "...", 26);
                var brt = b.GetComponent<RectTransform>();
                brt.sizeDelta = new Vector2(0, 92);

                int idx = i;
                b.onClick.AddListener(() => Choose(idx));
                _choices.Add(b);
            }

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

            var scenarios = new[]
            {
                "— Panie strażniku, a czemu ta latarnia mruga? To jakiś znak… czy promocja?",
                "— Widziałam Pana. Pan chodzi. Nocą. To się ludziom kojarzy…",
                "— A te śmieci to celowo? Bo jak to performance, to ja mogę udostępnić!",
            };
            _line.text = scenarios[Random.Range(0, scenarios.Length)];

            var options = new[]
            {
                "Uśmiech i uprzejmie: 'Już ogarniam, dziękuję za czujność!'",
                "Pół-żart: 'To latarnia ćwiczy stand-up. Ja tylko ochrona publiczności.'",
                "Surowo: 'Proszę nie przeszkadzać, prowadzę poważne czynności nocne.'",
                "Szeptem: 'Ciszej… bo cień słyszy. Tak mówią.'",
                "Przekupstwo: 'Jak będzie dobrze w raporcie, to ja… no… podleję Pani kwiatki.'",
                "Zmiana tematu: 'A Pani wie, że kosze na śmieci mają uczucia?'"
            };

            // Losowo wybieramy 3 odpowiedzi
            var idxs = new List<int>();
            while (idxs.Count < 3)
            {
                int r = Random.Range(0, options.Length);
                if (!idxs.Contains(r)) idxs.Add(r);
            }

            // Jedna z nich to "dobra" (zwykle 0 albo 1 w tej miniliscie)
            _goodIndex = Random.Range(0, 3);

            for (int i = 0; i < 3; i++)
            {
                _choices[i].GetComponentInChildren<Text>().text = options[idxs[i]];
            }
        }

        public void Hide()
        {
            _running = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Choose(int idx)
        {
            if (!_running) return;
            _running = false;

            // MVP: jedna "dobra" odpowiedź
            bool success = idx == _goodIndex;

            // Dodatkowo: jeśli napięcie wysokie, nawet dobra odpowiedź bywa średnia :)
            float tension01 = _mgr.Stats.Get(StatType.Tension) / 100f;
            if (success && tension01 > 0.75f && Random.value < 0.35f) success = false;

            _mgr.FinishMinigame(success: success, quality01: success ? 1f : 0f);
        }
    }
}
