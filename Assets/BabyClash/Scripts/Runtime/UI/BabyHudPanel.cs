using System;
using UnityEngine;
using UnityEngine.UI;

namespace BabyClash
{
    [Serializable]
    public struct ToyIcon
    {
        public ToyConfig toy;
        public Sprite sprite;
    }

    /// <summary>One player's HUD card: portrait, Cry Meter, diaper state, held toy and round wins. Read-only view.</summary>
    public sealed class BabyHudPanel : MonoBehaviour
    {
        [SerializeField] Text nameLabel;
        [SerializeField] Image portrait;
        [SerializeField] Image portraitRing;

        [Tooltip("Bar fill. Its right anchor is driven by the Cry Meter.")]
        [SerializeField] RectTransform cryFill;
        [SerializeField] Image cryFillImage;
        [SerializeField] Text cryLabel;
        [SerializeField] Image diaperIcon;
        [SerializeField] Text diaperLabel;
        [SerializeField] Image heldToyIcon;
        [SerializeField] Image[] winPips = new Image[0];
        [SerializeField] ToyIcon[] toyIcons = new ToyIcon[0];

        [SerializeField] Color calmColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] Color upsetColor = new Color(1f, 0.35f, 0.4f);
        [SerializeField] Color diaperOffColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] Color pipEmptyColor = new Color(1f, 1f, 1f, 0.25f);

        [Tooltip("How fast the bar catches up with the real value (per second).")]
        [SerializeField] float fillCatchUpSpeed = 2.5f;

        BabyController baby;
        RoundManager round;
        float shownFill;
        float barShake;
        Vector2 cryBarRest;
        RectTransform cryBar;
        bool scoring;
        bool simple;
        int shownScore = -1;

        /// <summary>Score modes show points after the name; toddler mode drops everything but the points.</summary>
        public void SetRules(bool usesScore, bool toddler)
        {
            scoring = usesScore;
            simple = toddler;
            shownScore = -1;
            cryLabel.enabled = !toddler;
            diaperLabel.enabled = !toddler;
            if (diaperIcon != null)
                diaperIcon.enabled = !toddler;
            if (baby != null)
                nameLabel.text = baby.DisplayName;
            RefreshPips();
        }

        /// <summary>Prefab wiring (editor tooling).</summary>
        public void Configure(Text babyName, Image face, Image faceRing, RectTransform fill, Image fillImage, Text cry,
            Image diaper, Text diaperText, Image toy, Image[] pips, ToyIcon[] icons)
        {
            nameLabel = babyName;
            portrait = face;
            portraitRing = faceRing;
            cryFill = fill;
            cryFillImage = fillImage;
            cryLabel = cry;
            diaperIcon = diaper;
            diaperLabel = diaperText;
            heldToyIcon = toy;
            winPips = pips;
            toyIcons = icons;
        }

        public void Bind(BabyController target, RoundManager manager, Sprite face)
        {
            Unbind();
            baby = target;
            round = manager;
            if (baby == null)
                return;

            baby.Cry.Changed += HandleCryChanged;
            baby.Diapers.Changed += HandleDiapersChanged;
            baby.Hands.Changed += HandleHandsChanged;
            baby.StartedCrying += HandleStartedCrying;
            baby.RoundReset += HandleRoundReset;

            cryBar = (RectTransform)cryFill.parent;
            cryBarRest = cryBar.anchoredPosition;
            nameLabel.text = baby.DisplayName;
            nameLabel.color = baby.PlayerColor;
            if (portraitRing != null)
                portraitRing.color = baby.PlayerColor;
            if (portrait != null && face != null)
                portrait.sprite = face;
            shownFill = baby.Cry.Normalized;
            HandleCryChanged(baby.Cry.Value);
            HandleDiapersChanged(baby.Diapers);
            HandleHandsChanged(baby.Hands);
            RefreshPips();
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (baby == null)
                return;
            baby.Cry.Changed -= HandleCryChanged;
            baby.Diapers.Changed -= HandleDiapersChanged;
            baby.Hands.Changed -= HandleHandsChanged;
            baby.StartedCrying -= HandleStartedCrying;
            baby.RoundReset -= HandleRoundReset;
            baby = null;
        }

        public void RefreshPips()
        {
            int wins = baby != null && round != null ? round.GetWins(baby) : 0;
            int needed = round != null ? round.RoundsToWin : winPips.Length;
            for (int i = 0; i < winPips.Length; i++)
            {
                winPips[i].gameObject.SetActive(i < needed && !scoring);
                winPips[i].color = i < wins ? (baby != null ? baby.PlayerColor : Color.white) : pipEmptyColor;
            }
        }

        void Update()
        {
            if (baby == null)
                return;

            if (scoring && round != null)
            {
                int score = Mathf.FloorToInt(round.GetScore(baby));
                if (score != shownScore)
                {
                    shownScore = score;
                    nameLabel.text = simple ? score.ToString() : $"{baby.DisplayName}   {score}";
                }
            }

            float target = baby.Cry.Normalized;
            // Rise quickly on hits, drop instantly on reset.
            shownFill = target < shownFill ? target : Mathf.MoveTowards(shownFill, target, fillCatchUpSpeed * Time.unscaledDeltaTime);
            cryFill.anchorMax = new Vector2(shownFill, 1f);
            cryFillImage.color = Color.Lerp(calmColor, upsetColor, shownFill);

            barShake = Mathf.MoveTowards(barShake, 0f, Time.unscaledDeltaTime * 3f);
            cryBar.anchoredPosition = cryBarRest + new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 6f * barShake, 0f);

            if (!baby.IsProtected && !baby.IsCrying && diaperLabel != null)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                diaperLabel.color = Color.Lerp(diaperOffColor, Color.white, flash * 0.6f);
            }
        }

        void HandleCryChanged(float value)
        {
            cryLabel.text = "CRY " + Mathf.RoundToInt(baby != null ? baby.Cry.Normalized * 100f : value).ToString() + "%";
            if (value > 0f)
                barShake = 1f;
        }

        void HandleDiapersChanged(BabyDiaperSlot slot)
        {
            if (baby != null && baby.IsCrying)
                return;
            bool on = slot.IsProtected;
            if (diaperIcon != null)
                diaperIcon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            diaperLabel.text = !on ? "NO DIAPER!" : slot.Count > 1 ? $"DIAPER x{slot.Count}" : "DIAPER ON";
            diaperLabel.color = on ? Color.white : diaperOffColor;
        }

        void HandleHandsChanged(BabyHands hands)
        {
            if (heldToyIcon == null)
                return;
            Sprite sprite = null;
            if (hands.IsHolding)
            {
                foreach (ToyIcon icon in toyIcons)
                {
                    if (icon.toy == hands.Held.Config)
                        sprite = icon.sprite;
                }
            }
            heldToyIcon.sprite = sprite;
            heldToyIcon.enabled = sprite != null;
        }

        void HandleStartedCrying(BabyController _)
        {
            diaperLabel.text = "WAAAH!";
            diaperLabel.color = upsetColor;
        }

        void HandleRoundReset(BabyController _)
        {
            barShake = 0f;
            if (baby != null)
                HandleDiapersChanged(baby.Diapers);
            RefreshPips();
        }
    }
}
