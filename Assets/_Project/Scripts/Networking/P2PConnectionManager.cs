using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using VoxelEngine.Networking.Messages;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Manages P2P connections via SteamNetworkingSockets relay.
    /// Host creates a relay socket; clients connect to it.
    /// Single connection per peer, message type distinguished by envelope header byte.
    /// </summary>
    public class P2PConnectionManager : MonoBehaviour
    {
        private VoxelSocketManager socketManager;
        private VoxelConnectionManager connectionManager;

        /// <summary>Whether this instance is the host.</summary>
        public bool IsHost { get; private set; }

        /// <summary>Whether we have an active connection (host: socket open, client: connected).</summary>
        public bool IsConnected { get; private set; }

        /// <summary>Active connections on the host side (excludes self-connection).</summary>
        public IReadOnlyList<Connection> ClientConnections => socketManager?.ConnectedClients ?? (IReadOnlyList<Connection>)Array.Empty<Connection>();

        /// <summary>Fired when a message is received. Parameters: (senderSteamId, messageType, payload).</summary>
        public event Action<ulong, NetworkMessageType, byte[]> OnMessageReceived;

        /// <summary>Fired when a new client connects (host only).</summary>
        public event Action<Connection, ConnectionInfo> OnClientConnected;

        /// <summary>Fired when a client disconnects (host only).</summary>
        public event Action<Connection, ConnectionInfo> OnClientDisconnected;

        /// <summary>Fired when we connect to the host (client only).</summary>
        public event Action<ConnectionInfo> OnConnectedToHost;

        /// <summary>Fired when we disconnect from the host (client only).</summary>
        public event Action<ConnectionInfo> OnDisconnectedFromHost;

        /// <summary>
        /// Creates a relay socket as the host.
        /// Also connects to self for uniform message handling.
        /// </summary>
        public void CreateHostSocket()
        {
            IsHost = true;
            socketManager = SteamNetworkingSockets.CreateRelaySocket<VoxelSocketManager>(0);
            socketManager.Setup(this);

            // Host connects to itself for uniform message routing
            connectionManager = SteamNetworkingSockets.ConnectRelay<VoxelConnectionManager>(SteamClient.SteamId, 0);
            connectionManager.Setup(this);

            IsConnected = true;
            Debug.Log("[P2PConnectionManager] Host socket created.");
        }

        /// <summary>
        /// Connects to a host as a client.
        /// </summary>
        public void ConnectToHost(SteamId hostId)
        {
            IsHost = false;
            connectionManager = SteamNetworkingSockets.ConnectRelay<VoxelConnectionManager>(hostId, 0);
            connectionManager.Setup(this);

            Debug.Log($"[P2PConnectionManager] Connecting to host: {hostId}");
        }

        private void Update()
        {
            socketManager?.Receive();
            connectionManager?.Receive();
        }

        /// <summary>
        /// Sends a packed message to all connected clients (host only).
        /// </summary>
        public void SendToAll(byte[] data, SendType sendType = SendType.Reliable)
        {
            if (!IsHost || socketManager == null) return;

            foreach (var conn in socketManager.ConnectedClients)
            {
                conn.SendMessage(data, sendType);
            }
        }

        /// <summary>
        /// Sends a packed message to the host (client only).
        /// </summary>
        public void SendToHost(byte[] data, SendType sendType = SendType.Reliable)
        {
            connectionManager?.Connection.SendMessage(data, sendType);
        }

        /// <summary>
        /// Sends a packed message to a specific connection.
        /// </summary>
        public void SendTo(Connection conn, byte[] data, SendType sendType = SendType.Reliable)
        {
            conn.SendMessage(data, sendType);
        }

        /// <summary>
        /// Sends to all clients except the specified one.
        /// </summary>
        public void SendToAllExcept(Connection except, byte[] data, SendType sendType = SendType.Reliable)
        {
            if (!IsHost || socketManager == null) return;

            foreach (var conn in socketManager.ConnectedClients)
            {
                if (conn.Id != except.Id)
                {
                    conn.SendMessage(data, sendType);
                }
            }
        }

        /// <summary>
        /// Closes all connections and shuts down.
        /// </summary>
        public void Shutdown()
        {
            connectionManager?.Close();
            connectionManager = null;

            socketManager?.Close();
            socketManager = null;

            IsHost = false;
            IsConnected = false;
            Debug.Log("[P2PConnectionManager] Shut down.");
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        // ==================== Internal Callbacks ====================

        internal void HandleMessage(ulong senderSteamId, byte[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return;

            var (type, payload) = NetworkMessage.Unpack(rawData);
            OnMessageReceived?.Invoke(senderSteamId, type, payload);
        }

        internal void HandleClientConnected(Connection conn, ConnectionInfo info)
        {
            IsConnected = true;
            OnClientConnected?.Invoke(conn, info);
        }

        internal void HandleClientDisconnected(Connection conn, ConnectionInfo info)
        {
            OnClientDisconnected?.Invoke(conn, info);
        }

        internal void HandleConnectedToHost(ConnectionInfo info)
        {
            IsConnected = true;
            OnConnectedToHost?.Invoke(info);
        }

        internal void HandleDisconnectedFromHost(ConnectionInfo info)
        {
            IsConnected = false;
            OnDisconnectedFromHost?.Invoke(info);
        }
    }

    /// <summary>
    /// Server-side socket manager. Handles incoming connections and messages on the host.
    /// </summary>
    public class VoxelSocketManager : SocketManager
    {
        private P2PConnectionManager manager;
        private List<Connection> connectedClients = new List<Connection>();
        public IReadOnlyList<Connection> ConnectedClients => connectedClients;

        private const int MaxPlayers = 4;

        public void Setup(P2PConnectionManager mgr)
        {
            manager = mgr;
        }

        public override void OnConnecting(Connection connection, ConnectionInfo info)
        {
            if (connectedClients.Count >= MaxPlayers)
            {
                connection.Close();
                Debug.Log($"[VoxelSocketManager] Rejected connection: max players ({MaxPlayers}) reached.");
                return;
            }

            connection.Accept();
            Debug.Log($"[VoxelSocketManager] Connection accepted from {info.Identity.SteamId}");
        }

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            connectedClients.Add(connection);
            Debug.Log($"[VoxelSocketManager] Client connected: {info.Identity.SteamId} (total: {connectedClients.Count})");
            manager?.HandleClientConnected(connection, info);
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            connectedClients.Remove(connection);
            Debug.Log($"[VoxelSocketManager] Client disconnected: {info.Identity.SteamId} (total: {connectedClients.Count})");
            manager?.HandleClientDisconnected(connection, info);
        }

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            byte[] rawData = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(data, rawData, 0, size);
            manager?.HandleMessage(identity.SteamId, rawData);
        }
    }

    /// <summary>
    /// Client-side connection manager. Handles connection state and messages from host.
    /// </summary>
    public class VoxelConnectionManager : ConnectionManager
    {
        private P2PConnectionManager manager;

        public void Setup(P2PConnectionManager mgr)
        {
            manager = mgr;
        }

        public override void OnConnected(ConnectionInfo info)
        {
            Debug.Log($"[VoxelConnectionManager] Connected to host: {info.Identity.SteamId}");
            manager?.HandleConnectedToHost(info);
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            Debug.Log($"[VoxelConnectionManager] Disconnected from host: {info.Identity.SteamId}");
            manager?.HandleDisconnectedFromHost(info);
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            byte[] rawData = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(data, rawData, 0, size);
            // For client messages, sender is always the host
            manager?.HandleMessage(0, rawData);
        }
    }
}
