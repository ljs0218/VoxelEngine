using System.IO;
using UnityEngine;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Sent by host when a client's block edit is rejected.
    /// Contains the correct block type for rollback.
    /// </summary>
    public struct BlockEditRejectMessage
    {
        public uint EditId;
        public Vector3Int WorldPos;
        public byte CorrectBlockType;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(EditId);
                writer.Write(WorldPos.x);
                writer.Write(WorldPos.y);
                writer.Write(WorldPos.z);
                writer.Write(CorrectBlockType);
                return ms.ToArray();
            }
        }

        public static BlockEditRejectMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new BlockEditRejectMessage
                {
                    EditId = reader.ReadUInt32(),
                    WorldPos = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    CorrectBlockType = reader.ReadByte()
                };
            }
        }
    }
}
