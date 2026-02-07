using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine;
using VoxelEngine.Networking.Messages;
using VoxelEngine.World;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Manages block synchronization between host and clients.
    /// Host-authoritative: clients predict locally, host confirms or rejects.
    /// </summary>
    public class BlockSyncManager : MonoBehaviour
    {
        private VoxelWorld world;
        private P2PConnectionManager connectionManager;
        private bool isHost;

        // Client-side pending edit tracking for prediction/rollback
        private uint nextEditId = 1;
        private readonly Dictionary<uint, PendingEdit> pendingEdits = new Dictionary<uint, PendingEdit>();

        private struct PendingEdit
        {
            public uint EditId;
            public Vector3Int WorldPos;
            public byte OldBlockType;
            public byte NewBlockType;
        }

        public void Initialize(VoxelWorld voxelWorld, P2PConnectionManager p2pManager, bool host)
        {
            world = voxelWorld;
            connectionManager = p2pManager;
            isHost = host;

            // Subscribe to local block changes
            world.OnBlockChanged += HandleLocalBlockChanged;

            // Subscribe to network messages
            connectionManager.OnMessageReceived += HandleNetworkMessage;
        }

        private void OnDestroy()
        {
            if (world != null)
                world.OnBlockChanged -= HandleLocalBlockChanged;
            if (connectionManager != null)
                connectionManager.OnMessageReceived -= HandleNetworkMessage;
        }

        /// <summary>
        /// Called when a block is changed locally (Local or Authority source).
        /// </summary>
        private void HandleLocalBlockChanged(Vector3Int worldPos, byte oldType, byte newType)
        {
            if (isHost)
            {
                // Host: broadcast the change to all clients
                var msg = new BlockEditMessage
                {
                    WorldPos = worldPos,
                    NewBlockType = newType,
                    EditId = 0 // Host broadcasts use editId 0
                };
                byte[] packed = NetworkMessage.Pack(NetworkMessageType.BlockEdit, msg.Serialize());
                connectionManager.SendToAll(packed, SendType.Reliable);
            }
            else
            {
                // Client: send edit request to host with prediction tracking
                uint editId = nextEditId++;
                pendingEdits[editId] = new PendingEdit
                {
                    EditId = editId,
                    WorldPos = worldPos,
                    OldBlockType = oldType,
                    NewBlockType = newType
                };

                var msg = new BlockEditMessage
                {
                    WorldPos = worldPos,
                    NewBlockType = newType,
                    EditId = editId
                };
                byte[] packed = NetworkMessage.Pack(NetworkMessageType.BlockEdit, msg.Serialize());
                connectionManager.SendToHost(packed, SendType.Reliable);
            }
        }

        /// <summary>
        /// Handles incoming network messages related to block sync.
        /// </summary>
        private void HandleNetworkMessage(ulong senderSteamId, NetworkMessageType type, byte[] payload)
        {
            switch (type)
            {
                case NetworkMessageType.BlockEdit:
                    HandleBlockEditReceived(senderSteamId, payload);
                    break;
                case NetworkMessageType.BlockEditConfirm:
                    HandleBlockEditConfirm(payload);
                    break;
                case NetworkMessageType.BlockEditReject:
                    HandleBlockEditReject(payload);
                    break;
            }
        }

        /// <summary>
        /// Host: validates and applies client edit, then confirms/rejects.
        /// Client: applies host-broadcast edit from other players.
        /// </summary>
        private void HandleBlockEditReceived(ulong senderSteamId, byte[] payload)
        {
            var msg = BlockEditMessage.Deserialize(payload);

            if (isHost)
            {
                // Host received a client's edit request — validate and apply
                if (ValidateBlockEdit(msg))
                {
                    // Apply with Authority source (fires OnBlockChanged → broadcasts to all)
                    world.SetBlock(msg.WorldPos, msg.NewBlockType, BlockChangeSource.Authority);

                    // Send confirm to the requesting client
                    var confirm = new BlockEditConfirmMessage { EditId = msg.EditId };
                    byte[] confirmPacked = NetworkMessage.Pack(NetworkMessageType.BlockEditConfirm, confirm.Serialize());
                    // Broadcast confirm to all (the requesting client uses it to clear pending)
                    connectionManager.SendToAll(confirmPacked, SendType.Reliable);
                }
                else
                {
                    // Reject: send correct block type for rollback
                    byte correctType = world.GetBlock(msg.WorldPos);
                    var reject = new BlockEditRejectMessage
                    {
                        EditId = msg.EditId,
                        WorldPos = msg.WorldPos,
                        CorrectBlockType = correctType
                    };
                    byte[] rejectPacked = NetworkMessage.Pack(NetworkMessageType.BlockEditReject, reject.Serialize());
                    connectionManager.SendToAll(rejectPacked, SendType.Reliable);
                }
            }
            else
            {
                // Client received a block edit from the host (another player's change)
                // Apply without firing event (Network source)
                world.SetBlock(msg.WorldPos, msg.NewBlockType, BlockChangeSource.Network);
            }
        }

        /// <summary>
        /// Client: host confirmed our edit — remove from pending.
        /// </summary>
        private void HandleBlockEditConfirm(byte[] payload)
        {
            if (isHost) return; // Host doesn't track pending edits

            var msg = BlockEditConfirmMessage.Deserialize(payload);
            pendingEdits.Remove(msg.EditId);
        }

        /// <summary>
        /// Client: host rejected our edit — rollback to correct state.
        /// </summary>
        private void HandleBlockEditReject(byte[] payload)
        {
            if (isHost) return;

            var msg = BlockEditRejectMessage.Deserialize(payload);

            if (pendingEdits.TryGetValue(msg.EditId, out var pending))
            {
                // Rollback: apply the correct block type (Network source — no re-broadcast)
                world.SetBlock(msg.WorldPos, msg.CorrectBlockType, BlockChangeSource.Network);
                pendingEdits.Remove(msg.EditId);
                Debug.Log($"[BlockSyncManager] Edit {msg.EditId} rejected. Rolled back {msg.WorldPos} to {msg.CorrectBlockType}.");
            }
        }

        /// <summary>
        /// Validates a block edit request. Returns true if the edit is allowed.
        /// </summary>
        private bool ValidateBlockEdit(BlockEditMessage msg)
        {
            // Basic validation: position must be in valid range
            if (msg.WorldPos.y < 0 || msg.WorldPos.y >= VoxelEngine.Utilities.VoxelConstants.ColumnHeight)
                return false;

            // Block type must be valid (0-255 is valid for byte, but check against registered types if needed)
            // For now, accept all valid positions
            return true;
        }
    }
}
