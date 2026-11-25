using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// "Game juice" in one place: listens to gameplay events and plays camera shake, particles, hit-stop, slow motion
    /// and sounds. Gameplay never calls into this, so it can be tuned, replaced or removed freely.
    /// </summary>
    public sealed class ImpactFeedback : MonoBehaviour
    {
        const int CountdownSteps = 3;

        [SerializeField] RoundManager roundManager;
        [SerializeField] ArenaCamera arenaCamera;
        [SerializeField] AudioDirector audioDirector;
        [SerializeField] TimeController timeController;

        [Tooltip("Burst emitter for hits (stars and sparkles).")]
        [SerializeField] ParticleSystem impactParticles;

        [Tooltip("Burst emitter for dust puffs (dashes, landings, toy drops).")]
        [SerializeField] ParticleSystem dustParticles;

        [Tooltip("Burst emitter for the round winner's confetti.")]
        [SerializeField] ParticleSystem confetti;

        [Header("Shake")]
        [Tooltip("Camera trauma per m/s of knockback.")]
        [SerializeField] float shakePerKnockbackSpeed = 0.05f;
        [SerializeField] float cryShake = 0.55f;
        [SerializeField] float shoveShake = 0.3f;

        [Header("Particles")]
        [SerializeField] int particlesPerHit = 12;
        [SerializeField] int extraParticlesWhenDiaperless = 14;
        [SerializeField] int dustPerDash = 10;
        [SerializeField] int confettiCount = 90;

        [Header("Time")]
        [Tooltip("Freeze frames on painful hits (real-time s).")]
        [SerializeField] float painfulHitStop = 0.06f;

        [Tooltip("Slow motion when a baby bursts into tears (real-time s).")]
        [SerializeField] float knockoutSlowMotion = 0.9f;
        [SerializeField] float knockoutTimeScale = 0.3f;

        int shownCountdown = -1;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(RoundManager round, ArenaCamera cameraRig, AudioDirector audio, TimeController time,
            ParticleSystem impacts, ParticleSystem dust, ParticleSystem confettiBurst)
        {
            roundManager = round;
            arenaCamera = cameraRig;
            audioDirector = audio;
            timeController = time;
            impactParticles = impacts;
            dustParticles = dust;
            confetti = confettiBurst;
        }

        void OnEnable()
        {
            if (roundManager == null)
                return;
            roundManager.PhaseChanged += HandlePhaseChanged;
            foreach (BabyController baby in roundManager.AllBabies)
            {
                if (baby == null)
                    continue;
                baby.HitReceived += HandleHit;
                baby.ThrewItem += HandleThrow;
                baby.PickedUpItem += HandlePickup;
                baby.Dashed += HandleDash;
                baby.StartedCrying += HandleCry;
            }
            foreach (Diaper diaper in roundManager.Diapers)
            {
                if (diaper != null)
                    diaper.HitBaby += HandleItemHit;
            }
            if (roundManager.Toys != null)
            {
                roundManager.Toys.Spawned += HandleToySpawned;
                foreach (Toy toy in roundManager.Toys.Pool)
                {
                    if (toy != null)
                        toy.HitBaby += HandleItemHit;
                }
            }
        }

        void OnDisable()
        {
            if (roundManager == null)
                return;
            roundManager.PhaseChanged -= HandlePhaseChanged;
            foreach (BabyController baby in roundManager.AllBabies)
            {
                if (baby == null)
                    continue;
                baby.HitReceived -= HandleHit;
                baby.ThrewItem -= HandleThrow;
                baby.PickedUpItem -= HandlePickup;
                baby.Dashed -= HandleDash;
                baby.StartedCrying -= HandleCry;
            }
            foreach (Diaper diaper in roundManager.Diapers)
            {
                if (diaper != null)
                    diaper.HitBaby -= HandleItemHit;
            }
            if (roundManager.Toys != null)
            {
                roundManager.Toys.Spawned -= HandleToySpawned;
                foreach (Toy toy in roundManager.Toys.Pool)
                {
                    if (toy != null)
                        toy.HitBaby -= HandleItemHit;
                }
            }
        }

        void Update()
        {
            if (roundManager == null || roundManager.Phase != RoundPhase.Starting || !roundManager.IsRunning)
            {
                shownCountdown = -1;
                return;
            }
            float progress = roundManager.CountdownRemaining / Mathf.Max(0.01f, roundManager.CountdownDuration);
            int step = Mathf.Clamp(Mathf.CeilToInt(progress * CountdownSteps), 1, CountdownSteps);
            if (step != shownCountdown)
            {
                shownCountdown = step;
                Play(SfxId.Tick, 1f + (CountdownSteps - step) * 0.12f);
            }
        }

        void HandlePhaseChanged(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Playing:
                    Play(SfxId.Go, 1f);
                    break;
                case RoundPhase.RoundOver:
                    BabyController winner = roundManager.RoundWinner;
                    if (winner != null)
                    {
                        Play(SfxId.Cheer, 1f);
                        Emit(confetti, winner.transform.position + Vector3.up * 2.2f, confettiCount);
                    }
                    break;
            }
        }

        void HandleHit(BabyController baby, BabyHitReport report)
        {
            if (arenaCamera != null)
                arenaCamera.Shake(report.KnockbackSpeed * shakePerKnockbackSpeed);
            Emit(impactParticles, report.Point, particlesPerHit + (report.WasProtected ? 0 : extraParticlesWhenDiaperless));

            if (report.Source == HitSource.Shove)
            {
                Play(SfxId.Shove, Random.Range(0.9f, 1.1f));
                if (arenaCamera != null)
                    arenaCamera.Shake(shoveShake);
            }
            else
            {
                Play(report.WasProtected ? SfxId.Hit : SfxId.HitHard, report.WasProtected ? 0.9f : 1.15f);
            }

            if (!report.WasProtected && report.WasDirectHit && timeController != null)
                timeController.HitStop(painfulHitStop);
        }

        void HandleItemHit(Throwable item, BabyController baby, bool direct)
        {
            if (item is Toy toy && toy.Config != null)
            {
                SfxId sound = toy.Config.impactSound == ToySound.Squeak ? SfxId.Squeak
                    : toy.Config.impactSound == ToySound.Slosh ? SfxId.Slosh : SfxId.Clack;
                Play(sound, Random.Range(0.92f, 1.08f));
            }
        }

        void HandleThrow(BabyController baby, Throwable item) => Play(SfxId.Throw, Random.Range(0.9f, 1.15f));

        void HandlePickup(BabyController baby, Throwable item) => Play(SfxId.Pickup, item is Diaper ? 1f : 1.2f);

        void HandleDash(BabyController baby)
        {
            Play(SfxId.Dash, Random.Range(0.95f, 1.1f));
            Emit(dustParticles, baby.transform.position + Vector3.up * 0.1f, dustPerDash);
        }

        void HandleToySpawned(Toy toy) => Play(SfxId.Spawn, Random.Range(0.95f, 1.1f), 0.7f);

        void HandleCry(BabyController baby)
        {
            if (arenaCamera != null)
                arenaCamera.Shake(cryShake);
            Play(SfxId.Cry, Random.Range(0.95f, 1.08f));
            if (timeController != null && roundManager != null && !roundManager.IsEndless)
                timeController.SlowMotion(knockoutSlowMotion, knockoutTimeScale);
        }

        void Play(SfxId id, float pitch, float volume = 1f)
        {
            if (audioDirector != null)
                audioDirector.Play(id, pitch, volume);
        }

        static void Emit(ParticleSystem system, Vector3 position, int count)
        {
            if (system == null || count <= 0)
                return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true,
            };
            system.Emit(emit, count);
        }
    }
}
