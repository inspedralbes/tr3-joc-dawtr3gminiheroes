using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiniHeroesWsGameplayClient : MonoBehaviour
{
    private const string MultiplayerActivePrefKey = "mh_ws_mp_active";
    private const string MultiplayerUrlPrefKey = "mh_ws_mp_url";
    private const string MultiplayerRoomPrefKey = "mh_ws_mp_room";
    private const string MultiplayerUsernamePrefKey = "mh_ws_mp_username";

    [Serializable]
    private class JoinMatchMessage
    {
        public string type;
        public string roomId;
        public string username;
    }

    [Serializable]
    private class PlayerStatePayload
    {
        public string clientId;
        public string id;
        public string username;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public bool facingRight;
        public bool running;
        public bool dead;

        public string ResolveClientId()
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                return clientId;
            }

            return id ?? string.Empty;
        }
    }

    [Serializable]
    private class PlayerStateMessage
    {
        public string type;
        public string roomId;
        public PlayerStatePayload player;
    }

    [Serializable]
    private class WelcomeMessage
    {
        public string type;
        public string clientId;
        public string roomId;
    }

    [Serializable]
    private class MatchStateMessage
    {
        public string type;
        public string roomId;
        public PlayerStatePayload[] players;
        public PlayerStatePayload[] states;
    }

    [Serializable]
    private class PlayerLeftMessage
    {
        public string type;
        public string clientId;
        public string id;
    }

    private sealed class RemotePlayerView
    {
        public string clientId;
        public JohnMovement movement;
        public float lastSeenTime;
    }

    [Range(1f, 60f)]
    public float sendRate = 20f;

    [Range(0f, 1f)]
    public float interpolation = 0.45f;

    public float staleRemoteTimeout = 8f;

    private MiniHeroesWebSocketClient ws;
    private readonly Dictionary<string, RemotePlayerView> remotes = new Dictionary<string, RemotePlayerView>();

    private JohnMovement localPlayer;
    private string websocketUrl = string.Empty;
    private string roomId = string.Empty;
    private string username = "Player";
    private string localClientId = string.Empty;
    private float nextSendTime;
    private bool joinSent;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        LoadSessionPrefs();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        TryResolveLocalPlayer();
        TryConnect();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CleanupSocket();
        ClearRemotePlayers();
    }

    private void Update()
    {
        if (PlayerPrefs.GetInt(MultiplayerActivePrefKey, 0) != 1)
        {
            CleanupSocket();
            ClearRemotePlayers();
            Destroy(gameObject);
            return;
        }

        ws?.Pump();

        if (localPlayer == null)
        {
            TryResolveLocalPlayer();
        }

        if (ws == null)
        {
            TryConnect();
        }

        if (ws != null && ws.IsOpen && !joinSent)
        {
            SendJoin();
        }

        if (ws != null && ws.IsOpen && localPlayer != null && Time.unscaledTime >= nextSendTime)
        {
            SendLocalState();
            float rate = Mathf.Max(1f, sendRate);
            nextSendTime = Time.unscaledTime + (1f / rate);
        }

        RemoveStaleRemotes();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearRemotePlayers();
        localPlayer = null;
    }

    private void LoadSessionPrefs()
    {
        websocketUrl = PlayerPrefs.GetString(MultiplayerUrlPrefKey, string.Empty);
        roomId = PlayerPrefs.GetString(MultiplayerRoomPrefKey, string.Empty);
        username = PlayerPrefs.GetString(MultiplayerUsernamePrefKey, "Player");
    }

    private void TryResolveLocalPlayer()
    {
        JohnMovement[] players = FindObjectsByType<JohnMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && !players[i].IsRemotePlayer)
            {
                localPlayer = players[i];
                return;
            }
        }
    }

    private void TryConnect()
    {
        if (string.IsNullOrEmpty(websocketUrl) || string.IsNullOrEmpty(roomId))
        {
            return;
        }

        ws = new MiniHeroesWebSocketClient();
        ws.Opened += OnWsOpened;
        ws.MessageReceived += OnWsMessage;
        ws.Error += OnWsError;
        ws.Closed += OnWsClosed;
        ws.Connect(websocketUrl);
    }

    private void CleanupSocket()
    {
        if (ws == null)
        {
            return;
        }

        ws.Opened -= OnWsOpened;
        ws.MessageReceived -= OnWsMessage;
        ws.Error -= OnWsError;
        ws.Closed -= OnWsClosed;
        ws.Close();
        ws = null;
        joinSent = false;
        localClientId = string.Empty;
    }

    private void SendJoin()
    {
        if (ws == null || !ws.IsOpen)
        {
            return;
        }

        JoinMatchMessage join = new JoinMatchMessage
        {
            type = "join_match",
            roomId = roomId,
            username = username
        };

        ws.Send(JsonUtility.ToJson(join));
        joinSent = true;
    }

    private void SendLocalState()
    {
        if (localPlayer == null || localPlayer.IsDead)
        {
            return;
        }

        JohnMovement.MultiplayerState state = localPlayer.CaptureMultiplayerState();

        PlayerStateMessage message = new PlayerStateMessage
        {
            type = "player_state",
            roomId = roomId,
            player = new PlayerStatePayload
            {
                clientId = localClientId,
                username = username,
                x = state.x,
                y = state.y,
                vx = state.vx,
                vy = state.vy,
                facingRight = state.facingRight,
                running = state.running,
                dead = state.dead
            }
        };

        ws.Send(JsonUtility.ToJson(message));
    }

    private void OnWsOpened()
    {
        joinSent = false;
    }

    private void OnWsMessage(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        string type = ExtractType(payload);
        if (type == "welcome" || type == "match_welcome")
        {
            WelcomeMessage welcome = JsonUtility.FromJson<WelcomeMessage>(payload);
            if (welcome != null)
            {
                localClientId = welcome.clientId ?? string.Empty;
                if (!string.IsNullOrEmpty(welcome.roomId))
                {
                    roomId = welcome.roomId;
                }
            }
            return;
        }

        if (type == "match_state" || type == "state" || type == "players_state")
        {
            MatchStateMessage message = JsonUtility.FromJson<MatchStateMessage>(payload);
            if (message == null)
            {
                return;
            }

            PlayerStatePayload[] players = message.players;
            if ((players == null || players.Length == 0) && message.states != null)
            {
                players = message.states;
            }

            ApplySnapshot(players);
            return;
        }

        if (type == "player_left")
        {
            PlayerLeftMessage left = JsonUtility.FromJson<PlayerLeftMessage>(payload);
            if (left == null)
            {
                return;
            }

            string id = !string.IsNullOrEmpty(left.clientId) ? left.clientId : (left.id ?? string.Empty);
            RemoveRemotePlayer(id);
        }
    }

    private void ApplySnapshot(PlayerStatePayload[] players)
    {
        if (players == null || players.Length == 0)
        {
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            PlayerStatePayload state = players[i];
            if (state == null)
            {
                continue;
            }

            string clientId = state.ResolveClientId();
            if (string.IsNullOrEmpty(clientId))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(localClientId) && clientId == localClientId)
            {
                continue;
            }

            RemotePlayerView remote = GetOrCreateRemotePlayer(clientId, state.x, state.y);
            if (remote == null || remote.movement == null)
            {
                continue;
            }

            remote.lastSeenTime = Time.unscaledTime;
            remote.movement.ApplyRemoteState(state.x, state.y, state.vx, state.vy, state.facingRight, state.running, state.dead, interpolation);
        }
    }

    private RemotePlayerView GetOrCreateRemotePlayer(string clientId, float x, float y)
    {
        if (remotes.TryGetValue(clientId, out RemotePlayerView existing))
        {
            return existing;
        }

        if (localPlayer == null)
        {
            return null;
        }

        Vector3 position = new Vector3(x, y, localPlayer.transform.position.z);
        GameObject clone = Instantiate(localPlayer.gameObject, position, Quaternion.identity);
        clone.name = "RemotePlayer_" + clientId;

        JohnMovement remoteMovement = clone.GetComponent<JohnMovement>();
        if (remoteMovement == null)
        {
            Destroy(clone);
            return null;
        }

        remoteMovement.ConfigureAsRemotePlayer();

        RemotePlayerView created = new RemotePlayerView
        {
            clientId = clientId,
            movement = remoteMovement,
            lastSeenTime = Time.unscaledTime
        };

        remotes[clientId] = created;
        return created;
    }

    private void RemoveStaleRemotes()
    {
        if (staleRemoteTimeout <= 0f || remotes.Count == 0)
        {
            return;
        }

        List<string> toRemove = null;
        float now = Time.unscaledTime;

        foreach (KeyValuePair<string, RemotePlayerView> kvp in remotes)
        {
            if (now - kvp.Value.lastSeenTime <= staleRemoteTimeout)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<string>();
            }

            toRemove.Add(kvp.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveRemotePlayer(toRemove[i]);
        }
    }

    private void RemoveRemotePlayer(string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return;
        }

        if (!remotes.TryGetValue(clientId, out RemotePlayerView remote))
        {
            return;
        }

        remotes.Remove(clientId);

        if (remote != null && remote.movement != null)
        {
            Destroy(remote.movement.gameObject);
        }
    }

    private void ClearRemotePlayers()
    {
        foreach (KeyValuePair<string, RemotePlayerView> kvp in remotes)
        {
            if (kvp.Value != null && kvp.Value.movement != null)
            {
                Destroy(kvp.Value.movement.gameObject);
            }
        }

        remotes.Clear();
    }

    private void OnWsError(string error)
    {
        Debug.LogWarning("[MiniHeroesWsGameplayClient] WebSocket error: " + error);
    }

    private void OnWsClosed(int code)
    {
        if (ws != null)
        {
            ws.Opened -= OnWsOpened;
            ws.MessageReceived -= OnWsMessage;
            ws.Error -= OnWsError;
            ws.Closed -= OnWsClosed;
            ws = null;
        }

        joinSent = false;
        localClientId = string.Empty;
    }

    private static string ExtractType(string json)
    {
        const string key = "\"type\"";
        int idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0)
        {
            return string.Empty;
        }

        idx = json.IndexOf(':', idx);
        if (idx < 0)
        {
            return string.Empty;
        }

        idx++;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '"'))
        {
            if (json[idx] == '"')
            {
                idx++;
                break;
            }
            idx++;
        }

        int end = json.IndexOf('"', idx);
        if (end < 0)
        {
            return string.Empty;
        }

        return json.Substring(idx, end - idx);
    }
}
