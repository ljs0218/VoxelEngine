using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using VoxelEngine.Networking.Messages;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Synchronizes player focal point positions at 20Hz (Unreliable).
    /// Host relays client positions to all other clients.
    /// </summary>
    public class PlayerSyncManager : MonoBehaviour
    {
        [SerializeField] private float sendInterval = 0.05f; // 20Hz
        [SerializeField] private GameObject remotePlayerIndicatorPrefab;

        private P2PConnectionManager connectionManager;
        private bool isHost;
        private float lastSendTime;

        // Callback to get local player's focal point
        private System.Func<Vector3> getFocalPoint;
        private System.Func<float> getCameraOrthoSize;

        // Remote player indicators
        private readonly Dictionary<ulong, RemotePlayerIndicator> remoteIndicators = new Dictionary<ulong, RemotePlayerIndicator>();

        // Colors for up to 4 players
        private static readonly UnityEngine.Color[] PlayerColors = new UnityEngine.Color[]
        {
            new UnityEngine.Color(0.2f, 0.6f, 1.0f), // Blue
            new UnityEngine.Color(1.0f, 0.4f, 0.2f), // Orange
            new UnityEngine.Color(0.3f, 1.0f, 0.3f), // Green
            new UnityEngine.Color(1.0f, 0.8f, 0.2f)  // Yellow
        };
        private int nextColorIndex;

        public void Initialize(P2PConnectionManager p2pManager, bool host,
            System.Func<Vector3> focalPointGetter, System.Func<float> orthoSizeGetter)
        {
            connectionManager = p2pManager;
            isHost = host;
            getFocalPoint = focalPointGetter;
            getCameraOrthoSize = orthoSizeGetter;

            connectionManager.OnMessageReceived += HandleNetworkMessage;

            if (isHost)
            {
                connectionManager.OnClientDisconnected += HandleClientDisconnected;
            }
            else
            {
                connectionManager.OnDisconnectedFromHost += HandleDisconnectedFromHost;
            }
        }

        private void OnDestroy()
        {
            if (connectionManager != null)
            {
                connectionManager.OnMessageReceived -= HandleNetworkMessage;
                if (isHost)
                {
                    connectionManager.OnClientDisconnected -= HandleClientDisconnected;
                }
                else
                {
                    connectionManager.OnDisconnectedFromHost -= HandleDisconnectedFromHost;
                }
            }

            // Cleanup indicators
            foreach (var kvp in remoteIndicators)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            remoteIndicators.Clear();
        }

        private void Update()
        {
            if (connectionManager == null || !connectionManager.IsConnected) return;

            // Send local position at 20Hz
            if (Time.time - lastSendTime >= sendInterval)
            {
                lastSendTime = Time.time;
                SendLocalPosition();
            }
        }

        private void SendLocalPosition()
        {
            if (getFocalPoint == null) return;

            var msg = new PlayerPositionMessage
            {
                PlayerId = SteamClient.SteamId,
                FocalPoint = getFocalPoint(),
                CameraOrthoSize = getCameraOrthoSize?.Invoke() ?? 10f
            };

            byte[] packed = NetworkMessage.Pack(NetworkMessageType.PlayerPosition, msg.Serialize());

            if (isHost)
            {
                connectionManager.SendToAll(packed, SendType.Unreliable);
            }
            else
            {
                connectionManager.SendToHost(packed, SendType.Unreliable);
            }
        }

        private void HandleNetworkMessage(ulong senderSteamId, NetworkMessageType type, byte[] payload)
        {
            if (type != NetworkMessageType.PlayerPosition) return;

            var msg = PlayerPositionMessage.Deserialize(payload);

            // Don't process our own position
            if (msg.PlayerId == SteamClient.SteamId) return;

            if (isHost)
            {
                // Host: relay to all other clients
                byte[] packed = NetworkMessage.Pack(NetworkMessageType.PlayerPosition, payload);
                connectionManager.SendToAll(packed, SendType.Unreliable);
            }

            // Update or create remote player indicator
            UpdateRemotePlayer(msg.PlayerId, msg.FocalPoint);
        }

        private void UpdateRemotePlayer(ulong playerId, Vector3 focalPoint)
        {
            if (!remoteIndicators.TryGetValue(playerId, out var indicator) || indicator == null)
            {
                indicator = CreateRemoteIndicator(playerId);
                remoteIndicators[playerId] = indicator;
            }

            indicator.UpdatePosition(focalPoint);
        }

        private RemotePlayerIndicator CreateRemoteIndicator(ulong playerId)
        {
            GameObject obj;
            if (remotePlayerIndicatorPrefab != null)
            {
                obj = Instantiate(remotePlayerIndicatorPrefab);
            }
            else
            {
                // Create simple cube marker if no prefab assigned
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.localScale = Vector3.one * 0.5f;
                // Remove collider to avoid physics interference
                var collider = obj.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            obj.name = $"RemotePlayer_{playerId}";
            var indicator = obj.AddComponent<RemotePlayerIndicator>();

            UnityEngine.Color color = PlayerColors[nextColorIndex % PlayerColors.Length];
            nextColorIndex++;
            indicator.SetColor(color);

            return indicator;
        }

        private void HandleClientDisconnected(Connection conn, ConnectionInfo info)
        {
            ulong playerId = info.Identity.SteamId;
            RemoveRemotePlayer(playerId);
        }

        private void HandleDisconnectedFromHost(ConnectionInfo info)
        {
            // Clean up all remote indicators on disconnect
            foreach (var kvp in remoteIndicators)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            remoteIndicators.Clear();
        }

        private void RemoveRemotePlayer(ulong playerId)
        {
            if (remoteIndicators.TryGetValue(playerId, out var indicator))
            {
                if (indicator != null)
                    Destroy(indicator.gameObject);
                remoteIndicators.Remove(playerId);
            }
        }
    }
}
