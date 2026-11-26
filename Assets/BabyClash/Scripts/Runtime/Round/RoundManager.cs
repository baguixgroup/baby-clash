using System;
using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Runs matches: rounds with countdown, time limit and result, resets babies/diapers/toys between rounds, and
    /// tallies round wins (first to N wins the match). Knows nothing about UI or input; menus drive it through
    /// <see cref="StartMatch"/> / <see cref="StopMatch"/> and listen to its events.
    /// </summary>
    public sealed class RoundManager : MonoBehaviour
    {
        const float TimeUpTieTolerance = 0.5f;

        [SerializeField] MatchConfig config;

        [Tooltip("Babies in player order. Baby i spawns at Spawn Point i and starts wearing Diaper i.")]
        [SerializeField] BabyController[] babies = Array.Empty<BabyController>();

        [SerializeField] Transform[] spawnPoints = Array.Empty<Transform>();

        [Tooltip("Every diaper in the match. They persist for the whole session and are re-handed each round.")]
        [SerializeField] Diaper[] diapers = Array.Empty<Diaper>();

        [Tooltip("Optional. Provides the neutral drop point for diapers no baby has room for.")]
        [SerializeField] ArenaBounds arena;

        [Tooltip("Optional. Drops toys during play.")]
        [SerializeField] ToySpawner toys;

        [Tooltip("Start a match by itself on Start (tests, sandbox scenes). In the game, the menus start matches.")]
        [SerializeField] bool autoStart = true;

        RoundStateMachine round;
        MatchStateMachine match;
        ScoreBoard scores;
        MatchSetup setup;
        BabyController[] roster;
        int[] sides = Array.Empty<int>();
        float[] cryScratch = Array.Empty<float>();
        readonly Dictionary<BabyController, float> reviveTimers = new Dictionary<BabyController, float>();
        readonly List<BabyController> reviveScratch = new List<BabyController>(4);
        bool running;
        bool drawnMatch;

        const float ReviveDelay = 2.6f;

        /// <summary>Babies taking part in the current match.</summary>
        public IReadOnlyList<BabyController> Babies => babies;

        /// <summary>Every baby in the scene, playing or not.</summary>
        public IReadOnlyList<BabyController> AllBabies => roster ?? babies;

        /// <summary>The current match's rules, or null for a plain duel.</summary>
        public MatchSetup Setup => setup;
        public GameMode Mode => setup != null ? setup.Mode : GameMode.Clash;
        public bool UsesScore => setup != null && setup.UsesScore;
        public ScoreBoard Scores => scores;
        public Transform SpawnOf(BabyController baby) => SpawnFor(Array.IndexOf(babies, baby), baby);
        public int WinningSide => round != null ? round.WinnerIndex : RoundStateMachine.NoPlayer;
        public int MatchWinningSide => match != null ? match.WinnerIndex : MatchStateMachine.NoPlayer;
        public IReadOnlyList<Diaper> Diapers => diapers;
        public ToySpawner Toys => toys;
        public MatchConfig Config => config;
        public bool IsRunning => running;
        public bool IsEndless => match != null && match.IsEndless;
        public int RoundsToWin => match != null ? match.RoundsToWin : 0;
        public RoundPhase Phase => round != null ? round.Phase : RoundPhase.Restarting;
        public int RoundNumber => round != null ? round.RoundNumber : 0;
        public float CountdownRemaining => round != null ? round.CountdownRemaining : 0f;
        public float CountdownDuration => round != null ? round.CountdownDuration : 0f;
        public bool HasTimeLimit => round != null && round.HasTimeLimit;
        public float TimeRemaining => round != null ? round.TimeRemaining : 0f;
        public bool RoundEndedOnTime => round != null && round.EndedOnTime;
        public BabyController RoundWinner => FirstOnSide(round != null ? round.WinnerIndex : RoundStateMachine.NoPlayer);
        public BabyController RoundLoser => BabyAt(round != null ? round.LoserIndex : RoundStateMachine.NoPlayer);
        public bool IsMatchOver => drawnMatch || (match != null && match.IsOver);
        public BabyController MatchWinner => FirstOnSide(match != null ? match.WinnerIndex : MatchStateMachine.NoPlayer);

        public event Action<RoundPhase> PhaseChanged;
        public event Action MatchStarted;

        /// <summary>A baby won the match (not raised in endless matches).</summary>
        public event Action<BabyController> MatchEnded;

        /// <summary>Scene wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(MatchConfig matchConfig, BabyController[] matchBabies, Transform[] spawns,
            Diaper[] matchDiapers, ArenaBounds arenaBounds, ToySpawner toySpawner, bool startAutomatically)
        {
            config = matchConfig;
            babies = matchBabies;
            spawnPoints = spawns;
            diapers = matchDiapers;
            arena = arenaBounds;
            toys = toySpawner;
            autoStart = startAutomatically;
        }

        public int GetWins(BabyController baby) => match != null ? match.GetWins(SideOf(baby)) : 0;

        public int SideOf(BabyController baby)
        {
            int index = IndexOf(baby);
            return index >= 0 && index < sides.Length ? sides[index] : index;
        }

        public float GetScore(BabyController baby) => scores != null ? scores.Get(SideOf(baby)) : 0f;

        public void AddScore(BabyController baby, float amount)
        {
            if (baby != null && scores != null && Phase == RoundPhase.Playing)
                scores.Add(SideOf(baby), amount);
        }

        /// <summary>Ends the running round now (modes with their own ending).</summary>
        public void FinishRound(int winningSide, bool onTime = false) => round.EndRound(winningSide, onTime);

        /// <summary>Replaces the full cast (extra players created at load). Call while no match is running.</summary>
        public void SetRoster(BabyController[] allBabies, Transform[] spawns, Diaper[] allDiapers)
        {
            Subscribe(false);
            roster = allBabies;
            babies = allBabies;
            spawnPoints = spawns;
            diapers = allDiapers;
            Subscribe(isActiveAndEnabled);
            IgnoreItemCollisions();
        }

        void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"{name}: no MatchConfig assigned, using defaults.", this);
                config = ScriptableObject.CreateInstance<MatchConfig>();
            }
            if (babies.Length == 0)
                Debug.LogError($"{name}: no babies assigned.", this);
            if (spawnPoints.Length < babies.Length)
                Debug.LogError($"{name}: needs one spawn point per baby.", this);

            CreateMachines(null, config.roundTimeLimit, config.roundsToWin);
        }

        void CreateMachines(int[] sideOfPlayer, float timeLimit, int roundsToWin)
        {
            if (round != null)
            {
                round.PhaseChanged -= HandlePhaseChanged;
                round.TimeExpired -= HandleTimeExpired;
            }
            int players = Mathf.Max(1, babies.Length);
            cryScratch = new float[players];
            sides = new int[players];
            for (int i = 0; i < players; i++)
                sides[i] = sideOfPlayer != null ? sideOfPlayer[i] : i;
            round = new RoundStateMachine(players, config.countdownDuration, timeLimit, sideOfPlayer);
            round.PhaseChanged += HandlePhaseChanged;
            round.TimeExpired += HandleTimeExpired;
            match = new MatchStateMachine(Mathf.Max(2, round.SideCount), roundsToWin);
        }

        void OnEnable() => Subscribe(true);

        void OnDisable() => Subscribe(false);

        void Subscribe(bool on)
        {
            foreach (BabyController baby in AllBabies)
            {
                if (baby == null)
                    continue;
                baby.StartedCrying -= HandleBabyStartedCrying;
                baby.Giggled -= HandleGiggled;
                if (!on)
                    continue;
                baby.StartedCrying += HandleBabyStartedCrying;
                baby.Giggled += HandleGiggled;
            }
        }

        void Start()
        {
            IgnoreItemCollisions();
            if (autoStart)
                StartMatch(false);
        }

        void IgnoreItemCollisions()
        {
            var babyColliders = new List<Collider>(AllBabies.Count);
            foreach (BabyController baby in AllBabies)
                babyColliders.Add(baby.BodyCollider);
            foreach (Diaper diaper in diapers)
                diaper.IgnoreCollisionsWith(babyColliders);
            if (toys != null)
            {
                foreach (Toy toy in toys.Pool)
                    toy.IgnoreCollisionsWith(babyColliders);
            }
        }

        /// <summary>Starts a match with its own cast and rules.</summary>
        public void StartMatch(MatchSetup matchSetup)
        {
            setup = matchSetup;
            BabyController[] all = roster ?? babies;
            var playing = new List<BabyController>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                bool plays = setup.IsPlaying(i);
                // Left over from the previous match.
                all[i].Team = -1;
                all[i].SetActiveInRound(false);
                all[i].gameObject.SetActive(plays);
                if (plays)
                    playing.Add(all[i]);
            }
            babies = playing.ToArray();
            roster = all;

            int[] sideOfPlayer = setup.BuildSides();
            float limit = setup.TimeLimit < 0f ? config.roundTimeLimit : setup.TimeLimit;
            int wins = setup.Endless ? 0 : setup.RoundsToWin > 0 ? setup.RoundsToWin : config.roundsToWin;
            CreateMachines(sideOfPlayer, limit, wins);
            scores = setup.UsesScore ? new ScoreBoard(Mathf.Max(2, round.SideCount), setup.ScoreTarget) : null;
            if (scores != null)
                scores.TargetReached += side => round.EndRound(side, false);
            bool teams = setup.Teams || setup.Mode == GameMode.Survival;
            for (int i = 0; i < babies.Length; i++)
            {
                BabyController baby = babies[i];
                bool boss = setup.BossSlot == baby.PlayerIndex;
                baby.Team = teams ? sides[i] : -1;
                baby.GigglesInsteadOfCrying = setup.IsToddler;
                baby.FullAimAssist = setup.IsToddler;
                // Survival: the player is sturdier and the horde goes down fast. Toddlers giggle after a couple of hits.
                bool horde = setup.Mode == GameMode.Survival && sides[i] == 1;
                baby.CryTakenScale = boss ? 0.6f : setup.IsToddler ? 13f : horde ? 2.6f : setup.Mode == GameMode.Survival ? 0.7f : 1f;
                baby.transform.localScale = Vector3.one * (boss ? 1.45f : 1f);
            }
            Begin();
        }

        void Begin()
        {
            running = true;
            drawnMatch = false;
            reviveTimers.Clear();
            round.ResetRoundNumber();
            MatchStarted?.Invoke();
            round.StartNewRound();
        }

        /// <summary>Starts a fresh match from round 1. Endless matches never end (attract mode).</summary>
        public void StartMatch(bool endless)
        {
            setup = null;
            scores = null;
            CreateMachines(null, config.roundTimeLimit, endless ? 0 : config.roundsToWin);
            Begin();
        }

        /// <summary>Freezes everything (back to the menus).</summary>
        public void StopMatch()
        {
            running = false;
            SetBabiesActive(false);
            if (toys != null)
                toys.SetRunning(false);
        }

        void Update()
        {
            if (!running)
                return;

            round.Tick(Time.deltaTime);
            UpdateRevives();
            if (round.Phase == RoundPhase.RoundOver && !IsMatchOver && round.ResultElapsed >= config.roundResultDuration)
                round.StartNewRound();
        }

        void HandlePhaseChanged(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Restarting:
                    ResetWorld();
                    break;
                case RoundPhase.Starting:
                    SetBabiesActive(false);
                    break;
                case RoundPhase.Playing:
                    SetBabiesActive(true);
                    if (toys != null)
                        toys.SetRunning(true);
                    break;
                case RoundPhase.RoundOver:
                    SetBabiesActive(false);
                    if (toys != null)
                        toys.SetRunning(false);
                    for (int i = 0; i < babies.Length; i++)
                    {
                        bool won = sides[i] == round.WinnerIndex;
                        if (Mode == GameMode.TinyTots || (won && !babies[i].IsCrying))
                            babies[i].Celebrate();
                    }
                    bool matchOver = match.RecordRound(round.WinnerIndex);
                    // A tied score match has no rematch round: it's simply a draw.
                    if (!matchOver && scores != null && !match.IsEndless)
                        matchOver = drawnMatch = true;
                    PhaseChanged?.Invoke(phase);
                    if (matchOver)
                        MatchEnded?.Invoke(MatchWinner);
                    return;
            }
            PhaseChanged?.Invoke(phase);
        }

        void HandleTimeExpired()
        {
            if (scores != null)
            {
                round.EndRound(scores.Leader(0.5f), true);
                return;
            }
            for (int i = 0; i < babies.Length; i++)
                cryScratch[i] = babies[i].Cry.Value;
            int winner = RoundRules.CalmestSide(cryScratch, sides, round.SideCount, TimeUpTieTolerance);
            round.EndRound(winner, true);

            // Losing the argument on points still ends in tears (after the round is decided, so this isn't a KO).
            for (int i = 0; i < babies.Length; i++)
            {
                if (winner != RoundStateMachine.NoPlayer && sides[i] != winner)
                    babies[i].BurstIntoTears();
            }
        }

        void HandleBabyStartedCrying(BabyController baby)
        {
            BabyCried?.Invoke(baby);
            if (setup == null || setup.Eliminates)
                round.ReportCrying(IndexOf(baby));
            else if (setup.Mode == GameMode.KingOfTheBottle && round.Phase == RoundPhase.Playing)
                reviveTimers[baby] = ReviveDelay;
        }

        /// <summary>A baby burst into tears, in any mode.</summary>
        public event Action<BabyController> BabyCried;

        void HandleGiggled(BabyController baby, BabyController joker)
        {
            if (Mode == GameMode.TinyTots)
                AddScore(joker != null ? joker : baby, 1f);
        }

        void UpdateRevives()
        {
            if (reviveTimers.Count == 0)
                return;
            reviveScratch.Clear();
            reviveScratch.AddRange(reviveTimers.Keys);
            foreach (BabyController baby in reviveScratch)
            {
                float left = reviveTimers[baby] - Time.deltaTime;
                reviveTimers[baby] = left;
                if (left > 0f)
                    continue;
                reviveTimers.Remove(baby);
                if (round.Phase != RoundPhase.Playing)
                    continue;
                Transform spawn = SpawnOf(baby);
                baby.Revive(spawn.position, spawn.rotation);
            }
        }

        Transform SpawnFor(int index, BabyController baby)
        {
            // With the whole cast in the scene, spawn points belong to slots; otherwise to the order given.
            int at = roster != null && baby != null ? baby.PlayerIndex : index;
            return at >= 0 && at < spawnPoints.Length ? spawnPoints[at] : baby != null ? baby.transform : transform;
        }

        BabyController FirstOnSide(int side)
        {
            BabyController first = null;
            for (int i = 0; side >= 0 && i < babies.Length; i++)
            {
                if (sides[i] != side)
                    continue;
                if (!babies[i].IsCrying)
                    return babies[i];
                if (first == null)
                    first = babies[i];
            }
            return first;
        }

        void ResetWorld()
        {
            for (int i = 0; i < babies.Length; i++)
            {
                Transform spawn = SpawnFor(i, babies[i]);
                babies[i].ResetForRound(spawn.position, spawn.rotation);
            }

            if (toys != null)
                toys.ResetForRound();

            // Babies were emptied above, so every diaper is handed out fresh: diaper i to baby i.
            reviveTimers.Clear();
            if (scores != null)
                scores.Reset();
            if (roster != null && setup != null)
            {
                // One diaper per playing baby; the others' stay out of sight.
                for (int i = 0; i < diapers.Length; i++)
                {
                    bool used = i < roster.Length && roster[i].gameObject.activeSelf;
                    if (used)
                    {
                        diapers[i].gameObject.SetActive(true);
                        roster[i].Diapers.ForceEquip(diapers[i]);
                    }
                    else
                    {
                        diapers[i].RecoverTo(new Vector3(0f, -2f, 0f));
                        diapers[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            for (int i = 0; i < diapers.Length; i++)
            {
                BabyController owner = babies.Length > 0 ? babies[i % babies.Length] : null;
                if (owner == null || !owner.Diapers.ForceEquip(diapers[i]))
                    diapers[i].RecoverTo(arena != null ? arena.DiaperRecoveryPosition : transform.position);
            }
        }

        void SetBabiesActive(bool active)
        {
            foreach (BabyController baby in babies)
                baby.SetActiveInRound(active);
        }

        int IndexOf(BabyController baby) => Array.IndexOf(babies, baby);

        BabyController BabyAt(int index) => index >= 0 && index < babies.Length ? babies[index] : null;
    }
}
