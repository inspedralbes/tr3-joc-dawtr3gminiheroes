using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;

public class AuthManager : MonoBehaviour
{
    private bool isLoggedIn = false;
    private bool isLoginMode = true; // true = Login, false = Register

    private string username = "";
    private string password = "";
    private string message = "";

    // URL del servidor Node.js
    private string backendUrl = "http://localhost:3000/api/";

    [Header("Configuración de Escenas")]
    [Tooltip("Escribe el nombre de la escena del juego (ej: 'SampleScene'). Si lo dejas vacío, simplemente se ocultará el login y reanudará el juego actual.")]
    public string sceneToLoadAfterLogin = "";

    // Clases para serializar/deserializar JSON nativamente en Unity
    [System.Serializable]
    private class AuthData
    {
        public string username;
        public string password;
    }

    [System.Serializable]
    private class LoginResponse
    {
        public string message;
        public string token;
        public string username;
    }

    void Start()
    {
        // Comprobar si ya existe una sesión guardada localmente
        if (PlayerPrefs.HasKey("session_token"))
        {
            isLoggedIn = true;
            Time.timeScale = 1; // Asegurarnos de que el tiempo corra
            if (!string.IsNullOrEmpty(sceneToLoadAfterLogin))
            {
                SceneManager.LoadScene(sceneToLoadAfterLogin);
            }
        }
        else
        {
            Time.timeScale = 0; // Pausar todo mientras esperamos
        }
    }

    void OnGUI()
    {
        // Si ya ha iniciado sesión, no dibujar esta ventana
        if (isLoggedIn) return;

        int width = 450;
        int height = 350;
        float x = (Screen.width - width) / 2;
        float y = (Screen.height - height) / 2;

        // ==== ESTILOS TIPO JUNGLA ====
        
        // Primero dibujamos un fondo para que sea una pantalla "A parte" completa si así se desea
        GUI.backgroundColor = new Color(0.05f, 0.15f, 0.05f); // Fondo verde muy, muy oscuro casi negro
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
        
        // Color general de fondo para cajas y botones oscuros (Verde Jungla Oscuro)
        GUI.backgroundColor = new Color(0.1f, 0.25f, 0.1f);
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 24;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.normal.textColor = new Color(0.9f, 0.95f, 0.6f); // Amarillo suave

        GUI.Box(new Rect(x, y, width, height), isLoginMode ? " CAMPAMENTO BASE " : " ALISTAMIENTO ", boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = new Color(0.7f, 1f, 0.7f); // Verde claro

        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = 20;

        GUIStyle primaryButtonStyle = new GUIStyle(GUI.skin.button);
        primaryButtonStyle.fontSize = 20;
        primaryButtonStyle.fontStyle = FontStyle.Bold;
        primaryButtonStyle.normal.textColor = new Color(0.95f, 1f, 0.8f);

        GUIStyle switchButtonStyle = new GUIStyle(GUI.skin.button);
        switchButtonStyle.fontSize = 15;
        switchButtonStyle.normal.textColor = new Color(1f, 0.85f, 0.6f);

        float padding = 40f;

        // Campos de texto y etiquetas
        GUI.Label(new Rect(x + padding, y + 80, 150, 30), "Usuario:", labelStyle);
        username = GUI.TextField(new Rect(x + 160, y + 80, width - 200, 35), username, textFieldStyle);

        GUI.Label(new Rect(x + padding, y + 140, 150, 30), "Contraseña:", labelStyle);
        password = GUI.PasswordField(new Rect(x + 160, y + 140, width - 200, 35), password, '*', textFieldStyle);

        // Botón principal
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f); // Verde hoja tropical
        if (GUI.Button(new Rect(x + padding, y + 210, width - (padding * 2), 45), isLoginMode ? "Entrar a la Jungla" : "Crear Explorador", primaryButtonStyle))
        {
            if (isLoginMode)
            {
                StartCoroutine(LoginRoutine(username, password));
            }
            else
            {
                StartCoroutine(RegisterRoutine(username, password));
            }
        }

        // Botón secundario
        GUI.backgroundColor = new Color(0.4f, 0.3f, 0.2f); // Marrón madera
        if (GUI.Button(new Rect(x + padding, y + 265, width - (padding * 2), 35), isLoginMode ? "¿No tienes cuenta? Fírmate como aventurero" : "¿Ya eres explorador? Ingresa al campamento", switchButtonStyle))
        {
            isLoginMode = !isLoginMode;
            message = "";
        }

        // Mensajes
        GUIStyle msgStyle = new GUIStyle(GUI.skin.label);
        msgStyle.fontSize = 16;
        msgStyle.alignment = TextAnchor.MiddleCenter;
        msgStyle.normal.textColor = Color.white;
        
        GUI.Label(new Rect(x, y + 310, width, 30), message, msgStyle);

        // Reset
        GUI.backgroundColor = Color.white;
    }

    IEnumerator LoginRoutine(string user, string pass)
    {
        message = "Conectando al servidor...";
        
        AuthData data = new AuthData();
        data.username = user;
        data.password = pass;
        string jsonData = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(backendUrl + "login", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            LoginResponse resData = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            
            if (resData != null && !string.IsNullOrEmpty(resData.token))
            {
                PlayerPrefs.SetString("session_token", resData.token);
                PlayerPrefs.Save();
                
                message = "¡Login exitoso! Entrando a la Jungla...";
                yield return new WaitForSecondsRealtime(0.5f);
                
                isLoggedIn = true;
                Time.timeScale = 1; // Volver a reanudar el tiempo
                
                if (!string.IsNullOrEmpty(sceneToLoadAfterLogin))
                {
                    SceneManager.LoadScene(sceneToLoadAfterLogin);
                }
            }
            else
            {
                message = "Error leyendo el token de la sesión.";
            }
        }
        else
        {
            message = "Error: Credenciales inválidas.";
        }
    }

    IEnumerator RegisterRoutine(string user, string pass)
    {
        message = "Creando cuenta...";
        
        AuthData data = new AuthData();
        data.username = user;
        data.password = pass;
        string jsonData = JsonUtility.ToJson(data);
        
        UnityWebRequest request = new UnityWebRequest(backendUrl + "register", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            message = "Cuenta creada. Ahora puedes iniciar sesión.";
            isLoginMode = true; // Volver a la pantalla de login
        }
        else
        {
            message = "Error: " + request.error + " (Quizás el usuario ya existe).";
        }
    }
}
