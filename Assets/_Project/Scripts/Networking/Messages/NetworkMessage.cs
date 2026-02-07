using System;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Message envelope: [1-byte type][payload bytes].
    /// Provides Pack/Unpack for all network messages.
    /// </summary>
    public static class NetworkMessage
    {
        /// <summary>
        /// Packs a message type and payload into a single byte array.
        /// Format: [type (1 byte)][payload (N bytes)]
        /// </summary>
        public static byte[] Pack(NetworkMessageType type, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return new byte[] { (byte)type };
            }

            byte[] packed = new byte[1 + payload.Length];
            packed[0] = (byte)type;
            Buffer.BlockCopy(payload, 0, packed, 1, payload.Length);
            return packed;
        }

        /// <summary>
        /// Unpacks a raw byte array into message type and payload.
        /// </summary>
        public static (NetworkMessageType type, byte[] payload) Unpack(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
                throw new ArgumentException("Cannot unpack empty or null message.");

            NetworkMessageType type = (NetworkMessageType)raw[0];

            byte[] payload;
            if (raw.Length > 1)
            {
                payload = new byte[raw.Length - 1];
                Buffer.BlockCopy(raw, 1, payload, 0, payload.Length);
            }
            else
            {
                payload = Array.Empty<byte>();
            }

            return (type, payload);
        }
    }
}
