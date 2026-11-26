using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BabyClash
{
    /// <summary>Toddler mode helper for a human baby: picks things up by itself, and (for a lone toddler) any key throws.</summary>
    public sealed class TotsHelper : MonoBehaviour
    {
        const float PickupEvery = 0.25f;

        BabyController baby;
        float timer;

        public bool AnyKey { get; set; }

        void Awake() => baby = GetComponent<BabyController>();

        void Update()
        {
            if (!baby.IsActiveInRound || Time.timeScale <= 0f)
                return;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = PickupEvery;
                if (baby.Status.CanAct)
                    baby.TryPickUp();
            }
            if (AnyKey && Keyboard.current != null && Pressed(Keyboard.current))
                baby.PressAction();
        }

        static bool Pressed(Keyboard keyboard)
        {
            if (!keyboard.anyKey.wasPressedThisFrame)
                return false;
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame)
                    continue;
                switch (key.keyCode)
                {
                    // Moving, pausing and the normal throw keys (already handled) don't count.
                    case Key.W: case Key.A: case Key.S: case Key.D:
                    case Key.UpArrow: case Key.DownArrow: case Key.LeftArrow: case Key.RightArrow:
                    case Key.Escape: case Key.P: case Key.Space: case Key.Enter: case Key.RightCtrl:
                    case Key.LeftShift: case Key.RightShift: case Key.Slash: case Key.Numpad0:
                    case Key.LeftMeta: case Key.RightMeta:
                        continue;
                    default:
                        return true;
                }
            }
            return false;
        }
    }
}
