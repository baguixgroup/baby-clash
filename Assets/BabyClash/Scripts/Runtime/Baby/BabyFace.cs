using UnityEngine;

namespace BabyClash
{
    public enum FaceExpression
    {
        Neutral,
        Happy,
        Effort,
        Hurt,
        Crying,
        Dizzy,
        Determined,
    }

    /// <summary>
    /// Cartoon face rig: eyes that blink and squint, brows that tilt, three mouth shapes and dizzy pupils.
    /// Driven by <see cref="BabyAnimator"/>; purely visual.
    /// </summary>
    public sealed class BabyFace : MonoBehaviour
    {
        const float BlinkDuration = 0.14f;
        const float ExpressionSpeed = 14f;

        [SerializeField] Transform eyeLeft;
        [SerializeField] Transform eyeRight;
        [SerializeField] Transform pupilLeft;
        [SerializeField] Transform pupilRight;
        [SerializeField] Transform browLeft;
        [SerializeField] Transform browRight;
        [SerializeField] Transform mouthSmile;
        [SerializeField] Transform mouthOpen;
        [SerializeField] Transform mouthFlat;

        [Tooltip("Seconds between blinks (random within range).")]
        [SerializeField] Vector2 blinkInterval = new Vector2(2f, 4.5f);

        [Tooltip("How far the pupils circle when dizzy (m).")]
        [SerializeField] float dizzyPupilRadius = 0.012f;

        FaceExpression expression;
        float blinkTimer;
        float blinkElapsed = BlinkDuration;
        float openness = 1f;
        float browAngle;
        float browLift;
        float smileSize = 1f;
        float openSize;
        float flatSize;
        Vector3 eyeRestScale;
        Vector3 pupilLeftRest;
        Vector3 pupilRightRest;
        Vector3 browLeftRest;
        Vector3 browRightRest;
        Vector3 smileRestScale;
        Vector3 openRestScale;
        Vector3 flatRestScale;

        public FaceExpression Expression => expression;

        /// <summary>Prefab wiring (editor tooling).</summary>
        public void Configure(Transform leftEye, Transform rightEye, Transform leftPupil, Transform rightPupil,
            Transform leftBrow, Transform rightBrow, Transform smile, Transform open, Transform flat)
        {
            eyeLeft = leftEye;
            eyeRight = rightEye;
            pupilLeft = leftPupil;
            pupilRight = rightPupil;
            browLeft = leftBrow;
            browRight = rightBrow;
            mouthSmile = smile;
            mouthOpen = open;
            mouthFlat = flat;
        }

        void Awake()
        {
            eyeRestScale = eyeLeft.localScale;
            pupilLeftRest = pupilLeft.localPosition;
            pupilRightRest = pupilRight.localPosition;
            browLeftRest = browLeft.localPosition;
            browRightRest = browRight.localPosition;
            smileRestScale = mouthSmile.localScale;
            openRestScale = mouthOpen.localScale;
            flatRestScale = mouthFlat.localScale;
            blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        }

        public void SetExpression(FaceExpression value) => expression = value;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-ExpressionSpeed * dt);

            // Targets: eye openness, brow tilt (+ = worried, - = angry), brow lift, mouth sizes.
            float targetOpen, targetBrow, targetLift, targetSmile, targetOpenMouth, targetFlat;
            switch (expression)
            {
                case FaceExpression.Happy:
                    targetOpen = 0.35f; targetBrow = 6f; targetLift = 0.012f; targetSmile = 1.35f; targetOpenMouth = 0f; targetFlat = 0f;
                    break;
                case FaceExpression.Effort:
                    targetOpen = 0.75f; targetBrow = -22f; targetLift = -0.006f; targetSmile = 0f; targetOpenMouth = 0.55f; targetFlat = 0f;
                    break;
                case FaceExpression.Hurt:
                    targetOpen = 0.12f; targetBrow = 24f; targetLift = 0.004f; targetSmile = 0f; targetOpenMouth = 0.8f; targetFlat = 0f;
                    break;
                case FaceExpression.Crying:
                    targetOpen = 0.08f; targetBrow = 30f; targetLift = 0.012f; targetSmile = 0f; targetOpenMouth = 1.45f; targetFlat = 0f;
                    break;
                case FaceExpression.Dizzy:
                    targetOpen = 1.05f; targetBrow = 12f; targetLift = 0.008f; targetSmile = 0f; targetOpenMouth = 0f; targetFlat = 1f;
                    break;
                case FaceExpression.Determined:
                    targetOpen = 0.8f; targetBrow = -14f; targetLift = -0.004f; targetSmile = 0.7f; targetOpenMouth = 0f; targetFlat = 0f;
                    break;
                default:
                    targetOpen = 1f; targetBrow = 0f; targetLift = 0f; targetSmile = 1f; targetOpenMouth = 0f; targetFlat = 0f;
                    break;
            }

            openness = Mathf.Lerp(openness, targetOpen, k);
            browAngle = Mathf.Lerp(browAngle, targetBrow, k);
            browLift = Mathf.Lerp(browLift, targetLift, k);
            smileSize = Mathf.Lerp(smileSize, targetSmile, k);
            openSize = Mathf.Lerp(openSize, targetOpenMouth, k);
            flatSize = Mathf.Lerp(flatSize, targetFlat, k);

            // Blinks only make sense with open eyes.
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
                blinkElapsed = 0f;
            }
            blinkElapsed += dt;
            float blink = blinkElapsed < BlinkDuration ? Mathf.Abs(blinkElapsed / BlinkDuration * 2f - 1f) : 1f;

            float eyeY = Mathf.Max(0.06f, openness * Mathf.Max(0.1f, blink));
            var eyeScale = new Vector3(eyeRestScale.x, eyeRestScale.y * eyeY, eyeRestScale.z);
            eyeLeft.localScale = eyeScale;
            eyeRight.localScale = eyeScale;

            Vector3 pupilOffset = Vector3.zero;
            if (expression == FaceExpression.Dizzy)
            {
                float t = Time.time * 9f;
                pupilOffset = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * dizzyPupilRadius;
            }
            pupilLeft.localPosition = pupilLeftRest + pupilOffset;
            pupilRight.localPosition = pupilRightRest - pupilOffset;

            // Brows tilt around their outer ends: left brow is at -x, right at +x.
            browLeft.localPosition = browLeftRest + Vector3.up * browLift;
            browRight.localPosition = browRightRest + Vector3.up * browLift;
            browLeft.localRotation = Quaternion.Euler(0f, 0f, -browAngle);
            browRight.localRotation = Quaternion.Euler(0f, 0f, browAngle);

            SetMouth(mouthSmile, smileRestScale, smileSize, smileSize);
            float wobble = expression == FaceExpression.Crying ? 1f + Mathf.Sin(Time.time * 22f) * 0.08f : 1f;
            SetMouth(mouthOpen, openRestScale, openSize, openSize * wobble);
            SetMouth(mouthFlat, flatRestScale, flatSize, flatSize);
        }

        static void SetMouth(Transform mouth, Vector3 restScale, float width, float height)
        {
            bool visible = width > 0.05f;
            if (mouth.gameObject.activeSelf != visible)
                mouth.gameObject.SetActive(visible);
            if (visible)
                mouth.localScale = new Vector3(restScale.x * width, restScale.y * height, restScale.z);
        }
    }
}
