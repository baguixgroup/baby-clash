using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BabyClash
{
    /// <summary>
    /// Balancing aid: live numbers for every baby, diaper and toy. Editor and development builds only; F1 toggles it.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] RoundManager roundManager;
        [SerializeField] bool visibleOnStart;
        [SerializeField] int fontSize = 15;

        readonly StringBuilder text = new StringBuilder(1024);
        GUIStyle style;
        bool visible;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(RoundManager round) => roundManager = round;

        void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }
            visible = visibleOnStart;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible || roundManager == null)
                return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = fontSize,
                richText = false,
                padding = new RectOffset(10, 10, 8, 8),
            };

            BuildText();
            var content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect(10f, Screen.height - size.y - 10f, size.x, size.y), content, style);
        }

        void BuildText()
        {
            text.Clear();
            text.Append("DEBUG (F1)   Round ").Append(roundManager.RoundNumber)
                .Append("   Phase: ").Append(roundManager.Phase)
                .Append("   Time: ").Append(roundManager.TimeRemaining.ToString("0.0"))
                .Append("   timeScale: ").Append(Time.timeScale.ToString("0.00")).Append('\n');

            foreach (BabyController baby in roundManager.Babies)
            {
                if (baby == null || baby.Cry == null)
                    continue;
                text.Append(baby.DisplayName)
                    .Append("  Cry: ").Append(baby.Cry.Value.ToString("0.#"))
                    .Append("  Protected: ").Append(baby.IsProtected)
                    .Append("  State: ").Append(baby.State)
                    .Append("  Diaper: ");
                if (baby.Diapers.Count == 0)
                    text.Append("None");
                for (int i = 0; i < baby.Diapers.Count; i++)
                    text.Append(i > 0 ? ", " : string.Empty).Append(baby.Diapers.Held[i].name);
                text.Append("  Holding: ").Append(baby.Hands.IsHolding ? baby.Hands.Held.name : "-")
                    .Append("  Speed: ").Append(baby.Movement.Velocity.magnitude.ToString("0.0"))
                    .Append("  Wins: ").Append(roundManager.GetWins(baby)).Append('\n');
            }

            foreach (Diaper diaper in roundManager.Diapers)
                AppendItem(diaper);
            if (roundManager.Toys != null)
            {
                foreach (Toy toy in roundManager.Toys.Pool)
                {
                    if (toy != null && toy.gameObject.activeSelf)
                        AppendItem(toy);
                }
            }
        }

        void AppendItem(Throwable item)
        {
            if (item == null)
                return;
            text.Append(item.name).Append("  ").Append(item.State);
            if (item.Holder != null)
                text.Append("  held by ").Append(item.Holder.DisplayName);
            if (item.Thrower != null)
                text.Append("  thrown by ").Append(item.Thrower.DisplayName)
                    .Append(item.Status.HasBounced ? " (bounced)" : " (direct)")
                    .Append(item.Status.HasHit ? " HIT" : string.Empty);
            Vector3 p = item.transform.position;
            text.Append("  pos ").Append(p.x.ToString("0.0")).Append(", ").Append(p.y.ToString("0.0"))
                .Append(", ").Append(p.z.ToString("0.0")).Append('\n');
        }
    }
}
