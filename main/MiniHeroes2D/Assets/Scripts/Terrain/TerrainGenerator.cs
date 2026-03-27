using UnityEngine;
using UnityEngine.Tilemaps;

namespace MiniHeroes2D.Terrain
{
    [RequireComponent(typeof(Tilemap))]
    public sealed class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;

        [Header("Shape")]
        [SerializeField] private int widthCells = 120;
        [SerializeField] private int baseHeightCells = 1;
        [SerializeField] private int heightVariationCells = 5;
        [SerializeField] private int minFillYCell = -10;

        [Header("Noise")]
        [SerializeField] private float noiseScale = 0.065f;
        [SerializeField] private int seed = 1234;

        [Header("Tile")]
        [SerializeField] private Color tileColor = new(0.35f, 0.23f, 0.15f, 1f);

        private TileBase cachedTile;

        private void Awake()
        {
            if (tilemap == null) tilemap = GetComponent<Tilemap>();
        }

        public void Generate()
        {
            if (tilemap == null) return;

            tilemap.ClearAllTiles();
            cachedTile ??= CreateRuntimeTile(tileColor);

            Random.InitState(seed);
            float offset = Random.Range(-1000f, 1000f);

            int halfWidth = Mathf.Max(2, widthCells / 2);
            for (int x = -halfWidth; x <= halfWidth; x += 1)
            {
                float noise = Mathf.PerlinNoise((x + offset) * noiseScale, 0.25f);
                int height = baseHeightCells + Mathf.RoundToInt((noise - 0.5f) * 2f * heightVariationCells);
                height = Mathf.Max(1, height);

                for (int y = minFillYCell; y <= height; y += 1)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), cachedTile);
                }
            }

            tilemap.RefreshAllTiles();
        }

        private static TileBase CreateRuntimeTile(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            return tile;
        }
    }
}
