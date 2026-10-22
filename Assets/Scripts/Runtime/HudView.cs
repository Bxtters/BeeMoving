using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Handline
{
    public enum HudMode
    {
        Attract,
        Play,
        Pause,
        Over
    }

    public sealed class HudView : MonoBehaviour
    {
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.72f, 0.66f, 0.56f);
        static readonly Color Amber = new Color(0.98f, 0.74f, 0.28f);
        static readonly Color Coral = new Color(0.95f, 0.38f, 0.26f);
        static readonly Color Teal = new Color(0.45f, 0.82f, 0.78f);
        static readonly Color Ink = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        static readonly Color CardColor = new Color(0.10f, 0.09f, 0.08f, 0.92f);

        public RawImage Preview;
        public Dropdown Cameras;
        public Dropdown People;
        public RectTransform OverlayRoot;
        public Image Vignette;

        Text scoreText;
        Text coinText;
        Text livesText;
        Text gestureText;
        Text statusText;
        Text overScore;
        Image runFill;
        GameObject attract;
        GameObject pause;
        GameObject over;
        bool suppressCameras;
        bool jumpWas;
        float punch = 1f;

        public static HudView Create()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Sprite sprite = WhiteSprite();

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>();
            }

            GameObject canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            HudView hud = canvasObject.AddComponent<HudView>();
            hud.Build(canvasObject.transform, font, sprite);
            return hud;
        }

        public void SetCameras(IList<string> names, int index)
        {
            suppressCameras = true;
            Cameras.ClearOptions();
            List<string> options = new List<string>();
            if (names == null || names.Count == 0) options.Add("No camera");
            else
            {
                for (int i = 0; i < names.Count; i++) options.Add(names[i]);
            }

            Cameras.AddOptions(options);
            int max = Mathf.Max(0, options.Count - 1);
            Cameras.value = Mathf.Clamp(index, 0, max);
            Cameras.RefreshShownValue();
            suppressCameras = false;
        }

        public void Bind(UnityAction<int> cameraChanged)
        {
            Cameras.onValueChanged.AddListener(delegate(int value)
            {
                if (!suppressCameras && cameraChanged != null) cameraChanged(value);
            });
        }

        public void SetBoard(int score, int coins, int lives, HudMode mode, GestureSignal signal, string status)
        {
            scoreText.text = score.ToString("N0");
            coinText.text = coins == 1 ? "1 coin" : coins + " coins";
            livesText.text = Dots(lives);
            statusText.text = status;
            gestureText.text = string.IsNullOrEmpty(signal.Label) ? "Ready" : signal.Label;
            gestureText.color = GestureColor(signal);
            runFill.fillAmount = Mathf.Clamp01(signal.Run);

            if (signal.LaneStep != 0 || (signal.JumpHeld && !jumpWas)) punch = 1.14f;
            jumpWas = signal.JumpHeld;
            punch = Mathf.MoveTowards(punch, 1f, Time.deltaTime * 0.9f);
            gestureText.rectTransform.localScale = new Vector3(punch, punch, 1f);

            attract.SetActive(mode == HudMode.Attract);
            pause.SetActive(mode == HudMode.Pause);
            over.SetActive(mode == HudMode.Over);
            if (mode == HudMode.Over)
            {
                overScore.text = score.ToString("N0") + "   ·   " + coins + " coins";
            }
        }

        public void SetHurt(float hurt01)
        {
            Vignette.color = new Color(0.55f, 0.08f, 0.05f, Mathf.Clamp01(hurt01) * 0.45f);
        }

        void Build(Transform parent, Font font, Sprite sprite)
        {
            Vignette = ImagePanel(parent, "Vignette", sprite, new Color(0f, 0f, 0f, 0f));
            Stretch(Vignette.rectTransform);
            Vignette.raycastTarget = false;

            Text kicker = TextAt(parent, font, "Kicker", "HANDLINE", 18, TextAnchor.UpperLeft, Amber);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(280f, 28f));
            scoreText = TextAt(parent, font, "Score", "0", 54, TextAnchor.UpperLeft, Cream);
            Place(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -52f), new Vector2(360f, 64f));
            coinText = TextAt(parent, font, "Coins", "0 coins", 20, TextAnchor.UpperLeft, Muted);
            Place(coinText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -112f), new Vector2(280f, 28f));
            livesText = TextAt(parent, font, "Lives", "Lives  3", 28, TextAnchor.UpperRight, Coral);
            Place(livesText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(220f, 40f));

            Image chip = ImagePanel(parent, "Gesture Chip", sprite, CardColor);
            Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(280f, 92f));
            gestureText = TextAt(chip.transform, font, "Gesture", "Ready", 36, TextAnchor.MiddleCenter, Cream);
            Stretch(gestureText.rectTransform, 12f, 28f, -12f, -8f);
            Image runTrack = ImagePanel(chip.transform, "Run Track", sprite, new Color(1f, 1f, 1f, 0.08f));
            Place(runTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(220f, 8f));
            runFill = ImagePanel(runTrack.transform, "Run Fill", sprite, Amber);
            runFill.type = Image.Type.Filled;
            runFill.fillMethod = Image.FillMethod.Horizontal;
            runFill.fillAmount = 0f;
            Stretch(runFill.rectTransform);

            Image previewCard = ImagePanel(parent, "Preview Card", sprite, Ink);
            Place(previewCard.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(300f, 430f));
            GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(previewCard.transform, false);
            Preview = previewObject.GetComponent<RawImage>();
            Preview.color = Color.white;
            Preview.raycastTarget = false;
            Place(Preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(276f, 276f));

            GameObject overlayObject = new GameObject("Skeleton", typeof(RectTransform));
            overlayObject.transform.SetParent(Preview.transform, false);
            OverlayRoot = overlayObject.GetComponent<RectTransform>();
            OverlayRoot.anchorMin = Vector2.zero;
            OverlayRoot.anchorMax = Vector2.zero;
            OverlayRoot.pivot = Vector2.zero;
            OverlayRoot.anchoredPosition = Vector2.zero;
            OverlayRoot.sizeDelta = new Vector2(276f, 276f);

            statusText = TextAt(previewCard.transform, font, "Status", "Looking for a camera", 15, TextAnchor.UpperLeft, Muted);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            Place(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -300f), new Vector2(276f, 48f));

            Cameras = DropdownAt(previewCard.transform, font, sprite);
            Place(Cameras.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 14f), new Vector2(276f, 36f));

            Image legend = ImagePanel(parent, "Legend", sprite, Ink);
            Place(legend.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(360f, 210f));
            Text help = TextAt(legend.transform, font, "Help",
                "<color=#f2b84a>Both hands up</color>     Jump\n" +
                "<color=#f2b84a>Pump your arms</color>     Sprint\n" +
                "<color=#f2b84a>Hands apart and down</color>     Slide\n" +
                "<color=#f2b84a>One hand out</color>     One lane\n\n" +
                "<size=15><color=#b7aa98>Arrows lane · Space jump · Down slide · Shift sprint\nF mirror · Q rotate · C next camera · Esc pause</color></size>",
                20, TextAnchor.UpperLeft, Cream);
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            help.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(help.rectTransform, 16f, 14f, -16f, -14f);

            attract = Card(parent, font, sprite, "Attract", "HANDLINE", "Your hands run the lane.", "Raise both hands, or press Space.");
            People = DropdownAt(attract.transform, font, sprite);
            Place(People.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(280f, 34f));
            People.ClearOptions();
            People.AddOptions(new List<string> { "1 person", "2 people", "3 people", "4 people", "5 people", "6 people" });
            People.value = 0;
            People.RefreshShownValue();
            pause = Card(parent, font, sprite, "Pause", "PAUSED", "The track is holding.", "Esc to run again.");
            over = Card(parent, font, sprite, "Over", "CAUGHT", "The lane got there first.", "Hold both hands up, or press Space.");
            overScore = TextAt(over.transform, font, "Over Score", "0", 28, TextAnchor.MiddleCenter, Amber);
            Place(overScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(520f, 40f));
        }

        GameObject Card(Transform parent, Font font, Sprite sprite, string name, string title, string body, string prompt)
        {
            Image panel = ImagePanel(parent, name, sprite, CardColor);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(640f, 250f));
            Text heading = TextAt(panel.transform, font, "Title", title, 58, TextAnchor.MiddleCenter, Cream);
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 72f));
            Text line = TextAt(panel.transform, font, "Body", body, 24, TextAnchor.MiddleCenter, Muted);
            Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(580f, 36f));
            Text cue = TextAt(panel.transform, font, "Prompt", prompt, 22, TextAnchor.MiddleCenter, Amber);
            Place(cue.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(580f, 36f));
            return panel.gameObject;
        }

        Dropdown DropdownAt(Transform parent, Font font, Sprite sprite)
        {
            GameObject root = new GameObject("Cameras", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.color = new Color(1f, 1f, 1f, 0.06f);
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;

            Text caption = TextAt(root.transform, font, "Label", "Camera", 15, TextAnchor.MiddleLeft, Cream);
            Stretch(caption.rectTransform, 10f, 2f, -28f, -2f);
            dropdown.captionText = caption;

            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            RectTransform templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0f, 1f);
            templateRt.anchorMax = new Vector2(1f, 1f);
            templateRt.pivot = new Vector2(0.5f, 0f);
            templateRt.anchoredPosition = new Vector2(0f, 4f);
            templateRt.sizeDelta = new Vector2(0f, 150f);
            Image templateImage = template.GetComponent<Image>();
            templateImage.sprite = sprite;
            templateImage.color = new Color(0.12f, 0.10f, 0.09f, 0.98f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 32f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<LayoutElement>().preferredHeight = 32f;
            Toggle toggle = item.GetComponent<Toggle>();
            Image itemBg = ImagePanel(item.transform, "Background", sprite, new Color(1f, 1f, 1f, 0.04f));
            Stretch(itemBg.rectTransform);
            toggle.targetGraphic = itemBg;
            ColorBlock colors = toggle.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.02f);
            colors.highlightedColor = new Color(0.98f, 0.74f, 0.28f, 0.28f);
            colors.pressedColor = new Color(0.98f, 0.74f, 0.28f, 0.45f);
            colors.selectedColor = colors.highlightedColor;
            toggle.colors = colors;
            Text itemLabel = TextAt(item.transform, font, "Item Label", "Camera", 15, TextAnchor.MiddleLeft, Cream);
            Stretch(itemLabel.rectTransform, 10f, 0f, -8f, 0f);

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
            return dropdown;
        }

        static string Dots(int lives)
        {
            if (lives < 0) lives = 0;
            return "Lives  " + lives;
        }

        static Color GestureColor(GestureSignal signal)
        {
            if (!signal.Tracking) return Muted;
            if (signal.JumpHeld) return Amber;
            if (signal.DuckHeld) return Teal;
            if (signal.LaneStep != 0 || signal.Label == "Left" || signal.Label == "Right") return Coral;
            if (signal.Run > 0.55f) return Amber;
            return Cream;
        }

        static Text TextAt(Transform parent, Font font, string name, string value, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.text = value;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static Image ImagePanel(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            Stretch(rt, 0f, 0f, 0f, 0f);
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        static Sprite WhiteSprite()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
