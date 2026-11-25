using System;
using System.Collections;
using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    public enum PowerUpKind
    {
        /// <summary>Absorbs the next two hits.</summary>
        Pacifier,

        /// <summary>Runs much faster for a while.</summary>
        SippyCup,

        /// <summary>Throws hit much harder for a while.</summary>
        Lollipop,

        /// <summary>Traps the nearest rival in a bubble.</summary>
        BubbleWand,

        /// <summary>A cuddle that calms the Cry Meter.</summary>
        Teddy,
    }

    /// <summary>A floating power-up; touching it uses it.</summary>
    public sealed class PowerUpPickup : MonoBehaviour
    {
        static readonly List<PowerUpPickup> active = new List<PowerUpPickup>();

        PowerUpSpawner owner;
        float life;
        Transform visual;

        public PowerUpKind Kind { get; private set; }

        public static PowerUpPickup Nearest(Vector3 position, float range)
        {
            PowerUpPickup best = null;
            float bestDistance = range * range;
            foreach (PowerUpPickup pickup in active)
            {
                float distance = (pickup.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = pickup;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public void Setup(PowerUpSpawner spawner, PowerUpKind kind, float lifetime)
        {
            owner = spawner;
            Kind = kind;
            life = lifetime;
            visual = BuildVisual(kind).transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * 1.5f;
            new ProcMesh().Disc(Vector3.zero, 0.42f, 0.55f, ColorOf(kind)).Disc(Vector3.zero, 0f, 0.42f, Fade(ColorOf(kind), 0.3f))
                .Spawn("Glow", transform, new Vector3(0f, 0.03f, 0f), ProcMesh.Transparent);
            var zone = gameObject.AddComponent<SphereCollider>();
            zone.isTrigger = true;
            zone.radius = 0.75f;
            zone.center = Vector3.up * 0.6f;
        }

        static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        void OnEnable() => active.Add(this);

        void OnDisable() => active.Remove(this);

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            visual.localPosition = Vector3.up * (0.75f + Mathf.Sin(Time.time * 3f) * 0.12f);
            visual.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
            visual.gameObject.SetActive(life > 2.5f || Mathf.Repeat(life, 0.3f) > 0.1f);
        }

        void OnTriggerStay(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out BabyController baby) || !baby.IsActiveInRound || baby.IsCrying)
                return;
            // Two babies can touch it in the same physics step; only the first one gets it.
            if (!enabled)
                return;
            enabled = false;
            owner.Use(Kind, baby);
            Destroy(gameObject);
        }

        public static Color ColorOf(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Pacifier: return new Color(0.4f, 0.75f, 1f);
                case PowerUpKind.SippyCup: return new Color(1f, 0.8f, 0.25f);
                case PowerUpKind.Lollipop: return new Color(1f, 0.4f, 0.6f);
                case PowerUpKind.BubbleWand: return new Color(0.6f, 0.95f, 0.95f);
                default: return new Color(0.85f, 0.62f, 0.4f);
            }
        }

        public static string NameOf(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Pacifier: return "SHIELD!";
                case PowerUpKind.SippyCup: return "ZOOM!";
                case PowerUpKind.Lollipop: return "MEGA THROW!";
                case PowerUpKind.BubbleWand: return "BUBBLE!";
                default: return "CUDDLE!";
            }
        }

        static GameObject BuildVisual(PowerUpKind kind)
        {
            Color main = ColorOf(kind);
            var m = new ProcMesh();
            switch (kind)
            {
                case PowerUpKind.Pacifier:
                    m.Ellipsoid(new Vector3(0f, 0f, 0.04f), new Vector3(0.26f, 0.2f, 0.06f), main)
                        .Sphere(new Vector3(0f, 0f, 0.16f), 0.09f, new Color(1f, 0.9f, 0.75f))
                        .Torus(new Vector3(0f, -0.04f, -0.06f), 0.13f, 0.03f, Color.white, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case PowerUpKind.SippyCup:
                    m.Cylinder(new Vector3(0f, -0.25f, 0f), 0.15f, 0.36f, main, 0.18f)
                        .Cylinder(new Vector3(0f, 0.1f, 0f), 0.2f, 0.08f, Color.white)
                        .Cylinder(new Vector3(0.08f, 0.17f, 0f), 0.05f, 0.1f, Color.white)
                        .Torus(new Vector3(0.22f, -0.07f, 0f), 0.09f, 0.03f, Color.white, Quaternion.Euler(90f, 0f, 0f))
                        .Torus(new Vector3(-0.22f, -0.07f, 0f), 0.09f, 0.03f, Color.white, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case PowerUpKind.Lollipop:
                    m.Cylinder(new Vector3(0f, -0.4f, 0f), 0.025f, 0.35f, Color.white)
                        .Ellipsoid(new Vector3(0f, 0.12f, 0f), new Vector3(0.25f, 0.25f, 0.08f), main)
                        .Torus(new Vector3(0f, 0.12f, 0f), 0.15f, 0.03f, Color.white, Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 2.8f, 1f))
                        .Sphere(new Vector3(0f, 0.12f, 0f), 0.09f, Color.white);
                    break;
                case PowerUpKind.BubbleWand:
                    m.Cylinder(new Vector3(0f, -0.4f, 0f), 0.03f, 0.42f, new Color(0.75f, 0.55f, 1f))
                        .Torus(new Vector3(0f, 0.17f, 0f), 0.17f, 0.03f, new Color(0.75f, 0.55f, 1f), Quaternion.Euler(90f, 0f, 0f))
                        .Sphere(new Vector3(0.24f, 0.34f, 0.05f), 0.08f, main).Sphere(new Vector3(-0.22f, 0.38f, 0f), 0.055f, main);
                    break;
                default:
                    Color dark = main * 0.8f;
                    m.Ellipsoid(new Vector3(0f, -0.12f, 0f), new Vector3(0.17f, 0.2f, 0.15f), main)
                        .Sphere(new Vector3(0f, 0.16f, 0f), 0.15f, main)
                        .Sphere(new Vector3(0.11f, 0.28f, 0f), 0.06f, dark).Sphere(new Vector3(-0.11f, 0.28f, 0f), 0.06f, dark)
                        .Ellipsoid(new Vector3(0f, 0.12f, 0.12f), new Vector3(0.07f, 0.055f, 0.05f), new Color(1f, 0.88f, 0.72f))
                        .Sphere(new Vector3(0.055f, 0.2f, 0.13f), 0.02f, Color.black).Sphere(new Vector3(-0.055f, 0.2f, 0.13f), 0.02f, Color.black)
                        .Sphere(new Vector3(0.18f, -0.05f, 0.04f), 0.07f, main).Sphere(new Vector3(-0.18f, -0.05f, 0.04f), 0.07f, main)
                        .Sphere(new Vector3(0.1f, -0.3f, 0.06f), 0.08f, dark).Sphere(new Vector3(-0.1f, -0.3f, 0.06f), 0.08f, dark);
                    break;
            }
            return m.Spawn("Visual", null, Vector3.zero);
        }
    }

    /// <summary>Drops power-ups during play and applies them. Listens to the round; created at runtime by GameFlow.</summary>
    public sealed class PowerUpSpawner : MonoBehaviour
    {
        const float FirstDelay = 5f;
        const float Lifetime = 13f;
        const int MaxAtOnce = 2;
        const float BoostSeconds = 7f;
        const float BubbleRange = 9f;
        const float BubbleSeconds = 2.2f;
        const float Clearance = 0.8f;

        readonly Collider[] probe = new Collider[4];
        readonly List<PowerUpPickup> live = new List<PowerUpPickup>();
        RoundManager round;
        ArenaBounds arena;
        AudioDirector audioDirector;
        float timer;

        /// <summary>A baby used a power-up.</summary>
        public event Action<BabyController, PowerUpKind> Used;

        public void Configure(RoundManager manager, ArenaBounds bounds, AudioDirector audio)
        {
            round = manager;
            arena = bounds;
            audioDirector = audio;
            round.PhaseChanged += HandlePhase;
        }

        void OnDestroy()
        {
            if (round != null)
                round.PhaseChanged -= HandlePhase;
        }

        void HandlePhase(RoundPhase phase)
        {
            if (phase != RoundPhase.Restarting)
                return;
            StopAllCoroutines();
            foreach (PowerUpPickup pickup in live)
            {
                if (pickup != null)
                    Destroy(pickup.gameObject);
            }
            live.Clear();
            foreach (BabyEffects effects in FindObjectsByType<BabyEffects>())
                effects.Clear();
            timer = FirstDelay;
        }

        void Update()
        {
            if (round == null || !round.IsRunning || round.Phase != RoundPhase.Playing || round.Setup == null || !round.Setup.PowerUps)
                return;
            timer -= Time.deltaTime;
            if (timer > 0f)
                return;
            timer = UnityEngine.Random.Range(6f, 10f);
            live.RemoveAll(pickup => pickup == null);
            if (live.Count < MaxAtOnce && TryFindSpot(out Vector3 spot))
                Spawn((PowerUpKind)UnityEngine.Random.Range(0, Enum.GetValues(typeof(PowerUpKind)).Length), spot);
        }

        public PowerUpPickup Spawn(PowerUpKind kind, Vector3 position)
        {
            var go = new GameObject("PowerUp_" + kind);
            go.transform.position = position;
            var pickup = go.AddComponent<PowerUpPickup>();
            pickup.Setup(this, kind, Lifetime);
            live.Add(pickup);
            if (audioDirector != null)
                audioDirector.Play(SfxId.Spawn, 1.4f, 0.6f);
            return pickup;
        }

        bool TryFindSpot(out Vector3 spot)
        {
            Vector2 half = arena != null ? arena.HalfExtents : new Vector2(6f, 4f);
            for (int i = 0; i < 16; i++)
            {
                spot = new Vector3(UnityEngine.Random.Range(-half.x + 1.3f, half.x - 1.3f), 0f, UnityEngine.Random.Range(-half.y + 1.3f, half.y - 1.3f));
                if (Physics.OverlapSphereNonAlloc(spot + Vector3.up * (Clearance + 0.1f), Clearance, probe, Physics.AllLayers, QueryTriggerInteraction.Ignore) == 0)
                    return true;
            }
            spot = default;
            return false;
        }

        public void Use(PowerUpKind kind, BabyController baby)
        {
            BabyEffects effects = BabyEffects.On(baby);
            switch (kind)
            {
                case PowerUpKind.Pacifier:
                    effects.Shield(2);
                    break;
                case PowerUpKind.SippyCup:
                    effects.Boost(BoostSeconds);
                    break;
                case PowerUpKind.Lollipop:
                    effects.Power(BoostSeconds);
                    break;
                case PowerUpKind.BubbleWand:
                    BabyController target = NearestRival(baby);
                    if (target != null && target.Trap(BubbleSeconds))
                        BabyEffects.On(target).Bubble(BubbleSeconds);
                    break;
                case PowerUpKind.Teddy:
                    baby.Cry.Reduce(baby.Cry.Max * 0.35f);
                    break;
            }
            effects.Say(PowerUpPickup.NameOf(kind), PowerUpPickup.ColorOf(kind));
            if (audioDirector != null)
                audioDirector.Play(SfxId.Cheer, 1.5f, 0.45f);
            Used?.Invoke(baby, kind);
        }

        BabyController NearestRival(BabyController baby)
        {
            BabyController best = null;
            float bestDistance = BubbleRange * BubbleRange;
            foreach (BabyController other in round.Babies)
            {
                if (other == baby || other.IsCrying || !other.IsActiveInRound || baby.IsAlly(other))
                    continue;
                float distance = (other.transform.position - baby.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = other;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }

    /// <summary>Timed power-up effects on one baby, and the visuals that go with them.</summary>
    public sealed class BabyEffects : MonoBehaviour
    {
        const float BoostScale = 1.45f;
        const float PowerScale = 1.5f;

        BabyController baby;
        float boostTimer;
        float powerTimer;
        float bubbleTimer;
        GameObject shield;
        GameObject bubble;
        GameObject powerAura;
        TextMesh callout;
        float calloutTimer;
        float floorScale = 1f;
        float floorTimer;

        /// <summary>Sticky floor (jam, sand): zones call this every physics step while the baby stands in them.</summary>
        public void StickTo(float scale)
        {
            floorScale = scale;
            floorTimer = 0.1f;
        }

        public static BabyEffects On(BabyController baby)
        {
            if (!baby.TryGetComponent(out BabyEffects effects))
            {
                effects = baby.gameObject.AddComponent<BabyEffects>();
                effects.baby = baby;
            }
            return effects;
        }

        public void Shield(int hits) => baby.ShieldHits = hits;

        public void Boost(float seconds) => boostTimer = seconds;

        public void Power(float seconds) => powerTimer = seconds;

        public void Bubble(float seconds) => bubbleTimer = seconds;

        public void Say(string text, Color color)
        {
            if (callout == null)
            {
                var go = new GameObject("Callout");
                callout = go.AddComponent<TextMesh>();
                callout.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                go.GetComponent<MeshRenderer>().sharedMaterial = callout.font.material;
                callout.fontSize = 64;
                callout.fontStyle = FontStyle.Bold;
                callout.characterSize = 0.06f;
                callout.anchor = TextAnchor.MiddleCenter;
            }
            callout.text = text;
            callout.color = Color.Lerp(color, MenuKit.Ink, 0.5f);
            callout.gameObject.SetActive(true);
            calloutTimer = 1.1f;
        }

        public void Clear()
        {
            boostTimer = powerTimer = bubbleTimer = calloutTimer = 0f;
            floorTimer = 0f;
            if (baby != null)
                baby.ShieldHits = 0;
            if (callout != null)
                callout.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (callout != null)
                Destroy(callout.gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            boostTimer = Mathf.Max(0f, boostTimer - dt);
            powerTimer = Mathf.Max(0f, powerTimer - dt);
            bubbleTimer = baby.Status.Activity == BabyActivity.Stunned ? Mathf.Max(0f, bubbleTimer - dt) : 0f;
            floorTimer = Mathf.Max(0f, floorTimer - dt);
            baby.BoostScale = (boostTimer > 0f ? BoostScale : 1f) * (floorTimer > 0f ? floorScale : 1f);
            baby.ThrowPower = powerTimer > 0f ? PowerScale : 1f;

            Toggle(ref shield, baby.ShieldHits > 0 && !baby.IsCrying, () => Ball("Shield", 0.85f, new Color(0.4f, 0.75f, 1f, 0.28f), 0.72f));
            Toggle(ref bubble, bubbleTimer > 0f, () => Ball("Bubble", 0.95f, new Color(0.75f, 1f, 1f, 0.4f), 0.8f));
            Toggle(ref powerAura, (powerTimer > 0f || boostTimer > 0f) && !baby.IsCrying,
                () => new ProcMesh().Disc(Vector3.zero, 0.5f, 0.85f, new Color(1f, 0.85f, 0.3f, 0.6f)).Spawn("Aura", transform, new Vector3(0f, 0.04f, 0f), ProcMesh.Transparent));
            if (bubble != null && bubbleTimer > 0f)
                bubble.transform.localPosition = Vector3.up * (0.8f + Mathf.Sin(Time.time * 6f) * 0.08f);
            if (powerAura != null)
                powerAura.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 9f) * 0.08f);

            if (calloutTimer > 0f && callout != null)
            {
                calloutTimer -= dt;
                callout.transform.position = transform.position + Vector3.up * (2.5f - calloutTimer * 0.4f);
                Camera view = Camera.main;
                if (view != null)
                    callout.transform.rotation = view.transform.rotation;
                if (calloutTimer <= 0f)
                    callout.gameObject.SetActive(false);
            }
        }

        GameObject Ball(string ballName, float radius, Color color, float height)
        {
            // Two layers so it reads as a shiny shell with a highlight.
            GameObject ball = new ProcMesh().Sphere(Vector3.zero, radius, color).Spawn(ballName, transform, Vector3.up * height, ProcMesh.Transparent);
            var mesh = ball.GetComponent<MeshFilter>().sharedMesh;
            var colors = new Color[mesh.vertexCount];
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < colors.Length; i++)
            {
                float shine = Mathf.Clamp01(Vector3.Dot(normals[i], new Vector3(-0.4f, 0.8f, -0.45f)) - 0.82f) * 5f;
                colors[i] = Color.Lerp(color, new Color(1f, 1f, 1f, 0.9f), shine);
            }
            mesh.colors = colors;
            return ball;
        }

        static void Toggle(ref GameObject visual, bool on, Func<GameObject> create)
        {
            if (on && visual == null)
                visual = create();
            if (visual != null && visual.activeSelf != on)
                visual.SetActive(on);
        }
    }
}
