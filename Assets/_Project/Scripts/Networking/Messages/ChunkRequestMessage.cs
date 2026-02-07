using System.IO;
using VoxelEngine.World;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by client to request a specific chunk from the host.
    /// </summary>
    public struct ChunkRequestMessage
    {
        public ChunkCoord Coord;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(Coord.x);
                writer.Write(Coord.y);
                writer.Write(Coord.z);
                return ms.ToArray();
            }
        }

        public static ChunkRequestMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new ChunkRequestMessage
                {
                    Coord = new ChunkCoord(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())
                };
            }
        }
    }
}
