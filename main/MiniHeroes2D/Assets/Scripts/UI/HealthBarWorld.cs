using MiniHeroes2D.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace MiniHeroes2D.UI
{
    public sealed class HealthBarWorld : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Health health;

        [Header("Layout")]
        [SerializeField] private Vector3 offset = new(0f, 1.35f, 0f);
        [SerializeField] private Vector2 size = new(1.4f, 0.18f);
        [SerializeField] private int sortingOrder = 200;
        [SerializeField] private bool faceCamera = true;

        private Image fillImage;
        private Transform root;
        private static Sprite cachedWhiteSprite;
        private static Sprite cachedRedGradientSprite;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (health == null) health = GetComponentInParent<Health>();
            EnsureUi();
            UpdateBar();
        }

        private void LateUpdate()
        {
            if (root == null || health == null) return;

            root.position = transform.position + offset;
            if (faceCamera && targetCamera != null)
                root.rotation = Quaternion.LookRotation(-targetCamera.transform.forward, targetCamera.transform.up);

            UpdateBar();
        }

        private void UpdateBar()
        {
            if (fillImage == null || health == null) return;
            fillImage.fillAmount = health.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
        }

        private void EnsureUi()
        {
            if (root != null) return;

            GameObject rootObject = new("HealthBar");
            rootObject.transform.SetParent(transform, worldPositionStays: true);
            root = rootObject.transform;

            Canvas canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = rootObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 40f;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = size;

            Sprite white = cachedWhiteSprite ??= CreateSolidSprite(Color.white);
            Sprite redGradient = cachedRedGradientSprite ??= CreateRedGradientSprite();

            // Outline
            GameObject outline = new("Outline");
            outline.transform.SetParent(root, worldPositionStays: false);
            Image outlineImage = outline.AddComponent<Image>();
            outlineImage.sprite = white;
            outlineImage.color = new Color(0f, 0f, 0f, 0.95f);
            RectTransform outlineRect = outlineImage.rectTransform;
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;

            // Background
            GameObject bg = new("Bg");
            bg.transform.SetParent(root, worldPositionStays: false);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.sprite = white;
            bgImage.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            RectTransform bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(0.02f, 0.02f);
            bgRect.offsetMax = new Vector2(-0.02f, -0.02f);

            GameObject fill = new("Fill");
            fill.transform.SetParent(root, worldPositionStays: false);
            fillImage = fill.AddComponent<Image>();
            fillImage.sprite = redGradient;
            fillImage.color = Color.white;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(0.04f, 0.04f);
            fillRect.offsetMax = new Vector2(-0.04f, -0.04f);
        }

        private static Sprite CreateSolidSprite(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Sprite CreateRedGradientSprite()
        {
            const int width = 64;
            const int height = 8;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color left = new(1f, 0.35f, 0.35f, 1f);
            Color right = new(0.65f, 0.05f, 0.05f, 1f);

            for (int y = 0; y < height; y += 1)
            {
                float highlight = Mathf.InverseLerp(0, height - 1, y);
                float highlightT = Mathf.SmoothStep(0f, 1f, highlight);
                for (int x = 0; x < width; x += 1)
                {
                    float t = x / (width - 1f);
                    Color c = Color.Lerp(left, right, t);
                    // subtle top highlight
                    c = Color.Lerp(c, Color.white, 0.18f * highlightT);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
