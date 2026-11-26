using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Connection to an online room. Rooms live on Unity's Lobby and Relay services (invite codes, quick match, no port
    /// forwarding); a direct IP connection is also available for testing and local networks. Players sign in
    /// anonymously: no accounts, names or chat.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public const int MaxPlayers = MatchSetup.MaxPlayers;

        NetworkManager manager;
        UnityTransport transport;
        ISession session;
        bool busy;
        bool leaving;
        bool active;

        public static NetSession Instance { get; private set; }

        public NetworkManager Manager => manager;

        /// <summary>Invite code friends type to join (empty for direct connections).</summary>
        public string Code { get; private set; } = "";

        /// <summary>Anyone can find this room with quick match.</summary>
        public bool IsPublic { get; private set; }

        public bool IsConnected => manager != null && manager.IsListening;

        /// <summary>True while signing in / creating / joining.</summary>
        public bool IsBusy => busy;

        /// <summary>Decides whether a guest may come in (host only). Return null to accept, or a reason to refuse.</summary>
        public Func<ulong, string> Admit;

        /// <summary>Became host or guest (the network is running).</summary>
        public event Action<NetMode> Started;

        /// <summary>Left the room, with a reason to show (empty when the player chose to leave).</summary>
        public event Action<string> Ended;

        /// <summary>Progress and errors for the menus.</summary>
        public event Action<string> StatusChanged;

        public static NetSession Create()
        {
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetSession>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            transport = gameObject.AddComponent<UnityTransport>();
            manager = gameObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                TickRate = 30,
                ConnectionData = new[] { NetWire.Version },
            };
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientDisconnectCallback += HandleDisconnect;
            manager.OnTransportFailure += () => End("The connection was lost.");
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            // The host's own connection.
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }
            if (request.Payload == null || request.Payload.Length < 1 || request.Payload[0] != NetWire.Version)
            {
                response.Approved = false;
                response.Reason = "Your game and the host's are different versions. Update both!";
                return;
            }
            string refusal = Admit?.Invoke(request.ClientNetworkId);
            response.Approved = refusal == null;
            response.Reason = refusal;
        }

        void HandleDisconnect(ulong clientId)
        {
            if (manager == null)
                return;
            // A guest losing its own connection (or being refused) ends its online game.
            if (!manager.IsServer && (clientId == manager.LocalClientId || clientId == NetworkManager.ServerClientId))
            {
                string reason = manager.DisconnectReason;
                End(string.IsNullOrEmpty(reason) ? "The host left the room." : reason);
            }
        }

        async Task<bool> SignIn()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    // Separate anonymous players when several copies run on one computer (testing).
                    string profile = CommandLineValue("-netProfile");
                    if (!string.IsNullOrEmpty(profile))
                        options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] Sign-in failed: " + e);
                Status("Couldn't reach the game servers. Check the internet connection and try again.");
                return false;
            }
        }

        /// <summary>Creates a room through Unity Relay + Lobby. Private rooms are only joinable with the code.</summary>
        public async void Host(bool isPrivate)
        {
            if (!Begin("Making a room..."))
                return;
            try
            {
                if (!await SignIn())
                    return;
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = isPrivate }.WithRelayNetwork();
                IHostSession created = await MultiplayerService.Instance.CreateSessionAsync(options);
                Attach(created);
                IsPublic = !isPrivate;
                OnStarted(NetMode.Host);
            }
            catch (Exception e)
            {
                Fail("Couldn't make a room.", e);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Joins a friend's room by its invite code.</summary>
        public async void Join(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                Status("Type the room code first.");
                return;
            }
            if (!Begin("Joining room " + code + "..."))
                return;
            try
            {
                if (!await SignIn())
                    return;
                ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Attach(joined);
                IsPublic = false;
                OnStarted(NetMode.Guest);
            }
            catch (SessionException e) when (e.Error == SessionError.SessionNotFound || e.Error == SessionError.InvalidParameter)
            {
                Fail("No room with that code. Check it and try again.", e);
            }
            catch (Exception e)
            {
                Fail("Couldn't join that room.", e);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Joins any public room with space, or opens a new public one.</summary>
        public async void QuickMatch()
        {
            if (!Begin("Looking for a room..."))
                return;
            try
            {
                if (!await SignIn())
                    return;
                var quick = new QuickJoinOptions { Timeout = TimeSpan.FromSeconds(4), CreateSession = true };
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = false }.WithRelayNetwork();
                ISession found = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                Attach(found);
                IsPublic = true;
                OnStarted(found.IsHost ? NetMode.Host : NetMode.Guest);
            }
            catch (Exception e)
            {
                Fail("Couldn't find a room.", e);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Hosts without Unity's services, on a port of this computer (tests and local networks).</summary>
        public void HostDirect(ushort port)
        {
            if (!Begin("Opening port " + port + "..."))
                return;
            busy = false;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            Code = "";
            IsPublic = false;
            if (manager.StartHost())
                OnStarted(NetMode.Host);
            else
                Fail("Couldn't open the port.", null);
        }

        /// <summary>Joins a direct host by address (tests and local networks).</summary>
        public void JoinDirect(string address, ushort port)
        {
            if (!Begin("Connecting to " + address + "..."))
                return;
            busy = false;
            transport.SetConnectionData(address, port);
            Code = "";
            IsPublic = false;
            if (manager.StartClient())
                OnStarted(NetMode.Guest);
            else
                Fail("Couldn't connect.", null);
        }

        /// <summary>Stops letting people find this room (public rooms close their doors once a match starts).</summary>
        public async void SetLocked(bool locked)
        {
            if (session == null || !session.IsHost)
                return;
            try
            {
                IHostSession host = session.AsHost();
                if (host.IsLocked == locked)
                    return;
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] Couldn't update the room: " + e.Message);
            }
        }

        /// <summary>Leaves the room (the room closes if this is the host).</summary>
        public void Leave() => End("");

        bool Begin(string status)
        {
            if (busy || IsConnected)
                return false;
            busy = true;
            leaving = false;
            Status(status);
            return true;
        }

        void Attach(ISession joined)
        {
            session = joined;
            Code = joined.Code ?? "";
            joined.RemovedFromSession += () => End("You left the room.");
            joined.Deleted += () => End("The room closed.");
        }

        void OnStarted(NetMode mode)
        {
            active = true;
            NetRole.Mode = mode;
            Status(mode == NetMode.Host ? "Room ready!" : "Joined!");
            Started?.Invoke(mode);
        }

        void Fail(string message, Exception e)
        {
            if (e != null)
                Debug.LogWarning($"[Online] {message} {e}");
            busy = false;
            Shutdown();
            Status(message);
        }

        async void End(string reason)
        {
            // Only once per room: the transport reports its own shutdown after we've already left.
            if (leaving || !active)
                return;
            active = false;
            leaving = true;
            reason = Friendly(reason);
            NetRole.Mode = NetMode.Offline;
            ISession old = session;
            session = null;
            Shutdown();
            Ended?.Invoke(reason ?? "");
            if (!string.IsNullOrEmpty(reason))
                Status(reason);
            if (old != null)
            {
                try
                {
                    if (old.IsHost)
                        await old.AsHost().DeleteAsync();
                    else
                        await old.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Online] Leaving the room: " + e.Message);
                }
            }
            leaving = false;
        }

        void Shutdown()
        {
            if (manager != null && (manager.IsListening || manager.ShutdownInProgress))
                manager.Shutdown();
            NetRole.Mode = NetMode.Offline;
            NetClock.Reset();
        }

        void Status(string text) => StatusChanged?.Invoke(text);

        /// <summary>Transport messages are for developers; players get plain words.</summary>
        static string Friendly(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return "";
            if (reason.IndexOf("host shutting down", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The host closed the room.";
            if (reason.StartsWith("[") || reason.IndexOf("Transport", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The connection was lost.";
            return reason;
        }

        void OnApplicationQuit()
        {
            if (session != null && session.IsHost)
                _ = session.AsHost().DeleteAsync();
        }

        public static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}
