using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RuntimeLevelExtender : MonoBehaviour
{
    public int AdditionalCopies = 4;
    public int SpawnPointSpacing = 12;

    private readonly List<Vector3> generatedSpawnPoints = new List<Vector3>();
    private bool generated;

    public IReadOnlyList<Vector3> GeneratedSpawnPoints => generatedSpawnPoints;

    public void Configure(GameObject playerObject)
    {
        if (!generated)
        {
            GenerateIfNeeded();
        }
    }

    private void Start()
    {
        GenerateIfNeeded();
    }

    public void GenerateIfNeeded()
    {
        if (generated)
        {
            return;
        }

        Tilemap terrainTilemap = ResolveTerrainTilemap();
        if (terrainTilemap == null)
        {
            return;
        }

        Dictionary<Vector3Int, TileBase> sourceTiles = new Dictionary<Vector3Int, TileBase>();
        Dictionary<int, int> surfaceHeights = new Dictionary<int, int>();
        BoundsInt bounds = terrainTilemap.cellBounds;
        int minX = int.MaxValue;
        int maxX = int.MinValue;

        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = terrainTilemap.GetTile(position);
            if (tile == null)
            {
                continue;
            }

            sourceTiles[position] = tile;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);

            if (!surfaceHeights.TryGetValue(position.x, out int currentHeight) || position.y > currentHeight)
            {
                surfaceHeights[position.x] = position.y;
            }
        }

        if (sourceTiles.Count == 0 || minX == int.MaxValue || maxX == int.MinValue)
        {
            return;
        }

        int width = (maxX - minX) + 1;

        for (int copyIndex = 1; copyIndex <= Mathf.Max(1, AdditionalCopies); copyIndex++)
        {
            int offsetX = width * copyIndex;
            foreach (KeyValuePair<Vector3Int, TileBase> tileEntry in sourceTiles)
            {
                Vector3Int targetPosition = new Vector3Int(tileEntry.Key.x + offsetX, tileEntry.Key.y, tileEntry.Key.z);
                terrainTilemap.SetTile(targetPosition, tileEntry.Value);
            }

            AddSpawnPointsForCopy(terrainTilemap, surfaceHeights, minX, maxX, offsetX);
        }

        terrainTilemap.CompressBounds();
        generated = true;
    }

    private void AddSpawnPointsForCopy(Tilemap terrainTilemap, Dictionary<int, int> surfaceHeights, int minX, int maxX, int offsetX)
    {
        int spacing = Mathf.Max(4, SpawnPointSpacing);
        for (int sourceX = minX + spacing; sourceX < maxX - spacing; sourceX += spacing)
        {
            if (!surfaceHeights.TryGetValue(sourceX, out int topY))
            {
                continue;
            }

            Vector3 cellCenter = terrainTilemap.GetCellCenterWorld(new Vector3Int(sourceX + offsetX, topY + 1, 0));
            generatedSpawnPoints.Add(cellCenter + new Vector3(0f, 0.35f, 0f));
        }
    }

    private Tilemap ResolveTerrainTilemap()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        Tilemap bestTilemap = null;
        int bestScore = -1;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.GetComponent<TilemapCollider2D>() == null && tilemap.GetComponent<CompositeCollider2D>() == null)
            {
                continue;
            }

            int score = CountTiles(tilemap);
            if (score > bestScore)
            {
                bestScore = score;
                bestTilemap = tilemap;
            }
        }

        return bestTilemap;
    }

    private static int CountTiles(Tilemap tilemap)
    {
        int count = 0;
        foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(position) != null)
            {
                count++;
            }
        }

        return count;
    }
}
