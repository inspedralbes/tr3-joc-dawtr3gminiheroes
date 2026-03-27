using UnityEngine;
using UnityEngine.Tilemaps;

namespace MiniHeroes2D.Terrain
{
    [RequireComponent(typeof(Tilemap))]
    public sealed class BedrockGenerator : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;

        [Header("Shape")]
        [SerializeField] private int widthCells = 120;
        [SerializeField] private int topYCell = -12;
        [SerializeField] private int bottomYCell = -40;

        [Header("Tile")]
        [SerializeField] private Color tileColor = new(0.18f, 0.18f, 0.2f, 1f);

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

            int halfWidth = Mathf.Max(2, widthCells / 2);
            int top = Mathf.Max(topYCell, bottomYCell);
            int bottom = Mathf.Min(topYCell, bottomYCell);

            for (int x = -halfWidth; x <= halfWidth; x += 1)
            {
                for (int y = bottom; y <= top; y += 1)
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

