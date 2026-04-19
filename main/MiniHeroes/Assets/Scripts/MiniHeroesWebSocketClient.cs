using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

public sealed class MiniHeroesWebSocketClient
{
    public bool IsOpen { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public event Action Opened;
    public event Action<string> MessageReceived;
    public event Action<string> Error;
    public event Action<int> Closed;

    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int MHWsCreate(string url, string gameObjectName);

    [DllImport("__Internal")]
    private static extern void MHWsConnect(int id);

    [DllImport("__Internal")]
    private static extern void MHWsSend(int id, string message);

    [DllImport("__Internal")]
    private static extern void MHWsClose(int id);

    private int socketId;
    private MiniHeroesWebSocketBridge bridge;
    private string url;
#else
    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    private Task receiveTask;
    private string url;
#endif

    public void Connect(string url)
    {
        this.url = url;
        LastError = string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
        bridge = MiniHeroesWebSocketBridge.Ensure();
        socketId = MHWsCreate(url, "MiniHeroesWebSocketBridge");
        bridge.Register(socketId, HandleOpen, HandleMessage, HandleError, HandleClose);
        MHWsConnect(socketId);
#else
        if (receiveTask != null)
        {
            return;
        }

        socket = new ClientWebSocket();
        cts = new CancellationTokenSource();
        receiveTask = RunSocketAsync(cts.Token);
#endif
    }

    public void Pump()
    {
        while (mainThreadQueue.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    public void Send(string text)
    {
        if (!IsOpen)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        MHWsSend(socketId, text);
#else
        _ = SendAsync(text);
#endif
    }

    public void Close()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (socketId != 0)
        {
            try { MHWsClose(socketId); } catch { /* ignore */ }
            bridge?.Unregister(socketId);
            socketId = 0;
        }

        IsOpen = false;
#else
        try { cts?.Cancel(); } catch { /* ignore */ }
        _ = CloseAsync();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void HandleOpen()
    {
        mainThreadQueue.Enqueue(() =>
        {
            IsOpen = true;
            Opened?.Invoke();
        });
    }

    private void HandleMessage(string message)
    {
        mainThreadQueue.Enqueue(() =>
        {
            MessageReceived?.Invoke(message);
        });
    }

    private void HandleError(string error)
    {
        mainThreadQueue.Enqueue(() =>
        {
            LastError = error ?? "WebSocket error";
            Error?.Invoke(LastError);
        });
    }

    private void HandleClose(int code)
    {
        mainThreadQueue.Enqueue(() =>
        {
            IsOpen = false;
            Closed?.Invoke(code);
        });
    }
#else
    private async Task RunSocketAsync(CancellationToken token)
    {
        try
        {
            await socket.ConnectAsync(new Uri(url), token);
            mainThreadQueue.Enqueue(() =>
            {
                IsOpen = true;
                Opened?.Invoke();
            });

            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                int count = result.Count;
                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Array.Length)
                    {
                        break;
                    }

                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer.Array, count, buffer.Array.Length - count), token);
                    count += result.Count;
                }

                string text = Encoding.UTF8.GetString(buffer.Array, 0, count);
                mainThreadQueue.Enqueue(() => MessageReceived?.Invoke(text));
            }
        }
        catch (Exception ex)
        {
            mainThreadQueue.Enqueue(() =>
            {
                LastError = ex.Message;
                Error?.Invoke(LastError);
            });
        }
        finally
        {
            mainThreadQueue.Enqueue(() =>
            {
                IsOpen = false;
                Closed?.Invoke(0);
            });
        }
    }

    private async Task SendAsync(string text)
    {
        try
        {
            if (socket == null || socket.State != WebSocketState.Open)
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            // Ignore.
        }
    }

    private async Task CloseAsync()
    {
        try
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None);
            }
        }
        catch
        {
            // Ignore.
        }
        finally
        {
            try { socket?.Dispose(); } catch { /* ignore */ }
            socket = null;
            cts = null;
            receiveTask = null;
            IsOpen = false;
        }
    }
#endif
}
