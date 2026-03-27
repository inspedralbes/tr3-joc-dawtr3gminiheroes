using MiniHeroes2D.Gameplay;
using UnityEngine;

namespace MiniHeroes2D.UI
{
    public sealed class HealthBarSpriteWorld : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Vector3 offset = new(0f, 1.55f, 0f);

        [Header("Visual")]
        [SerializeField] private float width = 1.35f;
        [SerializeField] private float height = 0.16f;
        [SerializeField] private float padding = 0.02f;
        [SerializeField] private int sortingOrder = 200;

        private Transform root;
        private Transform fillTransform;
        private SpriteRenderer fillRenderer;
        private float lastFill = -1f;

        private static Sprite cachedWhite;
        private static Sprite cachedRedGradient;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            EnsureSprites();
            EnsureObjects();
            UpdateBar(force: true);
        }

        private void LateUpdate()
        {
            if (health == null || root == null) return;
            root.position = transform.position + offset;
            UpdateBar(force: false);
        }

        private void UpdateBar(bool force)
        {
            float fill = health.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            if (!force && Mathf.Abs(fill - lastFill) < 0.001f) return;
            lastFill = fill;

            float innerWidth = Mathf.Max(0.01f, width - (padding * 2f));
            float innerHeight = Mathf.Max(0.01f, height - (padding * 2f));

            float scaledWidth = innerWidth * fill;
            Vector2 spriteSize = fillRenderer != null && fillRenderer.sprite != null ? fillRenderer.sprite.bounds.size : Vector2.one;
            fillTransform.localScale = new Vector3(
                scaledWidth / Mathf.Max(0.0001f, spriteSize.x),
                innerHeight / Mathf.Max(0.0001f, spriteSize.y),
                1f
            );
            fillTransform.localPosition = new Vector3((-innerWidth * 0.5f) + (scaledWidth * 0.5f), 0f, 0f);

            if (fillRenderer != null)
                fillRenderer.enabled = fill > 0.001f;
        }

        private void EnsureObjects()
        {
            if (root != null) return;

            GameObject rootObject = new("HealthBar");
            rootObject.transform.SetParent(transform, worldPositionStays: false);
            root = rootObject.transform;
            root.localPosition = offset;

            GameObject outline = new("Outline");
            outline.transform.SetParent(root, worldPositionStays: false);
            SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = cachedWhite;
            outlineRenderer.color = new Color(0f, 0f, 0f, 1f);
            outlineRenderer.sortingOrder = sortingOrder;
            Vector2 whiteSize = outlineRenderer.sprite != null ? outlineRenderer.sprite.bounds.size : Vector2.one;
            outline.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, whiteSize.x),
                height / Mathf.Max(0.0001f, whiteSize.y),
                1f
            );

            GameObject bg = new("Bg");
            bg.transform.SetParent(root, worldPositionStays: false);
            SpriteRenderer bgRenderer = bg.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = cachedWhite;
            bgRenderer.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            bgRenderer.sortingOrder = sortingOrder + 1;
            bg.transform.localScale = new Vector3(
                (width - padding) / Mathf.Max(0.0001f, whiteSize.x),
                (height - padding) / Mathf.Max(0.0001f, whiteSize.y),
                1f
            );

            GameObject fill = new("Fill");
            fill.transform.SetParent(root, worldPositionStays: false);
            fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = cachedRedGradient;
            fillRenderer.color = Color.white;
            fillRenderer.sortingOrder = sortingOrder + 2;

            fillTransform = fill.transform;
        }

        private static void EnsureSprites()
        {
            cachedWhite ??= CreateSolidSprite(Color.white, pixelsPerUnit: 1f);
            cachedRedGradient ??= CreateRedGradientSprite();
        }

        private static Sprite CreateSolidSprite(Color color, float pixelsPerUnit)
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, color);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static Sprite CreateRedGradientSprite()
        {
            const int texWidth = 64;
            const int texHeight = 8;

            Texture2D texture = new(texWidth, texHeight, TextureFormat.RGBA32, mipChain: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color left = new(1f, 0.40f, 0.40f, 1f);
            Color right = new(0.55f, 0.05f, 0.05f, 1f);

            for (int y = 0; y < texHeight; y += 1)
            {
                float highlight = Mathf.InverseLerp(0, texHeight - 1, y);
                float highlightT = Mathf.SmoothStep(0f, 1f, highlight);
                for (int x = 0; x < texWidth; x += 1)
                {
                    float t = x / (texWidth - 1f);
                    Color c = Color.Lerp(left, right, t);
                    c = Color.Lerp(c, Color.white, 0.22f * highlightT);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
