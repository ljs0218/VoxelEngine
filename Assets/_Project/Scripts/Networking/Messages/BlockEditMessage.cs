using System.IO;
using UnityEngine;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by clients to request a block change, or by host to broadcast confirmed changes.
    /// </summary>
    public struct BlockEditMessage
    {
        public Vector3Int WorldPos;
        public byte NewBlockType;
        public uint EditId;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(WorldPos.x);
                writer.Write(WorldPos.y);
                writer.Write(WorldPos.z);
                writer.Write(NewBlockType);
                writer.Write(EditId);
                return ms.ToArray();
            }
        }

        public static BlockEditMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new BlockEditMessage
                {
                    WorldPos = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    NewBlockType = reader.ReadByte(),
                    EditId = reader.ReadUInt32()
                };
            }
        }
    }
}
