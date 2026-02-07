using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks.Data;
using UnityEngine;
using VoxelEngine.Networking.Messages;
using VoxelEngine.Serialization;
using VoxelEngine.World;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Manages chunk streaming for late-joining players.
    /// Host sends chunks in distance-priority order; client receives and loads them.
    /// </summary>
    public class ChunkStreamingManager : MonoBehaviour
    {
        [SerializeField] private int chunksPerFrame = 10;

        private VoxelWorld world;
        private P2PConnectionManager connectionManager;
        private bool isHost;

        // Host: per-player sync tracking
        private readonly Dictionary<ulong, HashSet<ChunkCoord>> syncedChunks = new Dictionary<ulong, HashSet<ChunkCoord>>();

        // Client: sync progress
        private int totalChunksExpected;
        private int chunksReceived;
        public bool IsSyncComplete { get; private set; }
        public float SyncProgress => totalChunksExpected > 0 ? (float)chunksReceived / totalChunksExpected : 0f;

        public void Initialize(VoxelWorld voxelWorld, P2PConnectionManager p2pManager, bool host)
        {
            world = voxelWorld;
            connectionManager = p2pManager;
            isHost = host;

            connectionManager.OnMessageReceived += HandleNetworkMessage;

            if (isHost)
            {
                connectionManager.OnClientConnected += HandleNewClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (connectionManager != null)
            {
                connectionManager.OnMessageReceived -= HandleNetworkMessage;
                if (isHost)
                {
                    connectionManager.OnClientConnected -= HandleNewClientConnected;
                }
            }
        }

        /// <summary>
        /// Host: starts streaming chunks to a newly connected client.
        /// </summary>
        private void HandleNewClientConnected(Connection conn, ConnectionInfo info)
        {
            if (!isHost) return;

            ulong clientId = info.Identity.SteamId;
            syncedChunks[clientId] = new HashSet<ChunkCoord>();

            StartCoroutine(StreamChunksToClient(conn, clientId));
        }

        /// <summary>
        /// Coroutine that streams all loaded chunks to a client in distance-priority order.
        /// </summary>
        private IEnumerator StreamChunksToClient(Connection conn, ulong clientId)
        {
            // Send world metadata first
            var allChunkData = world.GetAllLoadedChunkData();
            var metaMsg = new WorldMetadataMessage
            {
                WorldName = world.Metadata?.worldName ?? "Unknown",
                Seed = world.Metadata?.seed ?? 0,
                TotalChunks = allChunkData.Count
            };
            byte[] metaPacked = NetworkMessage.Pack(NetworkMessageType.WorldMetadata, metaMsg.Serialize());
            connectionManager.SendTo(conn, metaPacked, SendType.Reliable);

            // Sort chunks by distance from origin (default spawn point)
            // TODO: Use actual player spawn position when available
            Vector3 spawnPos = Vector3.zero;
            var sortedChunks = allChunkData
                .OrderBy(kvp =>
                {
                    Vector3 chunkCenter = kvp.Key.GetWorldPosition();
                    return (chunkCenter - spawnPos).sqrMagnitude;
                })
                .ToList();

            int sentThisFrame = 0;
            foreach (var kvp in sortedChunks)
            {
                var chunkMsg = new ChunkDataMessage
                {
                    Coord = kvp.Key,
                    RleData = kvp.Value
                };
                byte[] chunkPacked = NetworkMessage.Pack(NetworkMessageType.ChunkData, chunkMsg.Serialize());
                connectionManager.SendTo(conn, chunkPacked, SendType.Reliable);

                if (syncedChunks.TryGetValue(clientId, out var synced))
                {
                    synced.Add(kvp.Key);
                }

                sentThisFrame++;
                if (sentThisFrame >= chunksPerFrame)
                {
                    sentThisFrame = 0;
                    yield return null; // Yield to next frame for bandwidth control
                }
            }

            // Send sync complete
            var syncMsg = new SyncCompleteMessage();
            byte[] syncPacked = NetworkMessage.Pack(NetworkMessageType.SyncComplete, syncMsg.Serialize());
            connectionManager.SendTo(conn, syncPacked, SendType.Reliable);

            Debug.Log($"[ChunkStreamingManager] Finished streaming {sortedChunks.Count} chunks to client {clientId}.");
        }

        /// <summary>
        /// Handles incoming network messages related to chunk streaming.
        /// </summary>
        private void HandleNetworkMessage(ulong senderSteamId, NetworkMessageType type, byte[] payload)
        {
            switch (type)
            {
                case NetworkMessageType.WorldMetadata:
                    HandleWorldMetadata(payload);
                    break;
                case NetworkMessageType.ChunkData:
                    HandleChunkData(payload);
                    break;
                case NetworkMessageType.SyncComplete:
                    HandleSyncComplete();
                    break;
            }
        }

        /// <summary>
        /// Client: receives world metadata from host.
        /// </summary>
        private void HandleWorldMetadata(byte[] payload)
        {
            if (isHost) return;

            var msg = WorldMetadataMessage.Deserialize(payload);
            totalChunksExpected = msg.TotalChunks;
            chunksReceived = 0;
            IsSyncComplete = false;
            Debug.Log($"[ChunkStreamingManager] World metadata received. Expecting {totalChunksExpected} chunks. World: '{msg.WorldName}'");
        }

        /// <summary>
        /// Client: receives and loads a chunk from the host.
        /// </summary>
        private void HandleChunkData(byte[] payload)
        {
            if (isHost) return;

            var msg = ChunkDataMessage.Deserialize(payload);
            if (msg.RleData == null) return;

            // Deserialize RLE data to raw blocks
            byte[] rawBlocks = ChunkSerializer.Deserialize(msg.RleData);
            if (rawBlocks == null)
            {
                Debug.LogWarning($"[ChunkStreamingManager] Failed to deserialize chunk data for {msg.Coord}");
                return;
            }

            world.LoadChunkFromNetwork(msg.Coord, rawBlocks);
            chunksReceived++;
        }

        /// <summary>
        /// Client: all initial chunks have been received.
        /// </summary>
        private void HandleSyncComplete()
        {
            if (isHost) return;

            IsSyncComplete = true;
            Debug.Log($"[ChunkStreamingManager] Sync complete! Received {chunksReceived}/{totalChunksExpected} chunks.");
        }

        /// <summary>
        /// Host: checks if a client has received a specific chunk.
        /// Used by BlockSyncManager to determine if block edits should be sent.
        /// </summary>
        public bool HasClientSyncedChunk(ulong clientId, ChunkCoord coord)
        {
            return syncedChunks.TryGetValue(clientId, out var synced) && synced.Contains(coord);
        }
    }
}
