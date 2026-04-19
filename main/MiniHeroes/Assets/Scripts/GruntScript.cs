using UnityEngine;

public class GruntScript : MonoBehaviour, IDamageable
{
    public GameObject John;
    public GameObject Bullet;
    public float MoveSpeed = 1.75f;
    public float ChaseRange = 7f;
    public float ShootRange = 4f;
    public float StopDistance = 1.25f;
    public float ShootCooldown = 0.9f;
    public int MaxHealth = 5;
    public int ExperienceReward = 5;
    public bool EnableMlAgents = true;

    private EnemyRespawnManager respawnManager;
    private Rigidbody2D body;
    private GruntAgent mlAgent;
    private float lastShootTime = -10f;
    private float moveInput;
    private int currentHealth;
    private bool isDead;
    private bool initialized;

    public DamageTeam Team => DamageTeam.Enemy;
    public bool IsDead => isDead;
    public Vector3 SpawnPoint { get; private set; }
    public float CurrentHealthRatio => MaxHealth <= 0 ? 0f : (float)currentHealth / MaxHealth;
    public bool IsPlayerInShootRange => John != null && Mathf.Abs(John.transform.position.x - transform.position.x) <= ShootRange && Mathf.Abs(John.transform.position.y - transform.position.y) <= 1.5f;
    public bool IsPlayerInChaseRange => John != null && Vector2.Distance(transform.position, John.transform.position) <= ChaseRange;
    public bool FacingPlayer => John != null && Mathf.Sign(transform.localScale.x) == Mathf.Sign(Mathf.Abs(John.transform.position.x - transform.position.x) < 0.01f ? transform.localScale.x : John.transform.position.x - transform.position.x);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        currentHealth = MaxHealth;
        SpawnPoint = transform.position;
    }

    private void Start()
    {
        if (!initialized)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Initialize(player, Object.FindFirstObjectByType<EnemyRespawnManager>(), SpawnPoint, EnableMlAgents);
        }
    }

    private void Update()
    {
        if (isDead || Time.timeScale == 0f)
        {
            return;
        }

        if (John == null)
        {
            John = GameObject.FindGameObjectWithTag("Player");
            if (John == null)
            {
                return;
            }
        }

        if (!IsControlledByAgent())
        {
            RunHeuristic();
        }
    }

    private void FixedUpdate()
    {
        if (body == null || isDead)
        {
            return;
        }

        body.linearVelocity = new Vector2(moveInput * MoveSpeed, body.linearVelocity.y);
    }

    public void Initialize(GameObject playerObject, EnemyRespawnManager manager, Vector3 spawnPoint, bool attachMlAgent)
    {
        John = playerObject;
        respawnManager = manager;
        SpawnPoint = spawnPoint;
        EnableMlAgents = attachMlAgent;
        currentHealth = MaxHealth;
        isDead = false;
        initialized = true;
        moveInput = 0f;

        if (attachMlAgent)
        {
            EnsureAgent();
        }
    }

    public void ResetEnemy()
    {
        transform.position = SpawnPoint;
        currentHealth = MaxHealth;
        isDead = false;
        moveInput = 0f;
        lastShootTime = Time.time;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (John == null)
        {
            John = GameObject.FindGameObjectWithTag("Player");
        }

        EnsureAgent();
        gameObject.SetActive(true);
        mlAgent?.NotifyRespawn();
    }

    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
        if (moveInput < -0.01f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (moveInput > 0.01f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    public void TryShoot(bool ignoreRange = false)
    {
        if (Bullet == null || John == null || Time.time < lastShootTime + ShootCooldown)
        {
            return;
        }

        if (!ignoreRange && !IsPlayerInShootRange)
        {
            return;
        }

        Vector3 travelDirection = transform.localScale.x >= 0f ? Vector3.right : Vector3.left;
        GameObject bullet = Instantiate(Bullet, transform.position + travelDirection * 0.18f, Quaternion.identity);
        BulletScript bulletScript = bullet.GetComponent<BulletScript>();
        if (bulletScript != null)
        {
            bulletScript.Configure(travelDirection, DamageTeam.Enemy, gameObject);
        }

        lastShootTime = Time.time;
    }

    public void ReceiveDamage(int amount, GameObject source, DamageTeam sourceTeam)
    {
        if (isDead || sourceTeam == Team)
        {
            return;
        }

        currentHealth -= amount;
        mlAgent?.NotifyDamageTaken();

        if (currentHealth > 0)
        {
            return;
        }

        isDead = true;
        moveInput = 0f;

        JohnMovement killer = source != null ? source.GetComponentInParent<JohnMovement>() : null;
        if (killer == null && John != null)
        {
            killer = John.GetComponent<JohnMovement>();
        }

        if (killer != null && sourceTeam == DamageTeam.Player)
        {
            killer.AddExperience(ExperienceReward);
        }

        mlAgent?.NotifyDeath();
        respawnManager?.HandleEnemyDeath(this);
        gameObject.SetActive(false);
    }

    public void Hit()
    {
        ReceiveDamage(1, John, DamageTeam.Player);
    }

    public void NotifySuccessfulAttack()
    {
        mlAgent?.NotifySuccessfulAttack();
    }

    private void RunHeuristic()
    {
        Vector3 toPlayer = John.transform.position - transform.position;
        float horizontalDistance = Mathf.Abs(toPlayer.x);

        if (horizontalDistance > StopDistance && horizontalDistance <= ChaseRange)
        {
            SetMoveInput(Mathf.Sign(toPlayer.x));
        }
        else
        {
            SetMoveInput(0f);
        }

        if (IsPlayerInShootRange)
        {
            TryShoot();
        }
    }

    private bool IsControlledByAgent()
    {
        return EnableMlAgents &&
               mlAgent != null &&
               mlAgent.enabled &&
               (MiniHeroesRuntimeMode.IsTraining || MiniHeroesInferenceBootstrap.HasLoadedModel);
    }

    private void EnsureAgent()
    {
        if (!EnableMlAgents)
        {
            return;
        }

        if (mlAgent == null)
        {
            mlAgent = GetComponent<GruntAgent>();
        }

        if (mlAgent == null)
        {
            mlAgent = gameObject.AddComponent<GruntAgent>();
        }

        mlAgent.Initialize(this, John != null ? John.transform : null);
    }

    private void OnGUI()
    {
        if (isDead || Camera.main == null)
        {
            return;
        }

        Vector2 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
        float barWidth = 40f;
        float barHeight = 10f;
        Rect rect = new Rect(screenPosition.x - barWidth / 2f, Screen.height - screenPosition.y - barHeight, barWidth, barHeight);

        GUI.color = Color.black;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.red;
        Rect healthRect = new Rect(rect.x, rect.y, rect.width * CurrentHealthRatio, rect.height);
        GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

