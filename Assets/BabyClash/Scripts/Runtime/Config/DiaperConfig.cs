using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// The diaper as a weapon. Its armour side lives in <see cref="BabyConfig"/> (protected vs diaperless defense).
    /// One asset per diaper type later on.
    /// </summary>
    [CreateAssetMenu(fileName = "DiaperConfig", menuName = "Baby Clash/Diaper Config")]
    public sealed class DiaperConfig : ThrowableConfig
    {
    }
}
