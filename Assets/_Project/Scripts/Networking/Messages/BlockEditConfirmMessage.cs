using System.IO;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by host to confirm a client's block edit was accepted.
    /// </summary>
    public struct BlockEditConfirmMessage
    {
        public uint EditId;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(EditId);
                return ms.ToArray();
            }
        }

        public static BlockEditConfirmMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new BlockEditConfirmMessage
                {
                    EditId = reader.ReadUInt32()
                };
            }
        }
    }
}
