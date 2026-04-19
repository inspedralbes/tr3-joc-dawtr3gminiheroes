using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class JohnMovement : MonoBehaviour, IDamageable
{
    public GameObject Bullet;
    public float Speed = 4f;
    public float JumpForce = 150f;
    public float GroundCheckDistance = 0.18f;
    public float CoyoteTime = 0.12f;
    public float JumpBufferTime = 0.12f;
    public LayerMask GroundMask = ~0;

    private Rigidbody2D body;
    private CapsuleCollider2D capsuleCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float horizontal;
    private bool grounded;
    private float lastShootTime;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private int maxHealth = 10;
    private int health = 10;
    private bool isDead;
    private bool showStatsMenu;
    private bool showPostDeathMenu;
    private int experience;
    private int maxExperience = 20;
    private int gruntsKilled;
    private int level = 1;
    private Vector3 spawnPoint;
    private bool useExternalControl;
    private float externalHorizontal;
    private bool externalJumpRequested;
    private bool externalShootRequested;

    private readonly string backendUrl = "http://localhost:3000/api/";

    public DamageTeam Team => DamageTeam.Player;
    public bool IsDead => isDead;

    [System.Serializable]
    private class StatsData
    {
        public int experience;
        public int grunts_killed;
        public int level;
    }

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spawnPoint = transform.position;

        CalculateMaxExperience();
        EnsureGameplaySystems();

        if (!MiniHeroesRuntimeMode.IsTraining && PlayerPrefs.HasKey("session_token"))
        {
            StartCoroutine(LoadStatsRoutine());
        }
    }

    private void Update()
    {
        if (isDead || Time.timeScale == 0f)
        {
            return;
        }

        if (!MiniHeroesRuntimeMode.IsTraining && Input.GetKeyDown(KeyCode.M))
        {
            showStatsMenu = !showStatsMenu;
        }

        if (MiniHeroesRuntimeMode.IsTraining && useExternalControl)
        {
            horizontal = externalHorizontal;
        }
        else
        {
            horizontal = Input.GetAxisRaw("Horizontal");
        }

        if (horizontal < 0f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (horizontal > 0f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }

        if (animator != null)
        {
            animator.SetBool("running", Mathf.Abs(horizontal) > 0.01f);
        }

        grounded = CheckGrounded();
        if (grounded)
        {
            lastGroundedTime = Time.time;
        }

        bool jumpRequested = MiniHeroesRuntimeMode.IsTraining && useExternalControl
            ? externalJumpRequested
            : Input.GetKeyDown(KeyCode.W);
        if (jumpRequested)
        {
            lastJumpPressedTime = Time.time;
        }

        if (CanUseBufferedJump())
        {
            Jump();
        }

        bool shootRequested = MiniHeroesRuntimeMode.IsTraining && useExternalControl ? externalShootRequested : Input.GetKeyDown(KeyCode.Space);
        if (shootRequested && Time.time > lastShootTime + 0.25f)
        {
            Shoot();
            lastShootTime = Time.time;
        }

        externalJumpRequested = false;
        externalShootRequested = false;
    }

    private void FixedUpdate()
    {
        if (isDead || body == null)
        {
            return;
        }

        body.linearVelocity = new Vector2(horizontal * Speed, body.linearVelocity.y);
    }

    public void AddExperience(int amount)
    {
        if (isDead)
        {
            return;
        }

        experience += amount;
        gruntsKilled += 1;

        while (experience >= maxExperience)
        {
            experience -= maxExperience;
            level += 1;
            CalculateMaxExperience();
        }

        if (!MiniHeroesRuntimeMode.IsTraining)
        {
            StartCoroutine(SaveStatsRoutine());
        }
    }

    public void ReceiveDamage(int amount, GameObject source, DamageTeam sourceTeam)
    {
        if (isDead || sourceTeam == Team)
        {
            return;
        }

        health -= amount;
        if (source != null)
        {
            GruntScript grunt = source.GetComponentInParent<GruntScript>();
            if (grunt != null)
            {
                grunt.NotifySuccessfulAttack();
            }
        }

        if (health > 0)
        {
            return;
        }

        if (MiniHeroesRuntimeMode.IsTraining)
        {
            Object.FindFirstObjectByType<MiniHeroesTrainingManager>()?.HandlePlayerDeath();
            return;
        }

        isDead = true;
        showPostDeathMenu = false;
        Time.timeScale = 0f;
    }

    public void Hit()
    {
        ReceiveDamage(1, null, DamageTeam.Enemy);
    }

    public void SetExternalControlState(float moveInput, bool jumpRequested, bool shootRequested)
    {
        useExternalControl = true;
        externalHorizontal = Mathf.Clamp(moveInput, -1f, 1f);
        externalJumpRequested |= jumpRequested;
        externalShootRequested |= shootRequested;
    }

    public void ResetForTraining()
    {
        health = maxHealth;
        isDead = false;
        transform.position = spawnPoint;
        horizontal = 0f;
        externalHorizontal = 0f;
        externalJumpRequested = false;
        externalShootRequested = false;
        lastGroundedTime = Time.time;
        lastJumpPressedTime = -10f;
        showPostDeathMenu = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    private void Jump()
    {
        if (body == null)
        {
            return;
        }

        lastJumpPressedTime = -10f;
        lastGroundedTime = -10f;
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        body.AddForce(Vector2.up * JumpForce);
    }

    private void Shoot()
    {
        if (Bullet == null)
        {
            return;
        }

        Vector3 direction = transform.localScale.x >= 0f ? Vector3.right : Vector3.left;
        GameObject bullet = Instantiate(Bullet, transform.position + direction * 0.18f, Quaternion.identity);
        BulletScript bulletScript = bullet.GetComponent<BulletScript>();
        if (bulletScript != null)
        {
            bulletScript.Configure(direction, DamageTeam.Player, gameObject);
        }
    }

    private bool CheckGrounded()
    {
        if (capsuleCollider == null)
        {
            return Physics2D.OverlapCircle(transform.position + Vector3.down * (GroundCheckDistance * 0.5f), GroundCheckDistance, GroundMask) != null;
        }

        Bounds bounds = capsuleCollider.bounds;
        Vector2 checkCenter = new Vector2(bounds.center.x, bounds.min.y - (GroundCheckDistance * 0.5f));
        Vector2 checkSize = new Vector2(bounds.size.x * 0.85f, GroundCheckDistance);
        Collider2D hit = Physics2D.OverlapBox(checkCenter, checkSize, 0f, GroundMask);
        return hit != null && hit.gameObject != gameObject;
    }

    private bool CanUseBufferedJump()
    {
        if (Time.time > lastJumpPressedTime + JumpBufferTime)
        {
            return false;
        }

        return grounded || Time.time <= lastGroundedTime + CoyoteTime;
    }

    private void EnsureGameplaySystems()
    {
        if (MiniHeroesRuntimeMode.IsTraining)
        {
            MiniHeroesTrainingManager trainingManager = Object.FindFirstObjectByType<MiniHeroesTrainingManager>();
            if (trainingManager == null)
            {
                GameObject trainingObject = new GameObject("MiniHeroesTrainingManager");
                trainingObject.AddComponent<MiniHeroesTrainingManager>();
            }
        }
        else
        {
            MiniHeroesInferenceBootstrap inferenceBootstrap = Object.FindFirstObjectByType<MiniHeroesInferenceBootstrap>();
            if (inferenceBootstrap == null)
            {
                GameObject inferenceObject = new GameObject("MiniHeroesInferenceBootstrap");
                inferenceObject.AddComponent<MiniHeroesInferenceBootstrap>();
            }
        }

        RuntimeLevelExtender levelExtender = Object.FindFirstObjectByType<RuntimeLevelExtender>();
        if (levelExtender == null)
        {
            GameObject extenderObject = new GameObject("RuntimeLevelExtender");
            levelExtender = extenderObject.AddComponent<RuntimeLevelExtender>();
        }
        levelExtender.Configure(gameObject);

        EnemyRespawnManager respawnManager = Object.FindFirstObjectByType<EnemyRespawnManager>();
        if (respawnManager == null)
        {
            GameObject respawnObject = new GameObject("EnemyRespawnManager");
            respawnManager = respawnObject.AddComponent<EnemyRespawnManager>();
        }
        respawnManager.Configure(gameObject, levelExtender);
    }

    private void CalculateMaxExperience()
    {
        maxExperience = 20;
        for (int i = 1; i < level; i++)
        {
            maxExperience = Mathf.FloorToInt(maxExperience * 1.5f);
        }
    }

    private IEnumerator LoadStatsRoutine()
    {
        UnityWebRequest request = UnityWebRequest.Get(backendUrl + "stats");
        request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("session_token"));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            yield break;
        }

        StatsData stats = JsonUtility.FromJson<StatsData>(request.downloadHandler.text);
        if (stats == null)
        {
            yield break;
        }

        experience = stats.experience;
        gruntsKilled = stats.grunts_killed;
        if (stats.level > 0)
        {
            level = stats.level;
        }
        CalculateMaxExperience();
    }

    private IEnumerator SaveStatsRoutine()
    {
        if (!PlayerPrefs.HasKey("session_token"))
        {
            yield break;
        }

        StatsData data = new StatsData
        {
            experience = experience,
            grunts_killed = gruntsKilled,
            level = level
        };

        string json = JsonUtility.ToJson(data);
        UnityWebRequest request = new UnityWebRequest(backendUrl + "stats", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("session_token"));

        yield return request.SendWebRequest();
    }

    private void OnGUI()
    {
        if (MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        if (showPostDeathMenu)
        {
            DrawMainMenu();
            return;
        }

        if (isDead)
        {
            DrawDefeatScreen();
            return;
        }

        if (showStatsMenu)
        {
            DrawStatsMenu();
        }

        if (Camera.main == null)
        {
            return;
        }

        Vector2 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
        float barWidth = 50f;
        float barHeight = 12f;
        Rect rect = new Rect(screenPosition.x - barWidth / 2f, Screen.height - screenPosition.y - barHeight, barWidth, barHeight);

        GUIStyle levelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        levelStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);
        GUI.Label(new Rect(rect.x, rect.y - 18f, rect.width, 20f), "Lv. " + level, levelStyle);

        GUI.color = Color.black;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.green;
        Rect healthRect = new Rect(rect.x, rect.y, rect.width * ((float)health / maxHealth), rect.height);
        GUI.DrawTexture(healthRect, Texture2D.whiteTexture);

        float xpBarHeight = 6f;
        Rect xpRect = new Rect(rect.x, rect.y + rect.height + 2f, barWidth, xpBarHeight);
        GUI.color = Color.white;
        GUI.DrawTexture(xpRect, Texture2D.whiteTexture);

        GUI.color = Color.yellow;
        Rect xpFillRect = new Rect(xpRect.x, xpRect.y, xpRect.width * Mathf.Clamp01((float)experience / maxExperience), xpRect.height);
        GUI.DrawTexture(xpFillRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawDefeatScreen()
    {
        GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);

        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        boxStyle.normal.textColor = Color.white;

        int width = 430;
        int height = 320;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.Box(new Rect(x, y, width, height), "YOU DIED", boxStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20
        };

        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.3f);
        if (GUI.Button(new Rect(x + 65f, y + 80f, 300f, 50f), "Play Again", buttonStyle))
        {
            RestartCurrentScene();
        }

        GUI.backgroundColor = new Color(0.25f, 0.45f, 0.7f);
        if (GUI.Button(new Rect(x + 65f, y + 150f, 300f, 50f), "Salir al Menu", buttonStyle))
        {
            showPostDeathMenu = true;
        }

        GUI.backgroundColor = new Color(0.6f, 0.3f, 0.3f);
        if (GUI.Button(new Rect(x + 65f, y + 220f, 300f, 50f), "Cerrar Sesion", buttonStyle))
        {
            ReturnToLogin();
        }

        GUI.backgroundColor = Color.white;
    }

    private void DrawMainMenu()
    {
        GUI.backgroundColor = Color.black;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);

        int panelWidth = 760;
        int panelHeight = 420;
        float x = (Screen.width - panelWidth) / 2f;
        float y = (Screen.height - panelHeight) / 2f;

        GUI.backgroundColor = new Color(0.08f, 0.18f, 0.16f, 1f);
        GUI.Box(new Rect(x, y, panelWidth, panelHeight), string.Empty);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.95f, 0.92f, 0.7f);
        GUI.Label(new Rect(x, y + 18f, panelWidth, 40f), "MENU DEL HEROE", titleStyle);

        DrawCharacterPreview(new Rect(x + 35f, y + 80f, 240f, 260f));
        DrawMenuStats(new Rect(x + 305f, y + 80f, 420f, 210f));

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        GUI.backgroundColor = new Color(0.26f, 0.62f, 0.28f);
        if (GUI.Button(new Rect(x + 305f, y + 315f, 190f, 48f), "Play Again", buttonStyle))
        {
            RestartCurrentScene();
        }

        GUI.backgroundColor = new Color(0.55f, 0.28f, 0.28f);
        if (GUI.Button(new Rect(x + 535f, y + 315f, 190f, 48f), "Cerrar Sesion", buttonStyle))
        {
            ReturnToLogin();
        }

        GUI.backgroundColor = Color.white;
    }

    private void DrawCharacterPreview(Rect rect)
    {
        GUI.backgroundColor = new Color(0.11f, 0.15f, 0.15f, 1f);
        GUI.Box(rect, string.Empty);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "JOHN", labelStyle);

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            GUI.Label(new Rect(rect.x, rect.y + 110f, rect.width, 30f), "Preview no disponible", labelStyle);
            return;
        }

        Sprite sprite = spriteRenderer.sprite;
        Texture2D texture = sprite.texture;
        Rect texCoords = new Rect(
            sprite.textureRect.x / texture.width,
            sprite.textureRect.y / texture.height,
            sprite.textureRect.width / texture.width,
            sprite.textureRect.height / texture.height);

        Rect previewRect = new Rect(rect.x + 32f, rect.y + 55f, rect.width - 64f, rect.height - 85f);
        GUI.DrawTextureWithTexCoords(previewRect, texture, texCoords, true);
    }

    private void DrawMenuStats(Rect rect)
    {
        GUI.backgroundColor = new Color(0.12f, 0.23f, 0.2f, 1f);
        GUI.Box(rect, string.Empty);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.95f, 0.92f, 0.7f);
        GUI.Label(new Rect(rect.x + 20f, rect.y + 15f, rect.width - 40f, 30f), "Estadisticas", titleStyle);

        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        statStyle.normal.textColor = Color.white;

        float lineY = rect.y + 60f;
        float lineHeight = 30f;
        GUI.Label(new Rect(rect.x + 20f, lineY, rect.width - 40f, 30f), "Nivel: " + level, statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight, rect.width - 40f, 30f), "Vida: " + health + " / " + maxHealth, statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 2f, rect.width - 40f, 30f), "Experiencia: " + experience + " / " + maxExperience, statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 3f, rect.width - 40f, 30f), "Grunts derrotados: " + gruntsKilled, statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 4f, rect.width - 40f, 30f), "Velocidad: " + Speed.ToString("0.0"), statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 5f, rect.width - 40f, 30f), "Salto: " + JumpForce.ToString("0"), statStyle);
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        showPostDeathMenu = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnToLogin()
    {
        showPostDeathMenu = false;
        AuthManager.Logout();

        if (Application.CanStreamedLevelBeLoaded("LoginScene"))
        {
            SceneManager.LoadScene("LoginScene");
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void DrawStatsMenu()
    {
        int width = 250;
        int height = 220;
        float x = 20f;
        float y = 20f;

        GUI.backgroundColor = new Color(0.1f, 0.2f, 0.1f, 0.9f);
        GUI.Box(new Rect(x, y, width, height), string.Empty);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);
        GUI.Label(new Rect(x, y + 10f, width, 30f), "STATS (Lv. " + level + ")", titleStyle);

        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        statStyle.normal.textColor = Color.white;

        float paddingY = 55f;
        float lineHeight = 28f;
        GUI.Label(new Rect(x + 20f, y + paddingY, width, 30f), "Health: " + health + " / " + maxHealth, statStyle);
        GUI.Label(new Rect(x + 20f, y + paddingY + lineHeight, width, 30f), "Speed: " + Speed, statStyle);
        GUI.Label(new Rect(x + 20f, y + paddingY + lineHeight * 2f, width, 30f), "Jump: " + JumpForce, statStyle);
        GUI.Label(new Rect(x + 20f, y + paddingY + lineHeight * 3f, width, 30f), "XP: " + experience + " / " + maxExperience, statStyle);
        GUI.Label(new Rect(x + 20f, y + paddingY + lineHeight * 4f, width, 30f), "Grunts defeated: " + gruntsKilled, statStyle);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };
        infoStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y + height - 30f, width, 20f), "Press M to close", infoStyle);
        GUI.backgroundColor = Color.white;
    }
}
