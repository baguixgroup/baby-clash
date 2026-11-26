using System;
using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Anything a baby can pick up and throw: a diaper (worn as armour) or a toy (held overhead).
    /// Physics layout: the solid collider bounces off the arena but never touches babies (collisions are ignored),
    /// hits are detected by the trigger, and pickups by the baby's overlap query. That avoids items getting stuck in
    /// babies, being shoved around by them, or clipping their own thrower.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Throwable : MonoBehaviour
    {
        const float MaxSpinRadiansPerSecond = 50f;
        const float MinDirectionSqr = 0.0001f;
        const float FloorNormalMinY = 0.5f;

        [Tooltip("Solid collider that bounces off the floor, walls and obstacles.")]
        [SerializeField] Collider solidCollider;

        [Tooltip("Trigger that detects hits on babies while flying.")]
        [SerializeField] Collider hitZone;

        [Tooltip("Optional trail shown while flying.")]
        [SerializeField] TrailRenderer trail;

        Rigidbody body;
        Transform looseParent;
        Vector3 baseScale = Vector3.one;
        Collider[] ignoredColliders = Array.Empty<Collider>();
        float flightTime;

        public ItemStatus Status { get; } = new ItemStatus();
        public ItemState State => Status.State;
        public bool CanBePickedUp => Status.CanBePickedUp && isActiveAndEnabled;

        /// <summary>The baby wearing or holding it.</summary>
        public BabyController Holder { get; private set; }

        /// <summary>Who threw it, while it's flying.</summary>
        public BabyController Thrower { get; private set; }

        public Rigidbody Body => body;

        /// <summary>Online guests: the host moves this item; it only follows <see cref="ShowRemote"/>.</summary>
        public bool IsPuppet { get; private set; }

        public void SetPuppet(bool puppet)
        {
            IsPuppet = puppet;
            if (body != null && Holder == null)
                SetPhysicsActive(true);
        }

        /// <summary>Online guests: shows the host's state for this item.</summary>
        public void ShowRemote(ItemState state, BabyController holder, BabyController thrower, Vector3 position, Quaternion rotation)
        {
            if (state == ItemState.Held && holder != null)
            {
                if (Holder != holder)
                {
                    ReleaseFromHolder();
                    if (this is Diaper diaper)
                        holder.Diapers.ShowWorn(diaper);
                    else if (this is Toy toy)
                        holder.Hands.ShowHeld(toy);
                }
                Status.SetMirrored(ItemState.Held, holder.PlayerIndex, ItemStatus.Nobody);
                return;
            }
            if (Holder != null)
            {
                ReleaseFromHolder();
                Holder = null;
                Detach(position, rotation);
            }
            Thrower = state == ItemState.Flying ? thrower : null;
            Status.SetMirrored(state, ItemStatus.Nobody, thrower != null ? thrower.PlayerIndex : ItemStatus.Nobody);
            transform.SetPositionAndRotation(position, rotation);
            if (trail != null && trail.emitting != (state == ItemState.Flying))
                SetTrail(state == ItemState.Flying);
        }

        void ReleaseFromHolder()
        {
            if (Holder == null)
                return;
            if (this is Diaper diaper)
                Holder.Diapers.ShowRemoved(diaper);
            else if (this is Toy toy)
                Holder.Hands.ShowReleased(toy);
        }

        /// <summary>Online guests: replays a hit this item made on the host.</summary>
        public void ShowRemoteHit(BabyController thrower, BabyController victim, bool direct)
        {
            Thrower = thrower;
            HitBaby?.Invoke(this, victim, direct);
        }
        public abstract ThrowableConfig Settings { get; }

        public event Action<Throwable> Launched;

        /// <summary>Flying -> Loose: it has come to rest and can be picked up.</summary>
        public event Action<Throwable> Landed;

        /// <summary>It left the arena and was put back at the neutral recovery point.</summary>
        public event Action<Throwable> Recovered;

        /// <summary>It hit a baby (baby, was it a direct hit).</summary>
        public event Action<Throwable, BabyController, bool> HitBaby;

        protected void ConfigureParts(Collider solid, Collider hitTrigger, TrailRenderer flightTrail)
        {
            solidCollider = solid;
            hitZone = hitTrigger;
            trail = flightTrail;
        }

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.maxAngularVelocity = MaxSpinRadiansPerSecond;
            looseParent = transform.parent;
            baseScale = transform.localScale;

            if (solidCollider == null)
                Debug.LogError($"{name}: no solid collider assigned.", this);
            if (hitZone == null || !hitZone.isTrigger)
                Debug.LogError($"{name}: the hit zone must be a trigger collider.", this);
            SetTrail(false);
            if (trail != null)
                trail.Clear();
        }

        /// <summary>Babies never physically touch items. Called once by the round manager with every baby's body.</summary>
        public void IgnoreCollisionsWith(IReadOnlyList<Collider> babyColliders)
        {
            ignoredColliders = new Collider[babyColliders.Count];
            for (int i = 0; i < ignoredColliders.Length; i++)
                ignoredColliders[i] = babyColliders[i];
            if (body != null && !body.isKinematic)
                ApplyIgnoredCollisions();
        }

        /// <summary>Pickup. Fails unless the item is loose, so two babies can't grab the same one.</summary>
        public bool TryPickUp(BabyController holder, Transform socket, Vector3 localOffset, Vector3 localScale)
        {
            if (holder == null || !Status.TryClaim(holder.PlayerIndex))
                return false;
            AttachTo(holder, socket, localOffset, localScale);
            return true;
        }

        /// <summary>Round setup: put it on this baby no matter what.</summary>
        public void ForceHold(BabyController holder, Transform socket, Vector3 localOffset, Vector3 localScale)
        {
            Status.ForceHold(holder.PlayerIndex);
            AttachTo(holder, socket, localOffset, localScale);
        }

        /// <summary>Throw. The thrower has already let go of it (see BabyDiaperSlot / BabyHands).</summary>
        public bool Launch(BabyController thrower, Vector3 origin, Vector3 direction, float strength)
        {
            if (IsPuppet || thrower == null || !Status.TryThrow(thrower.PlayerIndex))
                return false;

            direction.y = 0f;
            direction = direction.sqrMagnitude < MinDirectionSqr ? thrower.transform.forward : direction.normalized;

            Holder = null;
            Thrower = thrower;
            flightTime = 0f;
            Detach(origin, Quaternion.LookRotation(direction, Vector3.up));

            ThrowableConfig settings = Settings;
            body.linearVelocity = direction * (settings.throwSpeed * Mathf.Max(0.1f, strength)) + Vector3.up * settings.throwArc;
            Vector3 spinAxis = settings.spinStyle == SpinStyle.Frisbee ? Vector3.up : Vector3.Cross(Vector3.up, direction);
            body.angularVelocity = spinAxis * (settings.spinSpeed * Mathf.Deg2Rad);
            SetTrail(true);
            Launched?.Invoke(this);
            return true;
        }

        /// <summary>Knocked out of a baby's hands: becomes a loose item with the given velocity.</summary>
        public void Drop(Vector3 position, Vector3 velocity)
        {
            if (IsPuppet)
                return;
            Status.MakeLoose();
            Holder = null;
            Thrower = null;
            flightTime = 0f;
            Detach(position, transform.rotation);
            body.linearVelocity = velocity;
            SetTrail(false);
            OnBecameLoose();
        }

        /// <summary>Puts an escaped item back into play as a loose item. Same object, no duplicates.</summary>
        public void RecoverTo(Vector3 position)
        {
            if (IsPuppet)
                return;
            PlaceLoose(position);
            Recovered?.Invoke(this);
        }

        /// <summary>Places the item, at rest and loose, at a position (spawning, recovering).</summary>
        protected void PlaceLoose(Vector3 position)
        {
            if (IsPuppet)
                return;
            Status.MakeLoose();
            Holder = null;
            Thrower = null;
            flightTime = 0f;
            Detach(position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            SetTrail(false);
            OnBecameLoose();
        }

        /// <summary>Called when a thrown item comes to rest.</summary>
        protected virtual void OnLanded() { }

        /// <summary>Called when the item is picked up or equipped.</summary>
        protected virtual void OnHeld() { }

        /// <summary>Called when the item becomes loose without being thrown (dropped, recovered, spawned).</summary>
        protected virtual void OnBecameLoose() { }

        void FixedUpdate()
        {
            if (IsPuppet || Status.State != ItemState.Flying)
                return;

            ThrowableConfig settings = Settings;
            flightTime += Time.fixedDeltaTime;
            if (!Mathf.Approximately(settings.gravityMultiplier, 1f))
                body.AddForce(Physics.gravity * (settings.gravityMultiplier - 1f), ForceMode.Acceleration);

            bool slow = body.linearVelocity.sqrMagnitude <= settings.settleSpeed * settings.settleSpeed;
            if ((Status.HasBounced && slow) || flightTime >= settings.maxFlightTime)
                Settle();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsPuppet || Status.State != ItemState.Flying)
                return;

            // Soft items: hitting the floor kills most horizontal speed instead of skidding across the arena.
            if (IsFloorContact(collision))
            {
                Vector3 v = body.linearVelocity;
                float keep = Settings.landingSpeedRetention;
                body.linearVelocity = new Vector3(v.x * keep, v.y, v.z * keep);
            }
            Status.RegisterBounce();
        }

        static bool IsFloorContact(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(collision.GetContact(i).normal.y) > FloorNormalMinY)
                    return true;
            }
            return false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPuppet || !Status.IsDangerous)
                return;

            Rigidbody otherBody = other.attachedRigidbody;
            if (otherBody == null || !otherBody.TryGetComponent(out BabyController baby) || baby.IsCrying)
                return;
            // No friendly fire.
            if (Thrower != null && Thrower.IsAlly(baby))
                return;
            // A dodging baby lets the throw sail through, and it stays live for anyone behind.
            if (baby.IsDodging)
            {
                if (baby != Thrower && baby.IsActiveInRound)
                    baby.NotifyDodged();
                return;
            }
            if (!Status.TryRegisterHit(baby.PlayerIndex, out bool isDirect))
                return;

            ThrowableConfig settings = Settings;
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 direction = horizontal.sqrMagnitude > MinDirectionSqr
                ? horizontal.normalized
                : baby.transform.position - transform.position;

            float strength = Mathf.Max(settings.minImpactStrength, horizontal.magnitude / settings.throwSpeed);
            var request = new HitRequest(
                isDirect ? settings.directHitCry : settings.bounceHitCry,
                isDirect ? settings.directHitKnockback : settings.bounceHitKnockback,
                strength);
            if (baby.ReceiveHit(request, direction, other.ClosestPoint(body.position), isDirect, Thrower))
                HitBaby?.Invoke(this, baby, isDirect);

            // Pop off the victim and drop nearby so they (or the thrower) can go for it.
            body.linearVelocity = -horizontal * settings.reboundHorizontalFactor + Vector3.up * settings.reboundUpSpeed;
            Status.RegisterBounce();
        }

        void Settle()
        {
            if (!Status.TrySettle())
                return;
            Thrower = null;
            SetTrail(false);
            OnLanded();
            Landed?.Invoke(this);
        }

        void AttachTo(BabyController holder, Transform socket, Vector3 localOffset, Vector3 localScale)
        {
            Holder = holder;
            Thrower = null;
            flightTime = 0f;
            SetPhysicsActive(false);
            transform.SetParent(socket, false);
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.Scale(baseScale, localScale);
            SetTrail(false);
            OnHeld();
        }

        void Detach(Vector3 position, Quaternion rotation)
        {
            transform.SetParent(looseParent, true);
            transform.localScale = baseScale;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            SetPhysicsActive(true);
        }

        void SetPhysicsActive(bool active)
        {
            if (active && IsPuppet)
            {
                // Puppets are placed by the host's snapshots, never by physics.
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            else if (active)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                ApplyIgnoredCollisions();
            }
            else
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.detectCollisions = false;
                // An interpolated body parented to a moving baby would lag behind it.
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        void ApplyIgnoredCollisions()
        {
            if (solidCollider == null)
                return;
            // Re-applied on every release: Unity can drop ignore pairs when a body's collision state changes.
            foreach (Collider babyCollider in ignoredColliders)
            {
                if (babyCollider != null)
                    Physics.IgnoreCollision(solidCollider, babyCollider, true);
            }
        }

        void SetTrail(bool emitting)
        {
            if (trail == null)
                return;
            if (emitting)
                trail.Clear();
            trail.emitting = emitting;
        }
    }
}
