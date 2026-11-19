using BabyClash.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>In-match HUD: binds the player cards and shows round banners, countdown, timer and control hints.</summary>
    public sealed class HUDController : MonoBehaviour
    {
        const int CountdownSteps = 3;
        const float PopDuration = 0.35f;
        const float TimerWarning = 10f;

        [SerializeField] RoundManager roundManager;
        [SerializeField] CanvasGroup group;

        [Tooltip("Panel i shows baby i.")]
        [SerializeField] BabyHudPanel[] panels = new BabyHudPanel[0];

        [Tooltip("Portrait sprite per baby (same order as the babies).")]
        [SerializeField] Sprite[] portraits = new Sprite[0];

        [SerializeField] RectTransform banner;
        [SerializeField] Text headline;
        [SerializeField] Text subline;
        [SerializeField] Text timerLabel;
        [SerializeField] GameObject timerPill;
        [SerializeField] CanvasGroup hints;

        [Tooltip("How long GO! stays on screen (s).")]
        [SerializeField] float goMessageDuration = 0.8f;

        [Tooltip("Control hints show during the first round for this long (s).")]
        [SerializeField] float hintDuration = 9f;

        float goMessageTimer;
        int shownCountdown = -1;
        int shownSeconds = -1;
        float popTimer;
        float hintTimer;
        bool visible = true;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(RoundManager round, CanvasGroup canvasGroup, BabyHudPanel[] babyPanels, Sprite[] faces,
            RectTransform bannerRoot, Text headlineText, Text sublineText, Text timer, GameObject timerRoot, CanvasGroup hintGroup)
        {
            roundManager = round;
            group = canvasGroup;
            panels = babyPanels;
            portraits = faces;
            banner = bannerRoot;
            headline = headlineText;
            subline = sublineText;
            timerLabel = timer;
            timerPill = timerRoot;
            hints = hintGroup;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (group != null)
            {
                group.alpha = value ? 1f : 0f;
                group.blocksRaycasts = false;
            }
        }

        public void ShowHints() => hintTimer = hintDuration;

        public void HideBanner()
        {
            if (banner != null)
                banner.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (roundManager == null)
                return;
            roundManager.PhaseChanged += HandlePhaseChanged;
            roundManager.MatchStarted += HandleMatchStarted;
        }

        void OnDisable()
        {
            if (roundManager == null)
                return;
            roundManager.PhaseChanged -= HandlePhaseChanged;
            roundManager.MatchStarted -= HandleMatchStarted;
        }

        void Start()
        {
            if (roundManager == null)
            {
                Debug.LogError($"{name}: no RoundManager assigned.", this);
                return;
            }
            for (int i = 0; i < panels.Length; i++)
            {
                BabyController baby = i < roundManager.Babies.Count ? roundManager.Babies[i] : null;
                panels[i].Bind(baby, roundManager, i < portraits.Length ? portraits[i] : null);
            }
            HandlePhaseChanged(roundManager.Phase);
        }

        void Update()
        {
            if (roundManager == null)
                return;
            float dt = Time.unscaledDeltaTime;

            // Text is only rewritten when it actually changes, to keep Update allocation-free.
            switch (roundManager.Phase)
            {
                case RoundPhase.Starting:
                    float progress = roundManager.CountdownRemaining / Mathf.Max(0.01f, roundManager.CountdownDuration);
                    int step = Mathf.Clamp(Mathf.CeilToInt(progress * CountdownSteps), 1, CountdownSteps);
                    if (step != shownCountdown)
                    {
                        shownCountdown = step;
                        ShowBanner(step.ToString(), $"ROUND {roundManager.RoundNumber}", Color.white);
                    }
                    break;
                case RoundPhase.Playing when goMessageTimer > 0f:
                    goMessageTimer -= dt;
                    if (goMessageTimer <= 0f)
                        HideBanner();
                    break;
            }

            if (timerPill != null)
                timerPill.SetActive(roundManager.HasTimeLimit && !roundManager.IsEndless);
            if (roundManager.HasTimeLimit && timerLabel != null && roundManager.Phase != RoundPhase.RoundOver)
            {
                int seconds = Mathf.CeilToInt(roundManager.TimeRemaining);
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    timerLabel.text = seconds.ToString();
                }
                bool warning = roundManager.Phase == RoundPhase.Playing && roundManager.TimeRemaining <= TimerWarning;
                timerLabel.color = warning ? new Color(0.95f, 0.25f, 0.3f) : new Color(0.28f, 0.18f, 0.34f);
                timerLabel.transform.localScale = Vector3.one * (warning ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f);
            }

            if (banner != null && popTimer > 0f)
            {
                popTimer = Mathf.Max(0f, popTimer - dt);
                float u = 1f - popTimer / PopDuration;
                // Overshooting pop: 0 -> 1.25 -> 1.
                float scale = u < 0.6f ? Mathf.Lerp(0.2f, 1.25f, u / 0.6f) : Mathf.Lerp(1.25f, 1f, (u - 0.6f) / 0.4f);
                banner.localScale = Vector3.one * scale;
            }

            if (hints != null)
            {
                hintTimer = Mathf.Max(0f, hintTimer - dt);
                hints.alpha = visible ? Mathf.Clamp01(hintTimer) : 0f;
            }
        }

        void HandleMatchStarted()
        {
            foreach (BabyHudPanel panel in panels)
                panel.RefreshPips();
        }

        void HandlePhaseChanged(RoundPhase phase)
        {
            shownCountdown = -1;
            switch (phase)
            {
                case RoundPhase.Playing:
                    ShowBanner("GO!", string.Empty, new Color(1f, 0.9f, 0.35f));
                    goMessageTimer = goMessageDuration;
                    break;
                case RoundPhase.RoundOver:
                    BabyController winner = roundManager.RoundWinner;
                    string title = winner != null ? $"{winner.DisplayName} WINS THE ROUND!" : "IT'S A TIE!";
                    string detail = roundManager.RoundEndedOnTime ? "TIME'S UP! Calmest baby wins." : LoserLine();
                    ShowBanner(title, detail, winner != null ? winner.PlayerColor : Color.white);
                    foreach (BabyHudPanel panel in panels)
                        panel.RefreshPips();
                    break;
                case RoundPhase.Restarting:
                    HideBanner();
                    break;
            }
        }

        string LoserLine()
        {
            BabyController loser = roundManager.RoundLoser;
            return loser != null ? $"{loser.DisplayName} cried!" : string.Empty;
        }

        void ShowBanner(string title, string detail, Color color)
        {
            if (banner == null)
                return;
            banner.gameObject.SetActive(true);
            headline.text = title;
            headline.color = color;
            subline.text = detail;
            popTimer = PopDuration;
        }

    }
}
