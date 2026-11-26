using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// QA hook for online play: two copies of the game play an online match against each other on their own.
    /// <c>-netTest host -netOut DIR [-netDirect PORT | -netRelay] [-netGuests 1] [-netMode Clash] [-netRoom Kitchen] [-netCpus 0]</c>
    /// <c>-netTest join -netOut DIR [-netDirect IP:PORT | -netCodeFile FILE]</c>
    /// Logs what each side saw (snapshots, events, inputs, movement, round phases) to DIR/net.log and quits.
    /// </summary>
    public sealed class NetSmokeTest : MonoBehaviour
    {
        GameFlow flow;
        NetGame net;
        RoundManager round;
        string folder;
        bool host;
        float startTime;
        float maxSeconds = 120f;
        int hits, throws, cries, emotes, phases;
        readonly Dictionary<BabyController, Vector3> lastPosition = new Dictionary<BabyController, Vector3>();
        readonly Dictionary<BabyController, float> travelled = new Dictionary<BabyController, float>();
        bool matchSeen;
        bool matchOver;
        float nextAction;
        float nextDash;
        float strafe = 1f;
        int shots;
        int frames;

        public static void CreateIfRequested(GameFlow gameFlow, NetGame netGame, RoundManager manager)
        {
            string role = NetSession.CommandLineValue("-netTest");
            if (string.IsNullOrEmpty(role))
                return;
            NetSmokeTest test = new GameObject("NetSmokeTest").AddComponent<NetSmokeTest>();
            test.flow = gameFlow;
            test.net = netGame;
            test.round = manager;
            test.host = role.Equals("host", StringComparison.OrdinalIgnoreCase);
            test.folder = NetSession.CommandLineValue("-netOut") ?? Application.persistentDataPath;
            string seconds = NetSession.CommandLineValue("-netSeconds");
            if (seconds != null && float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                test.maxSeconds = value;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            startTime = Time.realtimeSinceStartup;
            Log($"Net test as {(host ? "HOST" : "GUEST")}. Unity {Application.unityVersion}");
            NetSession session = NetSession.Instance;
            session.StatusChanged += text => Log("status: " + text);
            session.Ended += reason => Log("session ended: " + (string.IsNullOrEmpty(reason) ? "(left)" : reason));
            net.EmoteShown += (slot, emote) =>
            {
                emotes++;
                Log($"emote shown: baby {slot + 1} says {NetWire.Emotes[emote]}");
            };
            round.PhaseChanged += phase =>
            {
                phases++;
                Log($"round {round.RoundNumber}: {phase}");
            };
            round.MatchEnded += winner =>
            {
                matchOver = true;
                Log($"match over, winner {(winner != null ? winner.DisplayName : "none")}");
            };
            foreach (BabyController baby in round.AllBabies)
            {
                baby.HitReceived += (b, r) => hits++;
                baby.ThrewItem += (b, i) => throws++;
                baby.StartedCrying += b => cries++;
            }

            while (!flow.IsReady)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);

            string direct = NetSession.CommandLineValue("-netDirect");
            if (host)
            {
                if (direct != null && ushort.TryParse(direct, out ushort port))
                    session.HostDirect(port);
                else
                    session.Host(true);
                float wait = Time.realtimeSinceStartup + 40f;
                while (!NetRole.IsHost && Time.realtimeSinceStartup < wait)
                    yield return null;
                if (!NetRole.IsHost)
                {
                    Log("FAILED: could not host.");
                    Finish();
                    yield break;
                }
                Log($"hosting. code '{session.Code}'");
                if (!string.IsNullOrEmpty(session.Code))
                    File.WriteAllText(Path.Combine(folder, "code.txt"), session.Code);
                Shot("host_room_empty");

                int wanted = int.TryParse(NetSession.CommandLineValue("-netGuests"), out int guests) ? guests : 1;
                wait = Time.realtimeSinceStartup + 90f;
                while (net.Lobby.People < 1 + wanted && Time.realtimeSinceStartup < wait)
                    yield return null;
                Log($"room has {net.Lobby.People} people");
                if (net.Lobby.People < 1 + wanted)
                {
                    Log("FAILED: guests never arrived.");
                    Finish();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1.5f);
                Shot("host_room_full");
                net.Lobby.Mode = Parse(NetSession.CommandLineValue("-netMode"), GameMode.Clash);
                net.Lobby.Room = Parse(NetSession.CommandLineValue("-netRoom"), RoomKind.Kitchen);
                net.Lobby.PowerUps = true;
                int cpus = int.TryParse(NetSession.CommandLineValue("-netCpus"), out int c) ? c : 0;
                for (int i = 0; i < net.Lobby.Slots.Length && cpus > 0; i++)
                {
                    if (net.Lobby.Slots[i] == SlotDriver.Off)
                    {
                        net.Lobby.Slots[i] = SlotDriver.CpuNormal;
                        cpus--;
                    }
                }
                net.HostRoomChanged();
                yield return new WaitForSecondsRealtime(1f);
                net.HostStartMatch();
                Log($"started {net.Lobby.Mode} in {net.Lobby.Room}");
                // Nobody is at the host's keyboard: its baby plays as a CPU.
                BabyController mine = round.AllBabies[0];
                if (mine.TryGetComponent(out BabyInputHandler input))
                    input.enabled = false;
                if (mine.TryGetComponent(out BabyBrain brain))
                {
                    brain.Difficulty = CpuDifficulty.Normal;
                    brain.enabled = true;
                }
            }
            else
            {
                string file = NetSession.CommandLineValue("-netCodeFile");
                if (direct != null && direct.Contains(":"))
                {
                    string[] parts = direct.Split(':');
                    session.JoinDirect(parts[0], ushort.Parse(parts[1]));
                }
                else if (file != null)
                {
                    float wait = Time.realtimeSinceStartup + 60f;
                    while (!File.Exists(file) && Time.realtimeSinceStartup < wait)
                        yield return null;
                    string code = File.Exists(file) ? File.ReadAllText(file).Trim() : "";
                    Log($"joining with code '{code}'");
                    session.Join(code);
                }
                float joinWait = Time.realtimeSinceStartup + 40f;
                while (!NetRole.IsGuest && Time.realtimeSinceStartup < joinWait)
                    yield return null;
                if (!NetRole.IsGuest)
                {
                    Log("FAILED: could not join.");
                    Finish();
                    yield break;
                }
                Log("joined as guest");
                float slotWait = Time.realtimeSinceStartup + 20f;
                while (net.LocalSlot < 0 && Time.realtimeSinceStartup < slotWait)
                    yield return null;
                Log($"my slot: baby {net.LocalSlot + 1}");
                yield return new WaitForSecondsRealtime(1f);
                Shot("guest_room");
            }
        }

        void Update()
        {
            frames++;
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (net.InMatch && !matchSeen)
            {
                matchSeen = true;
                Log("match running on this screen");
                lastPosition.Clear();
                travelled.Clear();
                nextShot = elapsed + 6f;
            }
            if (net.InMatch)
            {
                Track();
                if (!host)
                    Drive(elapsed);
                if (elapsed >= nextShot && shots < 4)
                {
                    Shot((host ? "host_match_" : "guest_match_") + shots);
                    nextShot = elapsed + 9f;
                }
                if (elapsed >= nextEmote && emotesSent < 2)
                {
                    net.SendEmote(host ? 3 : 0);
                    emotesSent++;
                    nextEmote = elapsed + 7f;
                }
            }
            if (elapsed >= nextReport)
            {
                nextReport = elapsed + 5f;
                Report(elapsed);
            }
            if (elapsed >= maxSeconds || (matchOver && elapsed - matchOverAt > 4f))
                Finish();
            if (matchOver && matchOverAt < 0f)
                matchOverAt = elapsed;
        }

        int reportFrames;
        float reportTime;
        float nextShot = 1e9f;
        float nextReport = 5f;
        float nextEmote = 12f;
        int emotesSent;
        float matchOverAt = -1f;

        void Track()
        {
            foreach (BabyController baby in round.AllBabies)
            {
                if (!baby.gameObject.activeInHierarchy)
                    continue;
                Vector3 now = baby.transform.position;
                if (lastPosition.TryGetValue(baby, out Vector3 before))
                    travelled[baby] = (travelled.TryGetValue(baby, out float sum) ? sum : 0f) + Vector3.Distance(before, now);
                lastPosition[baby] = now;
            }
        }

        /// <summary>A simple stand-in player for the guest: chase someone, throw often, dash sometimes.</summary>
        void Drive(float elapsed)
        {
            if (net.LocalSlot < 0)
                return;
            BabyController mine = round.AllBabies[net.LocalSlot];
            BabyController target = null;
            float best = float.MaxValue;
            foreach (BabyController other in round.AllBabies)
            {
                if (other == mine || !other.gameObject.activeInHierarchy || other.IsCrying)
                    continue;
                float distance = (other.transform.position - mine.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = other;
                }
            }
            Vector3 to = target != null ? target.transform.position - mine.transform.position : -mine.transform.position;
            to.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * strafe;
            Vector3 wish = to.magnitude > 3f ? to.normalized : (to.normalized * 0.4f + side).normalized;
            mine.SetMoveInput(new Vector2(wish.x, wish.z));
            if (elapsed >= nextAction)
            {
                nextAction = elapsed + 0.55f;
                mine.PressAction();
            }
            if (elapsed >= nextDash)
            {
                nextDash = elapsed + 3.1f;
                strafe = -strafe;
                mine.PressDash();
            }
        }

        void Report(float elapsed)
        {
            string babies = "";
            foreach (BabyController baby in round.AllBabies)
            {
                if (!baby.gameObject.activeInHierarchy)
                    continue;
                float moved = travelled.TryGetValue(baby, out float m) ? m : 0f;
                babies += $" | {baby.DisplayName}: moved {moved:0.0}m cry {baby.Cry.Value:0} {baby.Status.Activity}{(baby.IsPuppet ? " (puppet)" : "")}";
            }
            string scores = "";
            if (round.Setup != null)
            {
                foreach (BabyController baby in round.Babies)
                    scores += $" {baby.DisplayName}={(round.UsesScore ? round.GetScore(baby).ToString("0.0") : round.GetWins(baby).ToString())}";
            }
            float fps = (frames - reportFrames) / Mathf.Max(0.01f, elapsed - reportTime);
            reportFrames = frames;
            reportTime = elapsed;
            Log($"t={elapsed:0}s {round.Phase} r{round.RoundNumber} time {round.TimeRemaining:0} | snaps {net.SnapshotsReceived} events {net.EventsReceived} " +
                $"inputs {net.InputsReceived} presses {net.RemotePressesApplied} | seen: throws {throws} hits {hits} cries {cries} phases {phases} emotes {emotes} | " +
                $"scores{scores} | {fps:0} fps{babies}");
        }

        void Shot(string name)
        {
            shots++;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Log("screenshot " + name);
        }

        bool finished;

        void Finish()
        {
            if (finished)
                return;
            finished = true;
            Report(Time.realtimeSinceStartup - startTime);
            Log("Done.");
            StartCoroutine(QuitSoon());
        }

        IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            NetSession.Instance.Leave();
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        static T Parse<T>(string value, T fallback) where T : struct =>
            value != null && Enum.TryParse(value, true, out T parsed) ? parsed : fallback;

        void Log(string line)
        {
            Debug.Log("[NetTest] " + line);
            File.AppendAllText(Path.Combine(folder, "net.log"), $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
    }
}
