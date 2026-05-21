using System.Collections.Generic;
using DevsDaddy.Shared.UIFramework.Core.RoundedMasks;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public static class RuntimeUiFactory
    {
        private static Font _defaultFont;
        private static readonly Dictionary<string, Sprite> OneUiIconCache = new Dictionary<string, Sprite>();

        public static Font DefaultFont
        {
            get
            {
                if (_defaultFont != null)
                {
                    return _defaultFont;
                }

                _defaultFont = Resources.Load<Font>("MobilOflOneUI/Fonts/tilda-sans_semibold");
                if (_defaultFont != null)
                {
                    return _defaultFont;
                }

                _defaultFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Bahnschrift", "Arial" }, 16);
                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _defaultFont;
            }
        }

        public static RectTransform CreateUiRoot(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        public static Image AddImage(GameObject target, Color color)
        {
            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        public static Sprite LoadOneUiIcon(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return null;
            }

            if (OneUiIconCache.TryGetValue(iconName, out var cached))
            {
                return cached;
            }

            var path = "MobilOflOneUI/Icons/" + iconName;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            OneUiIconCache[iconName] = sprite;
            return sprite;
        }

        public static Image CreateIcon(string name, Transform parent, string iconName, Color color, Vector2 size)
        {
            var rect = CreateUiRoot(name, parent);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadOneUiIcon(iconName);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            EnsureLayoutElement(rect, preferredWidth: size.x, preferredHeight: size.y);
            return image;
        }

        public static void ApplyOneUiRounding(GameObject target, float radius = 14f)
        {
            var graphic = target.GetComponent<MaskableGraphic>();
            if (graphic == null)
            {
                return;
            }

            var rounded = target.GetComponent<ImageRoundedMask>();
            if (rounded == null)
            {
                rounded = target.AddComponent<ImageRoundedMask>();
            }

            rounded.radius = radius;
        }

        public static Outline AddOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            return outline;
        }

        public static Shadow AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = null;
            var shadows = target.GetComponents<Shadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] is Outline)
                {
                    continue;
                }

                shadow = shadows[i];
                break;
            }

            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            return shadow;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Color color,
            FontStyle fontStyle,
            TextAnchor anchor)
        {
            var rect = CreateUiRoot(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, fontSize + 12f);
            rect.localScale = Vector3.one;

            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.alignByGeometry = true;
            text.lineSpacing = 1.08f;
            text.raycastTarget = false;

            var shadow = rect.gameObject.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = rect.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -1f);

            var fitter = rect.gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, Color backgroundColor, int fontSize = 18)
        {
            var rect = CreateUiRoot(name, parent);
            var image = AddImage(rect.gameObject, backgroundColor);
            ApplyOneUiRounding(rect.gameObject, 14f);
            AddOutline(rect.gameObject, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));
            AddShadow(rect.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -4f));

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.9f, 1f, 0.95f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelText = CreateText("Label", rect, label, fontSize, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);
            RemoveContentSizeFitter(labelText.gameObject);
            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string placeholderText, int fontSize = 18)
        {
            var rect = CreateUiRoot(name, parent);
            AddImage(rect.gameObject, ModernGuiTheme.InputBgColor);
            ApplyOneUiRounding(rect.gameObject, 14f);
            AddOutline(rect.gameObject, ModernGuiTheme.BorderColor, new Vector2(1f, -1f));

            var textArea = CreateUiRoot("TextArea", rect);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(18f, 14f);
            textArea.offsetMax = new Vector2(-18f, -14f);

            var placeholder = CreateText(
                "Placeholder",
                textArea,
                placeholderText,
                fontSize,
                new Color(ModernGuiTheme.MutedTextColor.r, ModernGuiTheme.MutedTextColor.g, ModernGuiTheme.MutedTextColor.b, 0.74f),
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform);

            var text = CreateText("Text", textArea, string.Empty, fontSize, ModernGuiTheme.TextColor, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform);

            var inputField = rect.gameObject.AddComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretColor = ModernGuiTheme.AccentWarmColor;
            inputField.selectionColor = new Color(ModernGuiTheme.AccentWarmColor.r, ModernGuiTheme.AccentWarmColor.g, ModernGuiTheme.AccentWarmColor.b, 0.35f);
            return inputField;
        }

        public static ScrollRect CreateScrollView(string name, Transform parent, out RectTransform content)
        {
            var root = CreateUiRoot(name, parent);
            AddImage(root.gameObject, new Color(0.05f, 0.06f, 0.08f, 0.72f));
            ApplyOneUiRounding(root.gameObject, 12f);
            AddOutline(root.gameObject, new Color(0f, 0f, 0f, 0.5f), new Vector2(1f, -1f));

            var viewport = CreateUiRoot("Viewport", root);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateUiRoot("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            content.localScale = Vector3.one;

            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            return scrollRect;
        }

        public static VerticalLayoutGroup AddVerticalLayout(Transform target, float spacing, RectOffset padding, bool controlHeight = true)
        {
            var layout = target.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = controlHeight;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(Transform target, float spacing, RectOffset padding, bool forceExpandHeight = false)
        {
            var layout = target.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = forceExpandHeight;
            return layout;
        }

        public static ContentSizeFitter AddContentSizeFitter(Transform target, ContentSizeFitter.FitMode verticalMode)
        {
            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = verticalMode;
            return fitter;
        }

        public static void RemoveContentSizeFitter(GameObject target)
        {
            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(fitter);
            }
            else
            {
                Object.DestroyImmediate(fitter);
            }
        }

        public static LayoutElement EnsureLayoutElement(Transform target, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }

            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static RectTransform CreateCard(string name, Transform parent, Color color, Color accentColor)
        {
            var rect = CreateUiRoot(name, parent);
            AddImage(rect.gameObject, color);
            ApplyOneUiRounding(rect.gameObject, 16f);
            AddOutline(rect.gameObject, ModernGuiTheme.BorderColor, new Vector2(1f, -1f));
            AddShadow(rect.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -5f));

            var accent = CreateUiRoot("Accent", rect);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = new Vector2(1f, 1f);
            accent.pivot = new Vector2(0.5f, 1f);
            accent.sizeDelta = new Vector2(0f, 6f);
            accent.anchoredPosition = Vector2.zero;
            AddImage(accent.gameObject, accentColor);
            var accentLayout = EnsureLayoutElement(accent, preferredHeight: 6f);
            accentLayout.ignoreLayout = true;

            return rect;
        }

        public static GameObject CreateSpacer(string name, Transform parent, float preferredHeight)
        {
            var spacer = CreateUiRoot(name, parent).gameObject;
            EnsureLayoutElement(spacer.transform, preferredHeight: preferredHeight);
            return spacer;
        }

        public static void ClearChildren(Transform target)
        {
            for (var i = target.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(target.GetChild(i).gameObject);
            }
        }
    }
}
