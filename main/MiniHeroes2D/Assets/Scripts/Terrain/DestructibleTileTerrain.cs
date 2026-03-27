using UnityEngine;
using UnityEngine.Tilemaps;

namespace MiniHeroes2D.Terrain
{
    [RequireComponent(typeof(Tilemap))]
    public sealed class DestructibleTileTerrain : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;

        [Header("Scan")]
        [SerializeField] private int scanMinYCell = -20;
        [SerializeField] private int scanMaxYCell = 40;

        private void Awake()
        {
            if (tilemap == null) tilemap = GetComponent<Tilemap>();
        }

        public void DigCircle(Vector2 worldPoint, float radiusWorld)
        {
            if (tilemap == null) return;
            if (radiusWorld <= 0f) return;

            Vector3Int centerCell = tilemap.WorldToCell(worldPoint);

            Vector3 cellWorldSize = tilemap.layoutGrid.cellSize;
            float cellRadiusX = radiusWorld / Mathf.Max(0.001f, cellWorldSize.x);
            float cellRadiusY = radiusWorld / Mathf.Max(0.001f, cellWorldSize.y);

            int minX = Mathf.FloorToInt(centerCell.x - cellRadiusX) - 1;
            int maxX = Mathf.CeilToInt(centerCell.x + cellRadiusX) + 1;
            int minY = Mathf.FloorToInt(centerCell.y - cellRadiusY) - 1;
            int maxY = Mathf.CeilToInt(centerCell.y + cellRadiusY) + 1;

            float radiusSqr = radiusWorld * radiusWorld;
            for (int x = minX; x <= maxX; x += 1)
            {
                for (int y = minY; y <= maxY; y += 1)
                {
                    Vector3Int cell = new(x, y, 0);
                    if (tilemap.GetTile(cell) == null) continue;

                    Vector3 closest = tilemap.GetCellCenterWorld(cell);
                    float distanceSqr = ((Vector2)closest - worldPoint).sqrMagnitude;
                    if (distanceSqr > radiusSqr) continue;

                    tilemap.SetTile(cell, null);
                }
            }

            tilemap.RefreshAllTiles();
        }

        public bool TryGetSurfacePosition(float worldX, out Vector2 surfacePosition)
        {
            if (tilemap == null)
            {
                surfacePosition = default;
                return false;
            }

            Vector3Int cell = tilemap.WorldToCell(new Vector3(worldX, 0f, 0f));
            for (int y = scanMaxYCell; y >= scanMinYCell; y -= 1)
            {
                Vector3Int candidate = new(cell.x, y, 0);
                if (tilemap.GetTile(candidate) == null) continue;

                Vector3 center = tilemap.GetCellCenterWorld(candidate);
                surfacePosition = new Vector2(worldX, center.y + (tilemap.layoutGrid.cellSize.y * 0.5f));
                return true;
            }

            surfacePosition = default;
            return false;
        }
    }
}

