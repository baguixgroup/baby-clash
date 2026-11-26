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
        [NonSerialized] public UnityEngine.UI.Text challenges; // built at runtime
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
            Party,
            Story,
            Wardrobe,
            Challenges,
            Online,
            JoinCode,
            Room,
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
        MatchSetup setup = MatchSetup.Duel(true, CpuDifficulty.Normal);
        MenuKit kit;
        GameMenus menus;
        RoomDresser rooms;
        PowerUpSpawner powerUps;
        BottleZone bottle;
        SurvivalDirector survival;
        TouchControls touch;
        ArenaCamera arenaCamera;
        NetGame net;
        OnlineMenus online;
        BabyLook[] onlineLooks;
        bool guestMatch;
        bool wasRunningInBackground;
        readonly Image[] resultStars = new Image[3];
        int roundsLost;
        float matchSeconds;
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
            title.twoPlayers.onClick.AddListener(() => StartMatch(false, CpuDifficulty.Normal));
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
            pause.restart.onClick.AddListener(() =>
            {
                if (NetRole.IsOnline)
                    net.HostStartMatch();
                else
                    StartMatch(setup);
            });
            pause.options.onClick.AddListener(() => OpenOptions(Screen.Paused));
            pause.mainMenu.onClick.AddListener(() =>
            {
                if (NetRole.IsOnline)
                    LeaveOnlineMatch();
                else
                    ShowTitle();
            });

            results.rematch.onClick.AddListener(Rematch);
            results.mainMenu.onClick.AddListener(() =>
            {
                if (NetRole.IsOnline)
                {
                    LeaveOnlineMatch();
                    return;
                }
                bool story = setup.Level != null;
                ShowTitle();
                if (story)
                    OpenStory();
            });
        }

        void OnEnable()
        {
            if (roundManager == null)
                return;
            roundManager.MatchEnded += HandleMatchEnded;
            roundManager.PhaseChanged += HandlePhase;
            roundManager.BabyCried += RecordKnockout;
        }

        void OnDisable()
        {
            if (roundManager != null)
            {
                roundManager.MatchEnded -= HandleMatchEnded;
                roundManager.PhaseChanged -= HandlePhase;
                roundManager.BabyCried -= RecordKnockout;
            }
            StopTrackingProgress();
        }

        void StopTrackingProgress()
        {
            if (roundManager != null)
            {
                foreach (BabyController baby in roundManager.AllBabies)
                {
                    baby.ThrewItem -= RecordThrow;
                    baby.Shoved -= RecordShove;
                    baby.StartedCrying -= RecordCrying;
                    baby.Giggled -= RecordGiggle;
                    baby.Dodged -= RecordDodge;
                }
            }
            foreach (Throwable item in trackedItems)
                if (item != null)
                    item.HitBaby -= RecordHit;
            trackedItems.Clear();
            matchStats = null;
        }

        bool IsLocalHuman(BabyController baby) => baby != null && !setup.Endless && setup.IsHuman(baby.PlayerIndex);

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
        }

        void RecordKnockout(BabyController victim)
        {
            if (matchStats != null && victim.LastAttacker != victim && IsLocalHuman(victim.LastAttacker))
                matchStats.knockouts++;
        }

        void RecordGiggle(BabyController baby, BabyController joker)
        {
            if (matchStats != null && IsLocalHuman(joker))
                matchStats.giggles++;
        }

        void RecordDodge(BabyController baby)
        {
            if (matchStats != null && IsLocalHuman(baby))
                matchStats.dodges++;
        }

        void RecordPowerUp(BabyController baby, PowerUpKind kind)
        {
            if (matchStats != null && IsLocalHuman(baby))
                matchStats.powerUps++;
        }

        void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.RoundOver && screen == Screen.Match && roundManager.WinningSide != roundManager.SideOf(roundManager.AllBabies[0]))
                roundsLost++;
        }

        void RecordShove(BabyController baby, BabyController victim)
        {
            if (matchStats != null && IsLocalHuman(baby))
            {
                matchStats.shoves++;
            }
        }

        void RecordCrying(BabyController baby)
        {
            if (matchStats != null && IsLocalHuman(baby))
                matchStats.cried = true;
        }

        IEnumerator Start()
        {
            BuildGame();
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
            if (!focus && IsReady && screen == Screen.Match && !Application.isEditor && !Application.runInBackground && !NetRole.IsOnline)
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
                if (screen == Screen.Wardrobe || screen == Screen.Party || screen == Screen.Story || screen == Screen.Challenges
                    || (screen == Screen.Online && !NetSession.Instance.IsBusy))
                    BackToTitle();
                else if (screen == Screen.JoinCode && !NetSession.Instance.IsBusy)
                    Show(Screen.Online);
                else if (screen == Screen.Difficulty || screen == Screen.HowToPlay)
                    Show(Screen.Title);
                else if (screen == Screen.Options)
                    CloseOptions();
            }

            if (screen == Screen.Story)
                menus.UpdateStoryDetail();
            online.UpdateMatch(screen == Screen.Match);
            if (screen == Screen.Match && roundManager.Phase == RoundPhase.Playing)
            {
                matchSeconds += Time.deltaTime;
                if (matchStats != null && BottleZone.Active != null && IsLocalHuman(BottleZone.Active.Holder))
                    matchStats.bottleSeconds += Time.deltaTime;
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
            guestMatch = false;
            onlineLooks = null;
            StopTrackingProgress();
            if (title.challenges == null)
                CreateChallengePanel();
            title.challenges.text = PlayerProgress.ChallengeSummary();
            resultsTimer = -1f;
            timeController.SetPaused(false);
            timeController.ClearEffects();
            EndCloseUp();
            MatchSetup demo = MatchSetup.Demo(demoDifficulty);
            ApplySetup(demo);
            roundManager.StartMatch(demo);
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
            rect.anchoredPosition = new Vector2(-20f, 0f);
            rect.sizeDelta = new Vector2(400f, 360f);
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
            title.challenges.fontSize = 25;
            title.challenges.color = Color.white;
            title.challenges.alignment = TextAnchor.MiddleLeft;
            title.challenges.raycastTarget = false;
        }

        public void StartMatch(bool solo, CpuDifficulty cpuDifficulty)
        {
            difficulty = cpuDifficulty;
            StartMatch(MatchSetup.Duel(solo, cpuDifficulty));
        }

        public void StartMatch(MatchSetup matchSetup)
        {
            StopTrackingProgress();
            setup = matchSetup;
            rewardSummary = "";
            soloGame = setup.Count(true) == 1;
            resultsTimer = -1f;
            roundsLost = 0;
            matchSeconds = 0f;
            timeController.SetPaused(false);
            timeController.ClearEffects();
            EndCloseUp();
            ApplySetup(setup);
            matchStats = new MatchStats { mode = setup.Mode.ToString(), tinyTots = setup.IsToddler };
            foreach (BabyController baby in roundManager.AllBabies)
            {
                baby.ThrewItem += RecordThrow;
                baby.Shoved += RecordShove;
                baby.StartedCrying += RecordCrying;
                baby.Giggled += RecordGiggle;
                baby.Dodged += RecordDodge;
            }
            roundManager.StartMatch(setup);
            hud.SetVisible(true);
            // The control hints describe local players' keys; online each screen has a single player.
            if (!setup.IsToddler && !NetRole.IsOnline)
                hud.ShowHints();
            audioDirector.PlayMusic(MusicId.Match);
            audioDirector.DuckMusic(false);
            Show(Screen.Match);
        }

        void Pause()
        {
            // Online matches keep going for everyone else: the menu just floats over the action.
            if (!NetRole.IsOnline)
            {
                timeController.SetPaused(true);
                audioDirector.DuckMusic(true);
            }
            pause.restart.gameObject.SetActive(!NetRole.IsGuest);
            MenuKit.SetText(pause.mainMenu, NetRole.IsHost ? "BACK TO ROOM" : NetRole.IsGuest ? "LEAVE ROOM" : "MAIN MENU");
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

        /// <summary>Everything beyond the basic scene, built once at load: players 3 and 4, rooms, modes, menus, touch.</summary>
        void BuildGame()
        {
            kit = new MenuKit(title.onePlayer, audioDirector);
            arenaCamera = FindAnyObjectByType<ArenaCamera>();
            ArenaBounds arena = FindAnyObjectByType<ArenaBounds>();
            ImpactFeedback feedback = FindAnyObjectByType<ImpactFeedback>();
            // Feedback re-subscribes to the bigger cast when it comes back on.
            if (feedback != null)
                feedback.enabled = false;
            BabyController[] cast = BabyRoster.Expand(roundManager, arena, arenaCamera);
            if (feedback != null)
                feedback.enabled = true;
            humanInputs = Array.ConvertAll(cast, baby => baby.GetComponent<BabyInputHandler>());
            brains = Array.ConvertAll(cast, baby => baby.GetComponent<BabyBrain>());

            rooms = new GameObject("RoomDresser").AddComponent<RoomDresser>();
            rooms.Configure(roundManager);
            powerUps = gameObject.AddComponent<PowerUpSpawner>();
            powerUps.Configure(roundManager, arena, audioDirector);
            powerUps.Used += RecordPowerUp;
            bottle = BottleZone.Create(roundManager);
            survival = gameObject.AddComponent<SurvivalDirector>();
            survival.Configure(roundManager);
            hud.Expand(cast, survival);
            touch = TouchControls.Create(kit);

            net = NetGame.Create(this, roundManager, powerUps, bottle, survival, audioDirector);
            online = new OnlineMenus(kit, title.root.transform.parent, net, NetSession.Instance, BackToTitle,
                () => Show(Screen.JoinCode), () => OpenOnline(null));
            NetSession.Instance.Started += mode =>
            {
                wasRunningInBackground = Application.runInBackground;
                // The host keeps the match going even when its window isn't focused.
                Application.runInBackground = true;
                online.RefreshRoom();
                Show(Screen.Room);
            };
            NetSmokeTest.CreateIfRequested(this, net, roundManager);

            menus = new GameMenus(kit, title.root.transform.parent, StartMatch, BackToTitle);
            menus.WardrobeChanged += ShowWardrobeBaby;
            BuildTitleButtons();
            for (int i = 0; i < resultStars.Length; i++)
                resultStars[i] = kit.Star(results.title.transform.parent, new Vector2(-110f + i * 110f, 300f), 100f);
        }

        void BuildTitleButtons()
        {
            Transform column = title.onePlayer.transform.parent;
            PlaceTitleButton(title.onePlayer, -1, 0);
            PlaceTitleButton(title.twoPlayers, -1, 1);
            PlaceTitleButton(title.howToPlay, 1, 2);
            PlaceTitleButton(title.options, 1, 3);
            PlaceTitleButton(title.quit, 1, 4);
            PlaceTitleButton(kit.Button("Story", column, "STORY TOUR", MenuKit.Yellow, Vector2.zero, new Vector2(440f, 90f), 38, OpenStory), -1, 2);
            PlaceTitleButton(kit.Button("Party", column, "PARTY", MenuKit.Mint, Vector2.zero, new Vector2(440f, 90f), 38, () => Show(Screen.Party)), -1, 3);
            PlaceTitleButton(kit.Button("TinyTots", column, "TINY TOTS", MenuKit.Lilac, Vector2.zero, new Vector2(440f, 90f), 38,
                () => StartMatch(MatchSetup.Tots(Gamepad.all.Count > 0 ? 2 : 1))), -1, 4);
            PlaceTitleButton(kit.Button("Wardrobe", column, "WARDROBE", MenuKit.Pink, Vector2.zero, new Vector2(440f, 90f), 38, OpenWardrobe), 1, 0);
            PlaceTitleButton(kit.Button("ChallengeList", column, "CHALLENGES", MenuKit.Blue, Vector2.zero, new Vector2(440f, 90f), 38, OpenChallenges), 1, 1);
            Button onlineButton = kit.Button("Online", column, "PLAY ONLINE", MenuKit.Pink, Vector2.zero, new Vector2(904f, 90f), 40, () => OpenOnline(null));
            PlaceTitleButton(onlineButton, 0, 5);
        }

        public void OpenOnline(string message)
        {
            online.OpenOnline(message);
            Show(Screen.Online);
        }

        // ------------------------------------------------------------ online (see NetGame)

        /// <summary>Starts an online match on this screen: the real one on the host, a mirror of it on guests.</summary>
        public void StartOnlineMatch(MatchSetup matchSetup, BabyLook[] looks)
        {
            onlineLooks = looks;
            guestMatch = NetRole.IsGuest;
            StartMatch(matchSetup);
        }

        /// <summary>Host: a guest left mid-match; a CPU takes over their baby.</summary>
        public void OnlinePlayerLeft(int slot)
        {
            if (slot < 0 || slot >= brains.Length || screen == Screen.Title)
                return;
            if (setup != null && slot < setup.Slots.Length)
                setup.Slots[slot] = SlotDriver.CpuNormal;
            humanInputs[slot].enabled = false;
            brains[slot].Difficulty = CpuDifficulty.Normal;
            brains[slot].Gentle = setup != null && setup.IsToddler;
            brains[slot].enabled = roundManager.AllBabies[slot].gameObject.activeSelf;
        }

        /// <summary>Back to the room screen (host chose it, or a guest was told by the host).</summary>
        public void OnlineBackToRoom()
        {
            ShowTitle();
            online.RefreshRoom();
            Show(Screen.Room);
        }

        /// <summary>The online game ended (left, kicked, connection lost).</summary>
        public void OnlineEnded(string reason)
        {
            Application.runInBackground = wasRunningInBackground;
            ShowTitle();
            if (!string.IsNullOrEmpty(reason))
                OpenOnline(reason);
        }

        void LeaveOnlineMatch()
        {
            if (NetRole.IsHost)
            {
                net.HostBackToRoom();
                OnlineBackToRoom();
            }
            else
            {
                net.Leave();
            }
        }

        static void PlaceTitleButton(Button button, int side, int row)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(side * 232f, -row * 102f - 60f);
        }

        void OpenStory()
        {
            menus.RefreshStory();
            Show(Screen.Story);
        }

        void OpenChallenges()
        {
            menus.RefreshChallenges();
            Show(Screen.Challenges);
        }

        public void OpenWardrobe()
        {
            // Everyone stands still in the nursery; the chosen baby poses for the camera.
            var pose = new MatchSetup { Endless = true, Slots = new[] { SlotDriver.CpuEasy, SlotDriver.CpuEasy, SlotDriver.CpuEasy, SlotDriver.CpuEasy } };
            ApplySetup(pose);
            roundManager.StartMatch(pose);
            roundManager.StopMatch();
            hud.SetVisible(false);
            menus.RefreshWardrobe();
            ShowWardrobeBaby();
            Show(Screen.Wardrobe);
        }

        void ShowWardrobeBaby()
        {
            for (int i = 0; i < roundManager.AllBabies.Count; i++)
            {
                BabyController baby = roundManager.AllBabies[i];
                OutfitRig.On(baby).Wear(PlayerProgress.Profile.LookFor(i));
                bool chosen = i == menus.WardrobeSlot;
                Vector3 place = chosen ? new Vector3(-5.2f, 0f, -0.2f) : new Vector3(-1f + i * 2.4f, 0f, 3.9f);
                baby.Movement.Teleport(place, Quaternion.LookRotation(Vector3.back));
                if (chosen && arenaCamera != null)
                    arenaCamera.CloseUp = baby.transform;
            }
        }

        void EndCloseUp()
        {
            if (arenaCamera != null)
                arenaCamera.CloseUp = null;
        }

        void BackToTitle()
        {
            if (screen == Screen.Wardrobe)
                ShowTitle();
            else
                Show(Screen.Title);
        }

        void Rematch()
        {
            if (NetRole.IsOnline)
            {
                if (NetRole.IsHost)
                    net.HostStartMatch();
                return;
            }
            LevelDef level = setup.Level;
            int next = level != null ? LevelCatalog.IndexOf(level.Id) + 1 : 0;
            if (level != null && levelCleared && next < LevelCatalog.All.Count)
                StartMatch(MatchSetup.ForLevel(LevelCatalog.All[next]));
            else
                StartMatch(setup);
        }

        bool levelCleared;

        /// <summary>Room, controllers, outfits and mode objects for a match.</summary>
        void ApplySetup(MatchSetup next)
        {
            // Online guests only mirror the host's match: no physics, rules or CPUs of their own.
            bool mirror = guestMatch && NetRole.IsGuest;
            roundManager.SetMirror(mirror);
            foreach (BabyController baby in roundManager.AllBabies)
                baby.SetPuppet(mirror);
            foreach (Diaper diaper in roundManager.Diapers)
                diaper.SetPuppet(mirror);
            if (roundManager.Toys != null)
            {
                foreach (Toy toy in roundManager.Toys.Pool)
                    toy.SetPuppet(mirror);
            }

            rooms.Show(next.Room);
            bottle.gameObject.SetActive(next.Mode == GameMode.KingOfTheBottle);
            int localSlot = -1;
            for (int i = 0; i < next.Slots.Length && localSlot < 0; i++)
            {
                if (next.IsHuman(i))
                    localSlot = i;
            }
            touch.Playing = !next.Endless && localSlot >= 0;
            touch.Slot = Mathf.Max(0, localSlot);
            touch.Simple = next.IsToddler;
            hud.SetRules(next);

            int humans = next.Count(true), pads = Gamepad.all.Count, human = 0;
            for (int i = 0; i < brains.Length; i++)
            {
                bool isHuman = next.IsHuman(i);
                BabyController baby = roundManager.AllBabies[i];
                BabyLook look = PlayerProgress.Profile.LookFor(i);
                // Rivals dress up too, differently every time. Online, the host decides everyone's outfit.
                if (onlineLooks != null && i < onlineLooks.Length && onlineLooks[i] != null)
                    OutfitRig.On(baby).Wear(isHuman ? PlayerProgress.Profile.LookFor(0) : onlineLooks[i]);
                else
                    OutfitRig.On(baby).Wear(isHuman || !next.IsPlaying(i) || next.Endless ? look : RandomLook());
                if (isHuman)
                {
                    string scheme = human == 0 ? BabyInputHandler.KeyboardLeftScheme : human == 1 ? BabyInputHandler.KeyboardRightScheme : null;
                    // Two humans: a pad each. More: the ones without a keyboard half get pads first.
                    int pad = humans <= 2 ? human : human >= 2 ? human - 2 : human + humans - 2;
                    humanInputs[i].AssignDevices(scheme, pad < pads ? pad : -1);
                    human++;
                }
                humanInputs[i].enabled = isHuman;
                // A lone player can use either half of the keyboard.
                humanInputs[i].SetSoloKeyboard(isHuman && humans == 1);
                brains[i].Difficulty = next.Level != null && next.BossSlot == i ? CpuDifficulty.Hard : MatchSetup.DifficultyOf(next.Slots[i]);
                brains[i].Gentle = next.IsToddler;
                brains[i].enabled = !mirror && next.IsCpu(i);
                if (!isHuman)
                    baby.SpeedScale = 1f;

                if (!baby.TryGetComponent(out TotsHelper helper))
                    helper = baby.gameObject.AddComponent<TotsHelper>();
                // The host also helps online toddlers pick things up (their screens only mirror the match).
                helper.enabled = next.IsToddler && (isHuman || (!mirror && next.IsOnline(i)));
                helper.AnyKey = isHuman && humans == 1;
            }
        }

        internal static BabyLook RandomLook()
        {
            var look = new BabyLook();
            foreach (CosmeticSlot slot in new[] { CosmeticSlot.Hat, CosmeticSlot.Face })
            {
                var choices = new List<CosmeticItem>(CosmeticCatalog.InSlot(slot));
                look.Set(slot, choices[UnityEngine.Random.Range(0, choices.Count)].Id);
            }
            return look;
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
                if (setup.Count(true) > 0)
                {
                    matchStats.won = HumanWon();
                    if (setup.Mode == GameMode.Survival)
                    {
                        matchStats.survivalWave = survival.Wave;
                        PlayerProfile profile = PlayerProgress.Profile;
                        profile.bestSurvivalWave = Mathf.Max(profile.bestSurvivalWave, survival.Wave);
                        profile.bestSurvivalScore = Mathf.Max(profile.bestSurvivalScore, survival.Knockouts);
                    }
                    RecordLevel();
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
            if (NetRole.IsOnline)
            {
                results.rematch.interactable = NetRole.IsHost;
                MenuKit.SetText(results.mainMenu, NetRole.IsHost ? "BACK TO ROOM" : "LEAVE ROOM");
            }
            else
            {
                results.rematch.interactable = true;
            }
            results.title.text = ResultTitle(winner);
            results.title.color = setup.IsToddler || setup.Mode == GameMode.Survival ? MenuKit.Pink : winner != null ? winner.PlayerColor : MenuKit.Ink;
            ShowResultStars();
            bool nextLevel = setup.Level != null && levelCleared && LevelCatalog.IndexOf(setup.Level.Id) + 1 < LevelCatalog.All.Count;
            MenuKit.SetText(results.rematch, NetRole.IsGuest ? "HOST PICKS NEXT" : nextLevel ? "NEXT LEVEL" : setup.Level != null && !levelCleared ? "TRY AGAIN" : "REMATCH");
            if (!NetRole.IsOnline)
                MenuKit.SetText(results.mainMenu, setup.Level != null ? "LEVELS" : "MAIN MENU");

            string score = ScoreLine();
            string flavour;
            if (setup.IsToddler)
                flavour = "Everyone had fun!";
            else if (setup.Level != null)
                flavour = GoalLines();
            else if (setup.Mode == GameMode.Survival)
                flavour = $"Best: wave {PlayerProgress.Profile.bestSurvivalWave}";
            else if (NetRole.IsOnline && winner != null)
                flavour = HumanWon() ? "You win! What a champ!" : "So close! Rematch?";
            else if (!soloGame || winner == null)
                flavour = "What a tantrum!";
            else if (HumanWon())
                flavour = "You win! Nap time for the CPU.";
            else
                flavour = "The CPU baby wins... rematch?";
            results.detail.text = $"{score}\n{flavour}\n{rewardSummary}";

            audioDirector.Play(SfxId.Cheer);
            audioDirector.DuckMusic(true);
            hud.HideBanner();
            Show(Screen.Results);
        }

        bool HumanWon()
        {
            if (setup.Mode == GameMode.Survival)
                return setup.Level != null && survival.Wave >= setup.Level.WaveGoal;
            foreach (BabyController baby in roundManager.Babies)
            {
                if (IsLocalHuman(baby) && roundManager.SideOf(baby) == roundManager.MatchWinningSide)
                    return true;
            }
            return false;
        }

        LevelResult levelResult;
        int levelStars;

        void RecordLevel()
        {
            levelCleared = false;
            levelStars = 0;
            if (setup.Level == null)
                return;
            levelResult = new LevelResult
            {
                won = matchStats.won, cried = matchStats.cried, roundsLost = roundsLost, seconds = matchSeconds,
                hits = matchStats.hits, wave = survival.Wave,
            };
            levelStars = LevelCatalog.Evaluate(setup.Level, levelResult);
            levelCleared = levelStars > 0;
            matchStats.stars = levelStars;
            PlayerProgress.Profile.RecordStars(setup.Level.Id, levelStars);
        }

        void ShowResultStars()
        {
            for (int i = 0; i < resultStars.Length; i++)
            {
                resultStars[i].gameObject.SetActive(setup.Level != null || setup.IsToddler);
                MenuKit.SetStar(resultStars[i], setup.IsToddler || i < levelStars);
            }
        }

        string ResultTitle(BabyController winner)
        {
            if (setup.IsToddler)
                return "HOORAY!";
            if (setup.Mode == GameMode.Survival)
                return $"WAVE {survival.Wave}!";
            if (winner == null)
                return "IT'S A TIE!";
            string names = "";
            int count = 0;
            foreach (BabyController baby in roundManager.Babies)
            {
                if (roundManager.SideOf(baby) != roundManager.MatchWinningSide)
                    continue;
                names += (count++ > 0 ? " & " : "") + baby.DisplayName;
            }
            return count > 1 ? $"{names} WIN!" : $"{winner.DisplayName} WINS!";
        }

        string ScoreLine()
        {
            if (setup.Mode == GameMode.Survival)
                return $"{survival.Knockouts} babies sent crying";
            string line = "";
            if (setup.IsToddler)
                return line;
            foreach (BabyController baby in roundManager.Babies)
            {
                int value = roundManager.UsesScore ? Mathf.FloorToInt(roundManager.GetScore(baby)) : roundManager.GetWins(baby);
                line += (line.Length > 0 ? "  -  " : "") + value;
            }
            return line;
        }

        string GoalLines()
        {
            LevelDef level = setup.Level;
            string text = "";
            foreach (StarGoal goal in new[] { StarGoal.Win, level.Second, level.Third })
                text += (text.Length > 0 ? "     " : "") + (LevelCatalog.Meets(level, goal, levelResult) ? "[x] " : "[  ] ") + LevelCatalog.Describe(level, goal);
            return text;
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
            if (menus != null)
            {
                menus.PartyScreen.SetActive(next == Screen.Party);
                menus.StoryScreen.SetActive(next == Screen.Story);
                menus.WardrobeScreen.SetActive(next == Screen.Wardrobe);
                menus.ChallengesScreen.SetActive(next == Screen.Challenges);
            }
            if (online != null)
            {
                online.OnlineScreen.SetActive(next == Screen.Online);
                online.CodeScreen.SetActive(next == Screen.JoinCode);
                online.RoomScreen.SetActive(next == Screen.Room);
                online.SetTyping(next == Screen.JoinCode);
            }

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
                case Screen.Party: return menus.PartyFirst;
                case Screen.Story: return menus.StoryFirst;
                case Screen.Wardrobe: return menus.WardrobeFirst;
                case Screen.Challenges: return menus.ChallengesFirst;
                case Screen.Online: return online.OnlineFirst;
                case Screen.JoinCode: return online.CodeFirst;
                case Screen.Room: return online.RoomFirst;
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
