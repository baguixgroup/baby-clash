#nullable enable
using System;

namespace BabyClash.Core
{
    public enum ItemState
    {
        /// <summary>Lying in the arena. The only state in which it can be picked up.</summary>
        Loose,

        /// <summary>Held by a baby: a worn diaper (armour) or a toy in its hands.</summary>
        Held,

        /// <summary>Thrown and still moving. Can hit (once), can't be picked up.</summary>
        Flying,
    }

    /// <summary>
    /// Ownership and hit rules of one throwable item (diaper or toy). Items are never duplicated: they cycle
    /// Held -> Flying -> Loose -> Held (by whoever grabs it first). Baby ids are player indices; <see cref="Nobody"/>
    /// means no baby.
    /// </summary>
    public sealed class ItemStatus
    {
        public const int Nobody = -1;

        public ItemState State { get; private set; } = ItemState.Loose;
        public int HolderId { get; private set; } = Nobody;

        /// <summary>Who threw it, while it is flying. The thrower can never be hit by their own throw.</summary>
        public int ThrowerId { get; private set; } = Nobody;

        /// <summary>True once the flying item touched anything solid. Hits after that are not "direct".</summary>
        public bool HasBounced { get; private set; }

        /// <summary>True once this throw has hit someone. One throw = at most one hit.</summary>
        public bool HasHit { get; private set; }

        public bool CanBePickedUp => State == ItemState.Loose;
        public bool IsDangerous => State == ItemState.Flying && !HasHit;

        public event Action<ItemState>? StateChanged;

        /// <summary>Pickup: only a loose item can be claimed, so the first baby to ask gets it.</summary>
        public bool TryClaim(int babyId)
        {
            if (!CanBePickedUp || babyId < 0)
                return false;
            SetHeld(babyId);
            return true;
        }

        /// <summary>Round setup: hands the item to a baby regardless of where it is.</summary>
        public void ForceHold(int babyId)
        {
            if (babyId < 0)
                throw new ArgumentOutOfRangeException(nameof(babyId), babyId, "An item must be held by a real baby.");
            SetHeld(babyId);
        }

        public bool TryThrow(int babyId)
        {
            if (State != ItemState.Held || HolderId != babyId)
                return false;

            HolderId = Nobody;
            ThrowerId = babyId;
            HasBounced = false;
            HasHit = false;
            SetState(ItemState.Flying);
            return true;
        }

        public bool CanHit(int babyId) => IsDangerous && babyId >= 0 && babyId != ThrowerId;

        /// <summary>Consumes this throw's single hit. <paramref name="isDirect"/> is true if it hadn't bounced yet.</summary>
        public bool TryRegisterHit(int babyId, out bool isDirect)
        {
            isDirect = false;
            if (!CanHit(babyId))
                return false;

            isDirect = !HasBounced;
            HasHit = true;
            return true;
        }

        public void RegisterBounce()
        {
            if (State == ItemState.Flying)
                HasBounced = true;
        }

        /// <summary>Flying -> Loose once the item has come to rest.</summary>
        public bool TrySettle()
        {
            if (State != ItemState.Flying)
                return false;

            ThrowerId = Nobody;
            SetState(ItemState.Loose);
            return true;
        }

        /// <summary>Drops all ownership: dropped from a baby's hands, returned to the arena, or respawned.</summary>
        public void MakeLoose()
        {
            HolderId = Nobody;
            ThrowerId = Nobody;
            HasBounced = false;
            HasHit = false;
            SetState(ItemState.Loose);
        }

        void SetHeld(int babyId)
        {
            HolderId = babyId;
            ThrowerId = Nobody;
            HasBounced = false;
            HasHit = false;
            SetState(ItemState.Held);
        }

        void SetState(ItemState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
