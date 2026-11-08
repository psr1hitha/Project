// Builds world-space uGUI + TextMeshPro elements from code, so panels need no hand-made prefabs
// and two people never fight over the same prefab. 1 canvas pixel = 1 mm in the headset.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    public static class UIFactory
    {
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static Canvas SetupWorldCanvas(GameObject go, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            var canvas = GetOrAdd<Canvas>(go);
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = GetOrAdd<CanvasScaler>(go);
            scaler.dynamicPixelsPerUnit = 4;
            scaler.referencePixelsPerUnit = 100;
            GetOrAdd<GraphicRaycaster>(go);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * AtlasTheme.PixelsToMeters;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Panel(string name, Transform parent, Color color, float radius = AtlasTheme.Radius)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            if (radius > 0)
            {
                var r = rt.gameObject.AddComponent<RoundedImage>();
                r.radius = radius;
                r.Apply();
            }
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, string name = "Text")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Small uppercase label, e.g. "YOUR MISSIONS".</summary>
        public static TextMeshProUGUI Caps(Transform parent, string text, Color? color = null) =>
            Text(parent, text.ToUpperInvariant(), AtlasTheme.Caps, color ?? AtlasTheme.TextMuted, FontStyles.Bold, TextAlignmentOptions.Left, "Caps");

        public static Button Button(Transform parent, string label, Color bg, Color fg, float height = 48f,
            float fontSize = AtlasTheme.Body, TextAlignmentOptions align = TextAlignmentOptions.Center, float radius = AtlasTheme.RadiusSmall)
        {
            var img = Panel("Button: " + label, parent, bg, radius);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = Tints();
            LE(img.gameObject, minH: height, prefH: height);
            var t = Text(img.transform, label, fontSize, fg, FontStyles.Bold, align, "Label");
            Stretch(t.rectTransform);
            t.margin = new Vector4(16, 0, 16, 0);
            return btn;
        }

        public static ColorBlock Tints() => new ColorBlock
        {
            normalColor = new Color(0.88f, 0.88f, 0.88f, 1f),
            highlightedColor = Color.white,
            pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
            selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f),
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        public static VerticalLayoutGroup VLayout(GameObject go, int pad = AtlasTheme.Pad, float spacing = 10f, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = GetOrAdd<VerticalLayoutGroup>(go);
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, int pad = 0, float spacing = 8f, TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false)
        {
            var h = GetOrAdd<HorizontalLayoutGroup>(go);
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return h;
        }

        public static ContentSizeFitter FitHeight(GameObject go, bool fitWidth = false)
        {
            var f = GetOrAdd<ContentSizeFitter>(go);
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = fitWidth ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static LayoutElement LE(GameObject go, float minH = -1, float prefH = -1, float prefW = -1, float flexW = -1, float minW = -1)
        {
            var le = GetOrAdd<LayoutElement>(go);
            le.minHeight = minH;
            le.preferredHeight = prefH;
            le.preferredWidth = prefW;
            le.flexibleWidth = flexW;
            le.minWidth = minW;
            return le;
        }

        /// <summary>The card every panel sits on: rounded, padded, grows to fit its content.</summary>
        public static RectTransform Card(Transform canvasRoot, float width, Vector2 pivot, int pad = AtlasTheme.Pad, float spacing = 12f)
        {
            var img = Panel("Card", canvasRoot, AtlasTheme.PanelBg, AtlasTheme.Radius);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = pivot;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, 100);
            VLayout(img.gameObject, pad, spacing);
            FitHeight(img.gameObject);
            return rt;
        }

        public static RectTransform Row(Transform parent, float spacing = 8f, TextAnchor align = TextAnchor.MiddleLeft, string name = "Row")
        {
            var rt = Rect(name, parent);
            HLayout(rt.gameObject, 0, spacing, align);
            return rt;
        }

        public static Image Divider(Transform parent)
        {
            var img = Panel("Divider", parent, AtlasTheme.Divider, 0);
            img.raycastTarget = false;
            LE(img.gameObject, minH: 1, prefH: 1);
            return img;
        }

        /// <summary>Colored pill with text, e.g. a node kind or task status.</summary>
        public static TextMeshProUGUI Chip(Transform parent, string text, Color color, out Image bg)
        {
            bg = Panel("Chip", parent, new Color(color.r, color.g, color.b, 0.18f), 11f);
            bg.raycastTarget = false;
            HLayout(bg.gameObject, 0, 0, TextAnchor.MiddleCenter);
            var h = bg.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 4, 4);
            var t = Text(bg.transform, text, AtlasTheme.Small, color, FontStyles.Bold, TextAlignmentOptions.Center);
            return t;
        }

        public static TMP_InputField InputField(Transform parent, string placeholder, float height = 46f)
        {
            var bg = Panel("Input", parent, AtlasTheme.Surface, AtlasTheme.RadiusSmall);
            LE(bg.gameObject, minH: height, prefH: height);
            var go = bg.gameObject;
            go.SetActive(false); // configure before TMP_InputField.OnEnable runs

            var area = Rect("Text Area", go.transform);
            Stretch(area);
            area.offsetMin = new Vector2(14, 6);
            area.offsetMax = new Vector2(-14, -6);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Text(area, placeholder, AtlasTheme.Body, AtlasTheme.TextFaint, FontStyles.Italic, TextAlignmentOptions.Left, "Placeholder");
            Stretch(ph.rectTransform);
            var tx = Text(area, "", AtlasTheme.Body, AtlasTheme.Text, FontStyles.Normal, TextAlignmentOptions.Left, "Text");
            Stretch(tx.rectTransform);

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = tx;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.colors = Tints();
            field.characterLimit = 24;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretColor = AtlasTheme.Text;
            field.customCaretColor = true;
            field.selectionColor = new Color(AtlasTheme.Agent.r, AtlasTheme.Agent.g, AtlasTheme.Agent.b, 0.35f);
            go.SetActive(true);
            return field;
        }

        /// <summary>Destroys children in play mode or edit mode.</summary>
        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(c);
                else Object.DestroyImmediate(c);
            }
        }

        public static void SetAlpha(Graphic g, float a)
        {
            var c = g.color; c.a = a; g.color = c;
        }
    }
}
