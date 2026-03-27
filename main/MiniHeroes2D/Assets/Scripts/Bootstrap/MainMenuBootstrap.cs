using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MiniHeroes2D.Bootstrap
{
    public static class MainMenuBootstrap
    {
        private static BattleBootstrap.CharacterClass selectedPlayerClass = BattleBootstrap.CharacterClass.Caballero;
        private static BattleBootstrap.CharacterClass selectedAiClass = BattleBootstrap.CharacterClass.Arquero;
        private static readonly Color TitleColor = new(0.98f, 0.90f, 0.55f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "MainMenu") return;

            if (Object.FindObjectOfType<Canvas>() != null && GameObject.Find("MainMenu_UI") != null) return;

            EnsureEventSystem();

            GameSessionConfig.LoadFromPrefs();
            selectedPlayerClass = GameSessionConfig.PlayerClass;
            selectedAiClass = GameSessionConfig.AiClass;

            GameObject root = new("MainMenu_UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            Sprite white = CreateWhiteSprite();

            GameObject bg = new("Bg");
            bg.transform.SetParent(root.transform, worldPositionStays: false);
            Image bgImage = bg.AddComponent<Image>();
            Sprite menuBackground = TryLoadMenuBackground();
            if (menuBackground != null)
            {
                bgImage.sprite = menuBackground;
                bgImage.color = Color.white;
                bgImage.preserveAspect = false;
            }
            else
            {
                bgImage.sprite = white;
                bgImage.color = new Color(0.08f, 0.1f, 0.16f, 1f);
            }
            RectTransform bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Subtle overlay for readability
            GameObject overlay = new("BgOverlay");
            overlay.transform.SetParent(root.transform, worldPositionStays: false);
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.sprite = white;
            overlayImage.color = new Color(0f, 0f, 0f, 0.28f);
            RectTransform overlayRect = overlayImage.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Panels
            GameObject mainPanel = new("Panel_Main");
            mainPanel.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            GameObject selectPanel = new("Panel_SelectCharacter");
            selectPanel.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform selectRect = selectPanel.AddComponent<RectTransform>();
            selectRect.anchorMin = Vector2.zero;
            selectRect.anchorMax = Vector2.one;
            selectRect.offsetMin = Vector2.zero;
            selectRect.offsetMax = Vector2.zero;
            selectPanel.SetActive(false);

            GameObject titleObject = new("Title");
            titleObject.transform.SetParent(mainPanel.transform, worldPositionStays: false);
            Text title = titleObject.AddComponent<Text>();
            title.text = "Joc 2D Mini Heroes";
            title.alignment = TextAnchor.MiddleCenter;
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontStyle = FontStyle.Bold;
            title.fontSize = 72;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 42;
            title.resizeTextMaxSize = 84;
            title.color = TitleColor;

            Outline titleOutline = titleObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(1000f, 150f);
            titleRect.anchoredPosition = Vector2.zero;

            // Main button (goes to character select screen)
            GameObject buttonObject = new("Button_VS_IA");
            buttonObject.transform.SetParent(mainPanel.transform, worldPositionStays: false);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.sprite = white;
            buttonImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            colors.highlightedColor = new Color(0.18f, 0.18f, 0.18f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.35f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                mainPanel.SetActive(false);
                selectPanel.SetActive(true);
            });

            Outline buttonOutline = buttonObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            buttonOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.45f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.45f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(360f, 84f);
            buttonRect.anchoredPosition = new Vector2(0f, -40f);

            GameObject buttonTextObject = new("Text");
            buttonTextObject.transform.SetParent(buttonRect, worldPositionStays: false);
            Text buttonText = buttonTextObject.AddComponent<Text>();
            buttonText.text = "VS IA";
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.fontSize = 44;
            buttonText.color = new Color(0.95f, 0.95f, 0.98f, 1f);

            RectTransform buttonTextRect = buttonText.rectTransform;
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;

            // --- Character select "screen" ---
            GameObject selectTitleObject = new("SelectTitle");
            selectTitleObject.transform.SetParent(selectPanel.transform, worldPositionStays: false);
            Text selectTitle = selectTitleObject.AddComponent<Text>();
            selectTitle.text = "SELECT CHARACTER";
            selectTitle.alignment = TextAnchor.MiddleCenter;
            selectTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            selectTitle.fontStyle = FontStyle.Bold;
            selectTitle.fontSize = 82;
            selectTitle.resizeTextForBestFit = true;
            selectTitle.resizeTextMinSize = 58;
            selectTitle.resizeTextMaxSize = 92;
            selectTitle.color = TitleColor;
            Outline selectTitleOutline = selectTitleObject.AddComponent<Outline>();
            selectTitleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            selectTitleOutline.effectDistance = new Vector2(3f, -3f);
            Shadow selectTitleShadow = selectTitleObject.AddComponent<Shadow>();
            selectTitleShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            selectTitleShadow.effectDistance = new Vector2(7f, -7f);

            RectTransform selectTitleRect = selectTitle.rectTransform;
            selectTitleRect.anchorMin = new Vector2(0.5f, 0.92f);
            selectTitleRect.anchorMax = new Vector2(0.5f, 0.92f);
            selectTitleRect.pivot = new Vector2(0.5f, 0.5f);
            selectTitleRect.sizeDelta = new Vector2(1200f, 160f);
            selectTitleRect.anchoredPosition = Vector2.zero;

            GameObject selectSubtitleObject = new("SelectSubtitle");
            selectSubtitleObject.transform.SetParent(selectPanel.transform, worldPositionStays: false);
            Text selectSubtitle = selectSubtitleObject.AddComponent<Text>();
            selectSubtitle.text = "Selecciona el teu personatge";
            selectSubtitle.alignment = TextAnchor.MiddleCenter;
            selectSubtitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            selectSubtitle.fontStyle = FontStyle.Bold;
            selectSubtitle.fontSize = 40;
            selectSubtitle.color = new Color(0.95f, 0.95f, 0.98f, 0.95f);
            RectTransform selectSubtitleRect = selectSubtitle.rectTransform;
            selectSubtitleRect.anchorMin = new Vector2(0.5f, 0.84f);
            selectSubtitleRect.anchorMax = new Vector2(0.5f, 0.84f);
            selectSubtitleRect.pivot = new Vector2(0.5f, 0.5f);
            selectSubtitleRect.sizeDelta = new Vector2(1200f, 90f);
            selectSubtitleRect.anchoredPosition = Vector2.zero;

            // Previews
            Sprite whiteSprite = white;
            Image playerPreview = CreatePreview(selectPanel.transform, "Preview_Player", new Vector2(-260f, 15f), whiteSprite);
            Image aiPreview = CreatePreview(selectPanel.transform, "Preview_AI", new Vector2(260f, 15f), whiteSprite);
            UpdatePreview(playerPreview, selectedPlayerClass);
            UpdatePreview(aiPreview, selectedAiClass);
            aiPreview.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            // Character grid
            GameObject gridObject = new("CharacterGrid");
            gridObject.transform.SetParent(selectPanel.transform, worldPositionStays: false);
            RectTransform gridRect = gridObject.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.33f);
            gridRect.anchorMax = new Vector2(0.5f, 0.33f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(980f, 170f);
            gridRect.anchoredPosition = Vector2.zero;

            GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(110f, 110f);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CharacterSpriteLibrary.AllClasses.Length;
            grid.childAlignment = TextAnchor.MiddleCenter;

            Image[] selectionFrames = new Image[CharacterSpriteLibrary.AllClasses.Length];
            for (int i = 0; i < CharacterSpriteLibrary.AllClasses.Length; i += 1)
            {
                BattleBootstrap.CharacterClass cls = CharacterSpriteLibrary.AllClasses[i];
                selectionFrames[i] = CreateCharacterButton(
                    gridObject.transform,
                    whiteSprite,
                    cls,
                    onClick: () =>
                    {
                        selectedPlayerClass = cls;
                        UpdatePreview(playerPreview, selectedPlayerClass);
                        UpdateSelection(selectionFrames, selectedPlayerClass);
                    }
                );
            }
            UpdateSelection(selectionFrames, selectedPlayerClass);

            // Start match button
            GameObject startObject = new("Button_StartMatch");
            startObject.transform.SetParent(selectPanel.transform, worldPositionStays: false);
            Image startImage = startObject.AddComponent<Image>();
            startImage.sprite = white;
            startImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            Button startButton = startObject.AddComponent<Button>();
            startButton.colors = colors;
            startButton.onClick.AddListener(() =>
            {
                selectedAiClass = PickAiClassDifferentFrom(selectedPlayerClass);
                GameSessionConfig.SaveToPrefs(selectedPlayerClass, selectedAiClass);
                SceneManager.LoadScene("SampleScene");
            });

            Outline startOutline = startObject.AddComponent<Outline>();
            startOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            startOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0.14f);
            startRect.anchorMax = new Vector2(0.5f, 0.14f);
            startRect.pivot = new Vector2(0.5f, 0.5f);
            startRect.sizeDelta = new Vector2(460f, 86f);
            startRect.anchoredPosition = Vector2.zero;

            GameObject startTextObject = new("Text");
            startTextObject.transform.SetParent(startRect, worldPositionStays: false);
            Text startText = startTextObject.AddComponent<Text>();
            startText.text = "Empezar la Partida";
            startText.alignment = TextAnchor.MiddleCenter;
            startText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            startText.fontStyle = FontStyle.Bold;
            startText.fontSize = 40;
            startText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            RectTransform startTextRect = startText.rectTransform;
            startTextRect.anchorMin = Vector2.zero;
            startTextRect.anchorMax = Vector2.one;
            startTextRect.offsetMin = Vector2.zero;
            startTextRect.offsetMax = Vector2.zero;

            // Back button
            GameObject backObject = new("Button_Back");
            backObject.transform.SetParent(selectPanel.transform, worldPositionStays: false);
            Image backImage = backObject.AddComponent<Image>();
            backImage.sprite = white;
            backImage.color = new Color(0f, 0f, 0f, 0.22f);

            Button backButton = backObject.AddComponent<Button>();
            backButton.onClick.AddListener(() =>
            {
                selectPanel.SetActive(false);
                mainPanel.SetActive(true);
            });

            RectTransform backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.sizeDelta = new Vector2(130f, 56f);
            backRect.anchoredPosition = new Vector2(18f, -18f);

            GameObject backTextObject = new("Text");
            backTextObject.transform.SetParent(backRect, worldPositionStays: false);
            Text backText = backTextObject.AddComponent<Text>();
            backText.text = "Enrere";
            backText.alignment = TextAnchor.MiddleCenter;
            backText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            backText.fontStyle = FontStyle.Bold;
            backText.fontSize = 28;
            backText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            RectTransform backTextRect = backText.rectTransform;
            backTextRect.anchorMin = Vector2.zero;
            backTextRect.anchorMax = Vector2.one;
            backTextRect.offsetMin = Vector2.zero;
            backTextRect.offsetMax = Vector2.zero;
        }

        private static Sprite TryLoadMenuBackground()
        {
            // Recommended: put the image at `Assets/Resources/UI/MenuBackground.png`
            const string resourcesPath = "UI/MenuBackground";
            Sprite sprite = Resources.Load<Sprite>(resourcesPath);

#if !UNITY_EDITOR
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
#endif

#if UNITY_EDITOR
            sprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/MenuBackground.png");
            sprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/MenuBackground.jpg");
            sprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/MenuBackground.png");
            sprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/MenuBackground.jpg");

            if (sprite == null)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/MenuBackground.jpg");
                texture ??= AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/MenuBackground.png");
                texture ??= AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/MenuBackground.jpg");
                texture ??= AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/MenuBackground.png");

                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
#endif

            return sprite;
        }

        private static Image CreatePreview(Transform parent, string name, Vector2 anchoredPosition, Sprite white)
        {
            GameObject previewObject = new(name);
            previewObject.transform.SetParent(parent, worldPositionStays: false);

            Image bg = previewObject.AddComponent<Image>();
            bg.sprite = white;
            bg.color = new Color(0f, 0f, 0f, 0.18f);

            RectTransform rect = bg.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(330f, 330f);
            rect.anchoredPosition = anchoredPosition;

            GameObject spriteObject = new("Sprite");
            spriteObject.transform.SetParent(rect, worldPositionStays: false);
            Image spriteImage = spriteObject.AddComponent<Image>();
            RectTransform spriteRect = spriteImage.rectTransform;
            spriteRect.anchorMin = new Vector2(0.5f, 0.5f);
            spriteRect.anchorMax = new Vector2(0.5f, 0.5f);
            spriteRect.pivot = new Vector2(0.5f, 0.5f);
            spriteRect.sizeDelta = new Vector2(300f, 300f);
            spriteRect.anchoredPosition = Vector2.zero;

            Outline outline = previewObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);

            return spriteImage;
        }

        private static void UpdatePreview(Image previewImage, BattleBootstrap.CharacterClass cls)
        {
            if (previewImage == null) return;
            Sprite portrait = CharacterSpriteLibrary.TryGetPortrait(cls);
            if (portrait != null)
            {
                previewImage.sprite = portrait;
                previewImage.color = Color.white;
                previewImage.preserveAspect = true;
            }
            else
            {
                previewImage.sprite = null;
                previewImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private static Image CreateCharacterButton(Transform parent, Sprite white, BattleBootstrap.CharacterClass cls, UnityEngine.Events.UnityAction onClick)
        {
            GameObject item = new($"Pick_{cls}");
            item.transform.SetParent(parent, worldPositionStays: false);

            Image frame = item.AddComponent<Image>();
            frame.sprite = white;
            frame.color = new Color(0f, 0f, 0f, 0.55f);

            Button btn = item.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject icon = new("Icon");
            icon.transform.SetParent(item.transform, worldPositionStays: false);
            Image iconImage = icon.AddComponent<Image>();
            iconImage.sprite = CharacterSpriteLibrary.TryGetPortrait(cls);
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;

            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(92f, 92f);
            iconRect.anchoredPosition = Vector2.zero;

            Outline outline = item.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return frame;
        }

        private static void UpdateSelection(Image[] frames, BattleBootstrap.CharacterClass selected)
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i += 1)
            {
                if (frames[i] == null) continue;
                BattleBootstrap.CharacterClass cls = CharacterSpriteLibrary.AllClasses[i];
                frames[i].color = cls == selected ? new Color(1f, 0.85f, 0.2f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
            }
        }

        private static BattleBootstrap.CharacterClass PickAiClassDifferentFrom(BattleBootstrap.CharacterClass playerClass)
        {
            BattleBootstrap.CharacterClass[] all = CharacterSpriteLibrary.AllClasses;
            if (all.Length == 0) return BattleBootstrap.CharacterClass.Arquero;
            if (all.Length == 1) return all[0];

            int startIndex = (int)(Time.realtimeSinceStartup * 1000f) % all.Length;
            for (int i = 0; i < all.Length; i += 1)
            {
                BattleBootstrap.CharacterClass candidate = all[(startIndex + i) % all.Length];
                if (candidate != playerClass) return candidate;
            }

            return all[0];
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                MoveOffscreen(existing.transform);
                return;
            }

            GameObject es = new("EventSystem");
            EventSystem eventSystem = es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            MoveOffscreen(eventSystem.transform);
        }

        private static void MoveOffscreen(Transform t)
        {
            if (t == null) return;
            // Avoid seeing the EventSystem gizmo/icon in Game view when Gizmos are enabled.
            t.position = new Vector3(10_000f, 10_000f, 0f);
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
