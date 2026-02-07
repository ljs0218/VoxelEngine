using System.IO;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by host to new clients with world information.
    /// </summary>
    public struct WorldMetadataMessage
    {
        public string WorldName;
        public int Seed;
        public int TotalChunks;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(WorldName ?? string.Empty);
                writer.Write(Seed);
                writer.Write(TotalChunks);
                return ms.ToArray();
            }
        }

        public static WorldMetadataMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new WorldMetadataMessage
                {
                    WorldName = reader.ReadString(),
                    Seed = reader.ReadInt32(),
                    TotalChunks = reader.ReadInt32()
                };
            }
        }
    }
}
