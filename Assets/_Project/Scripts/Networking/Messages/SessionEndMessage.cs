using System.IO;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by host when the session ends (e.g., host disconnecting).
    /// </summary>
    public struct SessionEndMessage
    {
        /// <summary>0 = Host left, 1 = Kicked (reserved).</summary>
        public byte Reason;

        public byte[] Serialize()
        {
            return new byte[] { Reason };
        }

        public static SessionEndMessage Deserialize(byte[] data)
        {
            return new SessionEndMessage
            {
                Reason = data != null && data.Length > 0 ? data[0] : (byte)0
            };
        }
    }
}
