using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using VoxelEngine.Networking.Messages;
using VoxelEngine.World;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Central orchestrator for all networking components.
    /// Manages the lifecycle of hosting/joining games and coordinates all sub-managers.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [SerializeField] private VoxelWorld world;

        [Header("Sub-Managers")]
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private P2PConnectionManager connectionManager;
        [SerializeField] private BlockSyncManager blockSyncManager;
        [SerializeField] private ChunkStreamingManager chunkStreamingManager;
        [SerializeField] private PlayerSyncManager playerSyncManager;

        /// <summary>Whether we are currently in a multiplayer session.</summary>
        public bool IsInSession { get; private set; }

        /// <summary>Whether we are the host of the current session.</summary>
        public bool IsHost { get; private set; }

        /// <summary>Fired when the session ends (host disconnect, etc.).</summary>
        public event Action<string> OnSessionEnded;

        private void Awake()
        {
            // Subscribe to connection events for session management
            if (connectionManager != null)
            {
                connectionManager.OnMessageReceived += HandleSessionMessage;
                connectionManager.OnDisconnectedFromHost += HandleDisconnectedFromHost;
                connectionManager.OnClientDisconnected += HandleClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (connectionManager != null)
            {
                connectionManager.OnMessageReceived -= HandleSessionMessage;
                connectionManager.OnDisconnectedFromHost -= HandleDisconnectedFromHost;
                connectionManager.OnClientDisconnected -= HandleClientDisconnected;
            }
        }

        /// <summary>
        /// Starts hosting a game. Creates a lobby and opens a relay socket.
        /// </summary>
        public async void HostGame()
        {
            if (!SteamManager.Instance.Initialized)
            {
                Debug.LogError("[NetworkManager] Cannot host: Steam not initialized.");
                return;
            }

            IsHost = true;
            IsInSession = true;

            // Create lobby
            bool lobbyCreated = await lobbyManager.CreateLobby(4);
            if (!lobbyCreated)
            {
                Debug.LogError("[NetworkManager] Failed to create lobby.");
                IsHost = false;
                IsInSession = false;
                return;
            }

            // Create relay socket
            connectionManager.CreateHostSocket();

            // Host keeps local chunk streaming active
            world.SetMultiplayerClient(false);

            // Initialize sub-managers
            blockSyncManager.Initialize(world, connectionManager, host: true);
            chunkStreamingManager.Initialize(world, connectionManager, host: true);

            // Player sync with focal point getter
            var camera = FindObjectOfType<VoxelEngine.Player.IsometricCamera>();
            playerSyncManager.Initialize(
                connectionManager,
                host: true,
                focalPointGetter: () => camera != null ? camera.FocalPoint : Vector3.zero,
                orthoSizeGetter: () => camera != null ? camera.GetComponent<Camera>().orthographicSize : 10f
            );

            Debug.Log("[NetworkManager] Hosting game. Waiting for players...");
        }

        /// <summary>
        /// Joins an existing game via lobby.
        /// </summary>
        public async void JoinGame(Lobby lobby)
        {
            if (!SteamManager.Instance.Initialized)
            {
                Debug.LogError("[NetworkManager] Cannot join: Steam not initialized.");
                return;
            }

            IsHost = false;
            IsInSession = true;

            // Join lobby
            bool joined = await lobbyManager.JoinLobby(lobby);
            if (!joined)
            {
                Debug.LogError("[NetworkManager] Failed to join lobby.");
                IsInSession = false;
                return;
            }

            // Connect to host
            connectionManager.ConnectToHost(lobby.Owner.Id);

            // Client: disable local chunk streaming
            world.SetMultiplayerClient(true);

            // Initialize sub-managers
            blockSyncManager.Initialize(world, connectionManager, host: false);
            chunkStreamingManager.Initialize(world, connectionManager, host: false);

            var camera = FindObjectOfType<VoxelEngine.Player.IsometricCamera>();
            playerSyncManager.Initialize(
                connectionManager,
                host: false,
                focalPointGetter: () => camera != null ? camera.FocalPoint : Vector3.zero,
                orthoSizeGetter: () => camera != null ? camera.GetComponent<Camera>().orthographicSize : 10f
            );

            Debug.Log($"[NetworkManager] Joining game. Host: {lobby.Owner.Name}");
        }

        /// <summary>
        /// Leaves the current session gracefully.
        /// </summary>
        public void LeaveGame()
        {
            if (!IsInSession) return;

            if (IsHost)
            {
                // Notify all clients that session is ending
                var endMsg = new SessionEndMessage { Reason = 0 };
                byte[] packed = NetworkMessage.Pack(NetworkMessageType.SessionEnd, endMsg.Serialize());
                connectionManager.SendToAll(packed, SendType.Reliable);
            }

            // Cleanup
            connectionManager.Shutdown();
            lobbyManager.LeaveLobby();
            SyncRegistry.Clear();

            world.SetMultiplayerClient(false);
            IsHost = false;
            IsInSession = false;

            Debug.Log("[NetworkManager] Left game.");
            OnSessionEnded?.Invoke("Left game.");
        }

        /// <summary>
        /// Handles session-level network messages (SessionEnd, PlayerLeft, etc.).
        /// </summary>
        private void HandleSessionMessage(ulong senderSteamId, NetworkMessageType type, byte[] payload)
        {
            switch (type)
            {
                case NetworkMessageType.SessionEnd:
                    var endMsg = SessionEndMessage.Deserialize(payload);
                    Debug.Log($"[NetworkManager] Session ended. Reason: {endMsg.Reason}");
                    HandleSessionEnd("Host ended the session.");
                    break;

                case NetworkMessageType.PlayerLeft:
                    Debug.Log($"[NetworkManager] Player left: {senderSteamId}");
                    break;
            }
        }

        /// <summary>
        /// Client: disconnected from host unexpectedly.
        /// </summary>
        private void HandleDisconnectedFromHost(ConnectionInfo info)
        {
            if (!IsInSession) return;
            HandleSessionEnd("Lost connection to host.");
        }

        /// <summary>
        /// Host: a client disconnected.
        /// </summary>
        private void HandleClientDisconnected(Connection conn, ConnectionInfo info)
        {
            Debug.Log($"[NetworkManager] Client disconnected: {info.Identity.SteamId}");
            // Could broadcast PlayerLeft message to remaining clients if needed
        }

        private void HandleSessionEnd(string reason)
        {
            connectionManager.Shutdown();
            lobbyManager.LeaveLobby();
            SyncRegistry.Clear();

            world.SetMultiplayerClient(false);
            IsHost = false;
            IsInSession = false;

            OnSessionEnded?.Invoke(reason);
        }
    }
}
