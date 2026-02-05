using System;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Serialization
{
    /// <summary>
    /// Stores metadata for a saved world.
    /// Serialized as binary with BinaryWriter/Reader.
    /// </summary>
    [Serializable]
    public class WorldMetadata
    {
        /// <summary>
        /// Magic bytes for file identification.
        /// </summary>
        private const uint MetaMagic = 0x564B4D54; // "VKMT"

        /// <summary>
        /// Current metadata format version.
        /// </summary>
        private const byte FormatVersion = 1;

        /// <summary>
        /// Name of the world (user-defined).
        /// </summary>
        public string worldName;

        /// <summary>
        /// Random seed used for world generation (reserved for future use).
        /// </summary>
        public int seed;

        /// <summary>
        /// UTC timestamp when the world was created.
        /// </summary>
        public long createdTimestamp;

        /// <summary>
        /// UTC timestamp when the world was last saved.
        /// </summary>
        public long lastSavedTimestamp;

        /// <summary>
        /// Total play time in seconds.
        /// </summary>
        public float playTimeSeconds;

        /// <summary>
        /// Number of chunks that have been modified and saved.
        /// </summary>
        public int savedChunkCount;

        /// <summary>
        /// Creates a new WorldMetadata with default values.
        /// </summary>
        public static WorldMetadata CreateNew(string name, int seed = 0)
        {
            return new WorldMetadata
            {
                worldName = name ?? "Untitled",
                seed = seed,
                createdTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                lastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                playTimeSeconds = 0f,
                savedChunkCount = 0
            };
        }

        /// <summary>
        /// Serializes metadata to a byte array.
        /// </summary>
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(MetaMagic);
                writer.Write(FormatVersion);
                writer.Write(worldName ?? string.Empty);
                writer.Write(seed);
                writer.Write(createdTimestamp);
                writer.Write(lastSavedTimestamp);
                writer.Write(playTimeSeconds);
                writer.Write(savedChunkCount);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes metadata from a byte array.
        /// </summary>
        /// <returns>WorldMetadata instance, or null if data is invalid.</returns>
        public static WorldMetadata Deserialize(byte[] data)
        {
            if (data == null || data.Length < 6)
                return null;

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                uint magic = reader.ReadUInt32();
                if (magic != MetaMagic) return null;

                byte version = reader.ReadByte();
                if (version != FormatVersion) return null;

                return new WorldMetadata
                {
                    worldName = reader.ReadString(),
                    seed = reader.ReadInt32(),
                    createdTimestamp = reader.ReadInt64(),
                    lastSavedTimestamp = reader.ReadInt64(),
                    playTimeSeconds = reader.ReadSingle(),
                    savedChunkCount = reader.ReadInt32()
                };
            }
        }

        /// <summary>
        /// Saves metadata to a file.
        /// </summary>
        public bool SaveToFile(string filePath)
        {
            byte[] data = Serialize();
            if (data == null) return false;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(filePath, data);
            return true;
        }

        /// <summary>
        /// Loads metadata from a file.
        /// </summary>
        public static WorldMetadata LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            byte[] data = File.ReadAllBytes(filePath);
            return Deserialize(data);
        }
    }
}
