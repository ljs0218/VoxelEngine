using System.IO;
using UnityEngine;

namespace VoxelEngine.Networking.Messages
{
    /// <summary>
    /// Periodic player position update (20Hz, Unreliable).
    /// Uses ulong for SteamId to avoid direct Steamworks dependency in message layer.
    /// </summary>
    public struct PlayerPositionMessage
    {
        public ulong PlayerId;
        public Vector3 FocalPoint;
        public float CameraOrthoSize;

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(PlayerId);
                writer.Write(FocalPoint.x);
                writer.Write(FocalPoint.y);
                writer.Write(FocalPoint.z);
                writer.Write(CameraOrthoSize);
                return ms.ToArray();
            }
        }

        public static PlayerPositionMessage Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new PlayerPositionMessage
                {
                    PlayerId = reader.ReadUInt64(),
                    FocalPoint = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    CameraOrthoSize = reader.ReadSingle()
                };
            }
        }
    }
}
