using System;
using System.Collections.Generic;
using BabyClash.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Online play on top of the normal game. The host runs the real match (physics, rules, CPUs) and many times a
    /// second sends a snapshot of where everything is, plus the things that happened (hits, throws, tears) stamped
    /// with the host's clock. Guests show the match a tenth of a second behind, smoothing between snapshots and
    /// replaying the events at the right moment, so animation, sound and the HUD work exactly as offline. A guest's
    /// own stick and buttons go to the host, which applies them to that guest's baby.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class NetGame : MonoBehaviour, IBabyCommandSink
    {
        const float SendInterval = 1f / 30f;
        const float InterpolationDelay = 0.11f;
        const float EmoteCooldown = 0.8f;
        const float TeleportDistance = 2.5f;
        const int MaxItems = 24;
        const int MaxPowerUps = 8;
        const int BufferSize = 32;
        const byte NoSpot = 255;

        enum EventType : byte
        {
            Baby,
            ItemHit,
            ToySpawned,
            PowerUp,
        }

        struct NetEvent
        {
            public EventType Type;
            public float Time;
            public BabyNetEvent Kind;
            public sbyte A;
            public sbyte B;
            public sbyte Item;
            public byte Sub;
            public byte Flags;
            public Vector3 Point;
            public Vector3 Direction;
            public float Cry;
            public float Knockback;
        }

        struct BabySnap
        {
            public bool Active;
            public bool InRound;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public BabyActivity Activity;
            public float Cry;
            public int Shield;
        }

        struct ItemSnap
        {
            public bool Active;
            public bool Visible;
            public ItemState State;
            public int Holder;
            public int Thrower;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        struct PowerSnap
        {
            public ushort Id;
            public PowerUpKind Kind;
            public Vector3 Position;
        }

        sealed class Snap
        {
            public uint Seq;
            public float Time;
            public float GameTime;
            public readonly RoundMirror Round = new RoundMirror();
            public int BabyCount;
            public readonly BabySnap[] Babies = new BabySnap[MatchSetup.MaxPlayers];
            public int ItemCount;
            public readonly ItemSnap[] Items = new ItemSnap[MaxItems];
            public int PowerCount;
            public readonly PowerSnap[] Powers = new PowerSnap[MaxPowerUps];
            public int BottleSpot;
            public bool Vacuum;
            public Vector3 VacuumPosition;
            public Quaternion VacuumRotation;
            public int Wave;
            public int Knockouts;
        }

        sealed class RemoteInput
        {
            public Vector2 Move;
            public ushort Actions;
            public ushort Dashes;
            public ushort AppliedActions;
            public ushort AppliedDashes;
            public float LastEmote = -10f;
        }

        GameFlow flow;
        RoundManager round;
        PowerUpSpawner powerUps;
        BottleZone bottle;
        SurvivalDirector survival;
        AudioDirector audioDirector;
        NetSession net;
        readonly List<Throwable> items = new List<Throwable>();
        readonly RemoteInput[] inputs = new RemoteInput[MatchSetup.MaxPlayers];
        readonly BabyLook[] looks = new BabyLook[MatchSetup.MaxPlayers];
        readonly List<ulong> guests = new List<ulong>();
        readonly List<NetEvent> pending = new List<NetEvent>();
        readonly List<NetEvent> incoming = new List<NetEvent>();
        readonly Snap[] ring = new Snap[BufferSize];
        readonly RoundMirror mirrorScratch = new RoundMirror();
        readonly Dictionary<PowerUpPickup, ushort> powerIds = new Dictionary<PowerUpPickup, ushort>();
        readonly Dictionary<ushort, PowerUpPickup> mirroredPowers = new Dictionary<ushort, PowerUpPickup>();
        readonly List<ushort> powerScratch = new List<ushort>();
        int snapCount;
        int newest = -1;
        ushort matchId;
        ushort nextPowerId = 1;
        uint snapSeq;
        float sendTimer;
        float clockOffset;
        bool clockSynced;
        bool subscribed;
        bool matchLive;
        MatchSetup lastSetup;

        // Guest's own controls.
        Vector2 localMove;
        ushort localActions;
        ushort localDashes;
        float lastLocalEmote = -10f;

        TextMesh youMarker;

        /// <summary>The online room as the host set it up.</summary>
        public LobbyState Lobby { get; } = new LobbyState();

        /// <summary>This device's baby slot (0 for the host; -1 for a guest who is only watching).</summary>
        public int LocalSlot { get; private set; } = -1;

        /// <summary>The host's room changed (players, settings).</summary>
        public event Action LobbyChanged;

        /// <summary>Counters for the smoke test and debugging.</summary>
        public int SnapshotsReceived { get; private set; }
        public int EventsReceived { get; private set; }
        public int InputsReceived { get; private set; }
        public int RemotePressesApplied { get; private set; }

        public bool InMatch => matchLive;

        public static NetGame Create(GameFlow gameFlow, RoundManager manager, PowerUpSpawner spawner, BottleZone zone,
            SurvivalDirector director, AudioDirector audio)
        {
            NetGame game = new GameObject("NetGame").AddComponent<NetGame>();
            game.flow = gameFlow;
            game.round = manager;
            game.powerUps = spawner;
            game.bottle = zone;
            game.survival = director;
            game.audioDirector = audio;
            game.net = NetSession.Instance != null ? NetSession.Instance : NetSession.Create();
            game.net.Started += game.HandleStarted;
            game.net.Ended += game.HandleEnded;
            game.net.Admit = game.AdmitGuest;
            for (int i = 0; i < game.inputs.Length; i++)
                game.inputs[i] = new RemoteInput();
            for (int i = 0; i < game.ring.Length; i++)
                game.ring[i] = new Snap();
            game.CollectItems();
            return game;
        }

        void CollectItems()
        {
            items.Clear();
            foreach (Diaper diaper in round.Diapers)
                items.Add(diaper);
            if (round.Toys != null)
            {
                foreach (Toy toy in round.Toys.Pool)
                    items.Add(toy);
            }
            if (items.Count > MaxItems)
                items.RemoveRange(MaxItems, items.Count - MaxItems);
        }

        NetworkManager Manager => net != null ? net.Manager : null;

        CustomMessagingManager Messages => Manager != null ? Manager.CustomMessagingManager : null;

        // ------------------------------------------------------------------ session

        void HandleStarted(NetMode mode)
        {
            matchLive = false;
            pending.Clear();
            incoming.Clear();
            snapCount = 0;
            newest = -1;
            clockSynced = false;
            CustomMessagingManager messages = Messages;
            if (mode == NetMode.Host)
            {
                LocalSlot = 0;
                Lobby.Slots[0] = SlotDriver.Human;
                for (int i = 1; i < Lobby.Slots.Length; i++)
                {
                    Lobby.Slots[i] = SlotDriver.Off;
                    Lobby.Owners[i] = 0;
                }
                Lobby.Owners[0] = NetworkManager.ServerClientId;
                Lobby.InMatch = false;
                Lobby.Code = net.Code;
                Lobby.AutoStart = -1f;
                Array.Clear(looks, 0, looks.Length);
                messages.RegisterNamedMessageHandler(NetWire.Hello, OnHello);
                messages.RegisterNamedMessageHandler(NetWire.Input, OnInput);
                messages.RegisterNamedMessageHandler(NetWire.Emote, OnEmoteRequest);
                Manager.OnClientConnectedCallback += HostGuestConnected;
                Manager.OnClientDisconnectCallback += HostGuestDisconnected;
                Subscribe(true);
            }
            else
            {
                LocalSlot = -1;
                messages.RegisterNamedMessageHandler(NetWire.Lobby, OnLobby);
                messages.RegisterNamedMessageHandler(NetWire.Start, OnStart);
                messages.RegisterNamedMessageHandler(NetWire.Snapshot, OnSnapshot);
                messages.RegisterNamedMessageHandler(NetWire.Events, OnEvents);
                messages.RegisterNamedMessageHandler(NetWire.Emote, OnEmoteShown);
                messages.RegisterNamedMessageHandler(NetWire.Menu, OnMenu);
                Manager.OnClientConnectedCallback += GuestConnected;
                if (Manager.IsConnectedClient)
                    GuestConnected(Manager.LocalClientId);
            }
            LobbyChanged?.Invoke();
        }

        void HandleEnded(string reason)
        {
            NetworkManager manager = Manager;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= HostGuestConnected;
                manager.OnClientDisconnectCallback -= HostGuestDisconnected;
                manager.OnClientConnectedCallback -= GuestConnected;
            }
            Subscribe(false);
            matchLive = false;
            LocalSlot = -1;
            SetYouMarker(null);
            flow.OnlineEnded(reason);
        }

        /// <summary>Leaves the room (and closes it, for the host).</summary>
        public void Leave() => net.Leave();

        // ------------------------------------------------------------------ host: room

        string AdmitGuest(ulong clientId)
        {
            int free = 0;
            for (int i = 0; i < Lobby.Slots.Length; i++)
            {
                if (Lobby.Slots[i] != SlotDriver.Human && Lobby.Slots[i] != SlotDriver.Online)
                    free++;
            }
            return free > 0 ? null : "That room is full.";
        }

        void HostGuestConnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId || Lobby.SlotOf(clientId) >= 0)
                return;
            for (int i = 0; i < Lobby.Slots.Length; i++)
            {
                if (Lobby.Slots[i] == SlotDriver.Human || Lobby.Slots[i] == SlotDriver.Online)
                    continue;
                Lobby.Slots[i] = SlotDriver.Online;
                Lobby.Owners[i] = clientId;
                inputs[i] = new RemoteInput();
                looks[i] = null;
                break;
            }
            // Public rooms start by themselves once there's someone to play with.
            if (net.IsPublic && !matchLive && Lobby.People >= 2 && Lobby.AutoStart < 0f)
                Lobby.AutoStart = 20f;
            HostRoomChanged();
            // Joining mid-match: watch it until the next one.
            if (matchLive && lastSetup != null)
                SendStart(clientId, lastSetup);
        }

        void HostGuestDisconnected(ulong clientId)
        {
            int slot = Lobby.SlotOf(clientId);
            if (slot < 0)
                return;
            Lobby.Slots[slot] = SlotDriver.Off;
            Lobby.Owners[slot] = 0;
            looks[slot] = null;
            if (matchLive)
                flow.OnlinePlayerLeft(slot);
            HostRoomChanged();
        }

        /// <summary>Host: call after changing <see cref="Lobby"/> settings.</summary>
        public void HostRoomChanged()
        {
            if (!NetRole.IsHost)
                return;
            Lobby.Code = net.Code;
            Lobby.InMatch = matchLive;
            var writer = new FastBufferWriter(256, Allocator.Temp);
            try
            {
                Lobby.Write(ref writer);
                SendToGuests(NetWire.Lobby, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
            LobbyChanged?.Invoke();
        }

        /// <summary>Host: changes how a free slot is filled (Off or a CPU level). People's slots can't be changed.</summary>
        public void HostCycleSlot(int slot)
        {
            if (!NetRole.IsHost || slot <= 0 || slot >= Lobby.Slots.Length)
                return;
            SlotDriver driver = Lobby.Slots[slot];
            if (driver == SlotDriver.Human || driver == SlotDriver.Online)
                return;
            Lobby.Slots[slot] = driver == SlotDriver.Off ? SlotDriver.CpuEasy : driver == SlotDriver.CpuEasy ? SlotDriver.CpuNormal
                : driver == SlotDriver.CpuNormal ? SlotDriver.CpuHard : SlotDriver.Off;
            HostRoomChanged();
        }

        /// <summary>Host: true when the room's settings make a playable match.</summary>
        public bool CanStart
        {
            get
            {
                MatchSetup setup = Lobby.ToSetup();
                return setup.Mode == GameMode.Survival ? setup.Count(false) > 0 : setup.Count(false) > 1;
            }
        }

        /// <summary>Host: starts a match with the room's players and settings, on every screen.</summary>
        public void HostStartMatch()
        {
            if (!NetRole.IsHost || !CanStart)
                return;
            MatchSetup setup = Lobby.ToSetup();
            for (int i = 0; i < looks.Length; i++)
            {
                if (setup.IsHuman(i))
                    looks[i] = PlayerProgress.Profile.LookFor(i).Copy();
                else if (!setup.IsOnline(i) || looks[i] == null)
                    looks[i] = setup.IsOnline(i) ? new BabyLook() : GameFlow.RandomLook();
            }
            matchId++;
            lastSetup = setup;
            matchLive = true;
            pending.Clear();
            Lobby.AutoStart = -1f;
            foreach (RemoteInput input in inputs)
            {
                input.AppliedActions = input.Actions;
                input.AppliedDashes = input.Dashes;
                input.Move = Vector2.zero;
            }
            flow.StartOnlineMatch(setup, (BabyLook[])looks.Clone());
            RefreshGuests();
            foreach (ulong guest in guests)
                SendStart(guest, setup);
            HostRoomChanged();
            SetYouMarker(round.AllBabies[0]);
        }

        void SendStart(ulong guest, MatchSetup setup)
        {
            var writer = new FastBufferWriter(512, Allocator.Temp, 4096);
            try
            {
                WriteStart(ref writer, guest, setup);
                Messages.SendNamedMessage(NetWire.Start, guest, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        void WriteStart(ref FastBufferWriter writer, ulong guest, MatchSetup setup)
        {
            writer.WriteValueSafe(matchId);
            writer.WriteValueSafe((sbyte)Lobby.SlotOf(guest));
            writer.WriteValueSafe((byte)setup.Mode);
            writer.WriteValueSafe((byte)setup.Room);
            writer.WriteValueSafe(setup.PowerUps);
            writer.WriteValueSafe(setup.Teams);
            for (int i = 0; i < MatchSetup.MaxPlayers; i++)
            {
                writer.WriteValueSafe((byte)setup.Slots[i]);
                NetWire.WriteLook(ref writer, looks[i]);
            }
        }

        /// <summary>Host: everyone back to the room screen.</summary>
        public void HostBackToRoom()
        {
            if (!NetRole.IsHost)
                return;
            matchLive = false;
            SetYouMarker(null);
            using (var writer = new FastBufferWriter(8, Allocator.Temp))
            {
                writer.WriteValueSafe(matchId);
                SendToGuests(NetWire.Menu, writer, NetworkDelivery.ReliableSequenced);
            }
            if (net.IsPublic && Lobby.People >= 2)
                Lobby.AutoStart = 30f;
            HostRoomChanged();
        }

        /// <summary>Host: the local match ended or was abandoned without going back to the room.</summary>
        public void HostMatchStopped() => matchLive = false;

        void OnHello(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte version);
            BabyLook look = NetWire.ReadLook(ref reader);
            int slot = Lobby.SlotOf(sender);
            if (version != NetWire.Version)
            {
                Manager.DisconnectClient(sender, "Your game and the host's are different versions. Update both!");
                return;
            }
            if (slot >= 0)
                looks[slot] = look;
            HostRoomChanged();
        }

        void OnInput(ulong sender, FastBufferReader reader)
        {
            int slot = Lobby.SlotOf(sender);
            reader.ReadValueSafe(out sbyte x);
            reader.ReadValueSafe(out sbyte y);
            reader.ReadValueSafe(out ushort actions);
            reader.ReadValueSafe(out ushort dashes);
            if (slot < 0)
                return;
            RemoteInput input = inputs[slot];
            input.Move = Vector2.ClampMagnitude(new Vector2(x / 127f, y / 127f), 1f);
            input.Actions = actions;
            input.Dashes = dashes;
            InputsReceived++;
        }

        void OnEmoteRequest(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte emote);
            int slot = Lobby.SlotOf(sender);
            if (slot < 0 || emote >= NetWire.Emotes.Length || Time.unscaledTime - inputs[slot].LastEmote < EmoteCooldown)
                return;
            inputs[slot].LastEmote = Time.unscaledTime;
            BroadcastEmote(slot, emote);
        }

        void BroadcastEmote(int slot, byte emote)
        {
            using (var writer = new FastBufferWriter(8, Allocator.Temp))
            {
                writer.WriteValueSafe((byte)slot);
                writer.WriteValueSafe(emote);
                SendToGuests(NetWire.Emote, writer, NetworkDelivery.ReliableSequenced);
            }
            ShowEmote(slot, emote);
        }

        void RefreshGuests()
        {
            guests.Clear();
            NetworkManager manager = Manager;
            if (manager == null || !manager.IsServer)
                return;
            foreach (ulong id in manager.ConnectedClientsIds)
            {
                if (id != NetworkManager.ServerClientId)
                    guests.Add(id);
            }
        }

        void SendToGuests(string message, FastBufferWriter writer, NetworkDelivery delivery)
        {
            RefreshGuests();
            if (guests.Count > 0 && Messages != null)
                Messages.SendNamedMessage(message, guests, writer, delivery);
        }

        // ------------------------------------------------------------------ host: match

        void Update()
        {
            if (NetRole.IsHost)
                HostUpdate();
            else if (NetRole.IsGuest)
                GuestUpdate();
            UpdateYouMarker();
        }

        void HostUpdate()
        {
            if (Lobby.AutoStart >= 0f && !matchLive)
            {
                float before = Lobby.AutoStart;
                Lobby.AutoStart = Mathf.Max(0f, Lobby.AutoStart - Time.unscaledDeltaTime);
                if (Lobby.AutoStart <= 0f)
                    HostStartMatch();
                else if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Lobby.AutoStart))
                    HostRoomChanged();
            }
            if (!matchLive)
                return;

            for (int slot = 0; slot < inputs.Length; slot++)
            {
                if (Lobby.Slots[slot] != SlotDriver.Online || lastSetup == null || !lastSetup.IsOnline(slot))
                    continue;
                BabyController baby = round.AllBabies[slot];
                RemoteInput input = inputs[slot];
                baby.SetMoveInput(input.Move);
                // One press per frame at most, like a real button.
                if (input.AppliedActions != input.Actions)
                {
                    input.AppliedActions++;
                    baby.PressAction();
                    RemotePressesApplied++;
                }
                if (input.AppliedDashes != input.Dashes)
                {
                    input.AppliedDashes++;
                    baby.PressDash();
                    RemotePressesApplied++;
                }
            }

            sendTimer -= Time.unscaledDeltaTime;
            if (sendTimer > 0f)
                return;
            sendTimer += SendInterval;
            if (sendTimer < 0f)
                sendTimer = 0f;
            RefreshGuests();
            if (guests.Count == 0)
            {
                pending.Clear();
                return;
            }
            SendSnapshot();
            FlushEvents();
        }

        /// <summary>Host: lets the room start by itself in a while (public rooms, so nobody waits for a sleeping host).</summary>
        public void HostScheduleAutoStart(float seconds)
        {
            if (!NetRole.IsHost)
                return;
            Lobby.AutoStart = seconds;
            HostRoomChanged();
        }

        void SendSnapshot()
        {
            var writer = new FastBufferWriter(1200, Allocator.Temp);
            try
            {
                WriteSnapshot(ref writer);
                SendToGuests(NetWire.Snapshot, writer, NetworkDelivery.UnreliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        void WriteSnapshot(ref FastBufferWriter writer)
        {
            writer.WriteValueSafe(matchId);
            writer.WriteValueSafe(++snapSeq);
            writer.WriteValueSafe(Time.unscaledTime);
            writer.WriteValueSafe(Time.time);

            round.ExportMirror(mirrorScratch);
            WriteRound(ref writer, mirrorScratch);

            IReadOnlyList<BabyController> babies = round.AllBabies;
            writer.WriteValueSafe((byte)babies.Count);
            foreach (BabyController baby in babies)
            {
                byte flags = (byte)((baby.gameObject.activeSelf ? 1 : 0) | (baby.IsActiveInRound ? 2 : 0));
                writer.WriteValueSafe(flags);
                writer.WriteValueSafe(baby.transform.position);
                writer.WriteValueSafe(NetWire.PackYaw(baby.transform.rotation));
                Vector3 velocity = baby.Movement.Velocity;
                writer.WriteValueSafe(NetWire.PackSpeed(velocity.x));
                writer.WriteValueSafe(NetWire.PackSpeed(velocity.z));
                writer.WriteValueSafe((byte)baby.Status.Activity);
                writer.WriteValueSafe((ushort)Mathf.RoundToInt(Mathf.Clamp01(baby.Cry.Normalized) * 65535f));
                writer.WriteValueSafe((byte)Mathf.Clamp(baby.ShieldHits, 0, 255));
            }

            writer.WriteValueSafe((byte)items.Count);
            foreach (Throwable item in items)
            {
                bool active = item != null && item.gameObject.activeSelf;
                bool visible = !(item is Toy toy) || toy.IsVisible;
                ItemState state = item != null ? item.State : ItemState.Loose;
                byte flags = (byte)((active ? 1 : 0) | ((int)state << 1) | (visible ? 8 : 0));
                writer.WriteValueSafe(flags);
                if (!active)
                    continue;
                writer.WriteValueSafe((sbyte)(item.Holder != null ? item.Holder.PlayerIndex : -1));
                writer.WriteValueSafe((sbyte)(item.Thrower != null ? item.Thrower.PlayerIndex : -1));
                if (state != ItemState.Held)
                {
                    writer.WriteValueSafe(item.transform.position);
                    writer.WriteValueSafe(item.transform.rotation);
                }
            }

            powerScratch.Clear();
            int powerCount = 0;
            foreach (PowerUpPickup pickup in powerUps.Live)
            {
                if (pickup != null)
                    powerCount++;
            }
            powerCount = Mathf.Min(powerCount, MaxPowerUps);
            writer.WriteValueSafe((byte)powerCount);
            int written = 0;
            foreach (PowerUpPickup pickup in powerUps.Live)
            {
                if (pickup == null || written >= powerCount)
                    continue;
                if (!powerIds.TryGetValue(pickup, out ushort id))
                {
                    id = nextPowerId++;
                    if (nextPowerId == 0)
                        nextPowerId = 1;
                    powerIds[pickup] = id;
                }
                writer.WriteValueSafe(id);
                writer.WriteValueSafe((byte)pickup.Kind);
                writer.WriteValueSafe(pickup.transform.position);
                written++;
            }
            if (powerIds.Count > 64)
                PrunePowerIds();

            writer.WriteValueSafe(bottle != null && bottle.isActiveAndEnabled ? (byte)bottle.Spot : NoSpot);
            RobotVacuum vacuum = RobotVacuum.Active;
            writer.WriteValueSafe(vacuum != null);
            if (vacuum != null)
            {
                writer.WriteValueSafe(vacuum.transform.position);
                writer.WriteValueSafe(NetWire.PackYaw(vacuum.transform.rotation));
            }
            writer.WriteValueSafe((byte)Mathf.Clamp(survival.Wave, 0, 255));
            writer.WriteValueSafe((ushort)Mathf.Clamp(survival.Knockouts, 0, ushort.MaxValue));
        }

        void PrunePowerIds()
        {
            var dead = new List<PowerUpPickup>();
            foreach (PowerUpPickup pickup in powerIds.Keys)
            {
                if (pickup == null)
                    dead.Add(pickup);
            }
            foreach (PowerUpPickup pickup in dead)
                powerIds.Remove(pickup);
        }

        static void WriteRound(ref FastBufferWriter writer, RoundMirror state)
        {
            writer.WriteValueSafe((byte)state.Phase);
            writer.WriteValueSafe((byte)Mathf.Clamp(state.RoundNumber, 0, 255));
            writer.WriteValueSafe(state.CountdownRemaining);
            writer.WriteValueSafe(state.CountdownDuration);
            writer.WriteValueSafe(state.TimeRemaining);
            byte flags = (byte)((state.HasTimeLimit ? 1 : 0) | (state.EndedOnTime ? 2 : 0) | (state.MatchOver ? 4 : 0) | (state.Endless ? 8 : 0));
            writer.WriteValueSafe(flags);
            writer.WriteValueSafe((sbyte)state.WinnerSide);
            writer.WriteValueSafe((sbyte)state.LoserIndex);
            writer.WriteValueSafe((sbyte)state.MatchWinnerSide);
            writer.WriteValueSafe((byte)Mathf.Clamp(state.RoundsToWin, 0, 255));
            for (int side = 0; side < RoundMirror.MaxSides; side++)
            {
                writer.WriteValueSafe((byte)Mathf.Clamp(state.Wins[side], 0, 255));
                writer.WriteValueSafe(state.Scores[side]);
            }
        }

        static void ReadRound(ref FastBufferReader reader, RoundMirror state)
        {
            reader.ReadValueSafe(out byte phase);
            state.Phase = phase <= (byte)RoundPhase.RoundOver ? (RoundPhase)phase : RoundPhase.Restarting;
            reader.ReadValueSafe(out byte number);
            state.RoundNumber = number;
            reader.ReadValueSafe(out state.CountdownRemaining);
            reader.ReadValueSafe(out state.CountdownDuration);
            reader.ReadValueSafe(out state.TimeRemaining);
            reader.ReadValueSafe(out byte flags);
            state.HasTimeLimit = (flags & 1) != 0;
            state.EndedOnTime = (flags & 2) != 0;
            state.MatchOver = (flags & 4) != 0;
            state.Endless = (flags & 8) != 0;
            reader.ReadValueSafe(out sbyte winner);
            reader.ReadValueSafe(out sbyte loser);
            reader.ReadValueSafe(out sbyte matchWinner);
            state.WinnerSide = winner;
            state.LoserIndex = loser;
            state.MatchWinnerSide = matchWinner;
            reader.ReadValueSafe(out byte rounds);
            state.RoundsToWin = rounds;
            for (int side = 0; side < RoundMirror.MaxSides; side++)
            {
                reader.ReadValueSafe(out byte wins);
                state.Wins[side] = wins;
                reader.ReadValueSafe(out state.Scores[side]);
            }
        }

        // Host: what happened, for the guests.

        void Subscribe(bool on)
        {
            if (subscribed == on)
                return;
            subscribed = on;
            foreach (BabyController baby in round.AllBabies)
            {
                baby.HitReceived -= OnHit;
                baby.ThrewItem -= OnThrew;
                baby.PickedUpItem -= OnPickedUp;
                baby.Dashed -= OnDashed;
                baby.Shoved -= OnShoved;
                baby.StartedCrying -= OnCried;
                baby.Celebrated -= OnCelebrated;
                baby.Giggled -= OnGiggled;
                baby.Dodged -= OnDodged;
                baby.ShieldBroken -= OnShieldBroken;
                baby.RoundReset -= OnRoundReset;
                if (!on)
                    continue;
                baby.HitReceived += OnHit;
                baby.ThrewItem += OnThrew;
                baby.PickedUpItem += OnPickedUp;
                baby.Dashed += OnDashed;
                baby.Shoved += OnShoved;
                baby.StartedCrying += OnCried;
                baby.Celebrated += OnCelebrated;
                baby.Giggled += OnGiggled;
                baby.Dodged += OnDodged;
                baby.ShieldBroken += OnShieldBroken;
                baby.RoundReset += OnRoundReset;
            }
            foreach (Throwable item in items)
            {
                item.HitBaby -= OnItemHit;
                if (on)
                    item.HitBaby += OnItemHit;
            }
            if (round.Toys != null)
            {
                round.Toys.Spawned -= OnToySpawned;
                if (on)
                    round.Toys.Spawned += OnToySpawned;
            }
            powerUps.Used -= OnPowerUp;
            if (on)
                powerUps.Used += OnPowerUp;
        }

        bool Recording => NetRole.IsHost && matchLive;

        void AddBabyEvent(BabyNetEvent kind, BabyController baby, BabyController other = null, Throwable item = null)
        {
            if (!Recording)
                return;
            pending.Add(new NetEvent
            {
                Type = EventType.Baby, Time = Time.unscaledTime, Kind = kind, A = (sbyte)baby.PlayerIndex,
                B = (sbyte)(other != null ? other.PlayerIndex : -1), Item = (sbyte)items.IndexOf(item),
            });
        }

        void OnHit(BabyController baby, BabyHitReport report)
        {
            if (!Recording)
                return;
            BabyController attacker = baby.LastAttacker;
            pending.Add(new NetEvent
            {
                Type = EventType.Baby, Time = Time.unscaledTime, Kind = BabyNetEvent.Hit, A = (sbyte)baby.PlayerIndex,
                B = (sbyte)(attacker != null ? attacker.PlayerIndex : -1), Item = -1,
                Flags = (byte)((report.WasProtected ? 1 : 0) | (report.WasDirectHit ? 2 : 0) | (report.Source == HitSource.Shove ? 4 : 0)),
                Point = report.Point, Direction = report.Direction, Cry = report.CryAdded, Knockback = report.KnockbackSpeed,
            });
        }

        void OnThrew(BabyController baby, Throwable item) => AddBabyEvent(BabyNetEvent.Threw, baby, null, item);
        void OnPickedUp(BabyController baby, Throwable item) => AddBabyEvent(BabyNetEvent.PickedUp, baby, null, item);
        void OnDashed(BabyController baby) => AddBabyEvent(BabyNetEvent.Dashed, baby);
        void OnShoved(BabyController baby, BabyController victim) => AddBabyEvent(BabyNetEvent.Shoved, baby, victim);
        void OnCried(BabyController baby) => AddBabyEvent(BabyNetEvent.Cried, baby, baby.LastAttacker);
        void OnCelebrated(BabyController baby) => AddBabyEvent(BabyNetEvent.Celebrated, baby);
        void OnGiggled(BabyController baby, BabyController joker) => AddBabyEvent(BabyNetEvent.Giggled, baby, joker);
        void OnDodged(BabyController baby) => AddBabyEvent(BabyNetEvent.Dodged, baby);
        void OnShieldBroken(BabyController baby) => AddBabyEvent(BabyNetEvent.ShieldBroken, baby);
        void OnRoundReset(BabyController baby) => AddBabyEvent(BabyNetEvent.RoundReset, baby);

        void OnItemHit(Throwable item, BabyController victim, bool direct)
        {
            if (!Recording)
                return;
            pending.Add(new NetEvent
            {
                Type = EventType.ItemHit, Time = Time.unscaledTime, Item = (sbyte)items.IndexOf(item),
                A = (sbyte)(item.Thrower != null ? item.Thrower.PlayerIndex : -1), B = (sbyte)victim.PlayerIndex, Flags = (byte)(direct ? 1 : 0),
            });
        }

        void OnToySpawned(Toy toy)
        {
            if (Recording)
                pending.Add(new NetEvent { Type = EventType.ToySpawned, Time = Time.unscaledTime, Item = (sbyte)items.IndexOf(toy), A = -1, B = -1 });
        }

        void OnPowerUp(BabyController baby, PowerUpKind kind)
        {
            if (!Recording)
                return;
            BabyController target = kind == PowerUpKind.BubbleWand ? powerUps.LastBubbleTarget : null;
            pending.Add(new NetEvent
            {
                Type = EventType.PowerUp, Time = Time.unscaledTime, Sub = (byte)kind, A = (sbyte)baby.PlayerIndex,
                B = (sbyte)(target != null ? target.PlayerIndex : -1), Item = -1,
            });
        }

        void FlushEvents()
        {
            if (pending.Count == 0)
                return;
            using var writer = new FastBufferWriter(256 + pending.Count * 48, Allocator.Temp, 65536);
            writer.WriteValueSafe(matchId);
            writer.WriteValueSafe((ushort)pending.Count);
            foreach (NetEvent e in pending)
            {
                writer.WriteValueSafe((byte)e.Type);
                writer.WriteValueSafe(e.Time);
                writer.WriteValueSafe((byte)e.Kind);
                writer.WriteValueSafe(e.A);
                writer.WriteValueSafe(e.B);
                writer.WriteValueSafe(e.Item);
                writer.WriteValueSafe(e.Sub);
                writer.WriteValueSafe(e.Flags);
                if (e.Type == EventType.Baby && e.Kind == BabyNetEvent.Hit)
                {
                    writer.WriteValueSafe(e.Point);
                    writer.WriteValueSafe(e.Direction);
                    writer.WriteValueSafe(e.Cry);
                    writer.WriteValueSafe(e.Knockback);
                }
            }
            pending.Clear();
            SendToGuests(NetWire.Events, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // ------------------------------------------------------------------ guest

        void GuestConnected(ulong clientId)
        {
            if (Manager == null || clientId != Manager.LocalClientId)
                return;
            var writer = new FastBufferWriter(128, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(NetWire.Version);
                NetWire.WriteLook(ref writer, PlayerProgress.Profile.LookFor(0));
                Messages.SendNamedMessage(NetWire.Hello, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        void OnLobby(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId)
                return;
            Lobby.Read(ref reader);
            if (!matchLive)
                LocalSlot = Lobby.SlotOf(Manager.LocalClientId);
            LobbyChanged?.Invoke();
        }

        void OnStart(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId)
                return;
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out sbyte slot);
            reader.ReadValueSafe(out byte mode);
            reader.ReadValueSafe(out byte room);
            var setup = new MatchSetup
            {
                Mode = Enum.IsDefined(typeof(GameMode), (int)mode) ? (GameMode)mode : GameMode.Clash,
                Room = Enum.IsDefined(typeof(RoomKind), (int)room) ? (RoomKind)room : RoomKind.Nursery,
            };
            reader.ReadValueSafe(out setup.PowerUps);
            reader.ReadValueSafe(out setup.Teams);
            var matchLooks = new BabyLook[MatchSetup.MaxPlayers];
            setup.Slots = new SlotDriver[MatchSetup.MaxPlayers];
            for (int i = 0; i < MatchSetup.MaxPlayers; i++)
            {
                reader.ReadValueSafe(out byte driver);
                setup.Slots[i] = Enum.IsDefined(typeof(SlotDriver), (int)driver) ? (SlotDriver)driver : SlotDriver.Off;
                matchLooks[i] = NetWire.ReadLook(ref reader);
            }
            // From this screen's point of view: our baby is the local human, every other person is online.
            for (int i = 0; i < setup.Slots.Length; i++)
            {
                if (i == slot)
                    setup.Slots[i] = SlotDriver.Human;
                else if (setup.Slots[i] == SlotDriver.Human)
                    setup.Slots[i] = SlotDriver.Online;
            }

            matchId = id;
            LocalSlot = slot;
            matchLive = true;
            snapCount = 0;
            newest = -1;
            incoming.Clear();
            clockSynced = false;
            foreach (PowerUpPickup pickup in mirroredPowers.Values)
            {
                if (pickup != null)
                    Destroy(pickup.gameObject);
            }
            mirroredPowers.Clear();
            localMove = Vector2.zero;
            flow.StartOnlineMatch(setup, matchLooks);
            BabyController mine = slot >= 0 && slot < round.AllBabies.Count ? round.AllBabies[slot] : null;
            if (mine != null)
                mine.CommandSink = this;
            SetYouMarker(mine);
        }

        void OnMenu(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId)
                return;
            matchLive = false;
            SetYouMarker(null);
            flow.OnlineBackToRoom();
        }

        void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !matchLive)
                return;
            reader.ReadValueSafe(out ushort id);
            if (id != matchId)
                return;
            int index = (newest + 1) % BufferSize;
            Snap snap = ring[index];
            reader.ReadValueSafe(out snap.Seq);
            if (newest >= 0 && snap.Seq <= ring[newest].Seq)
                return;
            reader.ReadValueSafe(out snap.Time);
            reader.ReadValueSafe(out snap.GameTime);
            ReadRound(ref reader, snap.Round);

            reader.ReadValueSafe(out byte babyCount);
            snap.BabyCount = Mathf.Min(babyCount, MatchSetup.MaxPlayers);
            for (int i = 0; i < babyCount; i++)
            {
                reader.ReadValueSafe(out byte flags);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out ushort yaw);
                reader.ReadValueSafe(out short vx);
                reader.ReadValueSafe(out short vz);
                reader.ReadValueSafe(out byte activity);
                reader.ReadValueSafe(out ushort cry);
                reader.ReadValueSafe(out byte shield);
                if (i >= MatchSetup.MaxPlayers)
                    continue;
                snap.Babies[i] = new BabySnap
                {
                    Active = (flags & 1) != 0, InRound = (flags & 2) != 0, Position = position, Rotation = NetWire.UnpackYaw(yaw),
                    Velocity = new Vector3(NetWire.UnpackSpeed(vx), 0f, NetWire.UnpackSpeed(vz)),
                    Activity = activity <= (byte)BabyActivity.Crying ? (BabyActivity)activity : BabyActivity.Ready,
                    Cry = cry / 65535f, Shield = shield,
                };
            }

            reader.ReadValueSafe(out byte itemCount);
            snap.ItemCount = Mathf.Min(itemCount, MaxItems);
            for (int i = 0; i < itemCount; i++)
            {
                reader.ReadValueSafe(out byte flags);
                var item = new ItemSnap
                {
                    Active = (flags & 1) != 0, State = (ItemState)((flags >> 1) & 3), Visible = (flags & 8) != 0, Holder = -1, Thrower = -1,
                    Rotation = Quaternion.identity,
                };
                if (item.Active)
                {
                    reader.ReadValueSafe(out sbyte holder);
                    reader.ReadValueSafe(out sbyte thrower);
                    item.Holder = holder;
                    item.Thrower = thrower;
                    if (item.State != ItemState.Held)
                    {
                        reader.ReadValueSafe(out item.Position);
                        reader.ReadValueSafe(out item.Rotation);
                    }
                }
                if (i < MaxItems)
                    snap.Items[i] = item;
            }

            reader.ReadValueSafe(out byte powerCount);
            snap.PowerCount = Mathf.Min(powerCount, MaxPowerUps);
            for (int i = 0; i < powerCount; i++)
            {
                reader.ReadValueSafe(out ushort powerId);
                reader.ReadValueSafe(out byte kind);
                reader.ReadValueSafe(out Vector3 position);
                if (i < MaxPowerUps)
                    snap.Powers[i] = new PowerSnap { Id = powerId, Kind = (PowerUpKind)Mathf.Min(kind, (int)PowerUpKind.Teddy), Position = position };
            }

            reader.ReadValueSafe(out byte spot);
            snap.BottleSpot = spot;
            reader.ReadValueSafe(out snap.Vacuum);
            if (snap.Vacuum)
            {
                reader.ReadValueSafe(out snap.VacuumPosition);
                reader.ReadValueSafe(out ushort vacuumYaw);
                snap.VacuumRotation = NetWire.UnpackYaw(vacuumYaw);
            }
            reader.ReadValueSafe(out byte wave);
            reader.ReadValueSafe(out ushort knockouts);
            snap.Wave = wave;
            snap.Knockouts = knockouts;

            newest = index;
            snapCount = Mathf.Min(snapCount + 1, BufferSize);
            SnapshotsReceived++;

            // The host's clock, as seen from here: latest arrival wins quickly, slow drift is smoothed.
            float offset = snap.Time - Time.unscaledTime;
            if (!clockSynced)
            {
                clockOffset = offset;
                clockSynced = true;
            }
            else
            {
                clockOffset = Mathf.Lerp(clockOffset, offset, offset > clockOffset ? 0.3f : 0.02f);
            }
        }

        void OnEvents(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !matchLive)
                return;
            reader.ReadValueSafe(out ushort id);
            if (id != matchId)
                return;
            reader.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                var e = new NetEvent();
                reader.ReadValueSafe(out byte type);
                e.Type = (EventType)type;
                reader.ReadValueSafe(out e.Time);
                reader.ReadValueSafe(out byte kind);
                e.Kind = (BabyNetEvent)kind;
                reader.ReadValueSafe(out e.A);
                reader.ReadValueSafe(out e.B);
                reader.ReadValueSafe(out e.Item);
                reader.ReadValueSafe(out e.Sub);
                reader.ReadValueSafe(out e.Flags);
                if (e.Type == EventType.Baby && e.Kind == BabyNetEvent.Hit)
                {
                    reader.ReadValueSafe(out e.Point);
                    reader.ReadValueSafe(out e.Direction);
                    reader.ReadValueSafe(out e.Cry);
                    reader.ReadValueSafe(out e.Knockback);
                }
                incoming.Add(e);
                EventsReceived++;
            }
        }

        void OnEmoteShown(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId)
                return;
            reader.ReadValueSafe(out byte slot);
            reader.ReadValueSafe(out byte emote);
            ShowEmote(slot, emote);
        }

        void GuestUpdate()
        {
            if (!matchLive)
                return;
            SendInput(false);
            if (newest < 0)
                return;

            float renderTime = Time.unscaledTime + clockOffset - InterpolationDelay;
            Snap a = null, b = null;
            for (int n = 0; n < snapCount; n++)
            {
                Snap s = ring[(newest - n + BufferSize) % BufferSize];
                if (s.Time <= renderTime)
                {
                    a = s;
                    break;
                }
                b = s;
            }
            if (a == null)
            {
                // Not enough history yet: show the oldest we have.
                a = b;
                b = null;
            }
            if (a == null)
                return;
            float t = b != null && b.Time > a.Time ? Mathf.Clamp01((renderTime - a.Time) / (b.Time - a.Time)) : 0f;
            NetClock.Sync(a.GameTime + Mathf.Max(0f, renderTime - a.Time));
            ApplySnapshot(a, b, t);

            // Events due by now, in order.
            int due = 0;
            while (due < incoming.Count && incoming[due].Time <= renderTime)
                due++;
            for (int i = 0; i < due; i++)
                ApplyEvent(incoming[i]);
            if (due > 0)
                incoming.RemoveRange(0, due);
        }

        void ApplySnapshot(Snap a, Snap b, float t)
        {
            round.ShowMirrored(a.Round);

            IReadOnlyList<BabyController> babies = round.AllBabies;
            for (int i = 0; i < a.BabyCount && i < babies.Count; i++)
            {
                BabyController baby = babies[i];
                BabySnap s = a.Babies[i];
                if (baby.gameObject.activeSelf != s.Active)
                    baby.gameObject.SetActive(s.Active);
                if (!s.Active)
                    continue;
                if (baby.IsActiveInRound != s.InRound)
                    baby.SetActiveInRound(s.InRound);
                baby.Status.SetMirrored(s.Activity);
                baby.Cry.SetMirrored(s.Cry * baby.Cry.Max);
                baby.ShieldHits = s.Shield;
                Vector3 position = s.Position;
                Quaternion rotation = s.Rotation;
                Vector3 velocity = s.Velocity;
                if (b != null && i < b.BabyCount && b.Babies[i].Active)
                {
                    BabySnap next = b.Babies[i];
                    if ((next.Position - s.Position).sqrMagnitude < TeleportDistance * TeleportDistance)
                    {
                        position = Vector3.Lerp(s.Position, next.Position, t);
                        rotation = Quaternion.Slerp(s.Rotation, next.Rotation, t);
                        velocity = Vector3.Lerp(s.Velocity, next.Velocity, t);
                    }
                }
                baby.Movement.ShowPose(position, rotation, velocity);
            }

            for (int i = 0; i < a.ItemCount && i < items.Count; i++)
            {
                Throwable item = items[i];
                ItemSnap s = a.Items[i];
                if (item.gameObject.activeSelf != s.Active)
                    item.gameObject.SetActive(s.Active);
                if (!s.Active)
                    continue;
                Vector3 position = s.Position;
                Quaternion rotation = s.Rotation;
                if (s.State != ItemState.Held && b != null && i < b.ItemCount && b.Items[i].Active && b.Items[i].State != ItemState.Held
                    && (b.Items[i].Position - s.Position).sqrMagnitude < TeleportDistance * TeleportDistance)
                {
                    position = Vector3.Lerp(s.Position, b.Items[i].Position, t);
                    rotation = Quaternion.Slerp(s.Rotation, b.Items[i].Rotation, t);
                }
                BabyController holder = s.Holder >= 0 && s.Holder < babies.Count ? babies[s.Holder] : null;
                BabyController thrower = s.Thrower >= 0 && s.Thrower < babies.Count ? babies[s.Thrower] : null;
                item.ShowRemote(s.State, holder, thrower, position, rotation);
                if (item is Toy toy && toy.IsVisible != s.Visible)
                    toy.ShowRemoteVisible(s.Visible);
            }

            ApplyPowerUps(a);

            if (bottle != null && bottle.isActiveAndEnabled && a.BottleSpot != NoSpot)
                bottle.ShowRemoteSpot(a.BottleSpot);
            RobotVacuum vacuum = RobotVacuum.Active;
            if (vacuum != null && a.Vacuum)
            {
                Vector3 position = a.VacuumPosition;
                Quaternion rotation = a.VacuumRotation;
                if (b != null && b.Vacuum)
                {
                    position = Vector3.Lerp(a.VacuumPosition, b.VacuumPosition, t);
                    rotation = Quaternion.Slerp(a.VacuumRotation, b.VacuumRotation, t);
                }
                vacuum.transform.SetPositionAndRotation(position, rotation);
            }
            if (round.Mode == GameMode.Survival)
                survival.ShowRemote(a.Wave, a.Knockouts);
        }

        void ApplyPowerUps(Snap a)
        {
            powerScratch.Clear();
            for (int i = 0; i < a.PowerCount; i++)
            {
                PowerSnap s = a.Powers[i];
                powerScratch.Add(s.Id);
                if (!mirroredPowers.TryGetValue(s.Id, out PowerUpPickup pickup) || pickup == null)
                {
                    pickup = powerUps.SpawnMirrored(s.Kind, s.Position);
                    mirroredPowers[s.Id] = pickup;
                }
                pickup.transform.position = s.Position;
            }
            if (mirroredPowers.Count == powerScratch.Count)
                return;
            var gone = new List<ushort>();
            foreach (KeyValuePair<ushort, PowerUpPickup> pair in mirroredPowers)
            {
                if (!powerScratch.Contains(pair.Key))
                    gone.Add(pair.Key);
            }
            foreach (ushort id in gone)
            {
                if (mirroredPowers[id] != null)
                    Destroy(mirroredPowers[id].gameObject);
                mirroredPowers.Remove(id);
            }
        }

        void ApplyEvent(NetEvent e)
        {
            IReadOnlyList<BabyController> babies = round.AllBabies;
            BabyController a = e.A >= 0 && e.A < babies.Count ? babies[e.A] : null;
            BabyController b = e.B >= 0 && e.B < babies.Count ? babies[e.B] : null;
            Throwable item = e.Item >= 0 && e.Item < items.Count ? items[e.Item] : null;
            switch (e.Type)
            {
                case EventType.Baby:
                    if (a == null)
                        return;
                    BabyHitReport report = default;
                    if (e.Kind == BabyNetEvent.Hit)
                    {
                        report = new BabyHitReport(e.Point, e.Direction, e.Cry, e.Knockback, (e.Flags & 1) != 0, (e.Flags & 2) != 0,
                            (e.Flags & 4) != 0 ? HitSource.Shove : HitSource.Thrown);
                    }
                    if ((e.Kind == BabyNetEvent.Threw || e.Kind == BabyNetEvent.PickedUp) && item == null)
                        return;
                    a.ShowRemote(e.Kind, b, item, report);
                    break;
                case EventType.ItemHit:
                    if (item != null && b != null)
                        item.ShowRemoteHit(a, b, (e.Flags & 1) != 0);
                    break;
                case EventType.ToySpawned:
                    if (item is Toy toy && round.Toys != null)
                        round.Toys.ShowRemoteSpawn(toy);
                    break;
                case EventType.PowerUp:
                    if (a != null)
                        powerUps.ShowRemoteUse((PowerUpKind)Mathf.Min(e.Sub, (int)PowerUpKind.Teddy), a, b);
                    break;
            }
        }

        // Guest: our own baby's controls.

        void IBabyCommandSink.Move(BabyController baby, Vector2 input)
        {
            localMove = input;
        }

        void IBabyCommandSink.Action(BabyController baby)
        {
            localActions++;
            SendInput(true);
        }

        void IBabyCommandSink.Dash(BabyController baby)
        {
            localDashes++;
            SendInput(true);
        }

        void SendInput(bool now)
        {
            if (!NetRole.IsGuest || LocalSlot < 0 || Messages == null || !Manager.IsConnectedClient)
                return;
            sendTimer -= now ? 0f : Time.unscaledDeltaTime;
            if (!now && sendTimer > 0f)
                return;
            sendTimer = SendInterval;
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(localMove.x, -1f, 1f) * 127f));
            writer.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(localMove.y, -1f, 1f) * 127f));
            writer.WriteValueSafe(localActions);
            writer.WriteValueSafe(localDashes);
            Messages.SendNamedMessage(NetWire.Input, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
        }

        // ------------------------------------------------------------------ emotes

        /// <summary>Shows a kid-safe emote above this player's baby, on every screen.</summary>
        public void SendEmote(int emote)
        {
            if (emote < 0 || emote >= NetWire.Emotes.Length || LocalSlot < 0 || Time.unscaledTime - lastLocalEmote < EmoteCooldown)
                return;
            lastLocalEmote = Time.unscaledTime;
            if (NetRole.IsHost)
            {
                BroadcastEmote(LocalSlot, (byte)emote);
            }
            else if (NetRole.IsGuest && Messages != null)
            {
                using var writer = new FastBufferWriter(4, Allocator.Temp);
                writer.WriteValueSafe((byte)emote);
                Messages.SendNamedMessage(NetWire.Emote, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>Raised when an emote is shown (slot, emote), for the room screen.</summary>
        public event Action<int, int> EmoteShown;

        void ShowEmote(int slot, int emote)
        {
            if (slot < 0 || slot >= round.AllBabies.Count || emote < 0 || emote >= NetWire.Emotes.Length)
                return;
            BabyController baby = round.AllBabies[slot];
            if (matchLive && baby.gameObject.activeInHierarchy)
                BabyEffects.On(baby).Say(NetWire.Emotes[emote], NetWire.EmoteColors[emote]);
            if (audioDirector != null)
                audioDirector.Play(SfxId.Pickup, 1.3f + emote * 0.1f, 0.6f);
            EmoteShown?.Invoke(slot, emote);
        }

        // ------------------------------------------------------------------ "YOU" marker

        void SetYouMarker(BabyController baby)
        {
            if (youMarker == null)
            {
                var go = new GameObject("YouMarker");
                youMarker = go.AddComponent<TextMesh>();
                youMarker.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                go.GetComponent<MeshRenderer>().sharedMaterial = youMarker.font.material;
                youMarker.fontSize = 64;
                youMarker.fontStyle = FontStyle.Bold;
                youMarker.characterSize = 0.07f;
                youMarker.anchor = TextAnchor.LowerCenter;
                youMarker.text = "YOU\nv";
                youMarker.lineSpacing = 0.8f;
            }
            markedBaby = baby;
            youMarker.gameObject.SetActive(baby != null);
            if (baby != null)
                youMarker.color = Color.Lerp(baby.PlayerColor, MenuKit.Ink, 0.35f);
        }

        BabyController markedBaby;

        void UpdateYouMarker()
        {
            if (youMarker == null)
                return;
            bool show = markedBaby != null && matchLive && NetRole.IsOnline && markedBaby.gameObject.activeInHierarchy;
            if (youMarker.gameObject.activeSelf != show)
                youMarker.gameObject.SetActive(show);
            if (!show)
                return;
            youMarker.transform.position = markedBaby.transform.position + Vector3.up * (2.05f + Mathf.Sin(Time.unscaledTime * 4f) * 0.06f);
            Camera view = Camera.main;
            if (view != null)
                youMarker.transform.rotation = view.transform.rotation;
        }
    }
}
