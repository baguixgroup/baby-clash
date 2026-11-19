using UnityEngine;

namespace BabyClash
{
    /// <summary>Bouncing letters of the "BABY CLASH" logo. Unscaled time so it keeps moving in menus.</summary>
    public sealed class TitleLogo : MonoBehaviour
    {
        [SerializeField] RectTransform[] letters = new RectTransform[0];
        [SerializeField] float bounceHeight = 14f;
        [SerializeField] float bounceSpeed = 3.2f;
        [SerializeField] float letterPhase = 0.45f;
        [SerializeField] float tiltDegrees = 5f;

        Vector2[] rest;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(RectTransform[] letterRects) => letters = letterRects;

        void Awake()
        {
            rest = new Vector2[letters.Length];
            for (int i = 0; i < letters.Length; i++)
                rest[i] = letters[i].anchoredPosition;
        }

        void Update()
        {
            float t = Time.unscaledTime * bounceSpeed;
            for (int i = 0; i < letters.Length; i++)
            {
                float phase = t - i * letterPhase;
                letters[i].anchoredPosition = rest[i] + new Vector2(0f, Mathf.Abs(Mathf.Sin(phase)) * bounceHeight);
                letters[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * tiltDegrees);
            }
        }
    }
}
