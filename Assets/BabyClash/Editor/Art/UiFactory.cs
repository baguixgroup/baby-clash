using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Builds the HUD and every menu screen with uGUI, styled with generated rounded sprites.</summary>
    public sealed class UiFactory
    {
        static readonly Color Ink = new Color(0.28f, 0.18f, 0.34f);
        static readonly Color PanelColor = new Color(1f, 0.97f, 0.93f, 0.96f);
        static readonly Color Pink = new Color(1f, 0.55f, 0.66f);
        static readonly Color Blue = new Color(0.45f, 0.7f, 1f);
        static readonly Color Yellow = new Color(1f, 0.78f, 0.3f);
        static readonly Color Mint = new Color(0.45f, 0.85f, 0.62f);
        static readonly Color Lilac = new Color(0.7f, 0.58f, 0.98f);

        readonly Font font;
        readonly Sprite rounded;
        readonly Sprite circle;
        readonly Sprite star;
        readonly AudioDirector audio;

        public UiFactory(Font font, Sprite rounded, Sprite circle, Sprite star, AudioDirector audio)
        {
            this.font = font;
            this.rounded = rounded;
            this.circle = circle;
            this.star = star;
            this.audio = audio;
        }

        public static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ---------------------------------------------------------------- HUD

        public HUDController BuildHud(RoundManager round, Sprite[] portraits, ToyIcon[] toyIcons, Sprite diaperIcon)
        {
            Canvas canvas = Canvas("HUD", 10);
            var root = (RectTransform)canvas.transform;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var panels = new[]
            {
                PlayerCard(root, true, diaperIcon, toyIcons),
                PlayerCard(root, false, diaperIcon, toyIcons),
            };

            RectTransform timerPill = Rect("TimerPill", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(170f, 92f));
            Image pill = timerPill.gameObject.AddComponent<Image>();
            Style(pill, rounded, new Color(1f, 1f, 1f, 0.88f));
            Text timer = Label("Timer", timerPill, "75", 60, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(170f, 92f), false);

            RectTransform banner = Rect("Banner", root, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(1700f, 300f));
            Text headline = Label("Headline", banner, string.Empty, 128, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(1700f, 180f), true);
            Text subline = Label("Subline", banner, string.Empty, 46, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -85f), new Vector2(1700f, 80f), true);
            banner.gameObject.SetActive(false);

            RectTransform hints = Rect("Hints", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1500f, 70f));
            Style(hints.gameObject.AddComponent<Image>(), rounded, new Color(0.2f, 0.12f, 0.26f, 0.6f));
            Label("HintText", hints,
                "BABY 1: WASD + SPACE grab/throw + L-SHIFT dash      BABY 2: ARROWS + ENTER + R-SHIFT      PAD: stick + A + B",
                24, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1480f, 60f), false);
            var hintGroup = hints.gameObject.AddComponent<CanvasGroup>();

            var hud = canvas.gameObject.AddComponent<HUDController>();
            hud.Configure(round, group, panels, portraits, banner, headline, subline, timer, timerPill.gameObject, hintGroup);
            return hud;
        }

        BabyHudPanel PlayerCard(RectTransform root, bool left, Sprite diaperIcon, ToyIcon[] toyIcons)
        {
            Vector2 corner = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            float s = left ? 1f : -1f;
            RectTransform card = Rect(left ? "Card_Baby1" : "Card_Baby2", root, corner, corner, new Vector2(24f * s, -20f), new Vector2(600f, 160f));
            Style(card.gameObject.AddComponent<Image>(), rounded, new Color(1f, 1f, 1f, 0.86f));

            // Portrait in a coloured ring.
            RectTransform ringRect = Rect("PortraitRing", card, corner, corner, new Vector2(14f * s, -14f), new Vector2(132f, 132f));
            Image ring = ringRect.gameObject.AddComponent<Image>();
            Style(ring, circle, Color.white);
            RectTransform faceRect = Rect("Portrait", ringRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));
            Image face = faceRect.gameObject.AddComponent<Image>();
            face.preserveAspect = true;
            face.raycastTarget = false;

            TextAnchor align = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            Text nameLabel = Label("Name", card, "BABY", 40, Ink, align, new Vector2(160f * s, -12f), new Vector2(300f, 50f), false, corner);

            var pips = new Image[3];
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pipRect = Rect($"Pip{i}", card, corner, corner, new Vector2((440f + i * 40f) * s, -22f), new Vector2(32f, 32f));
                pips[i] = pipRect.gameObject.AddComponent<Image>();
                Style(pips[i], star, new Color(1f, 1f, 1f, 0.25f));
            }

            RectTransform bar = Rect("CryBar", card, corner, corner, new Vector2(160f * s, -64f), new Vector2(420f, 38f));
            Style(bar.gameObject.AddComponent<Image>(), rounded, new Color(0.28f, 0.18f, 0.34f, 0.2f));
            var fillObject = new GameObject("Fill", typeof(RectTransform));
            var fill = (RectTransform)fillObject.transform;
            fill.SetParent(bar, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            Image fillImage = fillObject.AddComponent<Image>();
            Style(fillImage, rounded, Color.white);
            Text cry = Label("CryValue", bar, "CRY 0%", 26, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(420f, 38f), true);

            RectTransform diaperRect = Rect("DiaperIcon", card, corner, corner, new Vector2(160f * s, -106f), new Vector2(56f, 46f));
            Image diaper = diaperRect.gameObject.AddComponent<Image>();
            diaper.sprite = diaperIcon;
            diaper.preserveAspect = true;
            diaper.raycastTarget = false;
            Text diaperLabel = Label("DiaperLabel", card, "DIAPER ON", 28, Ink, align, new Vector2(224f * s, -108f), new Vector2(260f, 40f), true, corner);

            RectTransform toyRect = Rect("HeldToy", card, corner, corner, new Vector2(520f * s, -96f), new Vector2(64f, 64f));
            Image toy = toyRect.gameObject.AddComponent<Image>();
            toy.preserveAspect = true;
            toy.raycastTarget = false;
            toy.enabled = false;

            var panel = card.gameObject.AddComponent<BabyHudPanel>();
            panel.Configure(nameLabel, face, ring, fill, fillImage, cry, diaper, diaperLabel, toy, pips, toyIcons);
            return panel;
        }

        // ---------------------------------------------------------------- Menus

        public TitleMenu BuildTitle(RectTransform root)
        {
            RectTransform screen = FullScreen("TitleScreen", root, new Color(0.15f, 0.08f, 0.2f, 0.25f));
            RectTransform logo = Rect("Logo", screen, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1400f, 200f));
            string word = "BABY CLASH";
            Color[] colors = { Pink, Yellow, Blue, Mint, Color.white, Lilac, Pink, Yellow, Blue, Mint };
            var letters = new System.Collections.Generic.List<RectTransform>();
            float x = -((word.Length - 1) * 118f) * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                if (word[i] != ' ')
                {
                    Text letter = Label($"Letter{i}", logo, word[i].ToString(), 190, colors[i], TextAnchor.MiddleCenter, new Vector2(x, 0f), new Vector2(150f, 220f), true);
                    Outline thick = letter.gameObject.AddComponent<Outline>();
                    thick.effectColor = Ink;
                    thick.effectDistance = new Vector2(6f, -6f);
                    letters.Add((RectTransform)letter.transform);
                }
                x += 118f;
            }
            logo.gameObject.AddComponent<TitleLogo>().Configure(letters.ToArray());
            Label("Tagline", screen, "a diaper-flinging nursery showdown", 40, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, -318f), new Vector2(1200f, 60f), true, new Vector2(0.5f, 1f));

            RectTransform column = Rect("Buttons", screen, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(460f, 560f));
            var menu = new TitleMenu
            {
                root = screen.gameObject,
                onePlayer = MenuButton("OnePlayer", column, "1 PLAYER", Pink, 0),
                twoPlayers = MenuButton("TwoPlayers", column, "2 PLAYERS", Blue, 1),
                howToPlay = MenuButton("HowToPlay", column, "HOW TO PLAY", Yellow, 2),
                options = MenuButton("Options", column, "OPTIONS", Mint, 3),
                quit = MenuButton("Quit", column, "QUIT", Lilac, 4),
            };
            Label("Footer", screen, "Keyboard: arrows / WASD + ENTER / SPACE      Gamepad: stick + A", 26, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(1400f, 40f), true, new Vector2(0.5f, 0f));
            return menu;
        }

        public DifficultyMenu BuildDifficulty(RectTransform root)
        {
            RectTransform screen = FullScreen("DifficultyScreen", root, new Color(0.15f, 0.08f, 0.2f, 0.45f));
            RectTransform panel = MenuPanel("Panel", screen, new Vector2(900f, 720f), "CHOOSE YOUR RIVAL");
            RectTransform column = Rect("Buttons", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(460f, 440f));
            var menu = new DifficultyMenu
            {
                root = screen.gameObject,
                easy = MenuButton("Easy", column, "EASY  (nap time)", Mint, 0),
                normal = MenuButton("Normal", column, "NORMAL  (cranky)", Yellow, 1),
                hard = MenuButton("Hard", column, "HARD  (teething)", Pink, 2),
                back = MenuButton("Back", column, "BACK", Lilac, 3),
            };
            Label("Tip", panel, "You play BABY 1 (arrows or WASD, gamepad works too)", 26, Ink, TextAnchor.MiddleCenter,
                new Vector2(0f, 50f), new Vector2(860f, 40f), false, new Vector2(0.5f, 0f));
            return menu;
        }

        public InfoMenu BuildHowToPlay(RectTransform root)
        {
            RectTransform screen = FullScreen("HowToPlayScreen", root, new Color(0.15f, 0.08f, 0.2f, 0.55f));
            RectTransform panel = MenuPanel("Panel", screen, new Vector2(1500f, 920f), "HOW TO PLAY");
            string rules =
                "Make the other baby cry! Every hit fills their CRY meter. At 100% they burst into tears and you win the round.\n\n" +
                "YOUR DIAPER IS YOUR ARMOUR: while you wear it, hits hurt 3x less and barely push you. It's also your best " +
                "weapon, but throw it and you're exposed until someone picks it up (maybe them!).\n\n" +
                "TOYS drop into the playroom: grab one to balance it on your head, then throw it without losing your diaper. " +
                "DASH to slide under throws, and bump into the other baby to shove them and knock their toy loose.\n\n" +
                "First to 2 rounds wins the match. Time up? The calmest baby wins.";
            Label("Rules", panel, rules, 28, Ink, TextAnchor.UpperLeft, new Vector2(0f, -175f), new Vector2(1360f, 380f), false, new Vector2(0.5f, 1f));

            RectTransform box = Rect("ControlsBox", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1300f, 240f));
            Style(box.gameObject.AddComponent<Image>(), rounded, new Color(0.45f, 0.7f, 1f, 0.22f));
            string[,] table =
            {
                { "", "MOVE", "GRAB / THROW", "DASH" },
                { "BABY 1", "W A S D", "SPACE", "LEFT SHIFT" },
                { "BABY 2", "ARROWS", "ENTER", "RIGHT SHIFT" },
                { "GAMEPAD", "LEFT STICK", "A / CROSS", "B / CIRCLE" },
            };
            float[] columns = { -470f, -160f, 150f, 450f };
            for (int row = 0; row < table.GetLength(0); row++)
            {
                for (int column = 0; column < table.GetLength(1); column++)
                {
                    Color color = row == 0 || column == 0 ? Pink : Ink;
                    Label($"Cell{row}{column}", box, table[row, column], 28, color, TextAnchor.MiddleCenter,
                        new Vector2(columns[column], 85f - row * 46f), new Vector2(300f, 44f), false);
                }
            }
            Label("PauseNote", box, "Pause: ESC or START", 24, Ink, TextAnchor.MiddleCenter, new Vector2(0f, -100f), new Vector2(600f, 36f), false);
            Button back = MenuButton("Back", panel, "BACK", Lilac, new Vector2(0f, 60f), new Vector2(0.5f, 0f));
            return new InfoMenu { root = screen.gameObject, back = back };
        }

        public OptionsMenu BuildOptions(RectTransform root)
        {
            RectTransform screen = FullScreen("OptionsScreen", root, new Color(0.15f, 0.08f, 0.2f, 0.55f));
            RectTransform panel = MenuPanel("Panel", screen, new Vector2(900f, 640f), "OPTIONS");
            var menu = new OptionsMenu
            {
                root = screen.gameObject,
                music = SliderRow(panel, "MUSIC", new Vector2(0f, 90f), Blue),
                effects = SliderRow(panel, "SOUNDS", new Vector2(0f, -10f), Pink),
                fullscreen = ToggleRow(panel, "FULLSCREEN", new Vector2(0f, -110f)),
            };
            menu.back = MenuButton("Back", panel, "BACK", Lilac, new Vector2(0f, 60f), new Vector2(0.5f, 0f));
            return menu;
        }

        public PauseMenu BuildPause(RectTransform root)
        {
            RectTransform screen = FullScreen("PauseScreen", root, new Color(0.1f, 0.05f, 0.15f, 0.5f));
            RectTransform panel = MenuPanel("Panel", screen, new Vector2(700f, 720f), "PAUSED");
            RectTransform column = Rect("Buttons", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(460f, 440f));
            return new PauseMenu
            {
                root = screen.gameObject,
                resume = MenuButton("Resume", column, "RESUME", Mint, 0),
                restart = MenuButton("Restart", column, "RESTART", Yellow, 1),
                options = MenuButton("Options", column, "OPTIONS", Blue, 2),
                mainMenu = MenuButton("MainMenu", column, "MAIN MENU", Pink, 3),
            };
        }

        public ResultsMenu BuildResults(RectTransform root)
        {
            RectTransform screen = FullScreen("ResultsScreen", root, new Color(0.1f, 0.05f, 0.15f, 0.35f));
            RectTransform panel = Rect("Panel", screen, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(1100f, 560f));
            Style(panel.gameObject.AddComponent<Image>(), rounded, PanelColor);
            Text title = Label("Winner", panel, "BABY 1 WINS!", 110, Pink, TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(1060f, 150f), true, new Vector2(0.5f, 1f));
            Text detail = Label("Detail", panel, "2 - 1", 44, Ink, TextAnchor.MiddleCenter, new Vector2(0f, -250f), new Vector2(1000f, 120f), false, new Vector2(0.5f, 1f));
            RectTransform row = Rect("Buttons", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(960f, 100f));
            Button rematch = MenuButton("Rematch", row, "REMATCH", Mint, new Vector2(-240f, 0f), new Vector2(0.5f, 0.5f));
            Button menu = MenuButton("MainMenu", row, "MAIN MENU", Blue, new Vector2(240f, 0f), new Vector2(0.5f, 0.5f));
            return new ResultsMenu { root = screen.gameObject, title = title, detail = detail, rematch = rematch, mainMenu = menu };
        }

        // ---------------------------------------------------------------- Loading

        /// <summary>Full-screen card shown while shaders compile at startup (see StartupWarmup). Starts visible.</summary>
        public GameObject BuildLoading(RectTransform root, out Image progress)
        {
            RectTransform screen = FullScreen("LoadingScreen", root, new Color(0.99f, 0.91f, 0.87f));
            Label("Title", screen, "BABY CLASH", 150, Pink, TextAnchor.MiddleCenter, new Vector2(0f, 90f), new Vector2(1400f, 200f), true);
            Label("Message", screen, "Warming up the nursery...", 40, Ink, TextAnchor.MiddleCenter, new Vector2(0f, -60f),
                new Vector2(1200f, 60f), false);

            RectTransform bar = Rect("Bar", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(640f, 36f));
            Style(bar.gameObject.AddComponent<Image>(), rounded, new Color(Ink.r, Ink.g, Ink.b, 0.18f));
            RectTransform fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(6f, 6f);
            fill.offsetMax = new Vector2(-6f, -6f);
            progress = fill.gameObject.AddComponent<Image>();
            Style(progress, rounded, Pink);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillOrigin = (int)Image.OriginHorizontal.Left;
            progress.fillAmount = 0f;
            return screen.gameObject;
        }

        // ---------------------------------------------------------------- Widgets

        RectTransform FullScreen(string name, RectTransform root, Color dim)
        {
            RectTransform screen = Rect(name, root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            screen.offsetMin = Vector2.zero;
            screen.offsetMax = Vector2.zero;
            Image image = screen.gameObject.AddComponent<Image>();
            image.color = dim;
            image.raycastTarget = true;
            return screen;
        }

        RectTransform MenuPanel(string name, RectTransform screen, Vector2 size, string title)
        {
            RectTransform panel = Rect(name, screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Style(panel.gameObject.AddComponent<Image>(), rounded, PanelColor);
            Text heading = Label("Title", panel, title, 72, Pink, TextAnchor.MiddleCenter, new Vector2(0f, -70f), new Vector2(size.x - 40f, 100f), false, new Vector2(0.5f, 1f));
            Outline outline = heading.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(3f, -3f);
            return panel;
        }

        /// <summary>Button in a vertical column (row 0 at the top).</summary>
        Button MenuButton(string name, RectTransform parent, string text, Color color, int row) =>
            MenuButton(name, parent, text, color, new Vector2(0f, -row * 108f - 45f), new Vector2(0.5f, 1f));

        Button MenuButton(string name, RectTransform parent, string text, Color color, Vector2 position, Vector2 anchor)
        {
            RectTransform rect = Rect(name, parent, anchor, anchor, position, new Vector2(440f, 90f));
            Image image = rect.gameObject.AddComponent<Image>();
            Style(image, rounded, color);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Label("Label", rect, text, 38, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(430f, 90f), true);
            rect.gameObject.AddComponent<MenuButtonFx>().Configure(audio);
            return button;
        }

        Slider SliderRow(RectTransform panel, string caption, Vector2 position, Color color)
        {
            RectTransform row = Rect(caption, panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(760f, 80f));
            Label("Caption", row, caption, 36, Ink, TextAnchor.MiddleLeft, new Vector2(-250f, 0f), new Vector2(260f, 60f), false);

            RectTransform track = Rect("Slider", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 0f), new Vector2(440f, 34f));
            Image background = track.gameObject.AddComponent<Image>();
            Style(background, rounded, new Color(0.28f, 0.18f, 0.34f, 0.2f));
            background.raycastTarget = true;

            RectTransform fillArea = Rect("FillArea", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(4f, 4f);
            fillArea.offsetMax = new Vector2(-4f, -4f);
            RectTransform fill = Rect("Fill", fillArea, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Style(fill.gameObject.AddComponent<Image>(), rounded, color);

            RectTransform handleArea = Rect("HandleArea", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            handleArea.offsetMin = new Vector2(20f, 0f);
            handleArea.offsetMax = new Vector2(-20f, 0f);
            RectTransform handle = Rect("Handle", handleArea, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(52f, 20f));
            Image knob = handle.gameObject.AddComponent<Image>();
            Style(knob, circle, Color.white);
            knob.raycastTarget = true;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;
            ColorBlock colors = slider.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.selectedColor = color;
            colors.highlightedColor = color;
            slider.colors = colors;
            track.gameObject.AddComponent<MenuButtonFx>().Configure(audio);
            return slider;
        }

        Toggle ToggleRow(RectTransform panel, string caption, Vector2 position)
        {
            RectTransform row = Rect(caption, panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(760f, 80f));
            Label("Caption", row, caption, 36, Ink, TextAnchor.MiddleLeft, new Vector2(-250f, 0f), new Vector2(300f, 60f), false);
            RectTransform box = Rect("Toggle", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 0f), new Vector2(70f, 70f));
            Image background = box.gameObject.AddComponent<Image>();
            Style(background, rounded, new Color(0.28f, 0.18f, 0.34f, 0.2f));
            background.raycastTarget = true;
            RectTransform check = Rect("Check", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            Image mark = check.gameObject.AddComponent<Image>();
            Style(mark, star, Yellow);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.isOn = true;
            box.gameObject.AddComponent<MenuButtonFx>().Configure(audio);
            return toggle;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor alignment,
            Vector2 position, Vector2 box, bool outlined, Vector2? anchor = null)
        {
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            RectTransform rect = Rect(name, parent, a, a, position, box);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            if (outlined)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.1f, 0.25f, 0.85f);
                outline.effectDistance = new Vector2(3f, -3f);
                Shadow shadow = rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
                shadow.effectDistance = new Vector2(0f, -6f);
            }
            return label;
        }

        static void Style(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
