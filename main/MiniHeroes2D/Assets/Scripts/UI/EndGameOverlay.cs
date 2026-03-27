using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MiniHeroes2D.UI
{
    public sealed class EndGameOverlay : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private int fontSize = 96;
        [SerializeField] private float popDurationSeconds = 0.35f;

        [Header("Victory")]
        [SerializeField] private Color victoryColor = new(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private string victoryText = "Victòria!";

        [Header("Defeat")]
        [SerializeField] private Color defeatColor = new(0.95f, 0.15f, 0.15f, 1f);
        [SerializeField] private string defeatText = "Derrota";

        private Canvas canvas;
        private RectTransform root;
        private Text label;
        private Image leftWing;
        private Image rightWing;
        private Button retryButton;
        private Coroutine anim;

        private static Sprite cachedWingSprite;
        private static Sprite cachedWhiteSprite;

        private void Awake()
        {
            EnsureUi();
            Hide();
        }

        public void ShowVictory()
        {
            EnsureUi();
            label.text = victoryText;
            label.color = victoryColor;
            leftWing.enabled = true;
            rightWing.enabled = true;
            leftWing.color = victoryColor;
            rightWing.color = victoryColor;
            Show();
        }

        public void ShowDefeat()
        {
            EnsureUi();
            label.text = defeatText;
            label.color = defeatColor;
            leftWing.enabled = false;
            rightWing.enabled = false;
            Show();
        }

        public void Hide()
        {
            EnsureUi();
            root.gameObject.SetActive(false);
        }

        private void Show()
        {
            root.gameObject.SetActive(true);
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Pop());
        }

        private IEnumerator Pop()
        {
            float duration = Mathf.Max(0.05f, popDurationSeconds);
            float t = 0f;
            root.localScale = Vector3.one * 0.75f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - x, 3f);
                root.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, eased);
                yield return null;
            }

            t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - x, 2f);
                root.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased);
                yield return null;
            }

            root.localScale = Vector3.one;
            anim = null;
        }

        private void EnsureUi()
        {
            if (root != null) return;

            EnsureEventSystem();

            GameObject rootObject = new("EndGameOverlay_UI");
            rootObject.transform.SetParent(transform, worldPositionStays: false);
            root = rootObject.AddComponent<RectTransform>();

            canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10_000;

            rootObject.AddComponent<CanvasScaler>();
            rootObject.AddComponent<GraphicRaycaster>();

            cachedWhiteSprite ??= CreateWhiteSprite();
            cachedWingSprite ??= CreateWingSprite();

            GameObject dim = new("Dim");
            dim.transform.SetParent(root, worldPositionStays: false);
            Image dimImage = dim.AddComponent<Image>();
            dimImage.sprite = cachedWhiteSprite;
            dimImage.color = new Color(0f, 0f, 0f, 0.2f);
            RectTransform dimRect = dimImage.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            GameObject container = new("Center");
            container.transform.SetParent(root, worldPositionStays: false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(900f, 320f);

            GameObject left = new("WingLeft");
            left.transform.SetParent(containerRect, worldPositionStays: false);
            leftWing = left.AddComponent<Image>();
            leftWing.sprite = cachedWingSprite;
            leftWing.preserveAspect = true;
            RectTransform leftRect = leftWing.rectTransform;
            leftRect.anchorMin = new Vector2(0.5f, 0.5f);
            leftRect.anchorMax = new Vector2(0.5f, 0.5f);
            leftRect.sizeDelta = new Vector2(220f, 120f);
            leftRect.anchoredPosition = new Vector2(-360f, 10f);

            GameObject right = new("WingRight");
            right.transform.SetParent(containerRect, worldPositionStays: false);
            rightWing = right.AddComponent<Image>();
            rightWing.sprite = cachedWingSprite;
            rightWing.preserveAspect = true;
            RectTransform rightRect = rightWing.rectTransform;
            rightRect.anchorMin = new Vector2(0.5f, 0.5f);
            rightRect.anchorMax = new Vector2(0.5f, 0.5f);
            rightRect.sizeDelta = new Vector2(220f, 120f);
            rightRect.anchoredPosition = new Vector2(360f, 10f);
            rightRect.localScale = new Vector3(-1f, 1f, 1f);

            GameObject textObject = new("Label");
            textObject.transform.SetParent(containerRect, worldPositionStays: false);
            label = textObject.AddComponent<Text>();
            label.text = victoryText;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(28, fontSize / 2);
            label.resizeTextMaxSize = fontSize;
            label.color = victoryColor;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(6f, -6f);

            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0f, 60f);
            textRect.offsetMax = new Vector2(0f, 0f);

            // Retry button
            GameObject buttonObject = new("RetryButton");
            buttonObject.transform.SetParent(containerRect, worldPositionStays: false);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.sprite = cachedWhiteSprite;
            buttonImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            retryButton = buttonObject.AddComponent<Button>();
            ColorBlock colors = retryButton.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            colors.highlightedColor = new Color(0.18f, 0.18f, 0.18f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.35f);
            retryButton.colors = colors;
            retryButton.onClick.AddListener(RestartMatch);

            Outline buttonOutline = buttonObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            buttonOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform buttonRect = retryButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 10f);
            buttonRect.sizeDelta = new Vector2(360f, 72f);

            GameObject buttonTextObject = new("Text");
            buttonTextObject.transform.SetParent(buttonRect, worldPositionStays: false);
            Text buttonText = buttonTextObject.AddComponent<Text>();
            buttonText.text = "Torna a jugar";
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.fontSize = 38;
            buttonText.color = new Color(0.95f, 0.95f, 0.98f, 1f);

            RectTransform buttonTextRect = buttonText.rectTransform;
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
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

        private void RestartMatch()
        {
            Time.timeScale = 1f;
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite CreateWingSprite()
        {
            const int w = 96;
            const int h = 48;

            Texture2D texture = new(w, h, TextureFormat.RGBA32, mipChain: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color clear = new(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y += 1)
                for (int x = 0; x < w; x += 1)
                    texture.SetPixel(x, y, clear);

            // Simple feather-like alpha mask
            for (int x = 0; x < w; x += 1)
            {
                float nx = x / (w - 1f);
                float curve = Mathf.Sin(nx * Mathf.PI) * 0.9f;

                int yMid = Mathf.RoundToInt((h * 0.5f) + (curve * 8f));
                int thickness = Mathf.RoundToInt(Mathf.Lerp(18f, 6f, nx));

                for (int y = yMid - thickness; y <= yMid + thickness; y += 1)
                {
                    if (y < 0 || y >= h) continue;
                    float ny = Mathf.InverseLerp(yMid - thickness, yMid + thickness, y);
                    float edge = Mathf.Abs(ny - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - edge);
                    a *= Mathf.Lerp(0.95f, 0.0f, nx * nx);

                    Color c = new(1f, 1f, 1f, a);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 96f);
        }
    }
}
