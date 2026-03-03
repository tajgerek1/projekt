using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NocnaStraz
{
    public sealed class NightGameManager : MonoBehaviour
    {
        public static NightGameManager Instance { get; private set; }

        public GameStats Stats { get; private set; }
        public TaskManager Tasks { get; private set; }
        public RandomEventSystem Events { get; private set; }

        public float NightDurationSeconds { get; private set; } = 210f; // ~3,5 min
        public float TimeSinceNightStart => Time.time - _nightStart;

        private float _nightStart;
        private float _driftTimer;

        private ParkWorld _world;
        private WorldTapInput _tapInput;

        // UI
        private Canvas _canvas;
        private NightScreen _nightScreen;
        private LampRepairMinigameUI _lampRepair;
        private CleaningMinigameUI _cleaning;
        private FuseBoxMinigameUI _fuse;
        private NeighbourDialogueUI _dialogue;
        private InspectionUI _inspection;

        private enum Mode { Night, Minigame, Inspection }
        private Mode _mode = Mode.Night;

        // QuickTapBins state
        private int _binsLeft;
        private bool _binOverflowSeen;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Stats = new GameStats();
            Tasks = new TaskManager(Stats);
            Events = new RandomEventSystem();

            BuildWorld();
            BuildUI();

            _nightStart = Time.time;

            SyncWorldWithStats();
            _world.RandomizeBins();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildWorld()
        {
            var worldGO = new GameObject("[World] Park");
            worldGO.transform.SetParent(transform, false);
            _world = worldGO.AddComponent<ParkWorld>();
            // v1.1.2: NIE budujemy mapy automatycznie.
            // Użytkownik ustawia obiekty ręcznie w scenie (lampy/kosze/śmieci itp.).
            // ParkWorld służy teraz głównie do "zbierania" Interactable i sterowania światłami.
            _world.ScanScene();

            var inputGO = new GameObject("[Input] WorldTap");
            inputGO.transform.SetParent(transform, false);
            _tapInput = inputGO.AddComponent<WorldTapInput>();

            // Kamera: jeśli brak (np. scena pusta), dodaj
            if (Camera.main == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.transform.position = new Vector3(0, 9, -12);
                camGO.transform.rotation = Quaternion.Euler(15, 0, 0);
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }

            // Światło: jeśli brak kierunkowego (chcemy delikatny fill nawet gdy są lampy punktowe)
            bool hasDirectional = false;
            foreach (var l0 in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l0 != null && l0.type == LightType.Directional) { hasDirectional = true; break; }
            }
            if (!hasDirectional)
            {
                var lGO = new GameObject("Directional Light");
                lGO.transform.rotation = Quaternion.Euler(40, -20, 0);
                var l = lGO.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 0.35f;
                l.color = new Color(0.15f, 0.15f, 0.2f);
            }
        }

        private void BuildUI()
        {
            UIFactory.EnsureEventSystem();

            // Własny canvas pod managerem (łatwy restart + brak konfliktów z innymi scenami/projektami)
            var canvasGO = new GameObject("[NocnaStraz] Canvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;

            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Night
            var nightGO = new GameObject("NightScreen");
            nightGO.transform.SetParent(canvasGO.transform, false);
            _nightScreen = nightGO.AddComponent<NightScreen>();
            _nightScreen.Build(this, nightGO.transform);

            // Minigames
            _lampRepair = CreateScreen<LampRepairMinigameUI>(canvasGO.transform, "LampRepairMinigame");
            _lampRepair.Build(this, canvasGO.transform);

            _cleaning = CreateScreen<CleaningMinigameUI>(canvasGO.transform, "CleaningMinigame");
            _cleaning.Build(this, canvasGO.transform);

            _fuse = CreateScreen<FuseBoxMinigameUI>(canvasGO.transform, "FuseBoxMinigame");
            _fuse.Build(this, canvasGO.transform);

            _dialogue = CreateScreen<NeighbourDialogueUI>(canvasGO.transform, "DialogueMinigame");
            _dialogue.Build(this, canvasGO.transform);

            _inspection = CreateScreen<InspectionUI>(canvasGO.transform, "Inspection");
            _inspection.Build(this, canvasGO.transform);

            HideAllMinigames();
            _inspection.Hide();
        }

        private T CreateScreen<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private void Update()
        {
            if (_mode == Mode.Night)
            {
                // Dev-helper: F5 = rescan interactables (useful after ręczne zmiany w scenie)
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    _world?.ScanScene();
                    _nightScreen?.Toast?.Show("Odświeżono obiekty interaktywne (F5).", 1.4f);
                }

                // Losowe eventy
                Events.Tick(Stats, Tasks, TimeSinceNightStart);

                // Powolny drift stanu, żeby gra "żyła"
                _driftTimer += Time.deltaTime;
                if (_driftTimer >= 8f)
                {
                    _driftTimer = 0f;
                    Drift();
                }

                SyncWorldWithStats();

                if (TimeSinceNightStart >= NightDurationSeconds)
                    EndNight();
            }
        }

        private void Drift()
        {
            // Zmęczenie rośnie zawsze
            Stats.Add(StatType.Fatigue, +2);

            // Jeśli ciemno -> napięcie rośnie
            if (Stats.Get(StatType.Illumination) < 45) Stats.Add(StatType.Tension, +3);

            // Jeśli bałagan -> plotki rosną
            if (Stats.Get(StatType.Order) < 45) Stats.Add(StatType.Gossip, +3);

            // Minimalny spadek porządku (ludzie istnieją)
            Stats.Add(StatType.Order, -1);

            // v1.1.2: nie generujemy automatycznie obiektów "śmieci" w świecie.
            // Jeśli chcesz wizualne śmieci – dodaj je ręcznie do sceny i oznacz komponentem Interactable (Kind=Trash).

            // Utrzymuj pulę zadań: min. 3
            EnsureTaskCount(3);
        }

        private void EnsureTaskCount(int min)
        {
            int active = 0;
            foreach (var t in Tasks.Active)
                if (!t.IsCompleted) active++;

            while (active < min)
            {
                Tasks.EnqueueRandom(Stats);
                active++;
            }
        }

        private void SyncWorldWithStats()
        {
            if (_world == null) return;
            _world.ApplyIllumination(Stats.Get(StatType.Illumination));
        }

        public void OnWorldTapped(Interactable inter)
        {
            if (inter == null) return;

            // Tap śmiecia = szybkie +porządek (mikro interakcja)
            if (_mode == Mode.Night && inter.Kind == InteractableKind.Trash)
            {
                Destroy(inter.gameObject);
                Stats.Add(StatType.Order, +1);
                _nightScreen.Toast.Show("Śmieć usunięty. Porządek +1");
                return;
            }

            // QuickTapBins task logic
            if (_mode == Mode.Night && Tasks.Current != null && Tasks.Current.Def.Kind == TaskKind.QuickTapBins && inter.Kind == InteractableKind.Bin)
            {
                _binsLeft--;
                if (inter.Flag)
                {
                    _binOverflowSeen = true;
                    _nightScreen.Toast.Show($"{inter.Id}: przepełniony! (Plotki lubią takie rzeczy…)");
                    // doraźna kara
                    Stats.Add(StatType.Order, -3);
                    Stats.Add(StatType.Gossip, +2);
                }
                else
                {
                    _nightScreen.Toast.Show($"{inter.Id}: OK. ({_binsLeft} do sprawdzenia)");
                }

                if (_binsLeft <= 0)
                {
                    // Zadanie wykonane
                    FinishBinsTask();
                }
                return;
            }

            // Tap latarni: jeśli bardzo ciemno – podrzucamy naprawę (dla klimatu)
            if (_mode == Mode.Night && inter.Kind == InteractableKind.Lamp && Stats.Get(StatType.Illumination) < 55)
            {
                Tasks.EnqueueRandom(Stats);
                _nightScreen.Toast.Show($"{inter.Id}: mruga. Dorzucono dodatkowe zadanie.", 2.0f);
                return;
            }
        }

        public void StartCurrentTask()
        {
            var cur = Tasks.Current;
            if (cur == null) return;

            if (cur.Def.Kind == TaskKind.QuickTapBins)
            {
                _binsLeft = 3;
                _binOverflowSeen = false;
                // Odśwież listę koszy z aktualnej sceny (jeśli użytkownik coś dodał/usunął).
                _world?.ScanScene();
                _world.RandomizeBins();
                _nightScreen.Toast.Show("Kontrola koszy: kliknij 3 kosze w 3D.", 2.6f);
                return;
            }

            _mode = Mode.Minigame;
            _nightScreen.Hide();
            HideAllMinigames();

            switch (cur.Def.Kind)
            {
                case TaskKind.RepairLamp:
                    _lampRepair.ShowForTask(cur);
                    break;
                case TaskKind.CleanPath:
                    _cleaning.ShowForTask(cur);
                    break;
                case TaskKind.FuseBox:
                    _fuse.ShowForTask(cur);
                    break;
                case TaskKind.NeighbourDialogue:
                    _dialogue.ShowForTask(cur);
                    break;
                default:
                    // fallback
                    _mode = Mode.Night;
                    _nightScreen.Show();
                    break;
            }
        }

        public void CancelMinigame()
        {
            // Anulowanie traktujemy jak porażkę (żeby było ryzyko)
            FinishMinigame(success: false, quality01: 0f);
        }

        public void FinishMinigame(bool success, float quality01)
        {
            var before = SnapshotStats();

            Tasks.CompleteCurrent(Stats, success);
            Tasks.RemoveCompleted();
            EnsureTaskCount(3);

            var after = SnapshotStats();
            var deltaText = BuildDeltaText(before, after);

            HideAllMinigames();
            _mode = Mode.Night;
            _nightScreen.Show();

            _nightScreen.Toast.Show((success ? "Sukces! " : "Porażka. ") + deltaText, 2.4f);

            SyncWorldWithStats();
        }

        private void FinishBinsTask()
        {
            var before = SnapshotStats();

            // Sukces zadania, ale jeśli były przepełnione -> dopalona kara już poszła
            Tasks.CompleteCurrent(Stats, success: true);
            Tasks.RemoveCompleted();
            EnsureTaskCount(3);

            var after = SnapshotStats();
            var deltaText = BuildDeltaText(before, after);

            _nightScreen.Toast.Show("Kontrola koszy zakończona. " + deltaText, 2.4f);
        }

        private Dictionary<StatType, int> SnapshotStats()
        {
            return new Dictionary<StatType, int>
            {
                {StatType.Illumination, Stats.Get(StatType.Illumination)},
                {StatType.Order, Stats.Get(StatType.Order)},
                {StatType.Gossip, Stats.Get(StatType.Gossip)},
                {StatType.Fatigue, Stats.Get(StatType.Fatigue)},
                {StatType.Tension, Stats.Get(StatType.Tension)},
            };
        }

        private string BuildDeltaText(Dictionary<StatType, int> before, Dictionary<StatType, int> after)
        {
            var sb = new StringBuilder();
            void add(StatType st, string shortName)
            {
                int d = after[st] - before[st];
                if (d == 0) return;
                if (sb.Length > 0) sb.Append("  |  ");
                sb.Append(shortName).Append(d > 0 ? " +" : " ").Append(d);
            }
            add(StatType.Illumination, "Ośw.");
            add(StatType.Order, "Porz.");
            add(StatType.Gossip, "Plot.");
            add(StatType.Fatigue, "Zm.");
            add(StatType.Tension, "Nap.");
            if (sb.Length == 0) sb.Append("Brak zmian.");
            return sb.ToString();
        }

        private void HideAllMinigames()
        {
            _lampRepair.Hide();
            _cleaning.Hide();
            _fuse.Hide();
            _dialogue.Hide();
        }

        private void EndNight()
        {
            _mode = Mode.Inspection;
            _nightScreen.Hide();
            HideAllMinigames();

            string report = BuildInspectionReport();
            _inspection.ShowResult(report);
        }

        public void ForceEndNight()
        {
            if (_mode == Mode.Night) EndNight();
        }

        private string BuildInspectionReport()
        {
            int illum = Stats.Get(StatType.Illumination);
            int order = Stats.Get(StatType.Order);
            int gossip = Stats.Get(StatType.Gossip);
            int fatigue = Stats.Get(StatType.Fatigue);
            int tension = Stats.Get(StatType.Tension);

            float score =
                0.32f * illum +
                0.32f * order +
                0.16f * (100 - gossip) +
                0.10f * (100 - fatigue) +
                0.10f * (100 - tension);

            string grade;
            string comment;

            if (score >= 85)
            {
                grade = "PERFEKCYJNIE";
                comment = Pick(new[]
                {
                    "— Nie wiem, jak Pan to zrobił, ale park wygląda… jakby był w reklamie.",
                    "— To aż podejrzane. Tu na pewno ktoś gdzieś nie zjadł chipsów?",
                    "— Wreszcie nocna zmiana, po której mam mniej roboty niż po dniu wolnym."
                });
            }
            else if (score >= 70)
            {
                grade = "PRZEJDZIE, ALE…";
                comment = Pick(new[]
                {
                    "— Da się przejść ścieżką bez uciekania przed workiem ze śmieciami. To sukces.",
                    "— Lampy świecą. Nie wszystkie… ale świecą. Doceniam konsekwencję.",
                    "— Plotki tylko trochę kipią. Jak na osiedle: świetnie."
                });
            }
            else if (score >= 55)
            {
                grade = "NA STYKU";
                comment = Pick(new[]
                {
                    "— W raporcie napiszę: 'Klimatycznie'. To słowo przykrywa dużo.",
                    "— Park przeżył noc. Pytanie czy przeżyje mój podpis.",
                    "— Latarnie mają osobowość. Szkoda, że głównie buntowniczą."
                });
            }
            else
            {
                grade = "RAPORT DO RATUSZA";
                comment = Pick(new[]
                {
                    "— Czy park jest w remoncie? Bo wygląda jakby remont był w parku, a nie odwrotnie.",
                    "— Gratuluję. Widziałem memy mniej chaotyczne niż ta alejka.",
                    "— Proszę się nie martwić. W ratuszu lubią historie. A tu jest cała saga."
                });
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Wynik inspekcji: {grade}");
            sb.AppendLine();
            sb.AppendLine(comment);
            sb.AppendLine();
            sb.AppendLine("Podsumowanie pasków:");
            sb.AppendLine($"- Oświetlenie: {illum}");
            sb.AppendLine($"- Porządek: {order}");
            sb.AppendLine($"- Plotki: {gossip}");
            sb.AppendLine($"- Zmęczenie: {fatigue}");
            sb.AppendLine($"- Napięcie: {tension}");
            sb.AppendLine();
            sb.AppendLine($"(Score: {score:0.0}/100)");

            return sb.ToString();
        }

        private string Pick(string[] options)
        {
            if (options == null || options.Length == 0) return "";
            return options[UnityEngine.Random.Range(0, options.Length)];
        }

        public void RestartRun()
        {
            var runner = new GameObject("[NocnaStraz] RestartRunner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<RestartHelper>().Begin(this);
        }
    }
}
