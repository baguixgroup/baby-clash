using System;
using System.Collections.Generic;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// The diaper(s) a baby is wearing. Wearing at least one = protected.
    /// All diaper ownership changes on the baby side go through here so the list and the diapers stay in sync.
    /// </summary>
    public sealed class BabyDiaperSlot : MonoBehaviour
    {
        [Tooltip("Where worn diapers attach (the hips). Also the centre of the pickup search.")]
        [SerializeField] Transform socket;

        [Tooltip("Vertical spacing when more than one diaper is worn (only with Max Diapers > 1).")]
        [SerializeField] float stackSpacing = 0.12f;

        readonly List<Diaper> held = new List<Diaper>(2);
        BabyController owner;
        BabyConfig config;

        public IReadOnlyList<Diaper> Held => held;
        public int Count => held.Count;
        public bool IsProtected => held.Count > 0;
        public bool HasRoom => held.Count < Capacity;
        public int Capacity => config != null ? Mathf.Max(1, config.maxDiapers) : 1;
        public Transform Socket => socket != null ? socket : transform;

        /// <summary>The diaper that would be thrown next.</summary>
        public Diaper Outermost => held.Count > 0 ? held[held.Count - 1] : null;

        /// <summary>Raised whenever a diaper is put on or taken off.</summary>
        public event Action<BabyDiaperSlot> Changed;

        /// <summary>Prefab wiring (editor tooling and tests).</summary>
        public void Configure(Transform diaperSocket) => socket = diaperSocket;

        public void Initialize(BabyController controller, BabyConfig babyConfig)
        {
            owner = controller;
            config = babyConfig;
        }

        /// <summary>Round setup: puts the diaper on regardless of where it is. False if the baby is full.</summary>
        public bool ForceEquip(Diaper diaper)
        {
            if (diaper == null || !HasRoom || held.Contains(diaper))
                return false;

            diaper.ForceHold(owner, Socket, StackOffset(held.Count), Vector3.one);
            held.Add(diaper);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Pickup: claims a loose diaper and puts it on. Fails if it isn't loose or there's no room.</summary>
        public bool TryEquip(Diaper diaper)
        {
            if (diaper == null || !HasRoom || !diaper.TryPickUp(owner, Socket, StackOffset(held.Count), Vector3.one))
                return false;
            held.Add(diaper);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Takes the outermost diaper off for throwing. The caller must launch it.</summary>
        public Diaper TakeForThrow()
        {
            if (held.Count == 0)
                return null;

            int last = held.Count - 1;
            Diaper diaper = held[last];
            held.RemoveAt(last);
            Changed?.Invoke(this);
            return diaper;
        }

        /// <summary>Round reset: forgets worn diapers. The round manager re-equips every diaper right after.</summary>
        public void ForgetAll()
        {
            held.Clear();
            Changed?.Invoke(this);
        }

        Vector3 StackOffset(int index) => Vector3.up * (stackSpacing * index);
    }
}
