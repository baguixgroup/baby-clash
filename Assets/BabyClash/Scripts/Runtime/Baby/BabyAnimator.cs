using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Procedural animation for the sculpted baby rig: waddle walk, holding toys on the head, throws, pickups,
    /// belly-slide dashes, hit flinches, dizzy stars, sit-down crying and victory hops, plus squash & stretch,
    /// facial expressions and per-player colours. Reads the controller's state and events; never affects gameplay,
    /// so an authored Animator rig can replace it later.
    /// </summary>
    public sealed class BabyAnimator : MonoBehaviour
    {
        static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        const float ThrowDuration = 0.4f;
        const float PickupDuration = 0.3f;
        const float HitDuration = 0.35f;
        const float GloatDuration = 0.8f;

        [SerializeField] BabyController baby;
        [SerializeField] BabyFace face;

        [Header("Rig")]
        [Tooltip("Root of the visual rig; squashed and stretched as a whole.")]
        [SerializeField] Transform body;
        [SerializeField] Transform hips;
        [SerializeField] Transform chest;
        [SerializeField] Transform head;
        [SerializeField] Transform armLeft;
        [SerializeField] Transform armRight;
        [SerializeField] Transform legLeft;
        [SerializeField] Transform legRight;

        [Header("Looks")]
        [SerializeField] Renderer skin;
        [SerializeField] Renderer[] hair = new Renderer[0];

        [Tooltip("Multiplies the sculpted skin colours (skin tone).")]
        [SerializeField] Color skinTint = Color.white;
        [SerializeField] Color hairColor = new Color(0.36f, 0.22f, 0.12f);

        [Tooltip("Ring under the baby: player colour while protected, pulsing warning colour while diaperless.")]
        [SerializeField] Renderer protectionRing;
        [SerializeField] Color diaperlessRingColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] ParticleSystem tears;
        [SerializeField] Transform dizzyStars;

        [Header("Motion")]
        [SerializeField] float strideCyclesPerSecond = 1.9f;
        [SerializeField] float strideAngle = 34f;
        [SerializeField] float armSwingAngle = 30f;
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float waddleRoll = 7f;
        [SerializeField] float runLean = 10f;
        [SerializeField] float poseSharpness = 16f;

        [Header("Squash & stretch")]
        [SerializeField] float springStiffness = 240f;
        [SerializeField] float springDamping = 15f;

        MaterialPropertyBlock block;
        Vector3 hipsRest;
        float walkPhase;
        float throwTimer;
        bool throwWasToy;
        float pickupTimer;
        float hitTimer;
        float gloatTimer;
        bool celebrating;
        float squash;
        float squashVelocity;

        /// <summary>Prefab wiring (editor tooling).</summary>
        public void Configure(BabyController owner, BabyFace babyFace, Transform root, Transform hipsBone, Transform chestBone,
            Transform headBone, Transform leftArm, Transform rightArm, Transform leftLeg, Transform rightLeg,
            Renderer skinRenderer, Renderer[] hairRenderers, Renderer ring, ParticleSystem tearsEffect, Transform stars)
        {
            baby = owner;
            face = babyFace;
            body = root;
            hips = hipsBone;
            chest = chestBone;
            head = headBone;
            armLeft = leftArm;
            armRight = rightArm;
            legLeft = leftLeg;
            legRight = rightLeg;
            skin = skinRenderer;
            hair = hairRenderers;
            protectionRing = ring;
            tears = tearsEffect;
            dizzyStars = stars;
        }

        /// <summary>Per-instance appearance (editor tooling).</summary>
        public void ConfigureLooks(Color skinTone, Color hairTone)
        {
            skinTint = skinTone;
            hairColor = hairTone;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (baby == null)
                baby = GetComponentInParent<BabyController>();
            hipsRest = hips.localPosition;
            if (dizzyStars != null)
                dizzyStars.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (baby == null)
                return;
            baby.HitReceived += HandleHit;
            baby.ThrewItem += HandleThrew;
            baby.PickedUpItem += HandlePickedUp;
            baby.Dashed += HandleDashed;
            baby.Shoved += HandleShoved;
            baby.StartedCrying += HandleStartedCrying;
            baby.RoundReset += HandleRoundReset;
            baby.Celebrated += HandleCelebrated;
        }

        void OnDisable()
        {
            if (baby == null)
                return;
            baby.HitReceived -= HandleHit;
            baby.ThrewItem -= HandleThrew;
            baby.PickedUpItem -= HandlePickedUp;
            baby.Dashed -= HandleDashed;
            baby.Shoved -= HandleShoved;
            baby.StartedCrying -= HandleStartedCrying;
            baby.RoundReset -= HandleRoundReset;
            baby.Celebrated -= HandleCelebrated;
        }

        void Start()
        {
            if (baby == null)
                return;
            if (skin != null)
            {
                skin.GetPropertyBlock(block);
                block.SetColor(AccentColorId, baby.PlayerColor);
                block.SetColor(BaseColorId, skinTint);
                skin.SetPropertyBlock(block);
            }
            foreach (Renderer strand in hair)
                SetColor(strand, hairColor);
        }

        void LateUpdate()
        {
            if (baby == null)
                return;

            float dt = Time.deltaTime;
            float t = Time.time;
            throwTimer = Mathf.Max(0f, throwTimer - dt);
            pickupTimer = Mathf.Max(0f, pickupTimer - dt);
            hitTimer = Mathf.Max(0f, hitTimer - dt);
            gloatTimer = Mathf.Max(0f, gloatTimer - dt);

            BabyActivity activity = baby.Status.Activity;
            float speed = activity == BabyActivity.Ready || activity == BabyActivity.Throwing ? baby.Movement.NormalizedSpeed : 0f;
            walkPhase += dt * strideCyclesPerSecond * Mathf.PI * 2f * speed;
            float swing = Mathf.Sin(walkPhase) * speed;

            // --- Locomotion base pose (degrees; pitch < 0 swings limbs forward).
            Vector3 hipsOffset = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * bobHeight * speed + Mathf.Sin(t * 2.2f) * 0.004f, 0f);
            Vector3 hipsEuler = new Vector3(runLean * speed, 0f, swing * waddleRoll);
            Vector3 chestEuler = new Vector3(0f, swing * 10f, -swing * waddleRoll * 0.6f);
            Vector3 headEuler = new Vector3(Mathf.Sin(t * 1.3f) * 2f, Mathf.Sin(t * 0.8f) * 7f * (1f - speed), -swing * waddleRoll * 0.4f);
            Vector3 armLeftEuler = new Vector3(swing * armSwingAngle, 0f, -4f);
            Vector3 armRightEuler = new Vector3(-swing * armSwingAngle, 0f, 4f);
            Vector3 legLeftEuler = new Vector3(-swing * strideAngle, 0f, 0f);
            Vector3 legRightEuler = new Vector3(swing * strideAngle, 0f, 0f);
            float sharpness = poseSharpness;

            if (baby.Hands.IsHolding)
            {
                // Balancing the toy on the head with both hands.
                armLeftEuler = new Vector3(-162f + swing * 6f, 0f, 12f);
                armRightEuler = new Vector3(-162f - swing * 6f, 0f, -12f);
                chestEuler.x -= 4f;
            }

            if (throwTimer > 0f)
            {
                float u = 1f - throwTimer / ThrowDuration;
                float release = Mathf.SmoothStep(0f, 1f, u / 0.3f);
                float weight = 1f - Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f);
                if (throwWasToy)
                {
                    Vector3 overhead = new Vector3(-165f, 0f, 0f);
                    Vector3 follow = new Vector3(-55f, 0f, 0f);
                    armLeftEuler = Vector3.Lerp(armLeftEuler, Vector3.Lerp(overhead, follow, release) + new Vector3(0f, 0f, 10f), weight);
                    armRightEuler = Vector3.Lerp(armRightEuler, Vector3.Lerp(overhead, follow, release) + new Vector3(0f, 0f, -10f), weight);
                    hipsEuler.x += 22f * release * weight;
                    headEuler.x += 12f * release * weight;
                }
                else
                {
                    // Rip the diaper off and fling it sidearm.
                    Vector3 windup = new Vector3(35f, 0f, 55f);
                    Vector3 fling = new Vector3(-105f, 0f, -5f);
                    armRightEuler = Vector3.Lerp(armRightEuler, Vector3.Lerp(windup, fling, release), weight);
                    armLeftEuler = Vector3.Lerp(armLeftEuler, new Vector3(-35f, 0f, -25f), weight);
                    chestEuler.y += Mathf.Lerp(-32f, 26f, release) * weight;
                    hipsEuler.x += 10f * release * weight;
                }
                sharpness = 30f;
            }

            if (pickupTimer > 0f)
            {
                float w = Mathf.Sin(Mathf.PI * (1f - pickupTimer / PickupDuration));
                hipsOffset.y -= 0.12f * w;
                hipsEuler.x += 30f * w;
                headEuler.x += 12f * w;
                armLeftEuler = Vector3.Lerp(armLeftEuler, new Vector3(-55f, 0f, 8f), w);
                armRightEuler = Vector3.Lerp(armRightEuler, new Vector3(-55f, 0f, -8f), w);
            }

            if (hitTimer > 0f)
            {
                float w = hitTimer / HitDuration;
                hipsEuler.x -= 20f * w;
                headEuler.x -= 22f * w;
                armLeftEuler += new Vector3(-20f, 0f, -60f) * w;
                armRightEuler += new Vector3(-20f, 0f, 60f) * w;
            }

            switch (activity)
            {
                case BabyActivity.Dashing:
                    // Superman belly slide.
                    hipsOffset = new Vector3(0f, -0.16f, 0f);
                    hipsEuler = new Vector3(74f, 0f, 0f);
                    chestEuler = new Vector3(6f, 0f, 0f);
                    headEuler = new Vector3(-58f, 0f, 0f);
                    armLeftEuler = new Vector3(-172f, 0f, 16f);
                    armRightEuler = new Vector3(-172f, 0f, -16f);
                    legLeftEuler = new Vector3(28f, 0f, -8f);
                    legRightEuler = new Vector3(28f, 0f, 8f);
                    sharpness = 28f;
                    break;
                case BabyActivity.Stunned:
                    headEuler = new Vector3(Mathf.Cos(t * 10f) * 9f, 0f, Mathf.Sin(t * 10f) * 16f);
                    hipsEuler.z = Mathf.Sin(t * 5f) * 6f;
                    armLeftEuler = new Vector3(Mathf.Sin(t * 7f) * 22f, 0f, -12f);
                    armRightEuler = new Vector3(-Mathf.Sin(t * 7f) * 22f, 0f, 12f);
                    break;
                case BabyActivity.Crying:
                    // Plop down on the bottom, kick, rub eyes, wail at the ceiling.
                    hipsOffset = new Vector3(0f, -0.2f, 0f);
                    hipsEuler = new Vector3(-12f + Mathf.Sin(t * 4f) * 4f, 0f, Mathf.Sin(t * 3f) * 7f);
                    chestEuler = new Vector3(-6f, 0f, 0f);
                    headEuler = new Vector3(-20f, 0f, Mathf.Sin(t * 4f) * 6f);
                    float rub = Mathf.Sin(t * 16f) * 10f;
                    armLeftEuler = new Vector3(-138f + rub, 0f, 40f);
                    armRightEuler = new Vector3(-138f - rub, 0f, -40f);
                    legLeftEuler = new Vector3(-82f + Mathf.Sin(t * 9f) * 14f, 0f, -12f);
                    legRightEuler = new Vector3(-82f + Mathf.Sin(t * 9f + 2f) * 14f, 0f, 12f);
                    break;
            }

            if (celebrating)
            {
                hipsOffset.y += Mathf.Abs(Mathf.Sin(t * 6.5f)) * 0.24f;
                float wave = Mathf.Sin(t * 12f) * 14f;
                armLeftEuler = new Vector3(-168f, 0f, -26f + wave);
                armRightEuler = new Vector3(-168f, 0f, 26f - wave);
                headEuler = new Vector3(-12f, 0f, Mathf.Sin(t * 6.5f) * 6f);
            }

            float k = 1f - Mathf.Exp(-sharpness * dt);
            hips.localPosition = Vector3.Lerp(hips.localPosition, hipsRest + hipsOffset, k);
            Pose(hips, hipsEuler, k);
            Pose(chest, chestEuler, k);
            Pose(head, headEuler, k);
            Pose(armLeft, armLeftEuler, k);
            Pose(armRight, armRightEuler, k);
            Pose(legLeft, legLeftEuler, k);
            Pose(legRight, legRightEuler, k);

            UpdateSquash(dt);
            UpdateFace(activity);
            UpdateEffects(activity, t, dt);
        }

        static void Pose(Transform bone, Vector3 euler, float k) =>
            bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.Euler(euler), k);

        void UpdateSquash(float dt)
        {
            // Damped spring back to neutral shape (squash > 0 = flattened, < 0 = stretched). Integrated in small fixed
            // steps so a long frame can't make the spring explode.
            const float step = 1f / 120f;
            int steps = Mathf.Min(16, Mathf.CeilToInt(dt / step));
            float h = steps > 0 ? dt / steps : 0f;
            for (int i = 0; i < steps; i++)
            {
                squashVelocity += (-springStiffness * squash - springDamping * squashVelocity) * h;
                squash = Mathf.Clamp(squash + squashVelocity * h, -0.6f, 0.6f);
            }
            body.localScale = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f);
        }

        void UpdateFace(BabyActivity activity)
        {
            if (face == null)
                return;
            FaceExpression expression;
            if (activity == BabyActivity.Crying)
                expression = FaceExpression.Crying;
            else if (activity == BabyActivity.Stunned)
                expression = hitTimer > 0f ? FaceExpression.Hurt : FaceExpression.Dizzy;
            else if (hitTimer > 0f)
                expression = FaceExpression.Hurt;
            else if (celebrating || gloatTimer > 0f)
                expression = FaceExpression.Happy;
            else if (throwTimer > 0f || activity == BabyActivity.Dashing)
                expression = FaceExpression.Effort;
            else if (baby.Hands.IsHolding)
                expression = FaceExpression.Determined;
            else
                expression = FaceExpression.Neutral;
            face.SetExpression(expression);
        }

        void UpdateEffects(BabyActivity activity, float t, float dt)
        {
            if (dizzyStars != null)
            {
                bool dizzy = activity == BabyActivity.Stunned;
                if (dizzyStars.gameObject.activeSelf != dizzy)
                    dizzyStars.gameObject.SetActive(dizzy);
                if (dizzy)
                    dizzyStars.Rotate(0f, 360f * dt, 0f, Space.Self);
            }

            if (protectionRing != null)
            {
                if (baby.IsProtected)
                {
                    SetColor(protectionRing, baby.PlayerColor);
                }
                else
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);
                    SetColor(protectionRing, Color.Lerp(Color.white, diaperlessRingColor, pulse));
                }
            }
        }

        void Punch(float amount)
        {
            squash = amount;
            squashVelocity = 0f;
        }

        void HandleHit(BabyController _, BabyHitReport report)
        {
            hitTimer = HitDuration;
            Punch(report.WasProtected ? 0.22f : 0.38f);
        }

        void HandleThrew(BabyController _, Throwable item)
        {
            throwTimer = ThrowDuration;
            throwWasToy = item is Toy;
            Punch(-0.22f);
        }

        void HandlePickedUp(BabyController _, Throwable __)
        {
            pickupTimer = PickupDuration;
            Punch(0.16f);
        }

        void HandleDashed(BabyController _) => Punch(-0.25f);

        void HandleShoved(BabyController shover, BabyController victim)
        {
            if (shover == baby)
                gloatTimer = GloatDuration;
        }

        void HandleStartedCrying(BabyController _)
        {
            Punch(0.35f);
            if (tears != null)
                tears.Play();
        }

        void HandleCelebrated(BabyController _) => celebrating = true;

        void HandleRoundReset(BabyController _)
        {
            celebrating = false;
            throwTimer = pickupTimer = hitTimer = gloatTimer = 0f;
            squash = squashVelocity = 0f;
            if (tears != null)
                tears.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void SetColor(Renderer target, Color color)
        {
            if (target == null)
                return;
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }
}
