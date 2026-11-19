using UnityEngine;

namespace BabyClash
{
    public enum MovementMode
    {
        /// <summary>Steering with the stick.</summary>
        Free,

        /// <summary>No control: stunned, crying or frozen between rounds. Slides out any knockback.</summary>
        Sliding,

        /// <summary>Belly-slide dash: keeps its launch velocity, slowly decelerating.</summary>
        Dashing,
    }

    /// <summary>
    /// Rigidbody locomotion: velocity-based running, facing, dashes and knockback slides.
    /// Doesn't decide *whether* the baby may move; <see cref="BabyController"/> tells it via <see cref="SetMode"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BabyMovement : MonoBehaviour
    {
        const float InputDeadZoneSqr = 0.01f;
        const float MinDirectionSqr = 0.0001f;

        Rigidbody body;
        BabyConfig config;
        Vector2 moveInput;
        MovementMode mode = MovementMode.Sliding;
        float speedMultiplier = 1f;
        Vector3 facing = Vector3.forward;

        /// <summary>Normalized horizontal facing direction. Throws go this way (plus aim assist).</summary>
        public Vector3 Facing => facing;
        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
        public Vector2 MoveInput => moveInput;
        public MovementMode Mode => mode;

        /// <summary>Stick direction on the ground plane, or zero when released.</summary>
        public Vector3 InputDirection
        {
            get
            {
                var input = new Vector3(moveInput.x, 0f, moveInput.y);
                return input.sqrMagnitude > InputDeadZoneSqr ? input.normalized : Vector3.zero;
            }
        }

        /// <summary>0..1 horizontal speed relative to top speed, for animation.</summary>
        public float NormalizedSpeed
        {
            get
            {
                if (config == null || config.moveSpeed <= 0f)
                    return 0f;
                Vector3 v = Velocity;
                return Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / config.moveSpeed);
            }
        }

        public void Initialize(BabyConfig babyConfig) => config = babyConfig;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            facing = Flatten(transform.forward, Vector3.forward);
        }

        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);

        public void SetMode(MovementMode movementMode, float multiplier)
        {
            mode = movementMode;
            speedMultiplier = multiplier;
        }

        public void BeginDash(Vector3 velocity)
        {
            FaceInstantly(velocity);
            Vector3 v = body.linearVelocity;
            body.linearVelocity = new Vector3(velocity.x, v.y, velocity.z);
            mode = MovementMode.Dashing;
        }

        /// <summary>Replaces the horizontal velocity (predictable, doesn't stack with running speed) and adds a hop.</summary>
        public void ApplyKnockback(Vector3 horizontalVelocity, float lift)
        {
            Vector3 v = body.linearVelocity;
            body.linearVelocity = new Vector3(horizontalVelocity.x, Mathf.Max(v.y, lift), horizontalVelocity.z);
        }

        public void FaceInstantly(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqr)
                return;

            facing = direction.normalized;
            Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
            body.rotation = rotation;
            transform.rotation = rotation;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            moveInput = Vector2.zero;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            facing = Flatten(rotation * Vector3.forward, facing);
        }

        void FixedUpdate()
        {
            if (config == null)
                return;

            float dt = Time.fixedDeltaTime;
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
            bool steering = mode == MovementMode.Free && input.sqrMagnitude > InputDeadZoneSqr;

            Vector3 target = Vector3.zero;
            float rate;
            switch (mode)
            {
                case MovementMode.Free:
                    target = input * (config.moveSpeed * speedMultiplier);
                    rate = steering ? config.acceleration : config.deceleration;
                    break;
                case MovementMode.Dashing:
                    rate = config.dashDeceleration;
                    break;
                default:
                    rate = config.knockbackDeceleration;
                    break;
            }

            horizontal = Vector3.MoveTowards(horizontal, target, rate * dt);
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

            if (steering)
                facing = input.normalized;

            Quaternion goal = Quaternion.LookRotation(facing, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, goal, config.turnSpeed * dt));
        }

        static Vector3 Flatten(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < MinDirectionSqr ? fallback : direction.normalized;
        }
    }
}
