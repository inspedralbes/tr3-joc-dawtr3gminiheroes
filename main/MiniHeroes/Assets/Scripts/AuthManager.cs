using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class AuthManager : MonoBehaviour
{
    private static AuthManager instance;
    private const string MultiplayerActivePrefKey = "mh_ws_mp_active";
    private const string MultiplayerUrlPrefKey = "mh_ws_mp_url";
    private const string MultiplayerRoomPrefKey = "mh_ws_mp_room";
    private const string MultiplayerUsernamePrefKey = "mh_ws_mp_username";

    private enum UiState
    {
        Login,
        ModeSelect,
        MultiplayerMenu,
        Lobby,
        InGame
    }

    private bool isLoggedIn;
    private bool isLoginMode = true;
    private UiState uiState = UiState.Login;

    private string username = string.Empty;
    private string password = string.Empty;
    private string message = string.Empty;

    private readonly string backendUrl = "https://backend-miniheroes.onrender.com/api/";

    [Header("Scene Configuration")]
    [Tooltip("Scene loaded after a successful login. Leave empty to stay on the current scene.")]
    public string sceneToLoadAfterLogin = string.Empty;

    [Header("Game Mode")]
    [Tooltip("Scene loaded when selecting Solo mode.")]
    public string soloSceneName = "SampleScene";

    [Header("Multiplayer (WebSocket)")]
    public string websocketUrl = "wss://backend-miniheroes.onrender.com";

    [Tooltip("Scene loaded when starting a multiplayer match.")]
    public string multiplayerSceneName = "SampleScene";

    private string roomCode = string.Empty;
    private string createdRoomCode = string.Empty;
    private string lobbyStatus = string.Empty;
    private bool lobbyBusy;
    private MiniHeroesWsLobbyClient wsLobby;
    private Camera menuCamera;

    private int cachedLevel = 1;
    private int cachedExperience;
    private int cachedGruntsKilled;

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

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void Start()
    {
        if (MiniHeroesRuntimeMode.IsTraining)
        {
            isLoggedIn = true;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(sceneToLoadAfterLogin))
            {
                SceneManager.LoadScene(sceneToLoadAfterLogin);
            }
            return;
        }

        if (PlayerPrefs.HasKey("session_token"))
        {
            isLoggedIn = true;
            Time.timeScale = 0f;
            uiState = UiState.ModeSelect;
        }
        else
        {
            Time.timeScale = 0f;
            uiState = UiState.Login;
        }

        wsLobby = GetComponent<MiniHeroesWsLobbyClient>();
        if (wsLobby == null)
        {
            wsLobby = gameObject.AddComponent<MiniHeroesWsLobbyClient>();
        }
        wsLobby.LobbyUpdated += OnLobbyUpdated;
        wsLobby.Error += OnLobbyError;
        wsLobby.StartGameReceived += OnStartGameReceived;

        EnsureMenuCamera();
    }

    public static bool Exists()
    {
        return instance != null;
    }

    public static void Logout()
    {
        if (instance == null)
        {
            ClearMultiplayerSessionPrefs();
            PlayerPrefs.DeleteKey("session_token");
            PlayerPrefs.Save();
            return;
        }

        instance.isLoggedIn = false;
        instance.isLoginMode = true;
        instance.uiState = UiState.Login;
        instance.password = string.Empty;
        instance.message = string.Empty;
        instance.lobbyStatus = string.Empty;
        instance.lobbyBusy = false;
        instance.wsLobby?.Disconnect();
        ClearMultiplayerSessionPrefs();
        PlayerPrefs.DeleteKey("session_token");
        PlayerPrefs.Save();
        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        EnsureMenuCamera();

        if (isLoggedIn)
        {
            if (uiState == UiState.InGame)
            {
                Time.timeScale = 1f;
                CleanupMenuCamera();
                return;
            }

            Time.timeScale = 0f;
            DrawLoggedInUi();
            return;
        }

        int width = 450;
        int height = 350;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.backgroundColor = Color.black;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);

        GUI.backgroundColor = new Color(0.1f, 0.25f, 0.1f);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 24;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.normal.textColor = new Color(0.9f, 0.95f, 0.6f);
        GUI.Box(new Rect(x, y, width, height), isLoginMode ? " BASE CAMP " : " RECRUITMENT ", boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = new Color(0.7f, 1f, 0.7f);

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

        GUI.Label(new Rect(x + padding, y + 80f, 150f, 30f), "User:", labelStyle);
        username = GUI.TextField(new Rect(x + 160f, y + 80f, width - 200f, 35f), username, textFieldStyle);

        GUI.Label(new Rect(x + padding, y + 140f, 150f, 30f), "Password:", labelStyle);
        password = GUI.PasswordField(new Rect(x + 160f, y + 140f, width - 200f, 35f), password, '*', textFieldStyle);

        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
        if (GUI.Button(new Rect(x + padding, y + 210f, width - (padding * 2f), 45f), isLoginMode ? "Enter Jungle" : "Create Explorer", primaryButtonStyle))
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

        GUI.backgroundColor = new Color(0.4f, 0.3f, 0.2f);
        if (GUI.Button(new Rect(x + padding, y + 265f, width - (padding * 2f), 35f), isLoginMode ? "Need an account?" : "Already registered?", switchButtonStyle))
        {
            isLoginMode = !isLoginMode;
            message = string.Empty;
        }

        GUIStyle msgStyle = new GUIStyle(GUI.skin.label);
        msgStyle.fontSize = 16;
        msgStyle.alignment = TextAnchor.MiddleCenter;
        msgStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y + 310f, width, 30f), message, msgStyle);

        GUI.backgroundColor = Color.white;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMenuCamera();
    }

    private void EnsureMenuCamera()
    {
        if (MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        if (isLoggedIn && uiState == UiState.InGame)
        {
            CleanupMenuCamera();
            return;
        }

        if (HasAnyEnabledCamera())
        {
            return;
        }

        if (menuCamera != null)
        {
            menuCamera.enabled = true;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            return;
        }

        GameObject cameraObject = new GameObject("MenuCamera");
        menuCamera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        menuCamera.orthographic = true;
        menuCamera.clearFlags = CameraClearFlags.SolidColor;
        menuCamera.backgroundColor = Color.black;
        menuCamera.depth = -100f;
        DontDestroyOnLoad(cameraObject);
    }

    private void CleanupMenuCamera()
    {
        if (menuCamera == null)
        {
            return;
        }

        Destroy(menuCamera.gameObject);
        menuCamera = null;
    }

    private static bool HasAnyEnabledCamera()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawLoggedInUi()
    {
        int width = 520;
        int height = 430;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUI.backgroundColor = Color.black;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty);
        GUI.backgroundColor = new Color(0.1f, 0.25f, 0.1f);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        boxStyle.normal.textColor = new Color(0.9f, 0.95f, 0.6f);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.textColor = new Color(0.95f, 1f, 0.8f);

        GUIStyle smallButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = Color.white;

        float padding = 40f;

        switch (uiState)
        {
            case UiState.ModeSelect:
            {
                GUI.Box(new Rect(x, y, width, height), " MODO DE JUEGO ", boxStyle);

                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
                if (GUI.Button(new Rect(x + padding, y + 110f, width - (padding * 2f), 55f), "1. Solo", buttonStyle))
                {
                    GoToSolo();
                }

                GUI.backgroundColor = new Color(0.2f, 0.45f, 0.6f);
                if (GUI.Button(new Rect(x + padding, y + 185f, width - (padding * 2f), 55f), "2. Multijugador", buttonStyle))
                {
                    uiState = UiState.MultiplayerMenu;
                    lobbyStatus = string.Empty;
                }

                GUI.backgroundColor = new Color(0.4f, 0.3f, 0.2f);
                if (GUI.Button(new Rect(x + padding, y + 265f, width - (padding * 2f), 40f), "Cerrar sesion", smallButtonStyle))
                {
                    Logout();
                    if (Application.CanStreamedLevelBeLoaded("LoginScene"))
                    {
                        SceneManager.LoadScene("LoginScene");
                    }
                }

                GUI.backgroundColor = Color.white;
                break;
            }
            case UiState.MultiplayerMenu:
            {
                GUI.Box(new Rect(x, y, width, height), " MULTIJUGADOR ", boxStyle);

                GUI.Label(new Rect(x + padding, y + 85f, width - (padding * 2f), 25f), "Codigo de sala:", labelStyle);
                roomCode = GUI.TextField(new Rect(x + padding, y + 115f, width - (padding * 2f), 35f), roomCode);

                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
                GUI.enabled = !lobbyBusy;
                if (GUI.Button(new Rect(x + padding, y + 170f, width - (padding * 2f), 50f), "Crear Sala", buttonStyle))
                {
                    lobbyStatus = string.Empty;
                    StartCoroutine(BeginCreateRoomRoutine());
                }

                GUI.backgroundColor = new Color(0.2f, 0.45f, 0.6f);
                if (GUI.Button(new Rect(x + padding, y + 235f, width - (padding * 2f), 50f), "Unirse a Sala", buttonStyle))
                {
                    lobbyStatus = string.Empty;
                    StartCoroutine(BeginJoinRoomRoutine());
                }
                GUI.enabled = true;

                GUI.backgroundColor = new Color(0.4f, 0.3f, 0.2f);
                if (GUI.Button(new Rect(x + padding, y + 300f, (width - (padding * 2f)) / 2f - 10f, 35f), "Atras", smallButtonStyle))
                {
                    uiState = UiState.ModeSelect;
                    lobbyStatus = string.Empty;
                    lobbyBusy = false;
                    wsLobby?.Disconnect();
                }

                if (GUI.Button(new Rect(x + padding + (width - (padding * 2f)) / 2f + 10f, y + 300f, (width - (padding * 2f)) / 2f - 10f, 35f), "Cerrar sesion", smallButtonStyle))
                {
                    Logout();
                    if (Application.CanStreamedLevelBeLoaded("LoginScene"))
                    {
                        SceneManager.LoadScene("LoginScene");
                    }
                }

                if (!string.IsNullOrEmpty(lobbyStatus))
                {
                    GUI.Label(new Rect(x + padding, y + 335f, width - (padding * 2f), 20f), lobbyStatus, labelStyle);
                }

                GUI.backgroundColor = Color.white;
                break;
            }
            case UiState.Lobby:
            {
                GUI.Box(new Rect(x, y, width, height), " LOBBY ", boxStyle);

                string codeToShow = !string.IsNullOrEmpty(createdRoomCode) ? createdRoomCode : roomCode;
                string header = wsLobby != null && wsLobby.IsHost
                    ? "Sala creada. Codigo: " + codeToShow
                    : "Conectado. Codigo: " + codeToShow;

                GUI.Label(new Rect(x + padding, y + 75f, width - (padding * 2f), 20f), header, labelStyle);

                DrawLobbySlots(new Rect(x + padding, y + 125f, width - (padding * 2f), 170f));

                if (!string.IsNullOrEmpty(lobbyStatus))
                {
                    GUI.Label(new Rect(x + padding, y + 295f, width - (padding * 2f), 20f), lobbyStatus, labelStyle);
                }

                if (wsLobby != null)
                {
                    bool canPlay = !lobbyBusy && wsLobby.IsConnected;
                    GUI.backgroundColor = canPlay ? new Color(0.26f, 0.62f, 0.28f) : new Color(0.5f, 0.5f, 0.5f);
                    GUI.enabled = canPlay;

                    if (GUI.Button(new Rect(x + padding, y + 315f, width - (padding * 2f), 35f), "Play", smallButtonStyle))
                    {
                        lobbyBusy = true;
                        if (wsLobby.IsHost)
                        {
                            wsLobby.StartGame(multiplayerSceneName);
                            EnterMultiplayerGame(multiplayerSceneName);
                        }
                        else
                        {
                            lobbyStatus = "Solo el creador puede iniciar la partida.";
                        }
                        lobbyBusy = false;
                    }
                    GUI.enabled = true;
                }

                GUI.backgroundColor = new Color(0.4f, 0.3f, 0.2f);
                float leaveY = y + 355f;
                if (GUI.Button(new Rect(x + padding, leaveY, width - (padding * 2f), 35f), "Salir del Lobby", smallButtonStyle))
                {
                    if (wsLobby != null && wsLobby.IsHost)
                    {
                        wsLobby.CloseRoom();
                    }
                    lobbyStatus = string.Empty;
                    lobbyBusy = false;
                    wsLobby?.Disconnect();
                    uiState = UiState.MultiplayerMenu;
                }

                GUI.backgroundColor = Color.white;
                break;
            }
        }
    }

    private void DrawLobbySlots(Rect rect)
    {
        GUIStyle slotStyle = new GUIStyle(GUI.skin.box);
        slotStyle.fontSize = 14;
        slotStyle.normal.textColor = Color.white;

        int maxSlots = 4;
        float slotHeight = 40f;
        float gap = 8f;

        for (int i = 0; i < maxSlots; i++)
        {
            float y = rect.y + i * (slotHeight + gap);
            Rect slotRect = new Rect(rect.x, y, rect.width, slotHeight);
            string text = "Slot " + (i + 1) + ": (vacio)";

            if (wsLobby != null)
            {
                MiniHeroesWsLobbyClient.LobbyPlayerInfo player = null;
                for (int j = 0; j < wsLobby.Players.Count; j++)
                {
                    if (wsLobby.Players[j] != null && wsLobby.Players[j].slot == (i + 1))
                    {
                        player = wsLobby.Players[j];
                        break;
                    }
                }

                if (player != null)
                {
                    text = "Perfil: " + player.username + " | Nivel " + player.level + " | XP " + player.experience + " | Kills " + player.grunts_killed;
                }
            }

            GUI.Box(slotRect, text, slotStyle);
        }
    }

    private void GoToSolo()
    {
        uiState = UiState.InGame;
        lobbyStatus = string.Empty;
        lobbyBusy = false;
        wsLobby?.Disconnect();
        ClearMultiplayerSessionPrefs();
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(soloSceneName) && Application.CanStreamedLevelBeLoaded(soloSceneName))
        {
            SceneManager.LoadScene(soloSceneName);
        }
        else if (Application.CanStreamedLevelBeLoaded("SampleScene"))
        {
            SceneManager.LoadScene("SampleScene");
        }
        else
        {
            uiState = UiState.ModeSelect;
            lobbyStatus = "No se pudo cargar la escena de juego.";
        }
    }

    private IEnumerator BeginCreateRoomRoutine()
    {
        lobbyBusy = true;
        lobbyStatus = "Cargando stats...";
        yield return LoadLocalStatsRoutine();

        string displayName = PlayerPrefs.GetString("username", "Player");

        createdRoomCode = GenerateRoomCode(6);
        roomCode = createdRoomCode;
        wsLobby.Disconnect();
        wsLobby.Connect(websocketUrl, createdRoomCode, displayName, cachedLevel, cachedExperience, cachedGruntsKilled);
        lobbyBusy = false;

        lobbyStatus = string.Empty;
        uiState = UiState.Lobby;
    }

    private IEnumerator BeginJoinRoomRoutine()
    {
        lobbyBusy = true;
        lobbyStatus = "Cargando stats...";
        yield return LoadLocalStatsRoutine();

        string code = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            lobbyBusy = false;
            lobbyStatus = "Introduce un codigo de sala.";
            yield break;
        }
        string displayName = PlayerPrefs.GetString("username", "Player");

        createdRoomCode = string.Empty;
        wsLobby.Disconnect();
        wsLobby.Connect(websocketUrl, code, displayName, cachedLevel, cachedExperience, cachedGruntsKilled);
        lobbyBusy = false;

        lobbyStatus = string.Empty;
        uiState = UiState.Lobby;
    }

    private IEnumerator LoadLocalStatsRoutine()
    {
        cachedLevel = 1;
        cachedExperience = 0;
        cachedGruntsKilled = 0;

        if (!PlayerPrefs.HasKey("session_token"))
        {
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get(backendUrl + "stats");
        request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("session_token"));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            yield break;
        }

        LobbyStatsData stats = JsonUtility.FromJson<LobbyStatsData>(request.downloadHandler.text);
        if (stats == null)
        {
            yield break;
        }

        cachedExperience = stats.experience;
        cachedGruntsKilled = stats.grunts_killed;
        if (stats.level > 0)
        {
            cachedLevel = stats.level;
        }
    }

    private void OnLobbyUpdated()
    {
        lobbyStatus = string.Empty;
    }

    private void OnLobbyError(string error)
    {
        lobbyStatus = error;
        // Do not auto-exit the lobby on socket errors/disconnects.
        // The player leaves only by pressing lobby/menu buttons.
        if (uiState == UiState.Lobby)
        {
            lobbyBusy = false;
        }
    }

    private void OnStartGameReceived(string sceneName)
    {
        EnterMultiplayerGame(sceneName);
    }

    private void EnterMultiplayerGame(string sceneName)
    {
        SaveMultiplayerSessionPrefs();

        uiState = UiState.InGame;
        lobbyStatus = string.Empty;
        lobbyBusy = false;
        wsLobby?.Disconnect();

        if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        if (!string.IsNullOrEmpty(multiplayerSceneName) && Application.CanStreamedLevelBeLoaded(multiplayerSceneName))
        {
            SceneManager.LoadScene(multiplayerSceneName);
            return;
        }

        uiState = UiState.MultiplayerMenu;
        lobbyStatus = "No se pudo cargar la escena de partida.";
    }

    private void SaveMultiplayerSessionPrefs()
    {
        string resolvedRoomId = string.Empty;
        if (wsLobby != null && !string.IsNullOrEmpty(wsLobby.RoomId))
        {
            resolvedRoomId = wsLobby.RoomId;
        }
        else if (!string.IsNullOrWhiteSpace(createdRoomCode))
        {
            resolvedRoomId = createdRoomCode.Trim().ToUpperInvariant();
        }
        else if (!string.IsNullOrWhiteSpace(roomCode))
        {
            resolvedRoomId = roomCode.Trim().ToUpperInvariant();
        }

        string displayName = PlayerPrefs.GetString("username", "Player");
        PlayerPrefs.SetInt(MultiplayerActivePrefKey, 1);
        PlayerPrefs.SetString(MultiplayerUrlPrefKey, websocketUrl ?? string.Empty);
        PlayerPrefs.SetString(MultiplayerRoomPrefKey, resolvedRoomId ?? string.Empty);
        PlayerPrefs.SetString(MultiplayerUsernamePrefKey, displayName ?? "Player");
        PlayerPrefs.Save();
    }

    private static void ClearMultiplayerSessionPrefs()
    {
        PlayerPrefs.DeleteKey(MultiplayerActivePrefKey);
        PlayerPrefs.DeleteKey(MultiplayerUrlPrefKey);
        PlayerPrefs.DeleteKey(MultiplayerRoomPrefKey);
        PlayerPrefs.DeleteKey(MultiplayerUsernamePrefKey);
    }

    private static string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Text.StringBuilder b = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            b.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        }
        return b.ToString();
    }

    [System.Serializable]
    private class LobbyStatsData
    {
        public int experience;
        public int grunts_killed;
        public int level;
    }

    private IEnumerator LoginRoutine(string user, string pass)
    {
        message = "Connecting...";

        AuthData data = new AuthData
        {
            username = user,
            password = pass
        };

        string jsonData = JsonUtility.ToJson(data);
        UnityWebRequest request = new UnityWebRequest(backendUrl + "login", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            message = "Invalid credentials.";
            yield break;
        }

        LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
        if (response == null || string.IsNullOrEmpty(response.token))
        {
            message = "Session token missing.";
            yield break;
        }

        PlayerPrefs.SetString("session_token", response.token);
        if (!string.IsNullOrEmpty(response.username))
        {
            PlayerPrefs.SetString("username", response.username);
        }
        PlayerPrefs.Save();

        message = "Login successful.";
        yield return new WaitForSecondsRealtime(0.5f);

        isLoggedIn = true;
        Time.timeScale = 0f;
        uiState = UiState.ModeSelect;

        message = string.Empty;
    }

    private IEnumerator RegisterRoutine(string user, string pass)
    {
        message = "Creating account...";

        AuthData data = new AuthData
        {
            username = user,
            password = pass
        };

        string jsonData = JsonUtility.ToJson(data);
        UnityWebRequest request = new UnityWebRequest(backendUrl + "register", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            message = "Account created. You can log in now.";
            isLoginMode = true;
        }
        else
        {
            message = "Register failed: " + request.error;
        }
    }
}
