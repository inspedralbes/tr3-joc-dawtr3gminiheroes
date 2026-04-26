using System;
using System.Collections.Generic;
using UnityEngine;

public class MiniHeroesWebSocketBridge : MonoBehaviour
{
    private static MiniHeroesWebSocketBridge instance;
    private readonly Dictionary<int, Action> openHandlers = new Dictionary<int, Action>();
    private readonly Dictionary<int, Action<string>> messageHandlers = new Dictionary<int, Action<string>>();
    private readonly Dictionary<int, Action<string>> errorHandlers = new Dictionary<int, Action<string>>();
    private readonly Dictionary<int, Action<int>> closeHandlers = new Dictionary<int, Action<int>>();

    public static MiniHeroesWebSocketBridge Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject go = GameObject.Find("MiniHeroesWebSocketBridge");
        if (go == null)
        {
            go = new GameObject("MiniHeroesWebSocketBridge");
        }

        instance = go.GetComponent<MiniHeroesWebSocketBridge>();
        if (instance == null)
        {
            instance = go.AddComponent<MiniHeroesWebSocketBridge>();
        }

        DontDestroyOnLoad(go);
        return instance;
    }

    public void Register(
        int socketId,
        Action onOpen,
        Action<string> onMessage,
        Action<string> onError,
        Action<int> onClose)
    {
        openHandlers[socketId] = onOpen;
        messageHandlers[socketId] = onMessage;
        errorHandlers[socketId] = onError;
        closeHandlers[socketId] = onClose;
    }

    public void Unregister(int socketId)
    {
        openHandlers.Remove(socketId);
        messageHandlers.Remove(socketId);
        errorHandlers.Remove(socketId);
        closeHandlers.Remove(socketId);
    }

    // Called from WebGL .jslib via SendMessage
    public void OnWsOpen(string socketIdStr)
    {
        if (!int.TryParse(socketIdStr, out int socketId))
        {
            return;
        }

        if (openHandlers.TryGetValue(socketId, out Action handler))
        {
            handler?.Invoke();
        }
    }

    // payload: "{id}|{message}"
    public void OnWsMessage(string payload)
    {
        if (!TrySplitPayload(payload, out int socketId, out string message))
        {
            return;
        }

        if (messageHandlers.TryGetValue(socketId, out Action<string> handler))
        {
            handler?.Invoke(message);
        }
    }

    // payload: "{id}|{error}"
    public void OnWsError(string payload)
    {
        if (!TrySplitPayload(payload, out int socketId, out string error))
        {
            return;
        }

        if (errorHandlers.TryGetValue(socketId, out Action<string> handler))
        {
            handler?.Invoke(error);
        }
    }

    // payload: "{id}|{code}"
    public void OnWsClose(string payload)
    {
        if (!TrySplitPayload(payload, out int socketId, out string codeStr))
        {
            return;
        }

        int.TryParse(codeStr, out int code);
        if (closeHandlers.TryGetValue(socketId, out Action<int> handler))
        {
            handler?.Invoke(code);
        }
    }

    private static bool TrySplitPayload(string payload, out int socketId, out string message)
    {
        socketId = 0;
        message = string.Empty;
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        int idx = payload.IndexOf('|');
        if (idx <= 0)
        {
            return false;
        }

        if (!int.TryParse(payload.Substring(0, idx), out socketId))
        {
            return false;
        }

        message = payload.Substring(idx + 1);
        return true;
    }
}

