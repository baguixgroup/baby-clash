using System;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// A pooled throwable toy (block, rubber duck, bottle...). Dropped into the playroom by the <see cref="ToySpawner"/>,
    /// held overhead, thrown once, then it blinks out a moment after landing.
    /// </summary>
    public sealed class Toy : Throwable
    {
        const float BlinkWarning = 1f;
        const float BlinkRate = 10f;

        [SerializeField] ToyConfig config;

        [Tooltip("Renderers hidden while the toy blinks out.")]
        [SerializeField] Renderer[] visuals = new Renderer[0];

        float lifetime;
        bool visible = true;

        public ToyConfig Config => config;
        public override ThrowableConfig Settings => config;

        /// <summary>Vanished and returned to the pool.</summary>
        public event Action<Toy> Expired;

        /// <summary>Scene/prefab wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(ToyConfig toyConfig, Collider solid, Collider hitTrigger, TrailRenderer flightTrail, Renderer[] renderers)
        {
            config = toyConfig;
            visuals = renderers;
            ConfigureParts(solid, hitTrigger, flightTrail);
        }

        protected override void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"{name}: no ToyConfig assigned, using defaults.", this);
                config = ScriptableObject.CreateInstance<ToyConfig>();
            }
            base.Awake();
        }

        /// <summary>Activates the toy loose at a position (it falls from there).</summary>
        public void Spawn(Vector3 position)
        {
            gameObject.SetActive(true);
            PlaceLoose(position);
        }

        /// <summary>Removes the toy from play (back to the pool).</summary>
        public void Despawn()
        {
            if (!gameObject.activeSelf)
                return;
            SetVisible(true);
            gameObject.SetActive(false);
            Expired?.Invoke(this);
        }

        protected override void OnLanded() => lifetime = config.despawnAfterLanding;

        protected override void OnBecameLoose() => lifetime = config.idleLifetime;

        protected override void OnHeld() => SetVisible(true);

        /// <summary>Online guests: the host decides when toys blink and disappear.</summary>
        public void ShowRemoteVisible(bool show) => SetVisible(show);

        public bool IsVisible => visible;

        void Update()
        {
            if (State != ItemState.Loose || IsPuppet)
                return;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                Despawn();
                return;
            }
            SetVisible(lifetime > BlinkWarning || Mathf.Repeat(lifetime * BlinkRate, 1f) > 0.35f);
        }

        void SetVisible(bool show)
        {
            if (visible == show)
                return;
            visible = show;
            foreach (Renderer visual in visuals)
            {
                if (visual != null)
                    visual.enabled = show;
            }
        }
    }
}
