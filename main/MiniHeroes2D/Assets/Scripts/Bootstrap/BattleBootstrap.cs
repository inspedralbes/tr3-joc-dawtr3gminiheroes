using MiniHeroes2D.Gameplay;
using MiniHeroes2D.Turns;
using UnityEngine;
using UnityEngine.Tilemaps;
using MiniHeroes2D.Terrain;
using MiniHeroes2D.UI;
using MiniHeroes2D.ML;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

namespace MiniHeroes2D.Bootstrap
{
    public sealed class BattleBootstrap : MonoBehaviour
    {
        public enum CharacterClass
        {
            Caballero,
            Arquero,
            Mago,
            Ladron,
            Curandero,
            Angel,
            Demonio
        }

        [Header("Players")]
        [SerializeField] private Vector2 player1Position = new(-7f, -0.5f);
        [SerializeField] private Vector2 player2Position = new(7f, -0.5f);
        [SerializeField] private CharacterClass player1Class = CharacterClass.Caballero;
        [SerializeField] private CharacterClass player2Class = CharacterClass.Arquero;
        [SerializeField] private float spawnHeightAboveGround = 4f;
        [SerializeField] private bool player1IsAi = false;
        [SerializeField] private bool player2IsAi = true;

        [Header("Projectile")]
        [SerializeField] private float projectileGravityScale = 1.25f;
        [SerializeField] private float projectileSpeed = 14f;

        private void Awake()
        {
            EnsureCamera();
            EnsureScreenBounds();
            DestructibleTileTerrain terrain = EnsureTerrain();

            TurnGameManager manager = FindObjectOfType<TurnGameManager>();
            if (manager == null)
            {
                GameObject managerObject = new("TurnGameManager");
                manager = managerObject.AddComponent<TurnGameManager>();
            }

            ApplySessionConfig();

            if (FindObjectOfType<Projectile>() == null)
            {
                Projectile prefab = CreateProjectilePrefab();
                manager.SetProjectilePrefab(prefab);
                manager.SetProjectileSpeed(projectileSpeed);
            }

            if (FindObjectOfType<TurnPlayerController>() == null)
            {
                CreatePlayer("Player1", player1Position, horizontalDirection: 1, player1Class, player1IsAi, terrain);
                CreatePlayer("Player2", player2Position, horizontalDirection: -1, player2Class, player2IsAi, terrain);
            }
        }

