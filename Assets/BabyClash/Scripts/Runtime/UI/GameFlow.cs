using System;
using System.Collections;
using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BabyClash
{
    [Serializable]
    public sealed class TitleMenu
    {
        public GameObject root;
        public Button onePlayer;
        public Button twoPlayers;
        public Button howToPlay;
        public Button options;
        public Button quit;
        public UnityEngine.UI.Text challenges;
    }

    [Serializable]
    public sealed class DifficultyMenu
    {
        public GameObject root;
        public Button easy;
        public Button normal;
        public Button hard;
        public Button back;
    }

    [Serializable]
    public sealed class InfoMenu
    {
        public GameObject root;
        public Button back;
    }

    [Serializable]
    public sealed class OptionsMenu
    {
        public GameObject root;
        public Slider music;
        public Slider effects;
        public Toggle fullscreen;
        public Button back;
    }

    [Serializable]
    public sealed class PauseMenu
    {
        public GameObject root;
        public Button resume;
        public Button restart;
        public Button options;
        public Button mainMenu;
    }

    [Serializable]
    public sealed class ResultsMenu
    {
        public GameObject root;
        public Text title;
        public Text detail;
        public Button rematch;
        public Button mainMenu;
    }

    /// <summary>
    /// Top-level game flow: title screen (with a CPU-vs-CPU demo playing behind it), difficulty select, how to play,
    /// options, the match itself, pause and results. Decides who controls each baby (keyboard/gamepad or CPU).
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        enum Screen
        {
            Title,
            Difficulty,
            HowToPlay,
            Options,
            Match,
            Paused,
            Results,
        }

        [SerializeField] RoundManager roundManager;
        [SerializeField] AudioDirector audioDirector;
        [SerializeField] TimeController timeController;
        [SerializeField] HUDController hud;

        [Tooltip("Loading screen that compiles shaders before the title appears (optional).")]
        [SerializeField] StartupWarmup warmup;

        [Tooltip("Human input per baby (same order as the babies).")]
        [SerializeField] BabyInputHandler[] humanInputs = new BabyInputHandler[0];

        [Tooltip("CPU brain per baby (same order as the babies).")]
        [SerializeField] BabyBrain[] brains = new BabyBrain[0];

        [SerializeField] TitleMenu title = new TitleMenu();
        [SerializeField] DifficultyMenu difficultyMenu = new DifficultyMenu();
        [SerializeField] InfoMenu howToPlay = new InfoMenu();
        [SerializeField] OptionsMenu options = new OptionsMenu();
        [SerializeField] PauseMenu pause = new PauseMenu();
        [SerializeField] ResultsMenu results = new ResultsMenu();

        [Tooltip("Results appear this long after the winning round ends (real-time s), to enjoy the moment.")]
        [SerializeField] float resultsDelay = 2.4f;

        [Tooltip("Skill of the two CPUs playing behind the title screen.")]
        [SerializeField] CpuDifficulty demoDifficulty = CpuDifficulty.Normal;

        Screen screen;
        Screen optionsReturn;
        bool soloGame;
        CpuDifficulty difficulty = CpuDifficulty.Normal;
        float resultsTimer = -1f;
        BabyController matchWinner;
        MatchStats matchStats;
        string rewardSummary;
        readonly HashSet<Throwable> trackedItems = new HashSet<Throwable>();

        public bool IsInMatch => screen == Screen.Match;

        /// <summary>False while the loading screen is up.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(RoundManager round, AudioDirector audio, TimeController time, HUDController hudController,
            BabyInputHandler[] inputs, BabyBrain[] cpuBrains, TitleMenu titleMenu, DifficultyMenu difficultySelect,
            InfoMenu howTo, OptionsMenu optionsMenu, PauseMenu pauseMenu, ResultsMenu resultsMenu, StartupWarmup startupWarmup = null)
        {
            warmup = startupWarmup;
            roundManager = round;
            audioDirector = audio;
            timeController = time;
            hud = hudController;
            humanInputs = inputs;
            brains = cpuBrains;
            title = titleMenu;
            difficultyMenu = difficultySelect;
            howToPlay = howTo;
            options = optionsMenu;
            pause = pauseMenu;
            results = resultsMenu;
        }

        void Awake()
        {
            title.onePlayer.onClick.AddListener(() => Show(Screen.Difficulty));
            title.twoPlayers.onClick.AddListener(() => StartMatch(false, difficulty));
            title.howToPlay.onClick.AddListener(() => Show(Screen.HowToPlay));
            title.options.onClick.AddListener(() => OpenOptions(Screen.Title));
            title.quit.onClick.AddListener(Quit);

            difficultyMenu.easy.onClick.AddListener(() => StartMatch(true, CpuDifficulty.Easy));
            difficultyMenu.normal.onClick.AddListener(() => StartMatch(true, CpuDifficulty.Normal));
            difficultyMenu.hard.onClick.AddListener(() => StartMatch(true, CpuDifficulty.Hard));
            difficultyMenu.back.onClick.AddListener(() => Show(Screen.Title));

            howToPlay.back.onClick.AddListener(() => Show(Screen.Title));

            options.music.onValueChanged.AddListener(value => GameSettings.MusicVolume = value);
            options.effects.onValueChanged.AddListener(value =>
            {
                GameSettings.SfxVolume = value;
                if (audioDirector != null)
                    audioDirector.Play(SfxId.Pickup);
            });
            options.fullscreen.onValueChanged.AddListener(value => GameSettings.Fullscreen = value);
            options.back.onClick.AddListener(CloseOptions);

            pause.resume.onClick.AddListener(Resume);
            pause.restart.onClick.AddListener(() => StartMatch(soloGame, difficulty));
            pause.options.onClick.AddListener(() => OpenOptions(Screen.Paused));
            pause.mainMenu.onClick.AddListener(ShowTitle);

            results.rematch.onClick.AddListener(() => StartMatch(soloGame, difficulty));
            results.mainMenu.onClick.AddListener(ShowTitle);
        }

        void OnEnable()
        {
            if (roundManager != null)
                roundManager.MatchEnded += HandleMatchEnded;
        }

        void OnDisable()
        {
            if (roundManager != null)
                roundManager.MatchEnded -= HandleMatchEnded;
            StopTrackingProgress();
        }

        void StopTrackingProgress()
        {
            if (roundManager != null)
            {
                foreach (BabyController baby in roundManager.Babies)
                {
                    baby.ThrewItem -= RecordThrow;
                    baby.Shoved -= RecordShove;
                    baby.StartedCrying -= RecordCrying;
                }
            }
            foreach (Throwable item in trackedItems)
                if (item != null)
                    item.HitBaby -= RecordHit;
            trackedItems.Clear();
            matchStats = null;
        }

        bool IsLocalHuman(BabyController baby) => baby != null &&
            baby.PlayerIndex >= 0 && baby.PlayerIndex < humanInputs.Length &&
            humanInputs[baby.PlayerIndex] != null && humanInputs[baby.PlayerIndex].enabled;

        void RecordThrow(BabyController baby, Throwable item)
        {
            if (matchStats == null)
                return;
            if (trackedItems.Add(item))
                item.HitBaby += RecordHit;
            if (IsLocalHuman(baby))
                matchStats.throws++;
        }

        void RecordHit(Throwable item, BabyController victim, bool direct)
        {
            if (matchStats == null || !IsLocalHuman(item.Thrower))
                return;
            matchStats.hits++;
            if (item is Diaper)
                matchStats.diaperHits++;
            else
                matchStats.toyHits++;
            if (victim.IsCrying)
                matchStats.knockouts++;
        }

        void RecordShove(BabyController baby, BabyController victim)
        {
            if (matchStats != null && IsLocalHuman(baby))
            {
                matchStats.shoves++;
                if (victim.IsCrying)
                    matchStats.knockouts++;
            }
        }

        void RecordCrying(BabyController baby)
        {
            if (matchStats != null && IsLocalHuman(baby))
                matchStats.cried = true;
        }

        IEnumerator Start()
        {
            // Hide every menu until the loading screen is done.
            Show(Screen.Match);
            if (warmup != null)
                yield return warmup.Run();
            IsReady = true;
            ShowTitle();
        }

        void OnApplicationFocus(bool focus)
        {
            // The player freezes while in the background; come back to the pause menu, not mid-fight.
            // Not in the editor, where clicking the Inspector to tune balance would pause the game.
            if (!focus && IsReady && screen == Screen.Match && !Application.isEditor && !Application.runInBackground)
                Pause();
        }

        void Update()
        {
            if (!IsReady)
                return;
            if ((screen == Screen.Match || screen == Screen.Paused) && PausePressed())
            {
                if (screen == Screen.Match)
                    Pause();
                else
                    Resume();
            }
            else if (CancelPressed())
            {
                if (screen == Screen.Difficulty || screen == Screen.HowToPlay)
                    Show(Screen.Title);
                else if (screen == Screen.Options)
                    CloseOptions();
            }

            if (resultsTimer >= 0f)
            {
                resultsTimer -= Time.unscaledDeltaTime;
                if (resultsTimer < 0f)
                    ShowResults();
            }

            // Keep a button selected so keyboards and gamepads can always navigate (e.g. after a mouse click elsewhere).
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && FirstSelectable(screen) != null)
                events.SetSelectedGameObject(FirstSelectable(screen).gameObject);
        }

        public void ShowTitle()
        {
            StopTrackingProgress();
            if (title.challenges == null)
                CreateChallengePanel();
            title.challenges.text = PlayerProgress.ChallengeSummary();
            resultsTimer = -1f;
            timeController.SetPaused(false);
            timeController.ClearEffects();
            SetControllers(false, false, demoDifficulty);
            roundManager.StartMatch(true);
            hud.SetVisible(false);
            audioDirector.PlayMusic(MusicId.Title);
            audioDirector.DuckMusic(false);
            Show(Screen.Title);
        }

        void CreateChallengePanel()
        {
            var panel = new GameObject("Challenges", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(title.root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.4f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-30f, 0f);
            rect.sizeDelta = new Vector2(430f, 420f);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.08f, 0.2f, 0.92f);
            var label = new GameObject("Progress", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            var textRect = (RectTransform)label.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 20f);
            textRect.offsetMax = new Vector2(-20f, -20f);
            title.challenges = label.GetComponent<UnityEngine.UI.Text>();
            title.challenges.font = title.onePlayer.GetComponentInChildren<UnityEngine.UI.Text>().font;
            title.challenges.fontSize = 28;
            title.challenges.color = Color.white;
            title.challenges.alignment = TextAnchor.MiddleLeft;
            title.challenges.raycastTarget = false;
        }

        public void StartMatch(bool solo, CpuDifficulty cpuDifficulty)
        {
            StopTrackingProgress();
            rewardSummary = "";
            soloGame = solo;
            difficulty = cpuDifficulty;
            resultsTimer = -1f;
            timeController.SetPaused(false);
            timeController.ClearEffects();
            SetControllers(true, !solo, cpuDifficulty);
            matchStats = new MatchStats { mode = GameMode.Clash.ToString() };
            foreach (BabyController baby in roundManager.Babies)
            {
                baby.ThrewItem += RecordThrow;
                baby.Shoved += RecordShove;
                baby.StartedCrying += RecordCrying;
            }
            roundManager.StartMatch(false);
            hud.SetVisible(true);
            hud.ShowHints();
            audioDirector.PlayMusic(MusicId.Match);
            audioDirector.DuckMusic(false);
            Show(Screen.Match);
        }

        void Pause()
        {
            timeController.SetPaused(true);
            audioDirector.DuckMusic(true);
            Show(Screen.Paused);
        }

        void Resume()
        {
            timeController.SetPaused(false);
            audioDirector.DuckMusic(false);
            Show(Screen.Match);
        }

        void OpenOptions(Screen returnTo)
        {
            optionsReturn = returnTo;
            options.music.SetValueWithoutNotify(GameSettings.MusicVolume);
            options.effects.SetValueWithoutNotify(GameSettings.SfxVolume);
            options.fullscreen.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            Show(Screen.Options);
        }

        void CloseOptions()
        {
            GameSettings.Save();
            Show(optionsReturn);
        }

        void HandleMatchEnded(BabyController winner)
        {
            if (screen != Screen.Match)
                return;
            matchWinner = winner;
            resultsTimer = resultsDelay;
        }

        void ShowResults()
        {
            if (matchStats != null)
            {
                if (Array.Exists(humanInputs, input => input != null && input.enabled))
                {
                    matchStats.won = IsLocalHuman(matchWinner);
                    rewardSummary = PlayerProgress.CompleteMatch(matchStats);
                }
                StopTrackingProgress();
            }
            results.detail.fontSize = 30;
            results.detail.resizeTextForBestFit = true;
            results.detail.resizeTextMinSize = 18;
            results.detail.resizeTextMaxSize = 30;
            var panel = (RectTransform)results.detail.transform.parent;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1100f, 740f);
            results.detail.rectTransform.sizeDelta = new Vector2(1000f, 240f);
            results.detail.rectTransform.anchoredPosition = new Vector2(0f, -260f);
            BabyController winner = matchWinner;
            results.title.text = winner != null ? $"{winner.DisplayName} WINS!" : "IT'S A TIE!";
            results.title.color = winner != null ? winner.PlayerColor : Color.white;

            string score = roundManager.Babies.Count >= 2
                ? $"{roundManager.GetWins(roundManager.Babies[0])}  -  {roundManager.GetWins(roundManager.Babies[1])}"
                : string.Empty;
            string flavour;
            if (!soloGame || winner == null)
                flavour = "What a tantrum!";
            else if (winner == roundManager.Babies[0])
                flavour = "You win! Nap time for the CPU.";
            else
                flavour = "The CPU baby wins... rematch?";
            results.detail.text = $"{score}\n{flavour}\n{rewardSummary}";

            audioDirector.Play(SfxId.Cheer);
            audioDirector.DuckMusic(true);
            hud.HideBanner();
            Show(Screen.Results);
        }

        void SetControllers(bool humanOne, bool humanTwo, CpuDifficulty cpuDifficulty)
        {
            for (int i = 0; i < brains.Length; i++)
            {
                bool human = i == 0 ? humanOne : i == 1 && humanTwo;
                if (i < humanInputs.Length && humanInputs[i] != null)
                {
                    humanInputs[i].enabled = human;
                    // A lone player can use either half of the keyboard.
                    humanInputs[i].SetSoloKeyboard(i == 0 && humanOne && !humanTwo);
                }
                if (brains[i] != null)
                {
                    brains[i].Difficulty = cpuDifficulty;
                    brains[i].enabled = !human;
                }
            }
        }

        void Show(Screen next)
        {
            screen = next;
            title.root.SetActive(next == Screen.Title);
            difficultyMenu.root.SetActive(next == Screen.Difficulty);
            howToPlay.root.SetActive(next == Screen.HowToPlay);
            options.root.SetActive(next == Screen.Options);
            pause.root.SetActive(next == Screen.Paused);
            results.root.SetActive(next == Screen.Results);

            EventSystem events = EventSystem.current;
            if (events != null)
            {
                Selectable first = FirstSelectable(next);
                events.SetSelectedGameObject(first != null ? first.gameObject : null);
            }
        }

        Selectable FirstSelectable(Screen value)
        {
            switch (value)
            {
                case Screen.Title: return title.onePlayer;
                case Screen.Difficulty: return difficultyMenu.normal;
                case Screen.HowToPlay: return howToPlay.back;
                case Screen.Options: return options.music;
                case Screen.Paused: return pause.resume;
                case Screen.Results: return results.rematch;
                default: return null;
            }
        }

        static bool PausePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
                return true;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.startButton.wasPressedThisFrame)
                    return true;
            }
            return false;
        }

        static bool CancelPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.buttonEast.wasPressedThisFrame)
                    return true;
            }
            return false;
        }

        static void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
