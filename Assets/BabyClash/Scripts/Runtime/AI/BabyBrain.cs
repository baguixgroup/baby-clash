using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// CPU player. Drives a <see cref="BabyController"/> through the same calls as human input (move, action, dash),
    /// so it follows every rule a player does. Priorities: get a diaper when diaperless, dodge incoming throws,
    /// grab toys, throw (with lead on harder settings), shove when close, otherwise strafe at a comfortable range.
    /// </summary>
    [RequireComponent(typeof(BabyController))]
    public sealed class BabyBrain : MonoBehaviour
    {
        enum Intent
        {
            Reposition,
            GetDiaper,
            GetToy,
            GetPowerUp,
            Attack,
        }

        const float ChestHeight = 0.8f;
        const float AimTurnTolerance = 50f;

        [SerializeField] CpuConfig config;
        [SerializeField] CpuDifficulty difficulty = CpuDifficulty.Normal;
        [SerializeField] RoundManager roundManager;

        [Tooltip("Farthest distance the CPU throws from (m).")]
        [SerializeField] float throwRange = 8.5f;

        [Tooltip("Distance the CPU likes to keep when it has nothing better to do (m).")]
        [SerializeField] float preferredDistance = 5.5f;

        [Tooltip("Dash-shove when the opponent is this close (m).")]
        [SerializeField] float shoveRange = 2.3f;

        [Tooltip("An incoming item passing within this distance is a threat (m).")]
        [SerializeField] float dangerRadius = 1.3f;

        [Tooltip("Only dodge items arriving within this many seconds.")]
        [SerializeField] float dangerTime = 0.5f;

        [Tooltip("Obstacle look-ahead for steering (m).")]
        [SerializeField] float avoidDistance = 1.4f;

        [Tooltip("Toys farther than this aren't worth walking to (m).")]
        [SerializeField] float toyInterestRange = 11f;

        BabyController self;
        BabyController opponent;
        Intent intent;
        PowerUpPickup targetPowerUp;
        Throwable targetItem;
        float decisionTimer;
        float strafeSign = 1f;
        Throwable lastThreat;
        float dodgeTimer = -1f;
        Vector3 dodgeDirection;
        float stuckTimer;

        public CpuDifficulty Difficulty
        {
            get => difficulty;
            set => difficulty = value;
        }

        /// <summary>Toddler mode: slow, forgiving and never dashes.</summary>
        public bool Gentle { get; set; }

        static readonly CpuSettings gentle = new CpuSettings(0.55f, 14f, 0f, 0.3f, 0f, 0.65f);

        /// <summary>Scene wiring (editor tooling and tests).</summary>
        public void Configure(CpuConfig cpuConfig, RoundManager manager, CpuDifficulty level)
        {
            config = cpuConfig;
            roundManager = manager;
            difficulty = level;
        }

        void Awake()
        {
            self = GetComponent<BabyController>();
            if (config == null)
                config = ScriptableObject.CreateInstance<CpuConfig>();
        }

        void OnDisable()
        {
            if (self != null)
            {
                self.SetMoveInput(Vector2.zero);
                self.SpeedScale = 1f;
            }
        }

        void Update()
        {
            if (!self.IsActiveInRound || roundManager == null || Time.timeScale <= 0f)
            {
                self.SetMoveInput(Vector2.zero);
                return;
            }

            CpuSettings settings = Gentle ? gentle : config.Get(difficulty);
            self.SpeedScale = settings.speedMultiplier;
            opponent = FindOpponent();

            if (HandleDodge(settings))
                return;

            decisionTimer -= Time.deltaTime;
            if (decisionTimer <= 0f)
            {
                decisionTimer = settings.reactionTime * Random.Range(0.8f, 1.3f);
                Decide(settings);
            }
            Act(settings);
        }

        void Decide(CpuSettings settings)
        {
            targetItem = null;
            if (Random.value < 0.25f)
                strafeSign = -strafeSign;

            if (!self.IsProtected)
            {
                Diaper diaper = NearestLooseDiaper();
                if (diaper != null)
                {
                    intent = Intent.GetDiaper;
                    targetItem = diaper;
                    return;
                }
            }

            if (self.Hands.IsHolding)
            {
                intent = Intent.Attack;
                return;
            }

            // King of the Bottle: the ring comes first most of the time.
            BottleZone bottle = BottleZone.Active;
            if (bottle != null && !bottle.Contains(transform.position) && Random.value < 0.6f)
            {
                intent = Intent.Reposition;
                return;
            }

            targetPowerUp = PowerUpPickup.Nearest(transform.position, toyInterestRange);
            if (targetPowerUp != null && Random.value < 0.7f)
            {
                intent = Intent.GetPowerUp;
                return;
            }

            Toy toy = NearestLooseToy();
            if (toy != null)
            {
                intent = Intent.GetToy;
                targetItem = toy;
                return;
            }

            if (self.IsProtected && opponent != null)
            {
                // Throwing the diaper is a gamble; more tempting when the opponent is exposed.
                float temptation = settings.diaperThrowWillingness * (opponent.IsProtected ? 0.35f : 1f);
                if (Random.value < temptation)
                {
                    intent = Intent.Attack;
                    return;
                }
            }

            intent = Intent.Reposition;
        }

        void Act(CpuSettings settings)
        {
            Vector3 position = transform.position;

            if (opponent != null && self.Status.CanDash && Random.value < settings.shoveChance * Time.deltaTime * 3f)
            {
                Vector3 toOpponent = Flat(opponent.transform.position - position);
                if (toOpponent.magnitude < shoveRange && !opponent.IsCrying)
                {
                    Steer(toOpponent.normalized, 1f);
                    self.PressDash();
                    return;
                }
            }

            switch (intent)
            {
                case Intent.GetDiaper:
                case Intent.GetToy:
                    if (targetItem == null || !targetItem.CanBePickedUp)
                    {
                        decisionTimer = 0f;
                        break;
                    }
                    Vector3 toItem = Flat(targetItem.transform.position - position);
                    if (toItem.magnitude < self.Config.pickupRadius * 0.75f)
                    {
                        self.PressAction();
                        decisionTimer = 0f;
                    }
                    Steer(toItem, 1f);
                    break;

                case Intent.GetPowerUp:
                    if (targetPowerUp == null || !targetPowerUp.isActiveAndEnabled)
                        decisionTimer = 0f;
                    else
                        Steer(Flat(targetPowerUp.transform.position - position), 1f);
                    break;

                case Intent.Attack:
                    Attack(settings);
                    break;

                default:
                    Reposition();
                    break;
            }
        }

        void Attack(CpuSettings settings)
        {
            if (opponent == null || (!self.Hands.IsHolding && !self.IsProtected))
            {
                decisionTimer = 0f;
                return;
            }

            Vector3 position = transform.position;
            Vector3 toOpponent = Flat(opponent.transform.position - position);
            float distance = toOpponent.magnitude;
            if (distance > throwRange)
            {
                Steer(toOpponent, 1f);
                return;
            }
            if (!HasLineOfSight(opponent))
            {
                // Sidestep around whatever is in the way.
                Steer(Vector3.Cross(Vector3.up, toOpponent).normalized * strafeSign + toOpponent.normalized * 0.3f, 1f);
                return;
            }

            // Turn toward the target first (walking slowly), then release.
            Vector3 aim = AimAt(opponent, settings);
            if (Vector3.Angle(self.Movement.Facing, aim) > AimTurnTolerance)
            {
                Steer(aim, 0.25f);
                return;
            }
            self.TryThrowToward(aim);
            decisionTimer = 0f;
        }

        Vector3 AimAt(BabyController target, CpuSettings settings)
        {
            Vector3 from = transform.position;
            Vector3 targetPosition = target.transform.position;
            // Harder CPUs lead moving targets.
            float lead = Mathf.InverseLerp(20f, 4f, settings.aimError);
            float flightTime = Flat(targetPosition - from).magnitude / 15f;
            targetPosition += Flat(target.Movement.Velocity) * (flightTime * lead);
            Vector3 aim = Flat(targetPosition - from).normalized;
            return Quaternion.Euler(0f, Random.Range(-settings.aimError, settings.aimError), 0f) * aim;
        }

        void Reposition()
        {
            if (opponent == null)
            {
                Steer(Flat(-transform.position), 0.6f);
                return;
            }

            Vector3 position = transform.position;
            BottleZone bottle = BottleZone.Active;
            if (bottle != null && !bottle.Contains(position))
            {
                Steer(Flat(bottle.transform.position - position), 1f);
                return;
            }
            Vector3 fromOpponent = Flat(position - opponent.transform.position);
            float distance = fromOpponent.magnitude;
            Vector3 away = distance > 0.01f ? fromOpponent / distance : Vector3.right;
            Vector3 side = Vector3.Cross(Vector3.up, away) * strafeSign;
            float closeIn = Mathf.Clamp((distance - preferredDistance) / preferredDistance, -1f, 1f);
            Steer(side * 0.8f - away * closeIn, 0.7f);
        }

        bool HandleDodge(CpuSettings settings)
        {
            if (dodgeTimer >= 0f)
            {
                dodgeTimer -= Time.deltaTime;
                if (dodgeTimer < 0f)
                {
                    Steer(dodgeDirection, 1f);
                    self.PressDash();
                }
                else
                {
                    Steer(dodgeDirection, 1f);
                }
                return true;
            }

            Throwable threat = FindThreat(out Vector3 threatVelocity);
            if (threat == null || threat == lastThreat)
                return false;

            lastThreat = threat;
            if (!self.Status.CanDash || Random.value > settings.dodgeChance)
                return false;

            // Dash sideways out of the item's path, after a human-ish reaction delay.
            Vector3 side = Vector3.Cross(Vector3.up, threatVelocity).normalized;
            Vector3 offset = Flat(transform.position - threat.transform.position);
            dodgeDirection = Vector3.Dot(side, offset) >= 0f ? side : -side;
            dodgeTimer = settings.reactionTime * 0.4f;
            return true;
        }

        Throwable FindThreat(out Vector3 threatVelocity)
        {
            threatVelocity = Vector3.zero;
            foreach (Diaper diaper in roundManager.Diapers)
            {
                if (IsThreat(diaper, ref threatVelocity))
                    return diaper;
            }
            if (roundManager.Toys == null)
                return null;
            foreach (Toy toy in roundManager.Toys.Pool)
            {
                if (IsThreat(toy, ref threatVelocity))
                    return toy;
            }
            return null;
        }

        bool IsThreat(Throwable item, ref Vector3 threatVelocity)
        {
            if (item == null || !item.isActiveAndEnabled || item.State != ItemState.Flying || !item.Status.IsDangerous
                || item.Thrower == self)
                return false;

            Vector3 velocity = Flat(item.Body.linearVelocity);
            float speedSqr = velocity.sqrMagnitude;
            if (speedSqr < 1f)
                return false;
            Vector3 relative = Flat(transform.position - item.transform.position);
            float timeToClosest = Vector3.Dot(relative, velocity) / speedSqr;
            if (timeToClosest <= 0f || timeToClosest > dangerTime || (relative - velocity * timeToClosest).magnitude >= dangerRadius)
                return false;
            threatVelocity = velocity;
            return true;
        }

        void Steer(Vector3 desired, float urgency)
        {
            desired = Flat(desired);
            if (desired.sqrMagnitude < 0.0001f)
            {
                self.SetMoveInput(Vector2.zero);
                return;
            }
            desired.Normalize();

            // Keep away from the arena edge and slide around obstacles.
            Vector3 position = transform.position;
            desired = AvoidObstacles(position, desired);

            if (self.Movement.Velocity.sqrMagnitude < 0.2f && urgency > 0.5f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 0.8f)
                {
                    strafeSign = -strafeSign;
                    desired = Quaternion.Euler(0f, 90f * strafeSign, 0f) * desired;
                    if (stuckTimer > 1.4f)
                        stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            Vector3 input = desired * Mathf.Clamp01(urgency);
            self.SetMoveInput(new Vector2(input.x, input.z));
        }

        Vector3 AvoidObstacles(Vector3 position, Vector3 desired)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (!Blocked(origin, desired))
                return desired;
            for (int step = 1; step <= 3; step++)
            {
                float angle = 35f * step;
                Vector3 left = Quaternion.Euler(0f, -angle * strafeSign, 0f) * desired;
                if (!Blocked(origin, left))
                    return left;
                Vector3 right = Quaternion.Euler(0f, angle * strafeSign, 0f) * desired;
                if (!Blocked(origin, right))
                    return right;
            }
            return desired;
        }

        bool Blocked(Vector3 origin, Vector3 direction)
        {
            if (!Physics.SphereCast(origin, 0.3f, direction, out RaycastHit hit, avoidDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            // Other babies aren't walls.
            return hit.rigidbody == null || !hit.rigidbody.TryGetComponent(out BabyController _);
        }

        bool HasLineOfSight(BabyController target)
        {
            Vector3 from = transform.position + Vector3.up * ChestHeight;
            Vector3 to = target.transform.position + Vector3.up * ChestHeight;
            Vector3 delta = to - from;
            if (!Physics.Raycast(from, delta.normalized, out RaycastHit hit, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.rigidbody != null && (hit.rigidbody.transform == target.transform || hit.rigidbody.transform == transform);
        }

        BabyController FindOpponent()
        {
            BabyController best = null;
            float bestDistance = float.MaxValue;
            foreach (BabyController baby in roundManager.Babies)
            {
                if (baby == null || baby == self || baby.IsCrying || self.IsAlly(baby) || !baby.isActiveAndEnabled)
                    continue;
                float distance = (baby.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = baby;
                    bestDistance = distance;
                }
            }
            return best;
        }

        Diaper NearestLooseDiaper()
        {
            Diaper best = null;
            float bestScore = float.MaxValue;
            foreach (Diaper diaper in roundManager.Diapers)
            {
                if (diaper == null || !diaper.CanBePickedUp)
                    continue;
                float score = Flat(diaper.transform.position - transform.position).magnitude;
                if (score < bestScore)
                {
                    best = diaper;
                    bestScore = score;
                }
            }
            return best;
        }

        Toy NearestLooseToy()
        {
            if (roundManager.Toys == null)
                return null;
            Toy best = null;
            float bestScore = toyInterestRange;
            foreach (Toy toy in roundManager.Toys.Pool)
            {
                if (toy == null || !toy.CanBePickedUp)
                    continue;
                float mine = Flat(toy.transform.position - transform.position).magnitude;
                // Don't race for toys the opponent is standing on.
                float theirs = opponent != null ? Flat(toy.transform.position - opponent.transform.position).magnitude : float.MaxValue;
                if (theirs < 1f && mine > 2f)
                    continue;
                if (mine < bestScore)
                {
                    best = toy;
                    bestScore = mine;
                }
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