        private void ApplySessionConfig()
        {
            GameSessionConfig.LoadFromPrefs();
            player1Class = GameSessionConfig.PlayerClass;
            player2Class = GameSessionConfig.AiClass;
            player1IsAi = false;
            player2IsAi = true;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;

            GameObject cameraObject = new("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            cameraObject.tag = "MainCamera";

            cameraObject.transform.position = new Vector3(0f, 2f, -10f);
        }

        private static void EnsureScreenBounds()
        {
            if (FindObjectOfType<ScreenBounds2D>() != null) return;

            GameObject bounds = new("ScreenBounds2D");
            ScreenBounds2D component = bounds.AddComponent<ScreenBounds2D>();
            component.gameObject.SetActive(true);
        }

        private void CreatePlayer(string name, Vector2 position, int horizontalDirection, CharacterClass characterClass, bool isAi, DestructibleTileTerrain terrain)
        {
            GameObject player = new(name);
            player.transform.position = position;
            player.tag = "Player";

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSolidSprite(new Color(0.85f, 0.85f, 0.9f, 1f));
            renderer.sortingOrder = 10;
            renderer.flipX = horizontalDirection < 0;

            TryApplyCharacterSprite(renderer, characterClass);

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1.5f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
            collider.size = GetDefaultColliderSize(renderer);

            player.AddComponent<Health>();
            player.AddComponent<HealthBarSpriteWorld>();

            GameObject firePointObject = new("FirePoint");
            firePointObject.transform.SetParent(player.transform, worldPositionStays: false);
            firePointObject.transform.localPosition = new Vector3(0.65f * horizontalDirection, 0.35f, 0f);

            TurnPlayerController controller = player.AddComponent<TurnPlayerController>();
            controller.SetFirePoint(firePointObject.transform);
            controller.SetHorizontalDirection(horizontalDirection);
            controller.SetIsAi(isAi);

            if (isAi)
            {
                // ML-Agents AI driver (Heuristic by default). Later you can attach a trained model in the inspector.
                player.AddComponent<MlAgentsTurnShooterAgent>();

                BehaviorParameters behavior = player.AddComponent<BehaviorParameters>();
                behavior.BehaviorName = "MiniHeroes2D_TurnShooter";
                behavior.BehaviorType = BehaviorType.HeuristicOnly;
                behavior.BrainParameters.VectorObservationSize = 3;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            }

            if (terrain != null && terrain.TryGetSurfacePosition(position.x, out Vector2 surface))
                player.transform.position = surface + Vector2.up * Mathf.Max(0.1f, spawnHeightAboveGround);
        }

        private static Vector2 GetDefaultColliderSize(SpriteRenderer renderer)
        {
            if (renderer != null && renderer.sprite != null)
            {
                Vector2 bounds = renderer.sprite.bounds.size;
                if (bounds.x > 0.05f && bounds.y > 0.05f)
                    return new Vector2(Mathf.Clamp(bounds.x * 0.7f, 0.35f, 1.2f), Mathf.Clamp(bounds.y * 0.9f, 0.5f, 1.8f));
            }

            return new Vector2(0.9f, 1.2f);
        }

        private static void TryApplyCharacterSprite(SpriteRenderer renderer, CharacterClass characterClass)
        {
            if (renderer == null) return;

            Sprite sprite = CharacterSpriteLibrary.TryGetPortrait(characterClass);
            if (sprite == null) return;
            renderer.sprite = sprite;
        }

        private DestructibleTileTerrain EnsureTerrain()
        {
            GameObject existing = GameObject.Find("Terrain_Destructible");
            if (existing != null)
            {
                return existing.GetComponent<DestructibleTileTerrain>();
            }

            Grid grid = FindObjectOfType<Grid>();
            if (grid == null)
            {
                GameObject gridObject = new("Grid");
                grid = gridObject.AddComponent<Grid>();
            }

            // Bedrock (indestructible base)
            GameObject bedrockObject = new("Terrain_Bedrock");
            bedrockObject.transform.SetParent(grid.transform, worldPositionStays: false);

            bedrockObject.AddComponent<Tilemap>();
            TilemapRenderer bedrockRenderer = bedrockObject.AddComponent<TilemapRenderer>();
            bedrockRenderer.sortingOrder = 0;

            Rigidbody2D bedrockBody = bedrockObject.AddComponent<Rigidbody2D>();
            bedrockBody.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D bedrockCollider = bedrockObject.AddComponent<TilemapCollider2D>();
            bedrockCollider.usedByComposite = true;
            bedrockObject.AddComponent<CompositeCollider2D>();

            BedrockGenerator bedrockGenerator = bedrockObject.AddComponent<BedrockGenerator>();
            bedrockGenerator.Generate();

            // Destructible terrain on top
            GameObject terrainObject = new("Terrain_Destructible");
            terrainObject.transform.SetParent(grid.transform, worldPositionStays: false);

            terrainObject.AddComponent<Tilemap>();
            TilemapRenderer terrainRenderer = terrainObject.AddComponent<TilemapRenderer>();
            terrainRenderer.sortingOrder = 1;

            Rigidbody2D body = terrainObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D tilemapCollider = terrainObject.AddComponent<TilemapCollider2D>();
            tilemapCollider.usedByComposite = true;

            terrainObject.AddComponent<CompositeCollider2D>();

            DestructibleTileTerrain destructible = terrainObject.AddComponent<DestructibleTileTerrain>();
            TerrainGenerator generator = terrainObject.AddComponent<TerrainGenerator>();
            generator.Generate();

            return destructible;
        }

        private Projectile CreateProjectilePrefab()
        {
            GameObject projectile = new("ProjectilePrefab");
            projectile.SetActive(false);

            SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateProjectileSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 20;

            Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
            body.gravityScale = projectileGravityScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
            collider.radius = 0.12f;

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.sortingOrder = 19;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.colorGradient = CreateTrailGradient();
            trail.emitting = true;

            return projectile.AddComponent<Projectile>();
        }

        private static Gradient CreateTrailGradient()
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.35f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Sprite CreateProjectileSprite()
        {
            const int w = 32;
            const int h = 12;
            Texture2D texture = new(w, h, TextureFormat.RGBA32, mipChain: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            Color clear = new(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y += 1)
            {
                for (int x = 0; x < w; x += 1)
                    texture.SetPixel(x, y, clear);
            }

            Color outline = new(0f, 0f, 0f, 1f);
            Color body = new(0.95f, 0.95f, 0.98f, 1f);
            Color bodyShade = new(0.75f, 0.75f, 0.8f, 1f);
            Color tip = new(1f, 0.25f, 0.25f, 1f);
            Color fin = new(0.2f, 0.2f, 0.25f, 1f);

            // Body rectangle
            for (int x = 7; x <= 23; x += 1)
            {
                for (int y = 4; y <= 7; y += 1)
                    texture.SetPixel(x, y, body);
                texture.SetPixel(x, 3, bodyShade);
            }

            // Nose (triangle)
            texture.SetPixel(24, 4, tip);
            texture.SetPixel(24, 5, tip);
            texture.SetPixel(24, 6, tip);
            texture.SetPixel(25, 5, tip);

            // Tail + fin
            for (int y = 4; y <= 7; y += 1) texture.SetPixel(6, y, bodyShade);
            texture.SetPixel(5, 4, fin);
            texture.SetPixel(5, 7, fin);

            // Outline pass (simple)
            for (int y = 0; y < h; y += 1)
            {
                for (int x = 0; x < w; x += 1)
                {
                    Color c = texture.GetPixel(x, y);
                    if (c.a <= 0.01f) continue;

                    for (int oy = -1; oy <= 1; oy += 1)
                    {
                        for (int ox = -1; ox <= 1; ox += 1)
                        {
                            if (ox == 0 && oy == 0) continue;
                            int nx = x + ox;
                            int ny = y + oy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            if (texture.GetPixel(nx, ny).a <= 0.01f) texture.SetPixel(nx, ny, outline);
                        }
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Sprite CreateSolidSprite(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
