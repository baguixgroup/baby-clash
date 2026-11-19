using System;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Gameplay hub for one baby. Turns intents (move / action / dash) into movement, pickups, throws and shoves,
    /// and receives hits. Human input, the CPU brain, animation, HUD and feedback all talk to the baby through this
    /// class and its events, so any of them can be swapped without touching gameplay.
    /// </summary>
    [RequireComponent(typeof(BabyMovement), typeof(BabyDiaperSlot), typeof(BabyHands))]
    public sealed class BabyController : MonoBehaviour
    {
        const int ReachBufferSize = 16;
        const int AimBufferSize = 32;
        const float MinDirectionSqr = 0.0001f;
        const float ShoveRecoilSpeed = 2f;
        const float CryDropSpeed = 2.5f;

        [SerializeField] BabyConfig config;

        [Tooltip("0-based player slot. Also used as the baby's id in item ownership.")]
        [SerializeField] int playerIndex;

        [SerializeField] string displayName = "BABY 1";
        [SerializeField] Color playerColor = new Color(0.25f, 0.55f, 1f);

        [Header("Throw release point (prefab geometry)")]
        [Tooltip("Height above the feet where thrown diapers are released (m).")]
        [SerializeField] float throwReleaseHeight = 0.8f;

        [Tooltip("Distance in front of the baby where thrown items are released (m).")]
        [SerializeField] float throwReleaseDistance = 0.65f;

        [Tooltip("If a wall is in the way, the release point is pulled back this far from it (m).")]
        [SerializeField] float throwReleaseWallGap = 0.25f;

        readonly Collider[] reachBuffer = new Collider[ReachBufferSize];
        readonly Collider[] aimBuffer = new Collider[AimBufferSize];
        BabyMovement movement;
        BabyDiaperSlot diapers;
        BabyHands hands;
        Collider bodyCollider;
        bool activeInRound;
        bool shoveUsed;

        public int PlayerIndex => playerIndex;
        public string DisplayName => displayName;
        public Color PlayerColor => playerColor;
        public BabyConfig Config => config;
        public CryMeter Cry { get; private set; }
        public BabyStatus Status { get; } = new BabyStatus();
        public BabyMovement Movement => movement;
        public BabyDiaperSlot Diapers => diapers;
        public BabyHands Hands => hands;
        public Collider BodyCollider => bodyCollider;

        /// <summary>False during countdown and after the round ends: no acting and no taking hits.</summary>
        public bool IsActiveInRound => activeInRound;

        public bool IsProtected => diapers.IsProtected;
        public bool IsCrying => Status.IsCrying;

        /// <summary>In the dodge window of a dash: thrown items pass straight through.</summary>
        public bool IsDodging => Status.IsDodging(config.dashDodgeWindow);

        public BabyState State => Status.ToBabyState(IsProtected);

        /// <summary>Scales top speed (the CPU walks slower on easy). 1 = normal.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>Raised on every action press, even when the baby can't act (menus use it to advance).</summary>
        public event Action<BabyController> ActionPressed;
        public event Action<BabyController, BabyHitReport> HitReceived;
        public event Action<BabyController, Throwable> ThrewItem;
        public event Action<BabyController, Throwable> PickedUpItem;
        public event Action<BabyController> Dashed;

        /// <summary>This baby dashed into another one (shover, victim).</summary>
        public event Action<BabyController, BabyController> Shoved;
        public event Action<BabyController> StartedCrying;
        public event Action<BabyController> RoundReset;
        public event Action<BabyController> Celebrated;

        /// <summary>Scene/prefab wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(BabyConfig babyConfig, int index, string babyName, Color color)
        {
            config = babyConfig;
            playerIndex = index;
            displayName = babyName;
            playerColor = color;
        }

        void Awake()
        {
            movement = GetComponent<BabyMovement>();
            diapers = GetComponent<BabyDiaperSlot>();
            hands = GetComponent<BabyHands>();
            bodyCollider = GetComponent<Collider>();

            if (config == null)
            {
                Debug.LogError($"{name}: no BabyConfig assigned, using defaults.", this);
                config = ScriptableObject.CreateInstance<BabyConfig>();
            }

            Cry = new CryMeter(config.maxCry);
            Cry.Filled += HandleCryFilled;
            movement.Initialize(config);
            diapers.Initialize(this, config);
            hands.Initialize(this);
        }

        void OnDestroy()
        {
            if (Cry != null)
                Cry.Filled -= HandleCryFilled;
        }

        void Update()
        {
            Status.Tick(Time.deltaTime);

            MovementMode mode;
            if (Status.IsDashing)
                mode = MovementMode.Dashing;
            else if (activeInRound && Status.CanMove)
                mode = MovementMode.Free;
            else
                mode = MovementMode.Sliding;
            float speed = SpeedScale * (Status.Activity == BabyActivity.Throwing ? config.throwingMoveMultiplier : 1f);
            movement.SetMode(mode, speed);
        }

        public void SetMoveInput(Vector2 input) => movement.SetMoveInput(input);

        /// <summary>
        /// The one contextual button. In order: grab a diaper if you have none and one is in reach; throw the toy
        /// you're holding; grab a toy in reach; throw your diaper.
        /// </summary>
        public void PressAction()
        {
            // Sampled first: a press that advances a menu must not also throw in the fresh round.
            bool couldAct = activeInRound;
            ActionPressed?.Invoke(this);
            if (!couldAct)
                return;

            if (diapers.HasRoom && TryPickUp(true))
                return;
            if (hands.IsHolding)
            {
                TryThrow();
                return;
            }
            if (!TryPickUp(false))
                TryThrow();
        }

        /// <summary>Picks up the closest diaper (if there's room) or else toy within reach.</summary>
        public bool TryPickUp(bool diapersOnly = false)
        {
            if (!activeInRound || !Status.CanAct)
                return false;

            bool wantDiaper = diapers.HasRoom;
            bool wantToy = !diapersOnly && !hands.IsHolding;
            if (!wantDiaper && !wantToy)
                return false;

            Vector3 center = diapers.Socket.position;
            int count = Physics.OverlapSphereNonAlloc(center, config.pickupRadius, reachBuffer,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            Diaper nearestDiaper = null;
            Toy nearestToy = null;
            float diaperDistance = float.MaxValue;
            float toyDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = reachBuffer[i].attachedRigidbody;
                reachBuffer[i] = null;
                if (body == null)
                    continue;

                float distance = (body.position - center).sqrMagnitude;
                if (wantDiaper && body.TryGetComponent(out Diaper diaper) && diaper.CanBePickedUp && distance < diaperDistance)
                {
                    nearestDiaper = diaper;
                    diaperDistance = distance;
                }
                else if (wantToy && body.TryGetComponent(out Toy toy) && toy.CanBePickedUp && distance < toyDistance)
                {
                    nearestToy = toy;
                    toyDistance = distance;
                }
            }

            Throwable picked = null;
            if (nearestDiaper != null && diapers.TryEquip(nearestDiaper))
                picked = nearestDiaper;
            else if (nearestToy != null && hands.TryPickUp(nearestToy))
                picked = nearestToy;
            if (picked == null)
                return false;

            Status.StartCooldown(config.actionCooldown);
            PickedUpItem?.Invoke(this, picked);
            return true;
        }

        /// <summary>Throws the held toy, or else the outermost worn diaper, where the baby faces (with aim assist).</summary>
        public bool TryThrow() => Throw(null);

        /// <summary>Throws in an exact direction, without aim assist (the CPU aims for itself).</summary>
        public bool TryThrowToward(Vector3 direction) => Throw(Flatten(direction, movement.Facing));

        bool Throw(Vector3? aim)
        {
            if (!activeInRound || (!hands.IsHolding && diapers.Count == 0))
                return false;
            if (!Status.TryBeginThrow(config.throwDuration, config.actionCooldown))
                return false;

            Vector3 direction = aim ?? ResolveThrowDirection();
            movement.FaceInstantly(direction);

            Throwable item;
            Vector3 origin;
            if (hands.IsHolding)
            {
                // Toys go from the overhead hold point, forward.
                item = hands.TakeForThrow();
                origin = ClearRelease(hands.Socket.position + direction * (throwReleaseDistance * 0.5f), direction);
            }
            else
            {
                item = diapers.TakeForThrow();
                origin = ClearRelease(transform.position + Vector3.up * throwReleaseHeight + direction * throwReleaseDistance, direction);
            }

            item.Launch(this, origin, direction, config.throwStrength);
            ThrewItem?.Invoke(this, item);
            return true;
        }

        /// <summary>Belly-slide dash in the stick direction (or facing). Bumping into a baby shoves it.</summary>
        public bool PressDash()
        {
            if (!activeInRound || !Status.TryBeginDash(config.dashDuration, config.dashCooldown))
                return false;

            Vector3 direction = movement.InputDirection;
            if (direction == Vector3.zero)
                direction = movement.Facing;
            movement.BeginDash(direction * config.dashSpeed);
            shoveUsed = false;
            Dashed?.Invoke(this);
            return true;
        }

        /// <summary>Applies a thrown item's hit using this baby's current defense. False if the hit was ignored.</summary>
        public bool ReceiveHit(in HitRequest request, Vector3 direction, Vector3 point, bool isDirectHit)
        {
            if (IsDodging)
                return false;
            return ApplyHit(request, direction, point, isDirectHit, HitSource.Thrown);
        }

        /// <summary>A dashing baby bumped into this one: knockback, a little Cry, and the held toy flies loose.</summary>
        public bool ReceiveShove(BabyController shover, Vector3 direction)
        {
            if (!activeInRound || Status.IsCrying)
                return false;

            BabyConfig shoverConfig = shover.Config;
            direction = Flatten(direction, -movement.Facing);
            hands.DropHeld((direction + Vector3.up) * shoverConfig.shoveDropSpeed);
            var request = new HitRequest(shoverConfig.shoveCry, shoverConfig.shoveKnockback, 1f);
            Vector3 point = bodyCollider != null ? bodyCollider.ClosestPoint(shover.transform.position) : transform.position;
            return ApplyHit(request, direction, point, false, HitSource.Shove);
        }

        public void SetActiveInRound(bool active)
        {
            activeInRound = active;
            if (!active)
                movement.SetMoveInput(Vector2.zero);
        }

        /// <summary>Back to a fresh baby at the given spawn. Items are forgotten here and re-handed by the round manager.</summary>
        public void ResetForRound(Vector3 position, Quaternion rotation)
        {
            SetActiveInRound(false);
            Status.Reset();
            Cry.Reset();
            diapers.ForgetAll();
            hands.ForgetHeld();
            shoveUsed = false;
            movement.Teleport(position, rotation);
            RoundReset?.Invoke(this);
        }

        public void Celebrate() => Celebrated?.Invoke(this);

        /// <summary>Cries without a full meter (lost on time). The round is already decided when this is called.</summary>
        public void BurstIntoTears()
        {
            if (!Status.IsCrying)
                HandleCryFilled();
        }

        bool ApplyHit(in HitRequest request, Vector3 direction, Vector3 point, bool isDirectHit, HitSource source)
        {
            if (!activeInRound || Status.IsCrying)
                return false;

            bool wasProtected = IsProtected;
            HitOutcome outcome = HitResolver.Resolve(request, config.GetDefense(wasProtected), config.knockbackResistance);
            direction = Flatten(direction, -movement.Facing);

            // May fill the meter, which starts crying (see HandleCryFilled) before the knockback lands. Intended:
            // the final hit still sends the loser flying.
            float cryAdded = Cry.Add(outcome.Cry);
            movement.ApplyKnockback(direction * outcome.KnockbackSpeed, outcome.KnockbackSpeed * config.knockbackLift);
            if (!Status.IsCrying)
                Status.ApplyStun(outcome.StunDuration);

            HitReceived?.Invoke(this, new BabyHitReport(point, direction, cryAdded, outcome.KnockbackSpeed,
                wasProtected, isDirectHit, source));
            return true;
        }

        void OnCollisionEnter(Collision collision) => TryShove(collision);

        void OnCollisionStay(Collision collision) => TryShove(collision);

        void TryShove(Collision collision)
        {
            if (!Status.IsDashing || shoveUsed || !activeInRound)
                return;
            Rigidbody otherBody = collision.rigidbody;
            if (otherBody == null || !otherBody.TryGetComponent(out BabyController victim) || victim == this)
                return;

            shoveUsed = true;
            Vector3 direction = Flatten(victim.transform.position - transform.position, movement.Facing);
            Status.EndDash();
            movement.ApplyKnockback(-direction * ShoveRecoilSpeed, 0f);
            if (victim.ReceiveShove(this, direction))
                Shoved?.Invoke(this, victim);
        }

        void HandleCryFilled()
        {
            Status.StartCrying();
            movement.SetMoveInput(Vector2.zero);
            hands.DropHeld(Vector3.up * CryDropSpeed);
            StartedCrying?.Invoke(this);
        }

        /// <summary>Facing direction, nudged toward the opponent closest to it within the aim-assist cone.</summary>
        Vector3 ResolveThrowDirection()
        {
            Vector3 facing = movement.Facing;
            if (config.aimAssistAngle <= 0f || config.aimAssistRange <= 0f)
                return facing;

            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, config.aimAssistRange, aimBuffer,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);

            Vector3 best = facing;
            float bestAngle = config.aimAssistAngle;
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = aimBuffer[i].attachedRigidbody;
                aimBuffer[i] = null;
                if (body == null || !body.TryGetComponent(out BabyController other) || other == this || other.IsCrying)
                    continue;

                Vector3 toOther = other.transform.position - origin;
                toOther.y = 0f;
                if (toOther.sqrMagnitude < MinDirectionSqr)
                    continue;

                float angle = Vector3.Angle(facing, toOther);
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    best = toOther.normalized;
                }
            }
            return best;
        }

        /// <summary>Pulls a release point back if it would be inside a wall the baby is hugging.</summary>
        Vector3 ClearRelease(Vector3 release, Vector3 direction)
        {
            Vector3 from = new Vector3(transform.position.x, release.y, transform.position.z);
            float distance = Vector3.Distance(from, release);
            if (distance > 0.001f
                && Physics.Raycast(from, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
            {
                release = hit.point - direction * throwReleaseWallGap;
            }
            return release;
        }

        static Vector3 Flatten(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < MinDirectionSqr ? fallback : direction.normalized;
        }
    }
}
