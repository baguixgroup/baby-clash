using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// A persistent diaper: armour while worn, a projectile while flying, a pickup while loose. Never destroyed and
    /// never respawned; there are exactly as many diapers as the match started with.
    /// </summary>
    public sealed class Diaper : Throwable
    {
        [SerializeField] DiaperConfig config;

        public override ThrowableConfig Settings => config;

        /// <summary>Scene/prefab wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(DiaperConfig diaperConfig, Collider solid, Collider hitTrigger, TrailRenderer flightTrail)
        {
            config = diaperConfig;
            ConfigureParts(solid, hitTrigger, flightTrail);
        }

        protected override void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"{name}: no DiaperConfig assigned, using defaults.", this);
                config = ScriptableObject.CreateInstance<DiaperConfig>();
            }
            base.Awake();
        }
    }
}
