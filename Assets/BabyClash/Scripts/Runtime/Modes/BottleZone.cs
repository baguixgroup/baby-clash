using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>King of the Bottle: standing alone (as a side) in the ring scores; rivals in it at once block scoring.</summary>
    public sealed class BottleZone : MonoBehaviour
    {
        const float Radius = 2.1f;
        const float PointsPerSecond = 2f;
        const float MoveEvery = 18f;

        static readonly Vector3[] spots = { new Vector3(0f, 0f, -2.9f), new Vector3(0f, 0f, 2.9f), new Vector3(-5.6f, 0f, -2.4f), new Vector3(5.6f, 0f, 2.4f) };

        RoundManager round;
        Transform bottle;
        Renderer ring;
        MaterialPropertyBlock block;
        float moveTimer;
        int spot;

        /// <summary>The zone in play, if this mode has one.</summary>
        public static BottleZone Active { get; private set; }

        public BabyController Holder { get; private set; }

        public static BottleZone Create(RoundManager manager)
        {
            var zone = new GameObject("BottleZone").AddComponent<BottleZone>();
            zone.round = manager;
            zone.Build();
            zone.gameObject.SetActive(false);
            return zone;
        }

        void Build()
        {
            block = new MaterialPropertyBlock();
            Color milk = new Color(1f, 0.98f, 0.92f), cap = new Color(1f, 0.6f, 0.7f), glass = new Color(0.75f, 0.88f, 1f);
            bottle = new ProcMesh().Cylinder(new Vector3(0f, 0f, 0f), 0.34f, 1f, glass).Cylinder(new Vector3(0f, 0.08f, 0f), 0.345f, 0.62f, milk)
                .Cylinder(new Vector3(0f, 1f, 0f), 0.3f, 0.2f, cap)
                .Lathe(new Vector3(0f, 1.2f, 0f), new Color(1f, 0.87f, 0.68f), new Vector2(0.24f, 0f), new Vector2(0.22f, 0.1f), new Vector2(0.1f, 0.18f), new Vector2(0.09f, 0.3f), new Vector2(0f, 0.36f))
                .Spawn("GiantBottle", transform, Vector3.zero).transform;
            ring = new ProcMesh().Disc(Vector3.zero, Radius - 0.22f, Radius, Color.white).Disc(Vector3.zero, 0f, Radius - 0.22f, new Color(1f, 1f, 1f, 0.28f))
                .Spawn("Ring", transform, new Vector3(0f, 0.035f, 0f), ProcMesh.Transparent).GetComponent<Renderer>();
        }

        void OnEnable()
        {
            Active = this;
            spot = 0;
            moveTimer = MoveEvery;
            transform.position = spots[0];
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= Radius * Radius;
        }

        void Update()
        {
            bottle.localRotation = Quaternion.Euler(0f, Time.time * 40f, Mathf.Sin(Time.time * 2f) * 4f);
            bottle.localPosition = Vector3.up * (1.9f + Mathf.Sin(Time.time * 2.4f) * 0.12f);
            bottle.localScale = Vector3.one * 0.8f;
            Holder = null;
            bool contested = false;
            if (round != null && round.Phase == RoundPhase.Playing)
            {
                foreach (BabyController baby in round.Babies)
                {
                    if (baby.IsCrying || !baby.IsActiveInRound || !Contains(baby.transform.position))
                        continue;
                    if (Holder == null)
                        Holder = baby;
                    else if (round.SideOf(baby) != round.SideOf(Holder))
                        contested = true;
                }
                if (Holder != null && !contested)
                    round.AddScore(Holder, PointsPerSecond * Time.deltaTime);

                moveTimer -= Time.deltaTime;
                if (moveTimer <= 0f)
                {
                    moveTimer = MoveEvery;
                    spot = (spot + 1) % spots.Length;
                }
            }
            transform.position = Vector3.Lerp(transform.position, spots[spot], 1f - Mathf.Exp(-3f * Time.deltaTime));
            Color color = contested ? new Color(1f, 0.35f, 0.35f) : Holder != null ? Holder.PlayerColor : Color.white;
            if (contested)
                Holder = null;
            color.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            ring.GetPropertyBlock(block);
            block.SetColor("_RendererColor", color);
            ring.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Survival: humans on one side against waves of CPU babies that leave when they cry and come back, meaner, as the
    /// waves go on. Ends when every human is crying.
    /// </summary>
    public sealed class SurvivalDirector : MonoBehaviour
    {
        const float ReturnDelay = 2.4f;
        const float WaveCalm = 22f;

        RoundManager round;
        float[] returnTimers = new float[MatchSetup.MaxPlayers];
        int knockoutsThisWave;

        public int Wave { get; private set; }
        public int Knockouts { get; private set; }

        public event System.Action<int> WaveStarted;

        public void Configure(RoundManager manager)
        {
            round = manager;
            round.BabyCried += HandleCried;
            round.PhaseChanged += HandlePhase;
        }

        void OnDestroy()
        {
            if (round == null)
                return;
            round.BabyCried -= HandleCried;
            round.PhaseChanged -= HandlePhase;
        }

        bool Running => round != null && round.Mode == GameMode.Survival && round.Phase == RoundPhase.Playing;
        int Goal => 2 + Wave;
        bool IsHuman(BabyController baby) => round.Setup != null && round.Setup.IsHuman(baby.PlayerIndex);

        void HandlePhase(RoundPhase phase)
        {
            if (phase != RoundPhase.Restarting || round.Mode != GameMode.Survival)
                return;
            Wave = 1;
            Knockouts = knockoutsThisWave = 0;
            for (int i = 0; i < returnTimers.Length; i++)
                returnTimers[i] = -1f;
            ApplyWave(true);
        }

        int MaxEnemies => Mathf.Clamp(1 + Wave / 2, 1, 3);

        void ApplyWave(bool resetting)
        {
            CpuDifficulty level = Wave <= 2 ? CpuDifficulty.Easy : Wave <= 5 ? CpuDifficulty.Normal : CpuDifficulty.Hard;
            int enemies = 0;
            foreach (BabyController baby in round.Babies)
            {
                if (IsHuman(baby))
                {
                    if (!resetting && !baby.IsCrying)
                        baby.Cry.Reduce(WaveCalm);
                    continue;
                }
                if (baby.TryGetComponent(out BabyBrain brain))
                    brain.Difficulty = level;
                bool present = enemies < MaxEnemies;
                enemies++;
                // Enemies waiting for a later wave sit out of sight.
                if (resetting)
                    baby.gameObject.SetActive(present);
                else if (present && !baby.gameObject.activeSelf && returnTimers[baby.PlayerIndex] < 0f)
                    returnTimers[baby.PlayerIndex] = 0.6f;
            }
        }

        void HandleCried(BabyController baby)
        {
            if (!Running)
                return;
            if (IsHuman(baby))
            {
                foreach (BabyController other in round.Babies)
                {
                    if (IsHuman(other) && !other.IsCrying)
                        return;
                }
                round.FinishRound(1);
                return;
            }
            Knockouts++;
            knockoutsThisWave++;
            foreach (BabyController other in round.Babies)
            {
                if (IsHuman(other))
                {
                    round.AddScore(other, 1f);
                    break;
                }
            }
            returnTimers[baby.PlayerIndex] = ReturnDelay;
            if (knockoutsThisWave >= Goal)
            {
                Wave++;
                knockoutsThisWave = 0;
                ApplyWave(false);
                WaveStarted?.Invoke(Wave);
            }
        }

        void Update()
        {
            if (!Running)
                return;
            foreach (BabyController baby in round.Babies)
            {
                int slot = baby.PlayerIndex;
                if (returnTimers[slot] < 0f)
                    continue;
                returnTimers[slot] -= Time.deltaTime;
                if (returnTimers[slot] > 0f)
                    continue;
                returnTimers[slot] = -1f;
                Transform spawn = round.SpawnOf(baby);
                baby.gameObject.SetActive(true);
                baby.Revive(spawn.position, spawn.rotation);
            }
        }
    }
}
