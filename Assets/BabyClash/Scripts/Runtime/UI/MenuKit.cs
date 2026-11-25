using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>Builds menu screens at runtime in the style of the generated ones (same font, sprites and button feel).</summary>
    public sealed class MenuKit
    {
        public static readonly Color Ink = new Color(0.28f, 0.18f, 0.34f);
        public static readonly Color Panel = new Color(1f, 0.97f, 0.93f, 0.96f);
        public static readonly Color Pink = new Color(1f, 0.55f, 0.66f);
        public static readonly Color Blue = new Color(0.45f, 0.7f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.78f, 0.3f);
        public static readonly Color Mint = new Color(0.45f, 0.85f, 0.62f);
        public static readonly Color Lilac = new Color(0.7f, 0.58f, 0.98f);
        public static readonly Color Grey = new Color(0.72f, 0.68f, 0.74f);

        readonly Font font;
        readonly Sprite rounded;
        readonly Sprite circle;
        readonly Sprite star;
        readonly Button template;
        readonly AudioDirector audioDirector;

        public MenuKit(Button buttonTemplate, AudioDirector audio)
        {
            template = buttonTemplate;
            audioDirector = audio;
            font = template.GetComponentInChildren<Text>(true).font;
            rounded = ((Image)template.targetGraphic).sprite;
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite.name.StartsWith("UiCircle", StringComparison.Ordinal))
                    circle = sprite;
                else if (sprite.name.StartsWith("UiStar", StringComparison.Ordinal))
                    star = sprite;
            }
        }

        public Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public RectTransform Screen(string name, Transform parent, float dim)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.08f, 0.2f, dim);
            rect.gameObject.SetActive(false);
            return rect;
        }


        public RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, anchor, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            return rect;
        }

        public RectTransform Circle(string name, Transform parent, Color color, float size)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = circle;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public Image Star(Transform parent, Vector2 position, float size)
        {
            RectTransform rect = Rect("Star", parent, new Vector2(0.5f, 0.5f), position, Vector2.one * size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = star;
            image.raycastTarget = false;
            return image;
        }

        public static void SetStar(Image star, bool on) => star.color = on ? new Color(1f, 0.78f, 0.2f) : new Color(0.28f, 0.18f, 0.34f, 0.22f);

        public Text Label(string name, Transform parent, string text, int size, Color color, Vector2 position, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, bool outlined = false)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, box);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = align;
            label.color = color;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            if (outlined)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.1f, 0.25f, 0.85f);
                outline.effectDistance = new Vector2(3f, -3f);
            }
            return label;
        }

        public Button Button(string name, Transform parent, string text, Color color, Vector2 position, Vector2 size, int fontSize,
            UnityAction onClick, Vector2? anchor = null)
        {
            RectTransform rect = Box(name, parent, anchor ?? new Vector2(0.5f, 0.5f), position, size, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = template.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            button.colors = colors;
            Label("Label", rect, text, fontSize, Color.white, Vector2.zero, size - new Vector2(20f, 4f), TextAnchor.MiddleCenter, true);
            rect.gameObject.AddComponent<MenuButtonFx>().Configure(audioDirector);
            button.onClick.AddListener(onClick);
            return button;
        }

        public static void SetText(Button button, string text) => button.GetComponentInChildren<Text>(true).text = text;

        public static void SetColor(Button button, Color color) => button.targetGraphic.color = color;
    }
}
