using System.IO;
using NUnit.Framework;
using VoxelEngine.Serialization;
using VoxelEngine.Utilities;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class SerializationTests
    {
        // ==================== ChunkSerializer Tests ====================

        [Test]
        public void ChunkSerializer_EmptyChunk_RoundTrips()
        {
            byte[] original = new byte[VoxelConstants.ChunkBlockCount];

            byte[] serialized = ChunkSerializer.Serialize(original);
            Assert.IsNotNull(serialized, "Serialized data should not be null");
            Assert.Less(serialized.Length, original.Length,
                "RLE of empty chunk should be much smaller than raw data");

            byte[] deserialized = ChunkSerializer.Deserialize(serialized);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Length, deserialized.Length);
            for (int i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(original[i], deserialized[i], $"Block mismatch at index {i}");
            }
        }

        [Test]
        public void ChunkSerializer_MixedBlocks_RoundTrips()
        {
            byte[] original = new byte[VoxelConstants.ChunkBlockCount];

            // Place some blocks in a pattern
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    int index = x + VoxelConstants.ChunkWidth * (8 + VoxelConstants.ChunkHeight * z);
                    original[index] = 1; // Grass
                }
            }
            // Add some variety
            original[100] = 2; // Dirt
            original[200] = 3; // Stone
            original[300] = 4; // Wood

            byte[] serialized = ChunkSerializer.Serialize(original);
            Assert.IsNotNull(serialized);

            byte[] deserialized = ChunkSerializer.Deserialize(serialized);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Length, deserialized.Length);
            for (int i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(original[i], deserialized[i], $"Block mismatch at index {i}");
            }
        }

        [Test]
        public void ChunkSerializer_FullChunk_RoundTrips()
        {
            byte[] original = new byte[VoxelConstants.ChunkBlockCount];
            // Fill entire chunk with stone
            for (int i = 0; i < original.Length; i++)
                original[i] = 3;

            byte[] serialized = ChunkSerializer.Serialize(original);
            Assert.IsNotNull(serialized);
            // Full homogeneous chunk should compress to just header + 1 RLE pair
            // Header: 4 (magic) + 1 (version) = 5, RLE pair: 1 (block) + 2 (count) = 3
            // ChunkBlockCount = 4096 fits in a single ushort, so just 1 pair
            Assert.Less(serialized.Length, 20,
                "Homogeneous chunk should compress to very few bytes");

            byte[] deserialized = ChunkSerializer.Deserialize(serialized);
            Assert.IsNotNull(deserialized);
            for (int i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(3, deserialized[i], $"Block mismatch at index {i}");
            }
        }

        [Test]
        public void ChunkSerializer_NullInput_ReturnsNull()
        {
            Assert.IsNull(ChunkSerializer.Serialize(null));
            Assert.IsNull(ChunkSerializer.Deserialize(null));
        }

        [Test]
        public void ChunkSerializer_InvalidData_ReturnsNull()
        {
            // Wrong magic bytes
            Assert.IsNull(ChunkSerializer.Deserialize(new byte[] { 0, 0, 0, 0, 1 }));

            // Too short
            Assert.IsNull(ChunkSerializer.Deserialize(new byte[] { 1, 2 }));
        }

        [Test]
        public void ChunkSerializer_FileRoundTrip()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "VoxelTest", "test_chunk.vkc");
            try
            {
                byte[] original = new byte[VoxelConstants.ChunkBlockCount];
                original[0] = 1;
                original[1000] = 2;
                original[4000] = 3;

                bool saved = ChunkSerializer.SaveToFile(tempPath, original);
                Assert.IsTrue(saved, "SaveToFile should succeed");
                Assert.IsTrue(File.Exists(tempPath), "File should exist on disk");

                byte[] loaded = ChunkSerializer.LoadFromFile(tempPath);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(original.Length, loaded.Length);
                Assert.AreEqual(1, loaded[0]);
                Assert.AreEqual(2, loaded[1000]);
                Assert.AreEqual(3, loaded[4000]);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                string dir = Path.GetDirectoryName(tempPath);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        // ==================== WorldMetadata Tests ====================

        [Test]
        public void WorldMetadata_CreateNew_HasDefaults()
        {
            var meta = WorldMetadata.CreateNew("TestWorld", 42);

            Assert.AreEqual("TestWorld", meta.worldName);
            Assert.AreEqual(42, meta.seed);
            Assert.Greater(meta.createdTimestamp, 0);
            Assert.AreEqual(0f, meta.playTimeSeconds);
            Assert.AreEqual(0, meta.savedChunkCount);
        }

        [Test]
        public void WorldMetadata_SerializeDeserialize_RoundTrips()
        {
            var original = WorldMetadata.CreateNew("MyWorld", 12345);
            original.playTimeSeconds = 123.5f;
            original.savedChunkCount = 42;

            byte[] data = original.Serialize();
            Assert.IsNotNull(data);

            var deserialized = WorldMetadata.Deserialize(data);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.worldName, deserialized.worldName);
            Assert.AreEqual(original.seed, deserialized.seed);
            Assert.AreEqual(original.createdTimestamp, deserialized.createdTimestamp);
            Assert.AreEqual(original.lastSavedTimestamp, deserialized.lastSavedTimestamp);
            Assert.AreEqual(original.playTimeSeconds, deserialized.playTimeSeconds, 0.01f);
            Assert.AreEqual(original.savedChunkCount, deserialized.savedChunkCount);
        }

        [Test]
        public void WorldMetadata_InvalidData_ReturnsNull()
        {
            Assert.IsNull(WorldMetadata.Deserialize(null));
            Assert.IsNull(WorldMetadata.Deserialize(new byte[] { 0, 0, 0, 0, 1 }));
        }

        [Test]
        public void WorldMetadata_FileRoundTrip()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "VoxelTest", "test.meta");
            try
            {
                var original = WorldMetadata.CreateNew("FileTest", 999);
                original.savedChunkCount = 7;

                bool saved = original.SaveToFile(tempPath);
                Assert.IsTrue(saved);
                Assert.IsTrue(File.Exists(tempPath));

                var loaded = WorldMetadata.LoadFromFile(tempPath);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("FileTest", loaded.worldName);
                Assert.AreEqual(999, loaded.seed);
                Assert.AreEqual(7, loaded.savedChunkCount);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                string dir = Path.GetDirectoryName(tempPath);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }
    }
}
