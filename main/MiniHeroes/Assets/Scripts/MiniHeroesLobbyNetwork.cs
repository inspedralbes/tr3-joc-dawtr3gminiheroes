using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class MiniHeroesLobbyNetwork : MonoBehaviour
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

    private const int MaxSlots = 4;

    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private readonly List<LobbyPlayerInfo> players = new List<LobbyPlayerInfo>();
    private readonly object playersLock = new object();

    private LobbyServer server;
    private LobbyClient client;

    public bool IsServer => server != null;
    public bool IsClient => client != null;
    public string LastError { get; private set; } = string.Empty;
    public string HostAddressHint { get; private set; } = string.Empty;

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private void OnDestroy()
    {
        StopAll();
    }

    public void StopAll()
    {
        server?.Stop();
        server = null;
        client?.Stop();
        client = null;

        lock (playersLock)
        {
            players.Clear();
        }

        LastError = string.Empty;
        HostAddressHint = string.Empty;
    }

    public LobbyPlayerInfo GetPlayerInSlot(int slot)
    {
        lock (playersLock)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].slot == slot)
                {
                    return players[i];
                }
            }
        }

        return null;
    }

    public bool StartHost(string username, int level, int experience, int gruntsKilled, int port)
    {
        StopAll();
        LastError = string.Empty;

        try
        {
            server = new LobbyServer(MaxSlots, EnqueueLobbyStateFromServer);
            server.Start(port);
            HostAddressHint = GuessLocalIpv4();

            client = new LobbyClient(EnqueueOnMainThread, ApplyLobbyStateFromClient, SetClientError);
            client.Start("127.0.0.1", port, username, level, experience, gruntsKilled);

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StopAll();
            return false;
        }
    }

    public bool StartClient(string address, string username, int level, int experience, int gruntsKilled, int port)
    {
        StopAll();
        LastError = string.Empty;

        try
        {
            client = new LobbyClient(EnqueueOnMainThread, ApplyLobbyStateFromClient, SetClientError);
            client.Start(address, port, username, level, experience, gruntsKilled);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StopAll();
            return false;
        }
    }

    private void EnqueueOnMainThread(Action action)
    {
        if (action == null)
        {
            return;
        }

        mainThreadActions.Enqueue(action);
    }

    private void EnqueueLobbyStateFromServer(LobbyPlayerInfo[] newPlayers)
    {
        mainThreadActions.Enqueue(() =>
        {
            lock (playersLock)
            {
                players.Clear();
                if (newPlayers != null)
                {
                    players.AddRange(newPlayers);
                }
            }
        });
    }

    private void ApplyLobbyStateFromClient(LobbyPlayerInfo[] newPlayers)
    {
        lock (playersLock)
        {
            players.Clear();
            if (newPlayers != null)
            {
                players.AddRange(newPlayers);
            }
        }
    }

    private void SetClientError(string error)
    {
        LastError = error ?? string.Empty;
    }

    private static string GuessLocalIpv4()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return addresses[i].ToString();
                }
            }
        }
        catch
        {
            // Ignore.
        }

        return string.Empty;
    }

    private sealed class LobbyServer
    {
        private sealed class ClientConnection
        {
            public TcpClient client;
            public StreamReader reader;
            public StreamWriter writer;
            public Thread thread;
            public LobbyPlayerInfo player;
        }

        private readonly int maxSlots;
        private readonly Action<LobbyPlayerInfo[]> publishLobbyState;

        private readonly object sync = new object();
        private TcpListener listener;
        private Thread acceptThread;
        private volatile bool running;
        private readonly List<ClientConnection> connections = new List<ClientConnection>();

        public LobbyServer(int maxSlots, Action<LobbyPlayerInfo[]> publishLobbyState)
        {
            this.maxSlots = maxSlots;
            this.publishLobbyState = publishLobbyState;
        }

        public void Start(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;

            acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "MiniHeroesLobbyServer.Accept"
            };
            acceptThread.Start();
        }

        public void Stop()
        {
            running = false;
            try { listener?.Stop(); } catch { /* Ignore */ }
            listener = null;

            lock (sync)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    TryClose(connections[i]);
                }
                connections.Clear();
            }
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient tcpClient = null;
                try
                {
                    tcpClient = listener.AcceptTcpClient();
                    tcpClient.NoDelay = true;
                }
                catch
                {
                    if (!running)
                    {
                        break;
                    }
                }

                if (tcpClient == null)
                {
                    continue;
                }

                ClientConnection conn = new ClientConnection
                {
                    client = tcpClient,
                    reader = new StreamReader(tcpClient.GetStream()),
                    writer = new StreamWriter(tcpClient.GetStream()) { AutoFlush = true }
                };

                conn.thread = new Thread(() => ClientLoop(conn))
                {
                    IsBackground = true,
                    Name = "MiniHeroesLobbyServer.Client"
                };

                lock (sync)
                {
                    connections.Add(conn);
                }

                conn.thread.Start();
            }
        }

        private void ClientLoop(ClientConnection conn)
        {
            try
            {
                while (running && conn.client != null && conn.client.Connected)
                {
                    string line = conn.reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    if (TryParseJoin(line, out string username, out int level, out int experience, out int gruntsKilled))
                    {
                        HandleJoin(conn, username, level, experience, gruntsKilled);
                    }
                }
            }
            catch
            {
                // Ignore client loop exceptions, treat as disconnect.
            }
            finally
            {
                HandleDisconnect(conn);
            }
        }

        private void HandleJoin(ClientConnection conn, string username, int level, int experience, int gruntsKilled)
        {
            int assignedSlot;
            LobbyPlayerInfo[] snapshot;

            lock (sync)
            {
                assignedSlot = FindNextFreeSlotLocked();
                if (assignedSlot < 1)
                {
                    SafeWrite(conn, "ERROR|Lobby lleno (max " + maxSlots + ").");
                    TryClose(conn);
                    return;
                }

                conn.player = new LobbyPlayerInfo
                {
                    slot = assignedSlot,
                    username = string.IsNullOrEmpty(username) ? "Player" : username,
                    level = level < 1 ? 1 : level,
                    experience = experience < 0 ? 0 : experience,
                    grunts_killed = gruntsKilled < 0 ? 0 : gruntsKilled
                };

                snapshot = BuildPlayersSnapshotLocked();
            }

            BroadcastLobbyState(snapshot);
        }

        private void HandleDisconnect(ClientConnection conn)
        {
            LobbyPlayerInfo[] snapshot = null;
            bool removed = false;

            lock (sync)
            {
                removed = connections.Remove(conn);
                if (removed)
                {
                    TryClose(conn);
                    snapshot = BuildPlayersSnapshotLocked();
                }
            }

            if (removed)
            {
                BroadcastLobbyState(snapshot);
            }
        }

        private void BroadcastLobbyState(LobbyPlayerInfo[] snapshot)
        {
            if (snapshot == null)
            {
                snapshot = Array.Empty<LobbyPlayerInfo>();
            }
            string payload = BuildStateLine(snapshot);

            lock (sync)
            {
                for (int i = connections.Count - 1; i >= 0; i--)
                {
                    if (!SafeWrite(connections[i], payload))
                    {
                        TryClose(connections[i]);
                        connections.RemoveAt(i);
                    }
                }
            }

            publishLobbyState?.Invoke(snapshot);
        }

        private int FindNextFreeSlotLocked()
        {
            bool[] occupied = new bool[maxSlots + 1];
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].player != null && connections[i].player.slot >= 1 && connections[i].player.slot <= maxSlots)
                {
                    occupied[connections[i].player.slot] = true;
                }
            }

            for (int slot = 1; slot <= maxSlots; slot++)
            {
                if (!occupied[slot])
                {
                    return slot;
                }
            }

            return -1;
        }

        private LobbyPlayerInfo[] BuildPlayersSnapshotLocked()
        {
            List<LobbyPlayerInfo> snapshot = new List<LobbyPlayerInfo>();
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].player != null)
                {
                    snapshot.Add(connections[i].player);
                }
            }

            snapshot.Sort((a, b) => a.slot.CompareTo(b.slot));
            return snapshot.ToArray();
        }

        private static bool TryParseJoin(string line, out string username, out int level, out int experience, out int gruntsKilled)
        {
            username = string.Empty;
            level = 1;
            experience = 0;
            gruntsKilled = 0;

            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            // JOIN|username|level|experience|gruntsKilled
            string[] parts = line.Split('|');
            if (parts.Length < 5)
            {
                return false;
            }

            if (!string.Equals(parts[0], "JOIN", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            username = parts[1];
            int.TryParse(parts[2], out level);
            int.TryParse(parts[3], out experience);
            int.TryParse(parts[4], out gruntsKilled);
            return true;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("|", "\\|").Replace(";", "\\;").Replace(",", "\\,");
        }

        private static string BuildStateLine(LobbyPlayerInfo[] snapshot)
        {
            // STATE|slot,username,level,experience,gruntsKilled;slot,username,level,experience,gruntsKilled
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("STATE|");
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(';');
                }

                LobbyPlayerInfo p = snapshot[i];
                builder.Append(p.slot);
                builder.Append(',');
                builder.Append(Escape(p.username));
                builder.Append(',');
                builder.Append(p.level);
                builder.Append(',');
                builder.Append(p.experience);
                builder.Append(',');
                builder.Append(p.grunts_killed);
            }
            return builder.ToString();
        }

        private static bool SafeWrite(ClientConnection conn, string line)
        {
            try
            {
                conn.writer.WriteLine(line);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryClose(ClientConnection conn)
        {
            try { conn.reader?.Dispose(); } catch { /* Ignore */ }
            try { conn.writer?.Dispose(); } catch { /* Ignore */ }
            try { conn.client?.Close(); } catch { /* Ignore */ }
            conn.reader = null;
            conn.writer = null;
            conn.client = null;
        }
    }

    private sealed class LobbyClient
    {
        private readonly Action<Action> runOnMainThread;
        private readonly Action<LobbyPlayerInfo[]> applyLobbyState;
        private readonly Action<string> onError;

        private TcpClient tcpClient;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread thread;
        private volatile bool running;

        public LobbyClient(Action<Action> runOnMainThread, Action<LobbyPlayerInfo[]> applyLobbyState, Action<string> onError)
        {
            this.runOnMainThread = runOnMainThread;
            this.applyLobbyState = applyLobbyState;
            this.onError = onError;
        }

        public void Start(string address, int port, string username, int level, int experience, int gruntsKilled)
        {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            tcpClient.Connect(address, port);

            reader = new StreamReader(tcpClient.GetStream());
            writer = new StreamWriter(tcpClient.GetStream()) { AutoFlush = true };

            string safeName = string.IsNullOrEmpty(username) ? "Player" : username;
            if (level < 1) level = 1;
            if (experience < 0) experience = 0;
            if (gruntsKilled < 0) gruntsKilled = 0;
            writer.WriteLine("JOIN|" + safeName + "|" + level + "|" + experience + "|" + gruntsKilled);

            running = true;
            thread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "MiniHeroesLobbyClient.Read"
            };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            try { tcpClient?.Close(); } catch { /* Ignore */ }
            tcpClient = null;
            try { reader?.Dispose(); } catch { /* Ignore */ }
            reader = null;
            try { writer?.Dispose(); } catch { /* Ignore */ }
            writer = null;
        }

        private void ReadLoop()
        {
            try
            {
                while (running && tcpClient != null && tcpClient.Connected)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    if (TryParseState(line, out LobbyPlayerInfo[] snapshot))
                    {
                        runOnMainThread(() => applyLobbyState(snapshot));
                    }
                    else if (TryParseError(line, out string error))
                    {
                        runOnMainThread(() => onError(error));
                    }
                }
            }
            catch (Exception ex)
            {
                runOnMainThread(() => onError(ex.Message));
            }
            finally
            {
                runOnMainThread(() => onError("Desconectado del lobby."));
                Stop();
            }
        }

        private static bool TryParseError(string line, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            if (!line.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            error = line.Length > 6 ? line.Substring(6) : "Error de lobby.";
            return true;
        }

        private static bool TryParseState(string line, out LobbyPlayerInfo[] snapshot)
        {
            snapshot = Array.Empty<LobbyPlayerInfo>();
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            if (!line.StartsWith("STATE|", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string payload = line.Length > 6 ? line.Substring(6) : string.Empty;
            if (string.IsNullOrEmpty(payload))
            {
                snapshot = Array.Empty<LobbyPlayerInfo>();
                return true;
            }

            string[] entries = payload.Split(';');
            List<LobbyPlayerInfo> list = new List<LobbyPlayerInfo>();
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                string[] parts = entry.Split(',');
                if (parts.Length < 5)
                {
                    continue;
                }

                LobbyPlayerInfo p = new LobbyPlayerInfo();
                int.TryParse(parts[0], out p.slot);
                p.username = Unescape(parts[1]);
                int.TryParse(parts[2], out p.level);
                int.TryParse(parts[3], out p.experience);
                int.TryParse(parts[4], out p.grunts_killed);
                list.Add(p);
            }

            list.Sort((a, b) => a.slot.CompareTo(b.slot));
            snapshot = list.ToArray();
            return true;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Reverse escaping used by the server.
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            bool escaping = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (escaping)
                {
                    builder.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
