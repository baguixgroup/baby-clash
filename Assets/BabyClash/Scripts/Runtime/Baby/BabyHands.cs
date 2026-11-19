using System;
using UnityEngine;

namespace BabyClash
{
    /// <summary>The toy a baby is holding overhead (at most one). Separate from the diaper it wears.</summary>
    public sealed class BabyHands : MonoBehaviour
    {
        [Tooltip("Where a held toy sits (above the head, between the raised hands).")]
        [SerializeField] Transform holdSocket;

        BabyController owner;
        Toy held;

        public Toy Held => held;
        public bool IsHolding => held != null;
        public Transform Socket => holdSocket != null ? holdSocket : transform;

        public event Action<BabyHands> Changed;

        /// <summary>Prefab wiring (editor tooling and tests).</summary>
        public void Configure(Transform socket) => holdSocket = socket;

        public void Initialize(BabyController controller) => owner = controller;

        public bool TryPickUp(Toy toy)
        {
            if (held != null || toy == null || !toy.TryPickUp(owner, Socket, Vector3.zero, Vector3.one))
                return false;
            held = toy;
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Lets go of the toy so it can be launched. The caller must launch it.</summary>
        public Toy TakeForThrow()
        {
            Toy toy = held;
            held = null;
            if (toy != null)
                Changed?.Invoke(this);
            return toy;
        }

        /// <summary>Knocks the toy loose (shoved, or started crying).</summary>
        public void DropHeld(Vector3 velocity)
        {
            if (held == null)
                return;
            Toy toy = held;
            held = null;
            toy.Drop(toy.transform.position, velocity);
            Changed?.Invoke(this);
        }

        /// <summary>Round reset: the toy spawner clears every toy right after.</summary>
        public void ForgetHeld()
        {
            if (held == null)
                return;
            held = null;
            Changed?.Invoke(this);
        }
    }
}
