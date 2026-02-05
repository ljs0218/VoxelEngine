using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VoxelEngine.Utilities;

namespace VoxelEngine.Serialization
{
    /// <summary>
    /// Serializes and deserializes chunk block data using Run-Length Encoding (RLE).
    /// Binary format: sequence of (byte blockType, ushort runLength) pairs.
    /// Compresses well for typical voxel data where large regions share the same block type.
    /// </summary>
    public static class ChunkSerializer
    {
        /// <summary>
        /// Magic bytes for chunk file identification.
        /// </summary>
        private const uint ChunkMagic = 0x564B4348; // "VKCH"

        /// <summary>
        /// Current serialization format version.
        /// </summary>
        private const byte FormatVersion = 2;

        /// <summary>
        /// Serializes block data to a byte array using RLE compression.
        /// Format: [magic(4)] [version(1)] [RLE pairs: blockType(1) + runLength(2)]...
        /// </summary>
        /// <param name="blocks">Raw block data array (length = ChunkBlockCount).</param>
        /// <returns>Compressed byte array.</returns>
        public static byte[] Serialize(byte[] blocks)
        {
            if (blocks == null || blocks.Length != VoxelConstants.ChunkBlockCount)
                return null;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Header
                writer.Write(ChunkMagic);
                writer.Write(FormatVersion);

                // RLE encode
                byte currentBlock = blocks[0];
                ushort runLength = 1;

                for (int i = 1; i < blocks.Length; i++)
                {
                    if (blocks[i] == currentBlock && runLength < ushort.MaxValue)
                    {
                        runLength++;
                    }
                    else
                    {
                        writer.Write(currentBlock);
                        writer.Write(runLength);
                        currentBlock = blocks[i];
                        runLength = 1;
                    }
                }

                // Write final run
                writer.Write(currentBlock);
                writer.Write(runLength);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes block data from a byte array.
        /// </summary>
        /// <param name="data">Compressed byte array.</param>
        /// <returns>Raw block data array (length = ChunkBlockCount), or null if invalid.</returns>
        public static byte[] Deserialize(byte[] data)
        {
            if (data == null || data.Length < 5) // minimum: magic(4) + version(1)
                return null;

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Verify header
                uint magic = reader.ReadUInt32();
                if (magic != ChunkMagic)
                    return null;

                byte version = reader.ReadByte();
                if (version != FormatVersion)
                    return null;

                // RLE decode
                byte[] blocks = new byte[VoxelConstants.ChunkBlockCount];
                int writeIndex = 0;

                while (ms.Position < ms.Length && writeIndex < blocks.Length)
                {
                    byte blockType = reader.ReadByte();
                    ushort runLength = reader.ReadUInt16();

                    // Guard against overflow
                    int end = writeIndex + runLength;
                    if (end > blocks.Length)
                        end = blocks.Length;

                    for (int i = writeIndex; i < end; i++)
                    {
                        blocks[i] = blockType;
                    }
                    writeIndex = end;
                }

                // Verify we decoded the expected amount
                if (writeIndex != VoxelConstants.ChunkBlockCount)
                    return null;

                return blocks;
            }
        }

        /// <summary>
        /// Saves chunk data to a file on disk.
        /// </summary>
        /// <param name="filePath">Full file path to write.</param>
        /// <param name="blocks">Raw block data array (length = ChunkBlockCount).</param>
        /// <returns>True if save succeeded.</returns>
        public static bool SaveToFile(string filePath, byte[] blocks)
        {
            byte[] data = Serialize(blocks);
            if (data == null) return false;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(filePath, data);
            return true;
        }

        public static byte[] LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            byte[] data = File.ReadAllBytes(filePath);
            return Deserialize(data);
        }

        public static Task<bool> SaveToFileAsync(string filePath, byte[] blocks)
        {
            return Task.Run(() =>
            {
                bool result = SaveToFile(filePath, blocks);
                if (result)
                    Debug.Log($"[AsyncIO] Save completed: {filePath}");
                return result;
            });
        }

        public static Task<byte[]> LoadFromFileAsync(string filePath)
        {
            return Task.Run(() =>
            {
                byte[] result = LoadFromFile(filePath);
                if (result != null)
                    Debug.Log($"[AsyncIO] Load completed: {filePath}");
                return result;
            });
        }
    }
}
