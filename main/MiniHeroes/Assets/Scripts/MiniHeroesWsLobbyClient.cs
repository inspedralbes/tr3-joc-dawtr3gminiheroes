using System;
using System.Collections.Generic;
using UnityEngine;

public class MiniHeroesWsLobbyClient : MonoBehaviour
{
    [Serializable]
    public class LobbyPlayerInfo
    {
        public int slot;
        public string username;
        public int level;
        public int experience;
        public int grunts_killed;
    }

    [Serializable]
    private class JoinMessage
    {
        public string type;
        public string roomId;
        public string username;
        public int level;
        public int experience;
        public int grunts_killed;
    }

    [Serializable]
    private class WelcomeMessage
    {
        public string type;
        public string clientId;
        public string roomId;
    }

    [Serializable]
    private class LobbyStateMessage
    {
        public string type;
        public string roomId;
        public string hostId;
        public LobbyPlayerInfo[] players;
    }

    [Serializable]
    private class ErrorMessage
    {
        public string type;
        public string error;
    }

    [Serializable]
    private class StartGameMessage
    {
        public string type;
        public string scene;
    }

    [Serializable]
    private class CloseRoomMessage
    {
        public string type;
    }

    public bool IsConnected => ws != null && ws.IsOpen;
    public bool IsHost => !string.IsNullOrEmpty(clientId) && clientId == hostId;
    public string RoomId => roomId;
    public string LastError => lastError;
    public IReadOnlyList<LobbyPlayerInfo> Players => players;

    public event Action LobbyUpdated;
    public event Action<string> Error;
    public event Action<string> StartGameReceived;

    private MiniHeroesWebSocketClient ws;
    private readonly List<LobbyPlayerInfo> players = new List<LobbyPlayerInfo>();

    private string url;
    private string roomId;
    private string username;
    private int level;
    private int experience;
    private int gruntsKilled;

    private string clientId = string.Empty;
    private string hostId = string.Empty;
    private string lastError = string.Empty;

    private void Update()
    {
        ws?.Pump();
    }

    public void Connect(string url, string roomId, string username, int level, int experience, int gruntsKilled)
    {
        Disconnect();

        this.url = url;
        this.roomId = roomId;
        this.username = username;
        this.level = level;
        this.experience = experience;
        this.gruntsKilled = gruntsKilled;

        lastError = string.Empty;
        clientId = string.Empty;
        hostId = string.Empty;
        players.Clear();

        ws = new MiniHeroesWebSocketClient();
        ws.Opened += OnWsOpened;
        ws.MessageReceived += OnWsMessage;
        ws.Error += OnWsError;
        ws.Closed += OnWsClosed;
        ws.Connect(url);
    }

    public void Disconnect()
    {
        if (ws != null)
        {
            ws.Opened -= OnWsOpened;
            ws.MessageReceived -= OnWsMessage;
            ws.Error -= OnWsError;
            ws.Closed -= OnWsClosed;
            ws.Close();
        }

        ws = null;
        players.Clear();
        lastError = string.Empty;
        clientId = string.Empty;
        hostId = string.Empty;
    }

    public void CloseRoom()
    {
        if (!IsConnected) return;

        CloseRoomMessage msg = new CloseRoomMessage { type = "close_room" };
        ws.Send(JsonUtility.ToJson(msg));
    }

    public void StartGame(string sceneName)
    {
        if (!IsConnected)
        {
            return;
        }

        StartGameMessage msg = new StartGameMessage
        {
            type = "start_game",
            scene = sceneName
        };
        ws.Send(JsonUtility.ToJson(msg));
    }

    private void OnWsOpened()
    {
        JoinMessage join = new JoinMessage
        {
            type = "join",
            roomId = roomId,
            username = username,
            level = level,
            experience = experience,
            grunts_killed = gruntsKilled
        };
        ws.Send(JsonUtility.ToJson(join));
    }

    private void OnWsMessage(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        string type = ExtractType(payload);
        if (type == "welcome")
        {
            WelcomeMessage welcome = JsonUtility.FromJson<WelcomeMessage>(payload);
            if (welcome != null)
            {
                clientId = welcome.clientId ?? string.Empty;
                if (!string.IsNullOrEmpty(welcome.roomId))
                {
                    roomId = welcome.roomId;
                }
            }
            return;
        }

        if (type == "lobby_state")
        {
            LobbyStateMessage state = JsonUtility.FromJson<LobbyStateMessage>(payload);
            if (state == null)
            {
                return;
            }
            hostId = state.hostId ?? string.Empty;

            players.Clear();
            if (state.players != null)
            {
                players.AddRange(state.players);
                players.Sort((a, b) => a.slot.CompareTo(b.slot));
            }

            LobbyUpdated?.Invoke();
            return;
        }

        if (type == "start_game")
        {
            StartGameMessage msg = JsonUtility.FromJson<StartGameMessage>(payload);
            if (msg != null)
            {
                StartGameReceived?.Invoke(msg.scene);
            }
            return;
        }

        if (type == "error")
        {
            ErrorMessage msg = JsonUtility.FromJson<ErrorMessage>(payload);
            lastError = msg != null && !string.IsNullOrEmpty(msg.error) ? msg.error : "Error de lobby.";
            Error?.Invoke(lastError);
            return;
        }
    }

    private void OnWsError(string error)
    {
        lastError = string.IsNullOrEmpty(error) ? "WebSocket error" : error;
        Error?.Invoke(lastError);
    }

    private void OnWsClosed(int code)
    {
        lastError = "Unable to connect to the remote server";
        Error?.Invoke(lastError);
    }

    private static string ExtractType(string json)
    {
        // Cheap extraction of "type" without extra json libs.
        // Works with our own protocol payloads: {"type":"..."}
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
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\"'))
        {
            if (json[idx] == '\"')
            {
                idx++;
                break;
            }
            idx++;
        }

        int end = json.IndexOf('\"', idx);
        if (end < 0)
        {
            return string.Empty;
        }

        return json.Substring(idx, end - idx);
    }
}
