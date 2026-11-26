using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BabyClash
{
    /// <summary>
    /// Touch play for tablets and phones (and touch screens in the browser): drag anywhere on the left half to move,
    /// tap the big buttons on the right to grab/throw and dash. In toddler mode any tap throws. Drives baby 1.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        const float StickRadius = 0.09f;
        const float ButtonRadius = 0.1f;

        RectTransform stickBase;
        RectTransform stickKnob;
        RectTransform actionButton;
        RectTransform dashButton;
        CanvasGroup group;
        int stickTouch = -1;
        Vector2 stickOrigin;
        bool action;
        bool dash;

        public static TouchControls Active { get; private set; }

        public Vector2 Move { get; private set; }

        /// <summary>Shown only during matches with a local human.</summary>
        public bool Playing { get; set; }

        /// <summary>Toddler mode: every tap throws and there is no dash button.</summary>
        public bool Simple { get; set; }

        public static TouchControls Create(MenuKit kit)
        {
            Canvas canvas = kit.Canvas("TouchControls", 15);
            var self = canvas.gameObject.AddComponent<TouchControls>();
            self.group = canvas.gameObject.AddComponent<CanvasGroup>();
            self.group.blocksRaycasts = false;
            var root = (RectTransform)canvas.transform;
            self.stickBase = kit.Circle("StickBase", root, new Color(1f, 1f, 1f, 0.3f), 230f);
            self.stickKnob = kit.Circle("StickKnob", root, new Color(1f, 1f, 1f, 0.75f), 110f);
            self.actionButton = kit.Circle("Throw", root, new Color(1f, 0.55f, 0.66f, 0.85f), 240f);
            self.dashButton = kit.Circle("Dash", root, new Color(0.45f, 0.7f, 1f, 0.85f), 170f);
            kit.Label("Label", self.actionButton, "THROW", 46, Color.white, Vector2.zero, new Vector2(240f, 100f));
            kit.Label("Label", self.dashButton, "DASH", 36, Color.white, Vector2.zero, new Vector2(170f, 80f));
            Place(self.actionButton, new Vector2(0.88f, 0.22f));
            Place(self.dashButton, new Vector2(0.75f, 0.14f));
            return self;
        }

        static void Place(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
        }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public bool ConsumeAction()
        {
            bool was = action;
            action = false;
            return was;
        }

        public bool ConsumeDash()
        {
            bool was = dash;
            dash = false;
            return was;
        }

        void Update()
        {
            Touchscreen screen = Touchscreen.current;
            bool show = screen != null && Playing && Time.timeScale > 0f;
            group.alpha = show ? 1f : 0f;
            dashButton.gameObject.SetActive(!Simple);
            if (!show)
            {
                Move = Vector2.zero;
                action = dash = false;
                stickTouch = -1;
                return;
            }

            float height = UnityEngine.Screen.height;
            bool stickHeld = false;
            foreach (TouchControl touch in screen.touches)
            {
                TouchPhase phase = touch.phase.ReadValue();
                if (phase == TouchPhase.None)
                    continue;
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();
                if (phase == TouchPhase.Began)
                {
                    if (position.x < UnityEngine.Screen.width * 0.5f && stickTouch < 0)
                    {
                        stickTouch = id;
                        stickOrigin = position;
                    }
                    else if (!Simple && Near(dashButton, position, height))
                    {
                        dash = true;
                    }
                    else if (Simple || position.x >= UnityEngine.Screen.width * 0.5f)
                    {
                        action = true;
                    }
                }
                if (id != stickTouch)
                    continue;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                {
                    // A quick tap on the left also counts as the action in toddler mode.
                    if (Simple && (position - stickOrigin).magnitude < height * 0.03f)
                        action = true;
                    continue;
                }
                stickHeld = true;
                Move = Vector2.ClampMagnitude((position - stickOrigin) / (height * StickRadius), 1f);
            }
            if (!stickHeld)
            {
                stickTouch = -1;
                Move = Vector2.zero;
            }

            Vector2 rest = new Vector2(UnityEngine.Screen.width * 0.13f, height * 0.22f);
            Vector2 origin = stickHeld ? stickOrigin : rest;
            stickBase.position = origin;
            stickKnob.position = origin + Move * (height * StickRadius);
        }

        static bool Near(RectTransform button, Vector2 position, float height) =>
            ((Vector2)button.position - position).magnitude < height * ButtonRadius;
    }
}
