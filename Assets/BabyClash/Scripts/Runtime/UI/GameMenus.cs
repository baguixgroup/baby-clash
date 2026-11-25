using System;
using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>The menu screens beyond the basic ones: party setup, story tour, wardrobe and challenges. Built at runtime.</summary>
    public sealed class GameMenus
    {
        static readonly GameMode[] partyModes = { GameMode.Clash, GameMode.KingOfTheBottle, GameMode.Survival, GameMode.TinyTots };
        static readonly Color[] roomColors = { MenuKit.Pink, MenuKit.Yellow, MenuKit.Blue, MenuKit.Mint, MenuKit.Lilac };

        readonly MenuKit kit;
        readonly Action<MatchSetup> startMatch;
        readonly Action goTitle;

        public GameMenus(MenuKit menuKit, Transform root, Action<MatchSetup> start, Action toTitle)
        {
            kit = menuKit;
            startMatch = start;
            goTitle = toTitle;
            BuildParty(root);
            BuildStory(root);
            BuildWardrobe(root);
            BuildChallenges(root);
        }

        public static string ModeName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.KingOfTheBottle: return "KING OF THE BOTTLE";
                case GameMode.Survival: return "SURVIVAL";
                case GameMode.TinyTots: return "TINY TOTS";
                case GameMode.Brawl: return "BRAWL";
                default: return "CLASH";
            }
        }

        static string ModeHint(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.KingOfTheBottle: return "Stand alone in the ring under the bottle. First to 20 points!";
                case GameMode.Survival: return "Wave after wave of cranky babies. How long can you last?";
                case GameMode.TinyTots: return "For little ones: everyone giggles, nobody loses.";
                default: return "Make the others cry. Last calm baby wins the round, first to two rounds wins.";
            }
        }

        // ------------------------------------------------------------ Party

        public GameObject PartyScreen { get; private set; }
        public Selectable PartyFirst => partyStart;

        readonly MatchSetup party = new MatchSetup { Slots = new[] { SlotDriver.Human, SlotDriver.CpuNormal, SlotDriver.CpuNormal, SlotDriver.Off } };
        Button partyMode;
        Button partyRoom;
        Button partyPower;
        Button partyTeams;
        Button partyStart;
        readonly Button[] partySlots = new Button[MatchSetup.MaxPlayers];
        Text partyHint;

        void BuildParty(Transform root)
        {
            RectTransform screen = kit.Screen("PartyScreen", root, 0.45f);
            PartyScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 860f), MenuKit.Panel);
            kit.Label("Title", panel, "PARTY", 76, MenuKit.Pink, new Vector2(0f, 360f), new Vector2(900f, 100f));
            var size = new Vector2(590f, 92f);
            partyMode = kit.Button("Mode", panel, "", MenuKit.Pink, new Vector2(-315f, 240f), size, 34, () => Cycle(CycleMode));
            partyRoom = kit.Button("Room", panel, "", MenuKit.Blue, new Vector2(-315f, 130f), size, 34, () => Cycle(CycleRoom));
            partyPower = kit.Button("PowerUps", panel, "", MenuKit.Yellow, new Vector2(-315f, 20f), size, 34, () => Cycle(() => party.PowerUps = !party.PowerUps));
            partyTeams = kit.Button("Teams", panel, "", MenuKit.Lilac, new Vector2(-315f, -90f), size, 34, () => Cycle(() => party.Teams = !party.Teams));
            for (int i = 0; i < partySlots.Length; i++)
            {
                int slot = i;
                partySlots[i] = kit.Button($"Slot{i + 1}", panel, "", MenuKit.Mint, new Vector2(315f, 240f - i * 110f), size, 34, () => Cycle(() => CycleSlot(slot)));
            }
            partyHint = kit.Label("Hint", panel, "", 30, MenuKit.Ink, new Vector2(0f, -195f), new Vector2(1200f, 80f));
            partyStart = kit.Button("Start", panel, "START!", MenuKit.Mint, new Vector2(-180f, -320f), new Vector2(480f, 110f), 50, () => startMatch(FinalParty()));
            kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(260f, -320f), new Vector2(300f, 110f), 40, () => goTitle());
            RefreshParty();
        }

        void Cycle(Action change)
        {
            change();
            RefreshParty();
        }

        void CycleMode() => party.Mode = partyModes[(Array.IndexOf(partyModes, party.Mode) + 1) % partyModes.Length];

        void CycleRoom() => party.Room = (RoomKind)(((int)party.Room + 1) % 5);

        void CycleSlot(int slot)
        {
            SlotDriver next = (SlotDriver)(((int)party.Slots[slot] + 1) % 5);
            // Baby 1 always plays.
            if (slot == 0 && next == SlotDriver.Off)
                next = SlotDriver.Human;
            party.Slots[slot] = next;
        }

        void RefreshParty()
        {
            MenuKit.SetText(partyMode, "MODE: " + ModeName(party.Mode));
            MenuKit.SetText(partyRoom, "ROOM: " + party.Room.ToString().ToUpperInvariant());
            MenuKit.SetText(partyPower, "POWER-UPS: " + (party.PowerUps ? "ON" : "OFF"));
            bool canTeam = party.Mode != GameMode.Survival && party.Mode != GameMode.TinyTots && party.Count(false) == 4;
            MenuKit.SetText(partyTeams, canTeam ? party.Teams ? "TEAMS: 1+3 VS 2+4" : "TEAMS: EVERY BABY FOR ITSELF" : "TEAMS: NEEDS 4 BABIES");
            partyTeams.interactable = canTeam;
            for (int i = 0; i < partySlots.Length; i++)
            {
                SlotDriver driver = party.Slots[i];
                string who = driver == SlotDriver.Human ? "PLAYER" : driver == SlotDriver.Off ? "---" : driver == SlotDriver.CpuEasy ? "CPU EASY" : driver == SlotDriver.CpuNormal ? "CPU NORMAL" : "CPU HARD";
                MenuKit.SetText(partySlots[i], $"BABY {i + 1}: {who}");
                MenuKit.SetColor(partySlots[i], driver == SlotDriver.Human ? MenuKit.Mint : driver == SlotDriver.Off ? MenuKit.Grey : MenuKit.Yellow);
            }
            partyHint.text = ModeHint(party.Mode);
            bool enough = party.Mode == GameMode.Survival ? party.Count(true) > 0 : party.Count(false) > 1;
            partyStart.interactable = enough;
        }

        MatchSetup FinalParty()
        {
            MatchSetup setup = party.Copy();
            if (setup.Mode == GameMode.Survival)
            {
                for (int i = 0; i < setup.Slots.Length; i++)
                {
                    if (setup.Slots[i] != SlotDriver.Human)
                        setup.Slots[i] = SlotDriver.CpuEasy;
                }
                // Four humans leave nobody to fight: the last one sits out.
                if (setup.Count(true) == MatchSetup.MaxPlayers)
                    setup.Slots[3] = SlotDriver.CpuEasy;
            }
            if (setup.Count(false) != 4 || setup.Mode == GameMode.Survival || setup.Mode == GameMode.TinyTots)
                setup.Teams = false;
            if (setup.Mode == GameMode.Clash && setup.Count(false) > 2)
                setup.Mode = GameMode.Brawl;
            return setup;
        }

        // ------------------------------------------------------------ Story

        public GameObject StoryScreen { get; private set; }
        public Selectable StoryFirst { get; private set; }

        readonly List<Button> levelButtons = new List<Button>();
        readonly List<Image[]> levelStars = new List<Image[]>();
        Text storyDetail;
        Text storyStars;

        void BuildStory(Transform root)
        {
            RectTransform screen = kit.Screen("StoryScreen", root, 0.45f);
            StoryScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 980f), MenuKit.Panel);
            kit.Label("Title", panel, "STORY TOUR", 66, MenuKit.Pink, new Vector2(0f, 432f), new Vector2(900f, 90f));
            storyStars = kit.Label("Stars", panel, "", 38, MenuKit.Ink, new Vector2(600f, 432f), new Vector2(300f, 60f));
            IReadOnlyList<LevelDef> levels = LevelCatalog.All;
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDef level = levels[i];
                int row = (int)level.Room, column = i % 4;
                if (column == 0)
                    kit.Label("Room", panel, level.Room.ToString().ToUpperInvariant(), 32, roomColors[row] * 0.8f + Color.black * 0.2f, new Vector2(-640f, 318f - row * 130f), new Vector2(240f, 60f));
                Button button = kit.Button("Level_" + level.Id, panel, "", roomColors[row], new Vector2(-365f + column * 315f, 318f - row * 130f), new Vector2(300f, 112f), 25,
                    () => startMatch(MatchSetup.ForLevel(level)));
                Text label = button.GetComponentInChildren<Text>(true);
                label.rectTransform.anchoredPosition = new Vector2(0f, 18f);
                var stars = new Image[3];
                for (int s = 0; s < 3; s++)
                    stars[s] = kit.Star(button.transform, new Vector2(-40f + s * 40f, -30f), 36f);
                levelButtons.Add(button);
                levelStars.Add(stars);
            }
            storyDetail = kit.Label("Detail", panel, "", 29, MenuKit.Ink, new Vector2(-110f, -385f), new Vector2(1120f, 150f), TextAnchor.MiddleLeft);
            kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(610f, -405f), new Vector2(260f, 96f), 38, () => goTitle());
        }

        public void RefreshStory()
        {
            PlayerProfile profile = PlayerProgress.Profile;
            storyStars.text = $"{profile.TotalStars} / {LevelCatalog.All.Count * 3} stars";
            for (int i = 0; i < levelButtons.Count; i++)
            {
                LevelDef level = LevelCatalog.All[i];
                bool open = LevelCatalog.IsUnlocked(profile, i);
                levelButtons[i].interactable = open;
                MenuKit.SetText(levelButtons[i], open ? level.Name : "LOCKED");
                for (int s = 0; s < 3; s++)
                    MenuKit.SetStar(levelStars[i][s], profile.StarsFor(level.Id) > s);
                if (open)
                    StoryFirst = levelButtons[i];
            }
            storyDetail.text = "";
        }

        // ------------------------------------------------------------ Wardrobe

        public GameObject WardrobeScreen { get; private set; }
        public Selectable WardrobeFirst => wardrobeSlotButton;
        public int WardrobeSlot { get; private set; }

        /// <summary>The slot shown changed, or something was bought or worn.</summary>
        public event Action WardrobeChanged;

        CosmeticSlot wardrobeTab;
        Button wardrobeSlotButton;
        Text wardrobeRattles;
        readonly Button[] tabs = new Button[3];
        readonly List<Button> itemButtons = new List<Button>();

        void BuildWardrobe(Transform root)
        {
            RectTransform screen = kit.Screen("WardrobeScreen", root, 0f);
            WardrobeScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(1f, 0.5f), new Vector2(-530f, 0f), new Vector2(980f, 980f), MenuKit.Panel);
            kit.Label("Title", panel, "WARDROBE", 62, MenuKit.Pink, new Vector2(-190f, 425f), new Vector2(560f, 90f));
            wardrobeRattles = kit.Label("Rattles", panel, "", 36, MenuKit.Ink, new Vector2(280f, 425f), new Vector2(380f, 60f));
            wardrobeSlotButton = kit.Button("Slot", panel, "", MenuKit.Mint, new Vector2(-330f, 320f), new Vector2(260f, 80f), 32, () =>
            {
                WardrobeSlot = (WardrobeSlot + 1) % MatchSetup.MaxPlayers;
                RefreshWardrobe();
                WardrobeChanged?.Invoke();
            });
            string[] names = { "HATS", "FACES", "OUTFITS" };
            for (int i = 0; i < 3; i++)
            {
                var tab = (CosmeticSlot)i;
                tabs[i] = kit.Button(names[i], panel, names[i], MenuKit.Blue, new Vector2(-65f + i * 210f, 320f), new Vector2(200f, 80f), 30, () =>
                {
                    wardrobeTab = tab;
                    RefreshWardrobe();
                });
            }
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                itemButtons.Add(kit.Button($"Item{i}", panel, "", MenuKit.Lilac, new Vector2(-230f + i % 2 * 460f, 215f - i / 2 * 112f), new Vector2(440f, 100f), 28, () => ClickItem(index)));
            }
            kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(0f, -410f), new Vector2(320f, 96f), 38, () => goTitle());
        }

        List<CosmeticItem> TabItems() => new List<CosmeticItem>(CosmeticCatalog.InSlot(wardrobeTab));

        void ClickItem(int index)
        {
            List<CosmeticItem> items = TabItems();
            if (index >= items.Count)
                return;
            PlayerProfile profile = PlayerProgress.Profile;
            if (!profile.Owns(items[index].Id))
                Shop.TryBuy(profile, items[index].Id);
            if (Shop.Equip(profile, WardrobeSlot, items[index].Id))
                PlayerProgress.Save();
            RefreshWardrobe();
            WardrobeChanged?.Invoke();
        }

        public void RefreshWardrobe()
        {
            PlayerProfile profile = PlayerProgress.Profile;
            BabyLook look = profile.LookFor(WardrobeSlot);
            wardrobeRattles.text = $"{profile.rattles} rattles";
            MenuKit.SetText(wardrobeSlotButton, $"BABY {WardrobeSlot + 1}");
            for (int i = 0; i < tabs.Length; i++)
                MenuKit.SetColor(tabs[i], (int)wardrobeTab == i ? MenuKit.Pink : MenuKit.Blue);
            List<CosmeticItem> items = TabItems();
            for (int i = 0; i < itemButtons.Count; i++)
            {
                Button button = itemButtons[i];
                button.gameObject.SetActive(i < items.Count);
                if (i >= items.Count)
                    continue;
                CosmeticItem item = items[i];
                bool owned = profile.Owns(item.Id), worn = look.Get(wardrobeTab) == item.Id;
                button.interactable = owned || profile.rattles >= item.Price;
                MenuKit.SetText(button, item.Name + "\n" + (worn ? "WEARING" : owned ? "OWNED" : item.Price + " rattles"));
                MenuKit.SetColor(button, worn ? MenuKit.Mint : owned ? MenuKit.Lilac : MenuKit.Yellow);
            }
        }

        // ------------------------------------------------------------ Challenges

        public GameObject ChallengesScreen { get; private set; }
        public Selectable ChallengesFirst { get; private set; }

        readonly Text[] challengeLines = new Text[ChallengeBook.DailyCount];
        readonly RectTransform[] challengeFills = new RectTransform[ChallengeBook.DailyCount];
        Text challengeFooter;

        void BuildChallenges(Transform root)
        {
            RectTransform screen = kit.Screen("ChallengesScreen", root, 0.45f);
            ChallengesScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 880f), MenuKit.Panel);
            kit.Label("Title", panel, "TODAY'S CHALLENGES", 62, MenuKit.Pink, new Vector2(0f, 370f), new Vector2(1100f, 90f));
            for (int i = 0; i < challengeLines.Length; i++)
            {
                RectTransform row = kit.Box("Row", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 240f - i * 140f), new Vector2(1100f, 122f), new Color(0.28f, 0.18f, 0.34f, 0.12f));
                challengeLines[i] = kit.Label("Text", row, "", 34, MenuKit.Ink, new Vector2(0f, 18f), new Vector2(1040f, 60f), TextAnchor.MiddleLeft);
                RectTransform bar = kit.Box("Bar", row, new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(1040f, 26f), new Color(0.28f, 0.18f, 0.34f, 0.2f));
                challengeFills[i] = kit.Box("Fill", bar, Vector2.zero, Vector2.zero, Vector2.zero, MenuKit.Mint);
                challengeFills[i].anchorMax = Vector2.one;
                challengeFills[i].offsetMin = challengeFills[i].offsetMax = Vector2.zero;
            }
            challengeFooter = kit.Label("Footer", panel, "", 32, MenuKit.Ink, new Vector2(0f, -215f), new Vector2(1100f, 180f));
            ChallengesFirst = kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(0f, -365f), new Vector2(320f, 96f), 38, () => goTitle());
        }

        public void RefreshChallenges()
        {
            PlayerProfile profile = PlayerProgress.Profile;
            ChallengeBook.Refresh(profile, DateTime.Today);
            for (int i = 0; i < challengeLines.Length && i < profile.challenges.Count; i++)
            {
                ChallengeState state = profile.challenges[i];
                ChallengeDef def = ChallengeBook.Find(state.id);
                if (def == null)
                    continue;
                challengeLines[i].text = state.done ? $"DONE!  {def.Text}" : $"{def.Text}   {state.progress}/{def.Goal}   +{def.Reward} rattles";
                challengeFills[i].anchorMax = new Vector2(Mathf.Clamp01(state.progress / (float)def.Goal), 1f);
            }
            LifetimeStats life = profile.lifetime;
            challengeFooter.text = $"Streak: {ChallengeBook.CurrentStreak(profile, DateTime.Today)} days      Rattles: {profile.rattles}\n" +
                                   $"Best survival wave: {profile.bestSurvivalWave}      Matches: {life.matches}      Wins: {life.wins}\n" +
                                   $"Hits: {life.hits}      Babies made to cry: {life.knockouts}      Giggles: {life.giggles}";
        }

        // ------------------------------------------------------------ Shared

        /// <summary>Shows the selected level's star goals. Call every frame while the story screen is open.</summary>
        public void UpdateStoryDetail()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < levelButtons.Count; i++)
            {
                if (levelButtons[i].gameObject != selected)
                    continue;
                LevelDef level = LevelCatalog.All[i];
                string text = $"{level.Name}: {ModeName(level.Mode)}\n1 star: {LevelCatalog.Describe(level, StarGoal.Win)}\n" +
                              $"+1: {LevelCatalog.Describe(level, level.Second)}      +1: {LevelCatalog.Describe(level, level.Third)}";
                if (storyDetail.text != text)
                    storyDetail.text = text;
                return;
            }
        }
    }
}
