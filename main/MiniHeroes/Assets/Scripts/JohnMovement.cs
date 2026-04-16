using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public class JohnMovement : MonoBehaviour
{
    public GameObject Bullet;
    public float Speed;
    public float JumpForce;

    private Rigidbody2D Rigidbody2D;
    private Animator Animator;
    private float Horizontal;
    private bool Grounded;
    private float LastShoot;
    private int MaxHealth = 10;
    private int Health = 10;
    private bool isDead = false;
    private bool showStatsMenu = false;
    private int Experience = 0;
    private int MaxExperience = 20;
    private int GruntsKilled = 0;
    private int Level = 1;

    private string backendUrl = "http://localhost:3000/api/";

    [System.Serializable]
    private class StatsData
    {
        public int experience;
        public int grunts_killed;
        public int level;
    }
    
    void Start()
    {
        Rigidbody2D = GetComponent<Rigidbody2D>();
        Animator = GetComponent<Animator>();

        CalculateMaxExperience();

        // Cargar estadísticas desde la BBDD si hay cuenta iniciada
        if (PlayerPrefs.HasKey("session_token"))
        {
            StartCoroutine(LoadStatsRoutine());
        }
    }

    private void CalculateMaxExperience()
    {
        MaxExperience = 20;
        for (int i = 1; i < Level; i++)
        {
            MaxExperience = Mathf.FloorToInt(MaxExperience * 1.5f);
        }
    }

    IEnumerator LoadStatsRoutine()
    {
        UnityWebRequest request = UnityWebRequest.Get(backendUrl + "stats");
        request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("session_token"));
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            StatsData stats = JsonUtility.FromJson<StatsData>(request.downloadHandler.text);
            if (stats != null)
            {
                Experience = stats.experience;
                GruntsKilled = stats.grunts_killed;
                if (stats.level > 0) Level = stats.level;
                CalculateMaxExperience();
            }
        }
    }

    IEnumerator SaveStatsRoutine()
    {
        if (!PlayerPrefs.HasKey("session_token")) yield break;

        StatsData data = new StatsData();
        data.experience = Experience;
        data.grunts_killed = GruntsKilled;
        data.level = Level;
        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(backendUrl + "stats", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("session_token"));

        yield return request.SendWebRequest();
    }

    void Update()
    {
        if (isDead || Time.timeScale == 0) return;

        // Abrir / Cerrar menú de estadísticas con la tecla M
        if (Input.GetKeyDown(KeyCode.M))
        {
            showStatsMenu = !showStatsMenu;
        }

        Horizontal = Input.GetAxis("Horizontal");

        if(Horizontal < 0.0f) transform.localScale = new Vector3(-1, 1, 1);
        else if(Horizontal > 0.0f) transform.localScale = new Vector3(1, 1, 1);

        Animator.SetBool("running", Horizontal != 0.0f);

        Debug.DrawRay(transform.position, Vector3.down, Color.red);

        if(Physics2D.Raycast(transform.position, Vector3.down, 0.1f))
        {
            Grounded = true;
        }
        else
        {
            Grounded = false;
        }

        if (Input.GetKeyDown(KeyCode.W) && Grounded)
        {
            Jump();
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time > LastShoot + 0.25f)
        {
            Shoot();
            LastShoot = Time.time;
        }
        
    }

    private void Jump()
    {
        Rigidbody2D.AddForce(Vector2.up * JumpForce);
    }

    private void Shoot()
    {
        Vector3 direction;
        if(transform.localScale.x == 1)
        {
            direction = Vector3.right;
        }
        else
        {
            direction = Vector3.left;
        }
        GameObject bullet = Instantiate(Bullet, transform.position + direction * 0.1f, Quaternion.identity);
        bullet.GetComponent<BulletScript>().SetDirection(direction);
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        Rigidbody2D.linearVelocity = new Vector2(Horizontal, Rigidbody2D.linearVelocity.y);
    }
    
    public void AddExperience(int amount)
    {
        if (isDead) return;
        Experience += amount;
        GruntsKilled += 1;
        
        // Comprobar si sube de nivel
        while (Experience >= MaxExperience)
        {
            Experience -= MaxExperience;
            Level++;
            CalculateMaxExperience();
        }

        // Guardar automáticamente en el backend Node.js
        StartCoroutine(SaveStatsRoutine());
    }

    public void Hit()
    {
        if (isDead) return;
        
        Health = Health - 1;
        if (Health <= 0)
        {
            isDead = true;
            Time.timeScale = 0; // Pausar todo el juego
        }
    }

    private void OnGUI()
    {
        if (isDead)
        {
            DrawDefeatScreen();
            return;
        }

        // Dibujar menú de estadísticas si está activo
        if (showStatsMenu)
        {
            DrawStatsMenu();
        }

        if (Camera.main != null)
        {
            Vector2 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
            
            float barWidth = 50f;
            float barHeight = 12f;
            Rect rect = new Rect(screenPosition.x - barWidth / 2, Screen.height - screenPosition.y - barHeight, barWidth, barHeight);
            
            // DIBUJAR EL NIVEL (ENCIMA DE LA BARRA DE VIDA)
            GUIStyle levelStyle = new GUIStyle();
            levelStyle.alignment = TextAnchor.MiddleCenter;
            levelStyle.fontStyle = FontStyle.Bold;
            levelStyle.normal.textColor = new Color(1f, 0.9f, 0.3f); // Dorado
            levelStyle.fontSize = 12;
            GUI.Label(new Rect(rect.x, rect.y - 18f, rect.width, 20), "Lv. " + Level, levelStyle);

            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            
            // Foreground (Health)
            GUI.color = Color.green;
            Rect healthRect = new Rect(rect.x, rect.y, rect.width * ((float)Health / MaxHealth), rect.height);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);

            // --- BARRA DE EXPERIENCIA ---
            float xpBarHeight = 6f; // Un poco más delgada que la de vida
            float spaceBetween = 2f;
            Rect xpRect = new Rect(rect.x, rect.y + rect.height + spaceBetween, barWidth, xpBarHeight);
            
            // Background (Blanco, como pediste)
            GUI.color = Color.white;
            GUI.DrawTexture(xpRect, Texture2D.whiteTexture);

            // Foreground (Amarillo)
            GUI.color = Color.yellow;
            float xpRatio = Mathf.Clamp01((float)Experience / MaxExperience);
            Rect xpFillRect = new Rect(xpRect.x, xpRect.y, xpRect.width * xpRatio, xpRect.height);
            GUI.DrawTexture(xpFillRect, Texture2D.whiteTexture);
            
            // Reset GUI color
            GUI.color = Color.white;
        }
    }

    private void DrawDefeatScreen()
    {
        // Fondo oscuro
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Tema ROJO
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 30;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.normal.textColor = Color.white;

        int width = 400;
        int height = 250;
        float x = (Screen.width - width) / 2;
        float y = (Screen.height - height) / 2;

        GUI.Box(new Rect(x, y, width, height), "¡ HAS MUERTO !", boxStyle);

        // Estilos de botones
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 20;

        // Botón Reintentar
        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.3f); // Verde
        if (GUI.Button(new Rect(x + 50, y + 80, 300, 50), "Volver a Intentarlo", buttonStyle))
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // Botón Salir / Logout
        GUI.backgroundColor = new Color(0.6f, 0.3f, 0.3f); // Rojo oscuro
        if (GUI.Button(new Rect(x + 50, y + 150, 300, 50), "Salir (Cerrar Sesión)", buttonStyle))
        {
            Time.timeScale = 1;
            
            // Eliminamos la sesión guardada
            PlayerPrefs.DeleteKey("session_token");
            PlayerPrefs.Save();
            
            // Verificamos si existe la escena 'LoginScene' en los Build Settings
            if (Application.CanStreamedLevelBeLoaded("LoginScene"))
            {
                SceneManager.LoadScene("LoginScene");
            }
            else
            {
                // Si usa el modo single-scene, simplemente recargamos para que el AuthManager vuelva a pedir login
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        
        GUI.backgroundColor = Color.white;
    }

    private void DrawStatsMenu()
    {
        int width = 250;
        int height = 240; // Más alto para añadir bajas
        float x = 20; // Alineado a la izquierda
        float y = 20; // Alineado arriba

        // Fondo verde oscuro semi-transparente
        GUI.backgroundColor = new Color(0.1f, 0.2f, 0.1f, 0.9f);
        GUI.Box(new Rect(x, y, width, height), "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1f, 0.9f, 0.3f); // Dorado tropical
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new Rect(x, y + 10, width, 30), "ESTADÍSTICAS (Lv. " + Level + ")", titleStyle);

        GUIStyle statStyle = new GUIStyle(GUI.skin.label);
        statStyle.fontSize = 16;
        statStyle.fontStyle = FontStyle.Bold;
        statStyle.normal.textColor = Color.white;

        float paddingY = 55;
        float lineHeight = 28;

        GUI.Label(new Rect(x + 20, y + paddingY, width, 30), "♥ Salud: " + Health + " / " + MaxHealth, statStyle);
        GUI.Label(new Rect(x + 20, y + paddingY + lineHeight, width, 30), "⚡ Velocidad: " + Speed, statStyle);
        GUI.Label(new Rect(x + 20, y + paddingY + lineHeight * 2, width, 30), "⬆ Salto: " + JumpForce, statStyle);
        GUI.Label(new Rect(x + 20, y + paddingY + lineHeight * 3, width, 30), "⭐ Experiencia: " + Experience + " XP", statStyle);
        GUI.Label(new Rect(x + 20, y + paddingY + lineHeight * 4, width, 30), "⚔️ Bajas: " + GruntsKilled + " Grunts", statStyle);

        // Texto informativo de cerrar
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 12;
        infoStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        infoStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(x, y + height - 30, width, 20), "Pulsa 'M' para cerrar", infoStyle);

        GUI.backgroundColor = Color.white;
    }
}
