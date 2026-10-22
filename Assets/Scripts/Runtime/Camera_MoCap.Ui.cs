using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Handline
{
    /// <summary>
    /// Start screen, settings card, dropdowns, and the widgets they are built from.
    /// Added so the camera capture and the pose read are not mixed with the layout.
    /// </summary>
    public sealed partial class Camera_MoCap
    {
        /// <summary>
        /// Shows a setup failure if the canvas could not be built.
        /// </summary>
        void OnGUI()
        {
            if (string.IsNullOrEmpty(setupError)) return;
            GUI.Label(new Rect(16f, 16f, Screen.width - 32f, 140f), setupError);
        }


        /// <summary>
        /// Shows the title card until the player starts. The character is already standing behind it.
        /// </summary>
        void BuildStartScreen(Font font, Sprite sprite)
        {
            startRoot = new GameObject("Start Screen", typeof(RectTransform));
            startRoot.transform.SetParent(canvasRect, false);
            Stretch(startRoot.GetComponent<RectTransform>());

            Image dim = MakeImage("Start Dim", startRoot.transform, sprite, new Color(0.35f, 0.26f, 0.08f, 0.28f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            Image card = MakeImage("Start Card", startRoot.transform, roundSprite, Card);
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 150f);
            cardRect.sizeDelta = new Vector2(560f, 340f);

            Image rim = MakeImage("Start Rim", startRoot.transform, roundSprite, Border);
            rim.type = Image.Type.Sliced;
            rim.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rim.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            rim.rectTransform.sizeDelta = new Vector2(572f, 352f);
            rim.rectTransform.SetSiblingIndex(card.transform.GetSiblingIndex());

            Text heading = MakeText("Start Title", card.transform, font, "Camera MoCap", 40, TextAnchor.MiddleCenter, Ink);
            heading.fontStyle = FontStyle.Bold;
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 52f));
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);

            Text body = MakeText("Start Body", card.transform, font, "A character stands on the floor and follows your camera pose. The picture stays in the corner and is not saved.", 20, TextAnchor.UpperCenter, Ink);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(460f, 90f));
            body.rectTransform.pivot = new Vector2(0.5f, 1f);

            Button start = MakeButton("Start", card.transform, roundSprite, font, "Start", BeginSession, Btn.Filled);
            Text startLabel = start.GetComponentInChildren<Text>();
            startLabel.fontSize = 22;
            startLabel.fontStyle = FontStyle.Bold;
            RectTransform startRect = start.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 36f);
            startRect.sizeDelta = new Vector2(220f, 56f);
        }


        /// <summary>
        /// Leaves the title card and opens the camera so the character can follow.
        /// </summary>
        void BeginSession()
        {
            if (sessionStarted) return;
            sessionStarted = true;
            if (startRoot != null) startRoot.SetActive(false);
            if (modelPrepared && ModelReady() && webcam == null && !cameraHeldOff)
            {
                OpenCamera(ChooseStartupCamera());
            }
        }


        /// <summary>
        /// Returns Unity's built-in UI font.
        /// </summary>
        static Font BuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }


        /// <summary>
        /// Creates the small white sprite used by the skeleton bars and the stage background.
        /// </summary>
        Sprite WhiteSprite()
        {
            whiteTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            whiteTex.filterMode = FilterMode.Point;
            whiteTex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            whiteTex.SetPixels(pixels);
            whiteTex.Apply(false, false);
            whiteSprite = Sprite.Create(whiteTex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            return whiteSprite;
        }


        /// <summary>
        /// Builds the rounded rectangle used by cards and buttons. The border keeps the corners even when the image is sliced.
        /// </summary>
        Sprite RoundedSprite()
        {
            const int size = 64;
            const float radius = 18f;
            roundTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            roundTex.filterMode = FilterMode.Bilinear;
            roundTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundAlpha(x + 0.5f, y + 0.5f, size, radius);
                    byte channel = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, channel);
                }
            }

            roundTex.SetPixels32(pixels);
            roundTex.Apply(false, false);
            roundSprite = Sprite.Create(roundTex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return roundSprite;
        }


        /// <summary>
        /// Signed coverage of one pixel in a rounded rectangle, 1 inside and 0 outside.
        /// </summary>
        static float RoundAlpha(float x, float y, float size, float radius)
        {
            float half = size * 0.5f;
            float dx = Mathf.Abs(x - half) - (half - radius);
            float dy = Mathf.Abs(y - half) - (half - radius);
            float outside = Mathf.Sqrt((Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)) + (Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01(0.5f - (outside + inside - radius));
        }


        /// <summary>
        /// Creates the event system the settings buttons need.
        /// A module created from code has no pointer actions until AssignDefaultActions runs, so clicks never reach the card.
        /// </summary>
        static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                InputSystemUIInputModule current = existing.GetComponent<InputSystemUIInputModule>();
                if (current == null)
                {
                    StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
                    if (legacy != null) Destroy(legacy);
                    current = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                current.AssignDefaultActions();
                return;
            }

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            InputSystemUIInputModule created = events.AddComponent<InputSystemUIInputModule>();
            created.AssignDefaultActions();
        }


        /// <summary>
        /// Builds the settings card on the stage canvas.
        /// The card is light yellow with a gold rim, and it scrolls so every row can be reached.
        /// </summary>
        void BuildSettings(Font font, Sprite sprite)
        {
            int u = 1;
            RectTransform root = canvasRect;

            Button open = MakeButton("Open Settings", root, roundSprite, font, "Settings", delegate { SetSettingsOpen(true); }, Btn.Filled);
            Text openLabel = open.GetComponentInChildren<Text>();
            openLabel.fontSize = 14 * u;
            openLabel.fontStyle = FontStyle.Bold;
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-24f * u, -20f * u);
            openRect.sizeDelta = new Vector2(132f * u, 40f * u);

            settingsRoot = new GameObject("Settings", typeof(RectTransform));
            settingsRoot.transform.SetParent(root, false);
            Stretch(settingsRoot.GetComponent<RectTransform>());

            Image dim = MakeImage("Dim", settingsRoot.transform, sprite, new Color(0.35f, 0.26f, 0.08f, 0.35f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(delegate { SetSettingsOpen(false); });

            settingsShadow = MakeImage("Shadow", settingsRoot.transform, roundSprite, Drop).rectTransform;
            settingsShadow.GetComponent<Image>().type = Image.Type.Sliced;
            settingsShadow.anchorMin = new Vector2(0.5f, 0.5f);
            settingsShadow.anchorMax = new Vector2(0.5f, 0.5f);
            settingsShadow.pivot = new Vector2(0.5f, 0.5f);

            settingsBorder = MakeImage("Border", settingsRoot.transform, roundSprite, Border).rectTransform;
            settingsBorder.GetComponent<Image>().type = Image.Type.Sliced;
            settingsBorder.anchorMin = new Vector2(0.5f, 0.5f);
            settingsBorder.anchorMax = new Vector2(0.5f, 0.5f);
            settingsBorder.pivot = new Vector2(0.5f, 0.5f);

            Image panel = MakeImage("Panel", settingsRoot.transform, roundSprite, Card);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            settingsCard = panel.rectTransform;
            settingsCard.anchorMin = new Vector2(0.5f, 0.5f);
            settingsCard.anchorMax = new Vector2(0.5f, 0.5f);
            settingsCard.pivot = new Vector2(0.5f, 0.5f);
            settingsCard.sizeDelta = new Vector2(520f, 640f);
            FitCardChrome(520f, 640f);

            Button close = MakeButton("Close", panel.transform, roundSprite, font, "Close", delegate { SetSettingsOpen(false); }, Btn.Text);
            Text closeLabel = close.GetComponentInChildren<Text>();
            closeLabel.fontSize = 14 * u;
            closeLabel.color = Muted;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-12f * u, -12f * u);
            closeRect.sizeDelta = new Vector2(72f * u, 36f * u);

            settingsMain = new GameObject("Main", typeof(RectTransform));
            settingsMain.transform.SetParent(panel.transform, false);
            Stretch(settingsMain.GetComponent<RectTransform>());
            Text heading = MakeText("Heading", settingsMain.transform, font, "Settings", 26 * u, TextAnchor.MiddleLeft, Ink);
            heading.fontStyle = FontStyle.Bold;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(0f, 1f);
            headingRect.pivot = new Vector2(0f, 1f);
            headingRect.anchoredPosition = new Vector2(24f * u, -16f * u);
            headingRect.sizeDelta = new Vector2(220f * u, 32f * u);

            Transform content = MakeScroll(settingsMain.transform, sprite, u, 64f * u, false);
            BuildSettingsMain(content, font, sprite, u);
            BuildPairingPage(panel.transform, font, sprite, u);
            BuildCablePage(panel.transform, font, sprite, u);

            settingsRoot.transform.SetAsLastSibling();
            settingsRoot.SetActive(false);
            LayoutSettingsCard();
            RefreshSettingLabels();
        }


        /// <summary>
        /// Keeps the settings card inside the window. A card taller than the view was drawing off screen.
        /// </summary>
        void LayoutSettingsCard()
        {
            if (settingsCard == null || canvasRect == null) return;
            Rect area = canvasRect.rect;
            if (area.width < 16f || area.height < 16f) return;
            int seenW = Mathf.RoundToInt(area.width);
            int seenH = Mathf.RoundToInt(area.height);
            if (seenW == settingsSeenW && seenH == settingsSeenH) return;
            settingsSeenW = seenW;
            settingsSeenH = seenH;
            float w = Mathf.Clamp(area.width - 80f, 360f, 520f);
            float h = Mathf.Clamp(area.height - 96f, 460f, 700f);
            settingsCard.sizeDelta = new Vector2(w, h);
            FitCardChrome(w, h);
        }


        /// <summary>
        /// Sizes the gold rim and the soft drop behind the settings card.
        /// </summary>
        void FitCardChrome(float width, float height)
        {
            if (settingsBorder != null)
            {
                settingsBorder.sizeDelta = new Vector2(width + 10f, height + 10f);
                settingsBorder.anchoredPosition = Vector2.zero;
            }

            if (settingsShadow == null) return;
            settingsShadow.sizeDelta = new Vector2(width + 10f, height + 10f);
            settingsShadow.anchoredPosition = new Vector2(0f, -12f);
        }


        /// <summary>
        /// Lays out the camera and capture rows inside the scrolling page.
        /// </summary>
        void BuildSettingsMain(Transform page, Font font, Sprite sprite, int u)
        {
            SectionLabel(page, font, "Camera", 14 * u, 24 * u);
            cameraDropdown = AddDropdown(page, roundSprite, font, "No camera yet", PickCamera);

            Transform actions = ActionRow(page, 40f * u);
            FlexButton(actions, roundSprite, font, "Refresh", 120f * u, 40f * u, 16 * u, Btn.Text, RefreshCameras);

            Transform links = ActionRow(page, 44f * u);
            FlexButton(links, roundSprite, font, "Pair phone", 140f * u, 40f * u, 16 * u, Btn.Filled, ShowPairing);
            FlexButton(links, roundSprite, font, "USB cable", 140f * u, 40f * u, 16 * u, Btn.Filled, ShowCable);

            Spacer(page, 10f * u);
            SectionLabel(page, font, "Picture", 14 * u, 24 * u);
            resolutionDropdown = AddDropdown(page, roundSprite, font, "Auto", PickResolution);
            FillFixedDropdown(resolutionDropdown, new string[] { "Auto", "640 × 480", "1280 × 720", "1920 × 1080" }, ResolutionIndex());
            fpsDropdown = AddDropdown(page, roundSprite, font, "30", PickFrameRate);
            FillFixedDropdown(fpsDropdown, new string[] { "15", "30", "60" }, FpsIndex());
            mirrorDropdown = AddDropdown(page, roundSprite, font, "On", PickMirror);
            FillFixedDropdown(mirrorDropdown, new string[] { "Mirror on", "Mirror off" }, mirror ? 0 : 1);
            rotationDropdown = AddDropdown(page, roundSprite, font, "0°", PickRotation);
            FillFixedDropdown(rotationDropdown, new string[] { "0°", "90°", "180°", "270°" }, extraRotation / 90);

            Spacer(page, 10f * u);
            SectionLabel(page, font, "Tracking", 14 * u, 24 * u);
            peopleDropdown = AddDropdown(page, roundSprite, font, "1 person", PickPeople);
            FillFixedDropdown(peopleDropdown, new string[] { "1 person", "2 people", "3 people", "4 people", "5 people", "6 people" }, peopleLimit - 1);
            detailDropdown = AddDropdown(page, roundSprite, font, "Normal", PickDetail);
            FillFixedDropdown(detailDropdown, new string[] { "Relaxed", "Normal", "Firm" }, DetailIndex());
            powerDropdown = AddDropdown(page, roundSprite, font, "On", PickPower);
            FillFixedDropdown(powerDropdown, new string[] { "Camera on", "Camera off" }, cameraHeldOff ? 1 : 0);
        }


        /// <summary>
        /// Adds a scroll view that fills the parent below a top inset, so the card can be shorter than its contents.
        /// </summary>
        static Transform MakeScroll(Transform parent, Sprite sprite, int u, float top, bool clip)
        {
            GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(12f * u, 12f * u);
            scrollRect.offsetMax = new Vector2(-12f * u, -top);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
            if (clip) viewport.AddComponent<RectMask2D>();
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8 * u, 8 * u, 4 * u, 12 * u);
            layout.spacing = 2f * u;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            return content.transform;
        }


        /// <summary>
        /// Builds the pairing page. It stays hidden until Pair phone is clicked.
        /// </summary>
        void BuildPairingPage(Transform panel, Font font, Sprite sprite, int u)
        {
            settingsPairing = new GameObject("Pairing", typeof(RectTransform));
            settingsPairing.transform.SetParent(panel, false);
            Stretch(settingsPairing.GetComponent<RectTransform>());

            PlaceCenteredBack(settingsPairing.transform, roundSprite, font, HidePairing);
            Text title = MakeText("Pair Title", settingsPairing.transform, font, "Pair phone", 24 * u, TextAnchor.MiddleCenter, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceHeaderLine(title.rectTransform, 72f * u, 36f * u);
            pairingStatus = MakeText("Pair Status", settingsPairing.transform, font, "", 18 * u, TextAnchor.MiddleCenter, Ink);
            pairingStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            pairingStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceHeaderLine(pairingStatus.rectTransform, 114f * u, 40f * u);
            AddTextButton(settingsPairing.transform, roundSprite, font, "Refresh", 20f * u, 162f * u, 140f * u, 40f * u, 18 * u, RefreshCameras);

            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(settingsPairing.transform, false);
            RectTransform stepsRect = scrollObject.GetComponent<RectTransform>();
            stepsRect.anchorMin = Vector2.zero;
            stepsRect.anchorMax = Vector2.one;
            stepsRect.offsetMin = new Vector2(20f * u, 16f * u);
            stepsRect.offsetMax = new Vector2(-16f * u, -214f * u);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, PairingGuide, 18 * u, TextAnchor.UpperLeft, Ink);
            guide.horizontalOverflow = HorizontalWrapMode.Wrap;
            guide.verticalOverflow = VerticalWrapMode.Overflow;
            guide.lineSpacing = 1.08f;
            RectTransform guideRect = guide.rectTransform;
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(1f, 1f);
            guideRect.pivot = new Vector2(0.5f, 1f);
            guideRect.anchoredPosition = Vector2.zero;
            guideRect.sizeDelta = new Vector2(-4f * u, 0f);
            ContentSizeFitter fitter = guide.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = guideRect;
            settingsPairing.SetActive(false);
        }


        /// <summary>
        /// Builds the USB setup. It stays hidden until USB cable is clicked.
        /// </summary>
        void BuildCablePage(Transform panel, Font font, Sprite sprite, int u)
        {
            settingsCable = new GameObject("Cable", typeof(RectTransform));
            settingsCable.transform.SetParent(panel, false);
            Stretch(settingsCable.GetComponent<RectTransform>());

            PlaceCenteredBack(settingsCable.transform, roundSprite, font, HideCable);
            Text title = MakeText("Cable Title", settingsCable.transform, font, "USB cable", 24 * u, TextAnchor.MiddleCenter, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceHeaderLine(title.rectTransform, 72f * u, 36f * u);
            cableStatus = MakeText("Cable Status", settingsCable.transform, font, "", 18 * u, TextAnchor.MiddleCenter, Ink);
            cableStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            cableStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceHeaderLine(cableStatus.rectTransform, 114f * u, 40f * u);
            Transform cableActions = ActionRow(settingsCable.transform, 40f * u);
            RectTransform cableRow = cableActions.GetComponent<RectTransform>();
            cableRow.anchorMin = new Vector2(0f, 1f);
            cableRow.anchorMax = new Vector2(1f, 1f);
            cableRow.pivot = new Vector2(0.5f, 1f);
            cableRow.anchoredPosition = new Vector2(0f, -162f * u);
            cableRow.sizeDelta = new Vector2(0f, 40f * u);
            HorizontalLayoutGroup cableLayout = cableActions.GetComponent<HorizontalLayoutGroup>();
            cableLayout.padding = new RectOffset(16 * u, 16 * u, 0, 0);
            cableLayout.childForceExpandWidth = true;
            FlexButton(cableActions, roundSprite, font, "Check cable", 0f, 36f * u, 13 * u, Btn.Text, RefreshCameras);
            FlexButton(cableActions, roundSprite, font, "Apple driver", 0f, 36f * u, 13 * u, Btn.Filled, OpenAppleDriver);
            FlexButton(cableActions, roundSprite, font, "Iriun", 0f, 36f * u, 13 * u, Btn.Text, OpenIriun);
            BuildGuideScroll(settingsCable.transform, font, sprite, u, CableGuide, 214f * u);
            settingsCable.SetActive(false);
        }


        /// <summary>
        /// Adds the scrolling steps used by the pairing and cable pages.
        /// </summary>
        static void BuildGuideScroll(Transform parent, Font font, Sprite sprite, int u, string body, float top)
        {
            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20f * u, 16f * u);
            scrollRect.offsetMax = new Vector2(-16f * u, -top);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, body, 18 * u, TextAnchor.UpperLeft, Ink);
            guide.horizontalOverflow = HorizontalWrapMode.Wrap;
            guide.verticalOverflow = VerticalWrapMode.Overflow;
            guide.lineSpacing = 1.08f;
            RectTransform guideRect = guide.rectTransform;
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(1f, 1f);
            guideRect.pivot = new Vector2(0.5f, 1f);
            guideRect.anchoredPosition = Vector2.zero;
            guideRect.sizeDelta = new Vector2(-4f * u, 0f);
            ContentSizeFitter fitter = guide.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = guideRect;
        }


        /// <summary>
        /// Draws a one-pixel yellow hairline.
        /// </summary>
        static void Rule(Transform parent, Sprite sprite, float x, float y, float width)
        {
            Image line = MakeImage("Rule", parent, sprite, Line);
            PlaceTop(line.rectTransform, x, y, width, 2f);
        }


        /// <summary>
        /// Puts Back in the middle of the top of a settings page, with the label centered in the button.
        /// </summary>
        void PlaceCenteredBack(Transform parent, Sprite sprite, Font font, UnityAction action)
        {
            Button button = MakeButton("Back", parent, sprite, font, "Back", action, Btn.Filled);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -16f);
            rect.sizeDelta = new Vector2(160f, 44f);
        }


        /// <summary>
        /// Stretches a header line across the card, measured down from the top.
        /// </summary>
        static void PlaceHeaderLine(RectTransform rect, float down, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -down);
            rect.sizeDelta = new Vector2(-48f, height);
        }


        /// <summary>
        /// Adds a text button with the label centered in its box.
        /// </summary>
        static void AddTextButton(Transform parent, Sprite sprite, Font font, string label, float x, float y, float width, float height, int fontSize, UnityAction action)
        {
            Button button = MakeButton(label, parent, sprite, font, label, action, Btn.Text);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleCenter;
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
        }


        /// <summary>
        /// Adds one settings row and returns the value label.
        /// </summary>
        static Text AddSettingRow(Transform parent, Sprite sprite, Font font, string caption, int u, UnityAction action)
        {
            Button button = MakeButton(caption, parent, sprite, font, "", action, Btn.Row);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 48f * u;
            element.preferredHeight = 48f * u;
            Text name = MakeText("Name", button.transform, font, caption, 17 * u, TextAnchor.MiddleLeft, Ink);
            PlaceTop(name.rectTransform, 12f * u, 0f, 150f * u, 48f * u);
            Text value = MakeText("Value", button.transform, font, "", 17 * u, TextAnchor.MiddleRight, YellowDeep);
            RectTransform valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(-12f * u, 0f);
            valueRect.sizeDelta = new Vector2(140f * u, 40f * u);
            return value;
        }


        /// <summary>
        /// A short gray section name inside the settings list.
        /// </summary>
        static void SectionLabel(Transform parent, Font font, string message, int size, float height)
        {
            Text text = MakeText("Section", parent, font, message, size, TextAnchor.MiddleLeft, Muted);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }


        /// <summary>
        /// A single line of primary text inside the settings list.
        /// </summary>
        static Text BlockLabel(Transform parent, Font font, string message, int size, float height, Color color)
        {
            Text text = MakeText("Block", parent, font, message, size, TextAnchor.MiddleLeft, color);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return text;
        }


        /// <summary>
        /// A horizontal row of buttons inside the settings list.
        /// </summary>
        static Transform ActionRow(Transform parent, float height)
        {
            GameObject go = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return go.transform;
        }


        /// <summary>
        /// A button that takes part in a layout row.
        /// </summary>
        static void FlexButton(Transform parent, Sprite sprite, Font font, string label, float width, float height, int fontSize, Btn kind, UnityAction action)
        {
            Button button = MakeButton(label, parent, sprite, font, label, action, kind);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (kind == Btn.Filled) text.color = Ink;
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.flexibleWidth = width <= 0f ? 1f : 0f;
            element.preferredHeight = height;
            element.minHeight = height;
        }


        /// <summary>
        /// Empty vertical space in the settings list.
        /// </summary>
        static void Spacer(Transform parent, float height)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }


        /// <summary>
        /// Shows or hides the settings card.
        /// </summary>
        void SetSettingsOpen(bool open)
        {
            if (settingsRoot == null) return;
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            settingsRoot.SetActive(open);
            if (open) RefreshSettingLabels();
        }


        /// <summary>
        /// Opens the pairing steps and watches for a phone webcam.
        /// </summary>
        void ShowPairing()
        {
            if (settingsMain != null) settingsMain.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsPairing != null) settingsPairing.SetActive(true);
            waitingForPhone = true;
            waitingForCable = false;
            bool live = webcam != null && webcam.isPlaying;
            WebCamDevice[] devices = ReadDevices(!live);
            knownDeviceCount = devices == null ? 0 : devices.Length;
            if (TryConnectPhone(devices, "Phone camera connected. The picture stays on this device.")) return;
            if (pairingStatus != null) pairingStatus.text = "No phone camera yet. Follow the steps, then press Refresh.";
            SetStatus("Waiting for the phone. Follow the pairing steps, then press Refresh.");
        }


        /// <summary>
        /// Opens the cable setup and watches for an iPhone plugged in with a data cable.
        /// </summary>
        void ShowCable()
        {
            if (settingsMain != null) settingsMain.SetActive(false);
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(true);
            waitingForCable = true;
            waitingForPhone = false;
            bool live = webcam != null && webcam.isPlaying;
            WebCamDevice[] devices = ReadDevices(!live);
            knownDeviceCount = devices == null ? 0 : devices.Length;
            if (TryConnectPhone(devices, "Cable camera connected. The picture stays on this device.")) return;
            if (cableStatus != null) cableStatus.text = "Plug in the data cable and tap Trust. This page connects the iPhone when the computer lists it.";
            SetStatus("Waiting for the USB camera.");
        }


        /// <summary>
        /// Returns from the cable setup to the settings rows.
        /// </summary>
        void HideCable()
        {
            waitingForCable = false;
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            RefreshSettingLabels();
        }


        /// <summary>
        /// Returns from the pairing steps to the settings rows.
        /// </summary>
        void HidePairing()
        {
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            RefreshSettingLabels();
        }


        /// <summary>
        /// Builds one settings dropdown. The list opens downward and is not clipped by the card.
        /// </summary>
        Dropdown AddDropdown(Transform parent, Sprite sprite, Font font, string caption, UnityAction<int> onPick)
        {
            Dropdown dropdown = BuildDropdown(parent, sprite, font, caption);
            LayoutElement element = dropdown.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 44f;
            element.preferredHeight = 44f;
            element.flexibleWidth = 1f;
            dropdown.onValueChanged.AddListener(delegate (int index)
            {
                if (suppressDropdown || onPick == null) return;
                onPick(index);
            });
            return dropdown;
        }


        /// <summary>
        /// Creates the dropdown control Unity's template expects: caption, scroll list, and one item.
        /// </summary>
        Dropdown BuildDropdown(Transform parent, Sprite sprite, Font font, string caption)
        {
            GameObject root = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = Card;
            Outline rim = root.AddComponent<Outline>();
            rim.effectColor = Border;
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;

            Text label = MakeText("Label", root.transform, font, caption, 17, TextAnchor.MiddleLeft, Ink);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14f, 4f);
            label.rectTransform.offsetMax = new Vector2(-28f, -4f);
            dropdown.captionText = label;

            Text arrow = MakeText("Arrow", root.transform, font, "▾", 16, TextAnchor.MiddleCenter, YellowDeep);
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 0f);

            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            Image templateImage = template.GetComponent<Image>();
            templateImage.sprite = sprite;
            templateImage.type = Image.Type.Sliced;
            templateImage.color = Card;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Card;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 36f);
            VerticalLayoutGroup listLayout = content.GetComponent<VerticalLayoutGroup>();
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter listFit = content.GetComponent<ContentSizeFitter>();
            listFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<LayoutElement>().preferredHeight = 36f;
            Toggle toggle = item.GetComponent<Toggle>();
            Image itemImage = item.AddComponent<Image>();
            itemImage.color = new Color(1f, 0.95f, 0.8f, 1f);
            toggle.targetGraphic = itemImage;
            ColorBlock colors = toggle.colors;
            colors.normalColor = Card;
            colors.highlightedColor = Yellow;
            colors.pressedColor = Yellow;
            colors.selectedColor = Yellow;
            toggle.colors = colors;
            Text itemLabel = MakeText("Item Label", item.transform, font, "Option", 17, TextAnchor.MiddleLeft, Ink);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
            return dropdown;
        }


        /// <summary>
        /// Replaces a dropdown's choices without treating that as a user click.
        /// </summary>
        void FillFixedDropdown(Dropdown dropdown, string[] options, int value)
        {
            if (dropdown == null || options == null) return;
            suppressDropdown = true;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            if (value < 0) value = 0;
            if (value >= options.Length) value = options.Length - 1;
            dropdown.value = value;
            dropdown.RefreshShownValue();
            suppressDropdown = false;
        }


        /// <summary>
        /// Shows the current choice. A refresh does not count as the user picking it.
        /// </summary>
        void SetDropdown(Dropdown dropdown, int value)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0) return;
            if (value < 0) value = 0;
            if (value >= dropdown.options.Count) value = dropdown.options.Count - 1;
            suppressDropdown = true;
            dropdown.value = value;
            dropdown.RefreshShownValue();
            suppressDropdown = false;
        }


        /// <summary>
        /// Index of the saved resolution in the dropdown.
        /// </summary>
        int ResolutionIndex()
        {
            if (requestWidth == 640) return 1;
            if (requestWidth == 1280) return 2;
            if (requestWidth == 1920) return 3;
            return 0;
        }


        /// <summary>
        /// Index of the saved frame rate in the dropdown.
        /// </summary>
        int FpsIndex()
        {
            if (requestFps == 15) return 0;
            if (requestFps == 60) return 2;
            return 1;
        }


        /// <summary>
        /// Index of the tracking strictness in the dropdown.
        /// </summary>
        int DetailIndex()
        {
            if (jointThreshold < 0.16f) return 0;
            if (jointThreshold < 0.28f) return 1;
            return 2;
        }


        /// <summary>
        /// Opens the camera chosen in the dropdown.
        /// </summary>
        void PickCamera(int index)
        {
            if (cachedDevices == null || index < 0 || index >= cachedDevices.Length) return;
            SelectListedCamera(index);
        }


        /// <summary>
        /// Applies a resolution from the dropdown and reopens the camera.
        /// </summary>
        void PickResolution(int index)
        {
            if (index == 1)
            {
                requestWidth = 640;
                requestHeight = 480;
            }
            else if (index == 2)
            {
                requestWidth = 1280;
                requestHeight = 720;
            }
            else if (index == 3)
            {
                requestWidth = 1920;
                requestHeight = 1080;
            }
            else
            {
                requestWidth = 0;
                requestHeight = 0;
            }

            SaveSettings();
            ReopenCamera();
        }


        /// <summary>
        /// Applies a frame rate from the dropdown and reopens the camera.
        /// </summary>
        void PickFrameRate(int index)
        {
            requestFps = index == 0 ? 15 : index == 2 ? 60 : 30;
            SaveSettings();
            ReopenCamera();
        }


        /// <summary>
        /// Turns the selfie mirror on or off.
        /// </summary>
        void PickMirror(int index)
        {
            mirror = index == 0;
            SaveSettings();
        }


        /// <summary>
        /// Sets the extra rotation from the dropdown.
        /// </summary>
        void PickRotation(int index)
        {
            extraRotation = index * 90;
            if (extraRotation < 0 || extraRotation > 270) extraRotation = 0;
            SaveSettings();
        }


        /// <summary>
        /// Sets how many people to track. One person uses the Handline Lightning model.
        /// </summary>
        void PickPeople(int index)
        {
            peopleLimit = Mathf.Clamp(index + 1, 1, MaxPeople);
            for (int i = peopleLimit; i < MaxPeople; i++)
            {
                Blank(stable[i]);
                ClearTrack(i);
                slotUsed[i] = false;
                slotSeen[i] = 0f;
            }

            ApplyModelLabel();
            SetStatus(modelLabel);
            SaveSettings();
        }


        /// <summary>
        /// Sets how sure a new joint must be before it moves the skeleton.
        /// </summary>
        void PickDetail(int index)
        {
            if (index == 0) jointThreshold = 0.12f;
            else if (index == 2) jointThreshold = 0.35f;
            else jointThreshold = 0.2f;
            SaveSettings();
        }


        /// <summary>
        /// Turns the camera on or off from the dropdown.
        /// </summary>
        void PickPower(int index)
        {
            if (index == 0)
            {
                if (cameraHeldOff && !resuming) StartCoroutine(Resume());
                return;
            }

            if (!cameraHeldOff) StopCapture("Camera off. Turn it on in Settings, or press Enter.");
            RefreshSettingLabels();
        }


        /// <summary>
        /// Rewrites the settings labels from the current choices.
        /// </summary>
        void RefreshSettingLabels()
        {
            SyncCameraDropdown();
            SetDropdown(resolutionDropdown, ResolutionIndex());
            SetDropdown(fpsDropdown, FpsIndex());
            SetDropdown(mirrorDropdown, mirror ? 0 : 1);
            SetDropdown(rotationDropdown, extraRotation / 90);
            SetDropdown(peopleDropdown, peopleLimit - 1);
            SetDropdown(detailDropdown, DetailIndex());
            SetDropdown(powerDropdown, cameraHeldOff ? 1 : 0);
        }


        /// <summary>
        /// Names the camera selected in settings. The main stage still hides the device name.
        /// </summary>
        string CurrentCameraLabel()
        {
            WebCamDevice[] devices = cachedDevices;
            if (devices == null || devices.Length == 0) return "No camera yet";
            int index = cameraIndex;
            if (index < 0 || index >= devices.Length) index = 0;
            string name = ShortName(devices[index].name);
            if (devices[index].isFrontFacing) name += "  ·  front";
            return name + "   " + (index + 1) + " of " + devices.Length;
        }


        /// <summary>
        /// Keeps a long device name inside the settings card.
        /// </summary>
        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Camera";
            if (name.Length <= 42) return name;
            return name.Substring(0, 39) + "...";
        }


        /// <summary>
        /// Describes the joint-confidence choice.
        /// </summary>
        string DetailLabel()
        {
            if (jointThreshold < 0.25f) return "Relaxed";
            if (jointThreshold < 0.4f) return "Normal";
            return "Firm";
        }


        /// <summary>
        /// Moves to the previous or next camera the operating system exposes.
        /// </summary>
        void StepCamera(int delta)
        {
            WebCamDevice[] devices = ReadDevices(cachedDevices == null || cachedDevices.Length == 0);
            int count = devices == null ? 0 : devices.Length;
            if (count == 0)
            {
                SetStatus("No camera is listed. Allow Unity under Privacy and Camera, then press Refresh.");
                RefreshSettingLabels();
                return;
            }

            int next = cameraIndex + delta;
            if (next < 0) next = count - 1;
            if (next >= count) next = 0;
            cameraHeldOff = false;
            waitingForPhone = false;
            waitingForCable = false;
            if (!ModelReady())
            {
                pendingCamera = next;
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            OpenCamera(next);
        }


        /// <summary>
        /// Asks the operating system for cameras again and updates the settings card.
        /// </summary>
        void RefreshCameras()
        {
            WebCamDevice[] devices = ReadDevices(true);
            knownDeviceCount = devices == null ? 0 : devices.Length;
            RefreshSettingLabels();
            bool pairingOpen = settingsPairing != null && settingsPairing.activeSelf;
            bool cableOpen = settingsCable != null && settingsCable.activeSelf;
            if (pairingOpen) waitingForPhone = true;
            if (cableOpen) waitingForCable = true;
            if (!waitingForPhone && !waitingForCable)
            {
                SetStatus(knownDeviceCount == 0 ? "No camera yet." : "Camera list refreshed.");
                return;
            }

            string connected = (cableOpen || waitingForCable)
                ? "Cable camera connected. The picture stays on this device."
                : "Phone camera connected. The picture stays on this device.";
            if (TryConnectPhone(devices, connected)) return;

            if (cableOpen || waitingForCable)
            {
                if (cableStatus != null) cableStatus.text = "No cable camera yet. Plug in the data cable, tap Trust, then press Check cable.";
                SetStatus("Waiting for the USB camera. Leave the cable connected, then press Check cable.");
            }

            if (pairingOpen || waitingForPhone)
            {
                waitingForPhone = true;
                if (pairingStatus != null) pairingStatus.text = "No phone camera yet. Follow the steps, then press Refresh.";
                if (!cableOpen) SetStatus("Still waiting for the phone. Leave the webcam app running, then press Refresh.");
            }
        }


        /// <summary>
        /// Fills the camera dropdown when the devices or the selection change.
        /// </summary>
        void SyncCameraDropdown()
        {
            if (cameraDropdown == null) return;
            WebCamDevice[] devices = cachedDevices ?? new WebCamDevice[0];
            string sig = cameraIndex.ToString();
            for (int i = 0; i < devices.Length; i++) sig += "|" + devices[i].name;
            if (sig != deviceSig || cameraDropdown.options.Count != Mathf.Max(1, devices.Length))
            {
                deviceSig = sig;
                List<string> names = new List<string>();
                if (devices.Length == 0) names.Add("No camera yet");
                else
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        string label = ShortName(devices[i].name);
                        if (devices[i].isFrontFacing) label += "  ·  front";
                        names.Add(label);
                    }
                }

                suppressDropdown = true;
                cameraDropdown.ClearOptions();
                cameraDropdown.AddOptions(names);
                suppressDropdown = false;
            }

            SetDropdown(cameraDropdown, devices.Length == 0 ? 0 : cameraIndex);
        }


        /// <summary>
        /// Opens the camera the user picked in the settings list.
        /// </summary>
        void SelectListedCamera(int index)
        {
            waitingForPhone = false;
            waitingForCable = false;
            cameraHeldOff = false;
            if (!ModelReady())
            {
                pendingCamera = index;
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            OpenCamera(index);
        }


        /// <summary>
        /// Switches Auto, 640×480, 1280×720, and 1920×1080, then reopens the camera.
        /// </summary>
        void CycleResolution()
        {
            if (requestWidth <= 0)
            {
                requestWidth = 640;
                requestHeight = 480;
            }
            else if (requestWidth == 640)
            {
                requestWidth = 1280;
                requestHeight = 720;
            }
            else if (requestWidth == 1280)
            {
                requestWidth = 1920;
                requestHeight = 1080;
            }
            else
            {
                requestWidth = 0;
                requestHeight = 0;
            }

            SaveSettings();
            ReopenCamera();
        }


        /// <summary>
        /// Switches the requested frame rate between 15, 30, and 60, then reopens the camera.
        /// </summary>
        void CycleFrameRate()
        {
            if (requestFps == 15) requestFps = 30;
            else if (requestFps == 30) requestFps = 60;
            else requestFps = 15;
            SaveSettings();
            ReopenCamera();
        }


        /// <summary>
        /// Flips the selfie mirror and keeps that choice.
        /// </summary>
        void ToggleMirror()
        {
            mirror = !mirror;
            SaveSettings();
            RefreshSettingLabels();
            RefreshStatus();
        }


        /// <summary>
        /// Turns the camera crop by another 90 degrees and keeps that choice.
        /// </summary>
        void CycleRotation()
        {
            extraRotation = (extraRotation + 90) % 360;
            SaveSettings();
            RefreshSettingLabels();
            RefreshStatus();
        }


        /// <summary>
        /// Cycles how many people MoveNet is asked to return, from 1 through 6.
        /// </summary>
        void CyclePeople()
        {
            peopleLimit++;
            if (peopleLimit > MaxPeople) peopleLimit = 1;
            for (int i = peopleLimit; i < MaxPeople; i++)
            {
                Blank(stable[i]);
                slotUsed[i] = false;
            }

            SaveSettings();
            RefreshSettingLabels();
        }


        /// <summary>
        /// Cycles how confident a joint must be before it is drawn.
        /// </summary>
        void CycleDetail()
        {
            if (jointThreshold < 0.25f) jointThreshold = 0.3f;
            else if (jointThreshold < 0.4f) jointThreshold = 0.45f;
            else jointThreshold = 0.2f;
            SaveSettings();
            RefreshSettingLabels();
        }


        /// <summary>
        /// Turns capture off, or starts it again with the current settings.
        /// </summary>
        void TogglePower()
        {
            if (cameraHeldOff)
            {
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            StopCapture("Camera off. Turn it on in Settings, or press Enter.");
            RefreshSettingLabels();
        }


        /// <summary>
        /// Applies a resolution or frame-rate change to the open camera.
        /// </summary>
        void ReopenCamera()
        {
            RefreshSettingLabels();
            if (cameraHeldOff || !ModelReady()) return;
            OpenCamera(cameraIndex);
        }


        /// <summary>
        /// Places a control from the top-left of its parent.
        /// </summary>
        static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }


        /// <summary>
        /// Visual weight of a settings button. Filled is the yellow primary action.
        /// </summary>
        enum Btn
        {
            Text,
            Filled,
            Row
        }


        /// <summary>
        /// Creates a clickable settings button. The graphic stays visible so the raycast is not culled.
        /// </summary>
        static Button MakeButton(string name, Transform parent, Sprite sprite, Font font, string label, UnityAction action, Btn kind)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
            if (sprite != null)
            {
                Image face = MakeImage("Face", go.transform, sprite, kind == Btn.Filled ? Yellow : Card);
                face.type = Image.Type.Sliced;
                face.raycastTarget = false;
                RectTransform faceRect = face.rectTransform;
                faceRect.anchorMin = Vector2.zero;
                faceRect.anchorMax = Vector2.one;
                faceRect.offsetMin = new Vector2(3f, 3f);
                faceRect.offsetMax = new Vector2(-3f, -3f);
            }

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            Color labelColor = kind == Btn.Filled ? Ink : YellowDeep;
            colors.normalColor = Border;
            colors.highlightedColor = YellowDeep;
            colors.pressedColor = new Color(0.42f, 0.28f, 0.04f, 1f);
            colors.selectedColor = colors.highlightedColor;

            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            Text text = MakeText("Label", go.transform, font, label, 17, TextAnchor.MiddleCenter, labelColor);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }


        /// <summary>
        /// Adds a dark rim so a joint or bone stays visible on the camera picture.
        /// </summary>
        static void AddInkEdge(Image image)
        {
            Outline edge = image.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.1f, 0.07f, 0.02f, 0.92f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = true;
        }


        /// <summary>
        /// Creates a non-interactive image parented to the stage or the canvas.
        /// </summary>
        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }


        /// <summary>
        /// Creates a non-interactive text label.
        /// </summary>
        static Text MakeText(string name, Transform parent, Font font, string message, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = message;
            text.fontSize = size;
            text.alignment = anchor;
            text.alignByGeometry = true;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }


        /// <summary>
        /// Stretches a rect over its parent.
        /// </summary>
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }


        /// <summary>
        /// Places a label by a single anchor, a pixel position, and a size.
        /// </summary>
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

    }
}
