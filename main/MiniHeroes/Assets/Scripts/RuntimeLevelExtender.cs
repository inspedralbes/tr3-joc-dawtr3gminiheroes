using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RuntimeLevelExtender : MonoBehaviour
{
    public int AdditionalCopies = 8;
    public int SpawnPointSpacing = 10;
    public int RandomGroundSpawnPointsPerCopy = 5;
    public int FloatingPlatformsPerCopy = 6;
    public int FloatingPlatformMinLength = 3;
    public int FloatingPlatformMaxLength = 6;
    public int FloatingPlatformMinHeight = 3;
    public int FloatingPlatformMaxHeight = 7;

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
        TileBase fallbackPlatformTile = null;

        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = terrainTilemap.GetTile(position);
            if (tile == null)
            {
                continue;
            }

            sourceTiles[position] = tile;
            if (fallbackPlatformTile == null)
            {
                fallbackPlatformTile = tile;
            }
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
        TileBase platformTile = ResolvePlatformTile(sourceTiles, surfaceHeights, fallbackPlatformTile);

        for (int copyIndex = 1; copyIndex <= Mathf.Max(1, AdditionalCopies); copyIndex++)
        {
            int offsetX = width * copyIndex;
            foreach (KeyValuePair<Vector3Int, TileBase> tileEntry in sourceTiles)
            {
                Vector3Int targetPosition = new Vector3Int(tileEntry.Key.x + offsetX, tileEntry.Key.y, tileEntry.Key.z);
                terrainTilemap.SetTile(targetPosition, tileEntry.Value);
            }

            AddSpawnPointsForCopy(terrainTilemap, surfaceHeights, minX, maxX, offsetX, copyIndex);
            AddFloatingPlatformsForCopy(terrainTilemap, surfaceHeights, minX, maxX, offsetX, copyIndex, platformTile);
        }

        terrainTilemap.CompressBounds();
        generated = true;
    }

    private void AddSpawnPointsForCopy(Tilemap terrainTilemap, Dictionary<int, int> surfaceHeights, int minX, int maxX, int offsetX, int copyIndex)
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

        int randomExtraSpawns = Mathf.Max(0, RandomGroundSpawnPointsPerCopy);
        if (randomExtraSpawns == 0)
        {
            return;
        }

        System.Random rng = new System.Random((copyIndex * 7919) ^ 1337);
        int rangeStart = minX + spacing;
        int rangeEnd = maxX - spacing;
        if (rangeEnd <= rangeStart)
        {
            return;
        }

        for (int i = 0; i < randomExtraSpawns; i++)
        {
            int sourceX = rng.Next(rangeStart, rangeEnd + 1);
            if (!surfaceHeights.TryGetValue(sourceX, out int topY))
            {
                continue;
            }

            Vector3 cellCenter = terrainTilemap.GetCellCenterWorld(new Vector3Int(sourceX + offsetX, topY + 1, 0));
            generatedSpawnPoints.Add(cellCenter + new Vector3(0f, 0.35f, 0f));
        }
    }

    private void AddFloatingPlatformsForCopy(
        Tilemap terrainTilemap,
        Dictionary<int, int> surfaceHeights,
        int minX,
        int maxX,
        int offsetX,
        int copyIndex,
        TileBase platformTile)
    {
        if (platformTile == null)
        {
            return;
        }

        int platformCount = Mathf.Max(0, FloatingPlatformsPerCopy);
        if (platformCount == 0)
        {
            return;
        }

        int minLength = Mathf.Max(2, FloatingPlatformMinLength);
        int maxLength = Mathf.Max(minLength, FloatingPlatformMaxLength);
        int minHeight = Mathf.Max(2, FloatingPlatformMinHeight);
        int maxHeight = Mathf.Max(minHeight, FloatingPlatformMaxHeight);

        int leftBound = minX + 3;
        int rightBound = maxX - 3;
        if (rightBound <= leftBound)
        {
            return;
        }

        System.Random rng = new System.Random((copyIndex * 15485863) ^ 97);

        for (int i = 0; i < platformCount; i++)
        {
            int length = rng.Next(minLength, maxLength + 1);
            int maxStart = rightBound - length;
            if (maxStart <= leftBound)
            {
                continue;
            }

            int sourceStartX = rng.Next(leftBound, maxStart + 1);
            int sourceCenterX = sourceStartX + (length / 2);
            if (!surfaceHeights.TryGetValue(sourceCenterX, out int groundY))
            {
                continue;
            }

            int platformY = groundY + rng.Next(minHeight, maxHeight + 1);
            for (int dx = 0; dx < length; dx++)
            {
                Vector3Int tilePos = new Vector3Int(sourceStartX + dx + offsetX, platformY, 0);
                if (terrainTilemap.GetTile(tilePos) == null)
                {
                    terrainTilemap.SetTile(tilePos, platformTile);
                }
            }

            Vector3 spawnCenter = terrainTilemap.GetCellCenterWorld(new Vector3Int(sourceCenterX + offsetX, platformY + 1, 0));
            generatedSpawnPoints.Add(spawnCenter + new Vector3(0f, 0.35f, 0f));
        }
    }

    private static TileBase ResolvePlatformTile(
        Dictionary<Vector3Int, TileBase> sourceTiles,
        Dictionary<int, int> surfaceHeights,
        TileBase fallback)
    {
        foreach (KeyValuePair<int, int> heightEntry in surfaceHeights)
        {
            Vector3Int topPos = new Vector3Int(heightEntry.Key, heightEntry.Value, 0);
            if (sourceTiles.TryGetValue(topPos, out TileBase tile) && tile != null)
            {
                return tile;
            }
        }

        return fallback;
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
