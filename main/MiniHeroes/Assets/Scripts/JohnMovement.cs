using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class JohnMovement : MonoBehaviour, IDamageable
{
    private const int StatPointsPerLevel = 3;
    private const int BaseMaxHealth = 10;
    private const int MaxHealthCap = 50;
    private const int BaseAttack = 1;
    private const int MaxAttackCap = 25;
    private const float FixedMoveSpeed = 1.5f;
    private const int HealthUpgradeAmount = 2;
    private const int DamageUpgradeAmount = 1;

    public GameObject Bullet;
    public float Speed = FixedMoveSpeed;
    public float JumpForce = 150f;
    public float GroundCheckDistance = 0.18f;
    public float CoyoteTime = 0.12f;
    public float JumpBufferTime = 0.12f;
    public float DamageInvulnerabilityDuration = 0.6f;
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
    private int maxHealth = BaseMaxHealth;
    private int health = BaseMaxHealth;
    private bool isDead;
    private bool showStatsMenu;
    private bool showPostDeathMenu;
    private int experience;
    private int maxExperience = 20;
    private int gruntsKilled;
    private int level = 1;
    private int statPoints;
    private int attack = BaseAttack;
    private Vector3 spawnPoint;
    private bool useExternalControl;
    private float externalHorizontal;
    private bool externalJumpRequested;
    private bool externalShootRequested;
    private float invulnerableUntil = -10f;

    

    private readonly string backendUrl = "http://localhost:3000/api/";

    public DamageTeam Team => DamageTeam.Player;
    public bool IsDead => isDead;

    [System.Serializable]
    private class StatsData
    {
        public int experience;
        public int grunts_killed;
        public int level;
        public int stat_points;
        public float speed;
        public int max_health;
        public int attack;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        body = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spawnPoint = transform.position;
        attack = BaseAttack;
        maxHealth = BaseMaxHealth;
        health = BaseMaxHealth;
        Speed = FixedMoveSpeed;

        CalculateMaxExperience();
        EnsureGameplaySystems();

        if (!MiniHeroesRuntimeMode.IsTraining && PlayerPrefs.HasKey("session_token"))
        {
            StartCoroutine(LoadStatsRoutine());
        }
    }

    private void Update()
    {
        UpdateInvulnerabilityVisual();

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
            statPoints += StatPointsPerLevel;
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

        if (IsInvulnerable())
        {
            return;
        }

        health -= amount;
        if (DamageInvulnerabilityDuration > 0f)
        {
            invulnerableUntil = Time.time + DamageInvulnerabilityDuration;
        }

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
            ResetVisualState();
            Object.FindFirstObjectByType<MiniHeroesTrainingManager>()?.HandlePlayerDeath();
            return;
        }

        isDead = true;
        showPostDeathMenu = false;
        Time.timeScale = 0f;
        ResetVisualState();
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
        invulnerableUntil = -10f;
        ResetVisualState();

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

    body.linearVelocity = new Vector2(body.linearVelocity.x, JumpForce * Time.fixedDeltaTime);
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
            bulletScript.Damage = Mathf.Max(1, attack);
        }
    }

    private bool CheckGrounded()
{
    if (capsuleCollider == null)
    {
        return false;
    }

    Bounds bounds = capsuleCollider.bounds;

    float rayLength = GroundCheckDistance;

    Vector2 center = new Vector2(bounds.center.x, bounds.min.y);
    Vector2 left = new Vector2(bounds.min.x + 0.05f, bounds.min.y);
    Vector2 right = new Vector2(bounds.max.x - 0.05f, bounds.min.y);

    RaycastHit2D hitCenter = Physics2D.Raycast(center, Vector2.down, rayLength, GroundMask);
    RaycastHit2D hitLeft = Physics2D.Raycast(left, Vector2.down, rayLength, GroundMask);
    RaycastHit2D hitRight = Physics2D.Raycast(right, Vector2.down, rayLength, GroundMask);

    return hitCenter.collider != null || hitLeft.collider != null || hitRight.collider != null;
}

    private bool CanUseBufferedJump()
    {
        if (Time.time > lastJumpPressedTime + JumpBufferTime)
        {
            return false;
        }

        return grounded || Time.time <= lastGroundedTime + CoyoteTime;
    }

    private bool IsInvulnerable()
    {
        return Time.time < invulnerableUntil;
    }

    private void UpdateInvulnerabilityVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (!IsInvulnerable() || isDead)
        {
            Color visible = spriteRenderer.color;
            visible.a = 1f;
            spriteRenderer.color = visible;
            return;
        }

        float alpha = Mathf.Sin(Time.time * 25f) > 0f ? 0.45f : 1f;
        Color blinking = spriteRenderer.color;
        blinking.a = alpha;
        spriteRenderer.color = blinking;
    }

    private void ResetVisualState()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color reset = spriteRenderer.color;
        reset.a = 1f;
        spriteRenderer.color = reset;
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
        statPoints = Mathf.Max(0, stats.stat_points);
        Speed = FixedMoveSpeed;
        if (stats.max_health > 0)
        {
            maxHealth = Mathf.Clamp(stats.max_health, BaseMaxHealth, MaxHealthCap);
        }
        else
        {
            maxHealth = BaseMaxHealth;
        }
        if (stats.attack > 0)
        {
            attack = Mathf.Clamp(stats.attack, BaseAttack, MaxAttackCap);
        }
        else
        {
            attack = BaseAttack;
        }
        health = maxHealth;
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
            level = level,
            stat_points = statPoints,
            speed = Speed,
            max_health = maxHealth,
            attack = attack
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

        if (IsInvulnerable())
        {
            GUIStyle invulnerableStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
            invulnerableStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
            GUI.Label(new Rect(rect.x - 20f, rect.y - 34f, rect.width + 40f, 16f), "INVULNERABLE", invulnerableStyle);
        }

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
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 4f, rect.width - 40f, 30f), "Daño: " + attack, statStyle);
        GUI.Label(new Rect(rect.x + 20f, lineY + lineHeight * 5f, rect.width - 40f, 30f), "Puntos: " + statPoints, statStyle);
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        showPostDeathMenu = false;
        invulnerableUntil = -10f;
        ResetVisualState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnToLogin()
    {
        showPostDeathMenu = false;
        invulnerableUntil = -10f;
        ResetVisualState();
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
        int width = 330;
        int height = 260;
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
        float lineHeight = 32f;
        GUI.Label(new Rect(x + 20f, y + paddingY - 24f, width - 40f, 24f), "Stat points: " + statPoints, statStyle);
        DrawUpgradeRow(x, y + paddingY, "MAX HP", maxHealth + " / " + MaxHealthCap, statPoints > 0 && maxHealth < MaxHealthCap, TryUpgradeMaxHealth);
        DrawUpgradeRow(x, y + paddingY + lineHeight, "Damage", attack + " / " + MaxAttackCap, statPoints > 0 && attack < MaxAttackCap, TryUpgradeDamage);
        GUI.Label(new Rect(x + 20f, y + paddingY + (lineHeight * 2f) + 4f, width - 40f, 24f), "XP: " + experience + " / " + maxExperience, statStyle);
        GUI.Label(new Rect(x + 20f, y + paddingY + (lineHeight * 3f) + 4f, width - 40f, 24f), "Grunts defeated: " + gruntsKilled, statStyle);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };
        infoStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y + height - 30f, width, 20f), "Press M to close", infoStyle);
        GUI.backgroundColor = Color.white;
    }

    private void DrawUpgradeRow(float x, float y, string label, string value, bool canUpgrade, System.Action onUpgrade)
    {
        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        statStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(x + 20f, y, 170f, 28f), label + ": " + value, statStyle);

        GUI.enabled = canUpgrade;
        GUI.backgroundColor = canUpgrade ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(new Rect(x + 245f, y, 65f, 28f), "+"))
        {
            onUpgrade?.Invoke();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
    }

    private void TryUpgradeMaxHealth()
    {
        if (statPoints <= 0 || maxHealth >= MaxHealthCap)
        {
            return;
        }

        statPoints -= 1;
        maxHealth = Mathf.Min(MaxHealthCap, maxHealth + HealthUpgradeAmount);
        health = Mathf.Min(maxHealth, health + HealthUpgradeAmount);
        SaveStatsIfNeeded();
    }

    private void TryUpgradeDamage()
    {
        if (statPoints <= 0 || attack >= MaxAttackCap)
        {
            return;
        }

        statPoints -= 1;
        attack = Mathf.Min(MaxAttackCap, attack + DamageUpgradeAmount);
        SaveStatsIfNeeded();
    }

    private void SaveStatsIfNeeded()
    {
        if (!MiniHeroesRuntimeMode.IsTraining)
        {
            StartCoroutine(SaveStatsRoutine());
        }
    }
}
