using UnityEngine;

namespace BabyClash
{
    public enum ToySound
    {
        Clack,
        Squeak,
        Slosh,
    }

    /// <summary>A throwable toy that appears in the playroom. Toys are single-use ammo: they vanish after landing.</summary>
    [CreateAssetMenu(fileName = "ToyConfig", menuName = "Baby Clash/Toy Config")]
    public sealed class ToyConfig : ThrowableConfig
    {
        [Header("Toy")]
        public string displayName = "Toy";

        [Tooltip("Sound played when it hits a baby.")]
        public ToySound impactSound = ToySound.Clack;

        [Tooltip("After being thrown and landing, the toy vanishes after this many seconds (s).")]
        [Min(0.1f)] public float despawnAfterLanding = 2.5f;

        [Tooltip("A toy nobody picks up vanishes after this many seconds (s), so the playroom keeps changing.")]
        [Min(1f)] public float idleLifetime = 16f;

        [Tooltip("Relative chance of this toy being picked by the spawner.")]
        [Min(0f)] public float spawnWeight = 1f;
    }
}
