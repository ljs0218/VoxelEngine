using System.IO;
using VoxelEngine.World;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Carries RLE-compressed chunk data for network transfer.
    /// Uses ChunkSerializer's RLE format directly (no re-encoding).
    /// </summary>
    public struct ChunkDataMessage
    {
        public ChunkCoord Coord;
        /// <summary>RLE-compressed chunk data from ChunkSerializer.Serialize().</summary>
        public byte[] RleData;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(Coord.x);
                writer.Write(Coord.y);
                writer.Write(Coord.z);
                writer.Write(RleData != null ? RleData.Length : 0);
                if (RleData != null && RleData.Length > 0)
                    writer.Write(RleData);
                return ms.ToArray();
            }
        }

        public static ChunkDataMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                var msg = new ChunkDataMessage();
                msg.Coord = new ChunkCoord(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                int length = reader.ReadInt32();
                msg.RleData = length > 0 ? reader.ReadBytes(length) : null;
                return msg;
            }
        }
    }
}
