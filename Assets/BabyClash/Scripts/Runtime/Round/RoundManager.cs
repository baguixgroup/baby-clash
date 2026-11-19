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
        float[] cryScratch = Array.Empty<float>();
        bool running;

        public IReadOnlyList<BabyController> Babies => babies;
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
        public BabyController RoundWinner => BabyAt(round != null ? round.WinnerIndex : RoundStateMachine.NoPlayer);
        public BabyController RoundLoser => BabyAt(round != null ? round.LoserIndex : RoundStateMachine.NoPlayer);
        public bool IsMatchOver => match != null && match.IsOver;
        public BabyController MatchWinner => BabyAt(match != null ? match.WinnerIndex : MatchStateMachine.NoPlayer);

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

        public int GetWins(BabyController baby) => match != null ? match.GetWins(IndexOf(baby)) : 0;

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

            int players = Mathf.Max(1, babies.Length);
            cryScratch = new float[players];
            round = new RoundStateMachine(players, config.countdownDuration, config.roundTimeLimit);
            round.PhaseChanged += HandlePhaseChanged;
            round.TimeExpired += HandleTimeExpired;
            match = new MatchStateMachine(players, config.roundsToWin);
        }

        void OnEnable()
        {
            foreach (BabyController baby in babies)
            {
                if (baby != null)
                    baby.StartedCrying += HandleBabyStartedCrying;
            }
        }

        void OnDisable()
        {
            foreach (BabyController baby in babies)
            {
                if (baby != null)
                    baby.StartedCrying -= HandleBabyStartedCrying;
            }
        }

        void Start()
        {
            var babyColliders = new List<Collider>(babies.Length);
            foreach (BabyController baby in babies)
                babyColliders.Add(baby.BodyCollider);
            foreach (Diaper diaper in diapers)
                diaper.IgnoreCollisionsWith(babyColliders);
            if (toys != null)
            {
                foreach (Toy toy in toys.Pool)
                    toy.IgnoreCollisionsWith(babyColliders);
            }

            if (autoStart)
                StartMatch(false);
        }

        /// <summary>Starts a fresh match from round 1. Endless matches never end (attract mode).</summary>
        public void StartMatch(bool endless)
        {
            match = new MatchStateMachine(Mathf.Max(1, babies.Length), endless ? 0 : config.roundsToWin);
            running = true;
            round.ResetRoundNumber();
            MatchStarted?.Invoke();
            round.StartNewRound();
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
            if (round.Phase == RoundPhase.RoundOver && !match.IsOver && round.ResultElapsed >= config.roundResultDuration)
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
                    BabyController winner = RoundWinner;
                    if (winner != null)
                        winner.Celebrate();
                    bool matchOver = match.RecordRound(round.WinnerIndex);
                    PhaseChanged?.Invoke(phase);
                    if (matchOver)
                        MatchEnded?.Invoke(MatchWinner);
                    return;
            }
            PhaseChanged?.Invoke(phase);
        }

        void HandleTimeExpired()
        {
            for (int i = 0; i < babies.Length; i++)
                cryScratch[i] = babies[i].Cry.Value;
            int winner = RoundRules.CalmestBaby(cryScratch, TimeUpTieTolerance);
            round.EndRound(winner, true);

            // Losing the argument on points still ends in tears (after the round is decided, so this isn't a KO).
            for (int i = 0; i < babies.Length; i++)
            {
                if (winner != RoundStateMachine.NoPlayer && i != winner)
                    babies[i].BurstIntoTears();
            }
        }

        void HandleBabyStartedCrying(BabyController baby) => round.ReportCrying(IndexOf(baby));

        void ResetWorld()
        {
            for (int i = 0; i < babies.Length; i++)
            {
                Transform spawn = i < spawnPoints.Length ? spawnPoints[i] : babies[i].transform;
                babies[i].ResetForRound(spawn.position, spawn.rotation);
            }

            if (toys != null)
                toys.ResetForRound();

            // Babies were emptied above, so every diaper is handed out fresh: diaper i to baby i.
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
