using System;
using BabyClash.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>
    /// Online screens: play online (quick match / make a room / join with a code), the code keypad (works with touch,
    /// gamepads and keyboards), the room where the host picks the match, and the emote buttons shown during online
    /// matches. Players are only ever shown as "you", "friend" or a CPU: no names, no chat.
    /// </summary>
    public sealed class OnlineMenus
    {
        const int CodeLength = 6;
        const string Keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static readonly GameMode[] modes = { GameMode.Clash, GameMode.KingOfTheBottle, GameMode.Survival, GameMode.TinyTots };

        readonly MenuKit kit;
        readonly NetGame game;
        readonly NetSession session;
        readonly Action toTitle;
        readonly Action openCode;
        readonly Action openOnline;

        Text onlineStatus;
        Text codeText;
        Text codeStatus;
        Text roomCode;
        Text roomHint;
        Button roomMode;
        Button roomRoom;
        Button roomPower;
        Button roomTeams;
        Button roomStart;
        readonly Button[] roomSlots = new Button[MatchSetup.MaxPlayers];
        readonly Button[] emoteButtons = new Button[4];
        GameObject emoteBar;
        string code = "";
        bool typing;

        public GameObject OnlineScreen { get; private set; }
        public GameObject CodeScreen { get; private set; }
        public GameObject RoomScreen { get; private set; }
        public Selectable OnlineFirst { get; private set; }
        public Selectable CodeFirst { get; private set; }
        public Selectable RoomFirst => roomStart.interactable ? roomStart : (Selectable)roomLeave;

        Button roomLeave;

        public OnlineMenus(MenuKit menuKit, Transform root, NetGame netGame, NetSession netSession, Action goTitle, Action showCode, Action showOnline)
        {
            kit = menuKit;
            game = netGame;
            session = netSession;
            toTitle = goTitle;
            openCode = showCode;
            openOnline = showOnline;
            BuildOnline(root);
            BuildCode(root);
            BuildRoom(root);
            BuildEmotes(root);
            session.StatusChanged += text =>
            {
                onlineStatus.text = text;
                codeStatus.text = text;
            };
            game.LobbyChanged += RefreshRoom;
        }

        // ------------------------------------------------------------ play online

        void BuildOnline(Transform root)
        {
            RectTransform screen = kit.Screen("OnlineScreen", root, 0.45f);
            OnlineScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 860f), MenuKit.Panel);
            kit.Label("Title", panel, "PLAY ONLINE", 76, MenuKit.Pink, new Vector2(0f, 345f), new Vector2(900f, 100f));
            kit.Label("Tagline", panel, "Play with family and friends anywhere.\nNo chat, no names - just babies and fun!", 30, MenuKit.Ink,
                new Vector2(0f, 245f), new Vector2(980f, 90f));
            var size = new Vector2(620f, 100f);
            Button quick = kit.Button("Quick", panel, "QUICK MATCH", MenuKit.Mint, new Vector2(0f, 120f), size, 42, () => session.QuickMatch());
            kit.Button("Make", panel, "MAKE A ROOM FOR FRIENDS", MenuKit.Pink, new Vector2(0f, 5f), size, 36, () => session.Host(true));
            kit.Button("Join", panel, "JOIN A FRIEND'S ROOM", MenuKit.Blue, new Vector2(0f, -110f), size, 36, () =>
            {
                code = "";
                RefreshCode();
                codeStatus.text = "Type the code your friend sees on their screen.";
                openCode();
            });
            onlineStatus = kit.Label("Status", panel, "", 28, MenuKit.Ink, new Vector2(0f, -215f), new Vector2(980f, 80f));
            kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(0f, -325f), new Vector2(300f, 100f), 40, () => toTitle());
            OnlineFirst = quick;
        }

        /// <summary>Clears old messages when the screen opens.</summary>
        public void OpenOnline(string message)
        {
            onlineStatus.text = message ?? "";
        }

        // ------------------------------------------------------------ code keypad

        void BuildCode(Transform root)
        {
            RectTransform screen = kit.Screen("CodeScreen", root, 0.45f);
            CodeScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 940f), MenuKit.Panel);
            kit.Label("Title", panel, "ROOM CODE", 64, MenuKit.Blue, new Vector2(0f, 395f), new Vector2(900f, 90f));
            RectTransform box = kit.Box("CodeBox", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(620f, 110f), Color.white);
            codeText = kit.Label("Code", box, "", 72, MenuKit.Ink, Vector2.zero, new Vector2(600f, 100f));
            Button first = null;
            for (int i = 0; i < Keys.Length; i++)
            {
                char key = Keys[i];
                int column = i % 9, row = i / 9;
                Color color = char.IsDigit(key) ? MenuKit.Yellow : MenuKit.Lilac;
                Button button = kit.Button("Key" + key, panel, key.ToString(), color, new Vector2(-480f + column * 120f, 170f - row * 110f),
                    new Vector2(108f, 98f), 44, () => Type(key));
                first ??= button;
            }
            kit.Button("Delete", panel, "DELETE", MenuKit.Grey, new Vector2(-330f, -300f), new Vector2(280f, 100f), 38, Backspace);
            kit.Button("Go", panel, "JOIN!", MenuKit.Mint, new Vector2(0f, -300f), new Vector2(320f, 100f), 46, () => session.Join(code));
            kit.Button("Back", panel, "BACK", MenuKit.Grey, new Vector2(330f, -300f), new Vector2(280f, 100f), 38, () => openOnline());
            codeStatus = kit.Label("Status", panel, "", 28, MenuKit.Ink, new Vector2(0f, -395f), new Vector2(1100f, 70f));
            CodeFirst = first;
            RefreshCode();
        }

        void Type(char key)
        {
            if (code.Length < CodeLength)
            {
                code += key;
                RefreshCode();
            }
        }

        void Backspace()
        {
            if (code.Length > 0)
            {
                code = code.Substring(0, code.Length - 1);
                RefreshCode();
            }
        }

        void RefreshCode()
        {
            string shown = code;
            while (shown.Length < CodeLength)
                shown += "_";
            codeText.text = string.Join(" ", shown.ToCharArray());
        }

        /// <summary>Physical keyboards can type the code too.</summary>
        public void SetTyping(bool on)
        {
            if (typing == on || Keyboard.current == null)
                return;
            typing = on;
            if (on)
                Keyboard.current.onTextInput += OnText;
            else
                Keyboard.current.onTextInput -= OnText;
        }

        void OnText(char c)
        {
            if (c == '\b')
            {
                Backspace();
                return;
            }
            if (c == '\r' || c == '\n')
            {
                if (code.Length > 0)
                    session.Join(code);
                return;
            }
            c = char.ToUpperInvariant(c);
            if (Keys.IndexOf(c) >= 0)
                Type(c);
        }

        // ------------------------------------------------------------ room

        void BuildRoom(Transform root)
        {
            RectTransform screen = kit.Screen("RoomScreen", root, 0.45f);
            RoomScreen = screen.gameObject;
            RectTransform panel = kit.Box("Panel", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 900f), MenuKit.Panel);
            roomCode = kit.Label("Code", panel, "", 56, MenuKit.Pink, new Vector2(0f, 375f), new Vector2(1240f, 80f));
            kit.Label("Share", panel, "Tell your friends this code so they can join!", 28, MenuKit.Ink, new Vector2(0f, 315f), new Vector2(1200f, 50f));
            var size = new Vector2(590f, 92f);
            roomMode = kit.Button("Mode", panel, "", MenuKit.Pink, new Vector2(-315f, 205f), size, 34, () => Change(() =>
                game.Lobby.Mode = modes[(Array.IndexOf(modes, game.Lobby.Mode) + 1) % modes.Length]));
            roomRoom = kit.Button("Room", panel, "", MenuKit.Blue, new Vector2(-315f, 95f), size, 34, () => Change(() =>
                game.Lobby.Room = (RoomKind)(((int)game.Lobby.Room + 1) % 5)));
            roomPower = kit.Button("PowerUps", panel, "", MenuKit.Yellow, new Vector2(-315f, -15f), size, 34, () => Change(() =>
                game.Lobby.PowerUps = !game.Lobby.PowerUps));
            roomTeams = kit.Button("Teams", panel, "", MenuKit.Lilac, new Vector2(-315f, -125f), size, 34, () => Change(() =>
                game.Lobby.Teams = !game.Lobby.Teams));
            for (int i = 0; i < roomSlots.Length; i++)
            {
                int slot = i;
                roomSlots[i] = kit.Button($"Slot{i + 1}", panel, "", MenuKit.Mint, new Vector2(315f, 205f - i * 110f), size, 32, () => game.HostCycleSlot(slot));
            }
            roomHint = kit.Label("Hint", panel, "", 30, MenuKit.Ink, new Vector2(0f, -225f), new Vector2(1240f, 80f));
            roomStart = kit.Button("Start", panel, "START!", MenuKit.Mint, new Vector2(-180f, -345f), new Vector2(480f, 110f), 50, () => game.HostStartMatch());
            roomLeave = kit.Button("Leave", panel, "LEAVE", MenuKit.Grey, new Vector2(260f, -345f), new Vector2(300f, 110f), 40, () => game.Leave());
            RefreshRoom();
        }

        void Change(Action change)
        {
            if (!NetRole.IsHost)
                return;
            change();
            game.HostRoomChanged();
        }

        public void RefreshRoom()
        {
            LobbyState lobby = game.Lobby;
            bool host = NetRole.IsHost;
            roomCode.text = string.IsNullOrEmpty(lobby.Code) ? "YOUR ROOM" : "ROOM CODE:  " + lobby.Code;
            MenuKit.SetText(roomMode, "MODE: " + GameMenus.ModeName(lobby.Mode));
            MenuKit.SetText(roomRoom, "ROOM: " + lobby.Room.ToString().ToUpperInvariant());
            MenuKit.SetText(roomPower, lobby.PowerUps ? "POWER-UPS: ON" : "POWER-UPS: OFF");
            bool canTeam = lobby.CanTeam;
            MenuKit.SetText(roomTeams, canTeam ? lobby.Teams ? "TEAMS: 1+3 VS 2+4" : "TEAMS: EVERY BABY FOR ITSELF" : "TEAMS: NEEDS 4 BABIES");
            roomMode.interactable = roomRoom.interactable = roomPower.interactable = host;
            roomTeams.interactable = host && canTeam;
            for (int i = 0; i < roomSlots.Length; i++)
            {
                SlotDriver driver = lobby.Slots[i];
                bool mine = i == game.LocalSlot;
                string who;
                switch (driver)
                {
                    case SlotDriver.Human:
                    case SlotDriver.Online:
                        who = mine ? "YOU!" : "FRIEND";
                        break;
                    case SlotDriver.Off:
                        who = host ? "--- (ADD CPU?)" : "---";
                        break;
                    default:
                        who = driver == SlotDriver.CpuEasy ? "CPU EASY" : driver == SlotDriver.CpuNormal ? "CPU NORMAL" : "CPU HARD";
                        break;
                }
                if (lobby.Mode == GameMode.Survival && driver != SlotDriver.Human && driver != SlotDriver.Online)
                    who = "CRANKY BABY";
                MenuKit.SetText(roomSlots[i], $"BABY {i + 1}: {who}");
                bool person = driver == SlotDriver.Human || driver == SlotDriver.Online;
                MenuKit.SetColor(roomSlots[i], person ? (mine ? MenuKit.Pink : MenuKit.Mint) : driver == SlotDriver.Off ? MenuKit.Grey : MenuKit.Yellow);
                roomSlots[i].interactable = host && !person && lobby.Mode != GameMode.Survival;
            }
            roomStart.gameObject.SetActive(host);
            roomStart.interactable = host && game.CanStart && !game.InMatch;
            string hint;
            if (lobby.InMatch && !host)
                hint = game.LocalSlot >= 0 ? "A match is on - get ready!" : "A match is on. You'll play in the next one!";
            else if (lobby.AutoStart > 0f)
                hint = $"Starting in {Mathf.CeilToInt(lobby.AutoStart)}...";
            else if (host)
                hint = lobby.People < 2 ? "Waiting for friends... You can add CPU babies too." : "Everyone's here? Press START!";
            else
                hint = "Waiting for the host to start...";
            roomHint.text = hint;
        }

        // ------------------------------------------------------------ emotes

        void BuildEmotes(Transform root)
        {
            RectTransform bar = kit.Rect("EmoteBar", root, new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(170f, 420f));
            emoteBar = bar.gameObject;
            for (int i = 0; i < emoteButtons.Length; i++)
            {
                int emote = i;
                emoteButtons[i] = kit.Button("Emote" + i, bar, $"{i + 1}  " + NetWire.Emotes[i], NetWire.EmoteColors[i], new Vector2(0f, 150f - i * 100f),
                    new Vector2(165f, 86f), 22, () => game.SendEmote(emote));
                // Taps only: keyboards and gamepads use the number keys / d-pad so the buttons never steal focus.
                Navigation none = default;
                none.mode = Navigation.Mode.None;
                emoteButtons[i].navigation = none;
            }
            emoteBar.SetActive(false);
        }

        /// <summary>Emote buttons and keys (1-4, gamepad d-pad) during online matches.</summary>
        public void UpdateMatch(bool show)
        {
            show &= NetRole.IsOnline && game.LocalSlot >= 0;
            if (emoteBar.activeSelf != show)
                emoteBar.SetActive(show);
            if (!show)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) game.SendEmote(0);
                if (keyboard.digit2Key.wasPressedThisFrame) game.SendEmote(1);
                if (keyboard.digit3Key.wasPressedThisFrame) game.SendEmote(2);
                if (keyboard.digit4Key.wasPressedThisFrame) game.SendEmote(3);
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.dpad.up.wasPressedThisFrame) game.SendEmote(0);
                if (pad.dpad.right.wasPressedThisFrame) game.SendEmote(1);
                if (pad.dpad.down.wasPressedThisFrame) game.SendEmote(2);
                if (pad.dpad.left.wasPressedThisFrame) game.SendEmote(3);
            }
        }
    }
}
