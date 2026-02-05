using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Utilities;
using VoxelEngine.World;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class ChunkDataTests
    {
        [Test]
        public void SetBlock_GetBlock_RoundTrip()
        {
            var data = new ChunkData();
            try
            {
                data.SetBlock(5, 10, 7, BlockType.Stone);
                Assert.AreEqual(BlockType.Stone, data.GetBlock(5, 10, 7));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void SetBlock_GetBlock_MultiplePositions()
        {
            var data = new ChunkData();
            try
            {
                data.SetBlock(0, 0, 0, BlockType.Grass);
                data.SetBlock(15, 15, 15, BlockType.Dirt);
                data.SetBlock(8, 8, 8, BlockType.Wood);

                Assert.AreEqual(BlockType.Grass, data.GetBlock(0, 0, 0));
                Assert.AreEqual(BlockType.Dirt, data.GetBlock(15, 15, 15));
                Assert.AreEqual(BlockType.Wood, data.GetBlock(8, 8, 8));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void GetBlock_OutOfBounds_ReturnsAir()
        {
            var data = new ChunkData();
            try
            {
                Assert.AreEqual(BlockType.Air, data.GetBlock(-1, 0, 0));
                Assert.AreEqual(BlockType.Air, data.GetBlock(0, -1, 0));
                Assert.AreEqual(BlockType.Air, data.GetBlock(0, 0, -1));
                Assert.AreEqual(BlockType.Air, data.GetBlock(16, 0, 0));
                Assert.AreEqual(BlockType.Air, data.GetBlock(0, 16, 0));
                Assert.AreEqual(BlockType.Air, data.GetBlock(0, 0, 16));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void NewChunk_IsEmpty()
        {
            var data = new ChunkData();
            try
            {
                Assert.IsTrue(data.IsEmpty());
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void ChunkWithBlock_IsNotEmpty()
        {
            var data = new ChunkData();
            try
            {
                data.SetBlock(0, 0, 0, BlockType.Stone);
                Assert.IsFalse(data.IsEmpty());
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void SetBlock_OutOfBounds_DoesNotThrow()
        {
            var data = new ChunkData();
            try
            {
                Assert.DoesNotThrow(() => data.SetBlock(-1, 0, 0, BlockType.Stone));
                Assert.DoesNotThrow(() => data.SetBlock(16, 0, 0, BlockType.Stone));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void IsInBounds_CorrectForBoundaryValues()
        {
            var data = new ChunkData();
            try
            {
                Assert.IsTrue(data.IsInBounds(0, 0, 0));
                Assert.IsTrue(data.IsInBounds(15, 15, 15));
                Assert.IsFalse(data.IsInBounds(16, 0, 0));
                Assert.IsFalse(data.IsInBounds(0, 16, 0));
                Assert.IsFalse(data.IsInBounds(-1, 0, 0));
            }
            finally { data.Dispose(); }
        }

        // --- ChunkCoord tests (no ChunkData needed, no dispose) ---

        [Test]
        public void ChunkCoord_FromWorldPosition_Positive()
        {
            var coord = ChunkCoord.FromWorldPosition(new Vector3(10f, 0f, 20f));
            Assert.AreEqual(0, coord.x);
            Assert.AreEqual(0, coord.y);
            Assert.AreEqual(1, coord.z);
        }

        [Test]
        public void ChunkCoord_FromWorldPosition_Negative()
        {
            var coord = ChunkCoord.FromWorldPosition(new Vector3(-1f, 0f, -1f));
            Assert.AreEqual(-1, coord.x);
            Assert.AreEqual(0, coord.y);
            Assert.AreEqual(-1, coord.z);
        }

        [Test]
        public void ChunkCoord_FromWorldPosition_ExactBoundary()
        {
            var coord = ChunkCoord.FromWorldPosition(new Vector3(16f, 0f, 0f));
            Assert.AreEqual(1, coord.x);
            Assert.AreEqual(0, coord.y);
            Assert.AreEqual(0, coord.z);
        }

        [Test]
        public void ChunkCoord_Equality()
        {
            var a = new ChunkCoord(3, 5);
            var b = new ChunkCoord(3, 5);
            var c = new ChunkCoord(3, 6);

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a != c);
        }

        [Test]
        public void ChunkCoord_HashCode_Consistent()
        {
            var a = new ChunkCoord(7, 12);
            var b = new ChunkCoord(7, 12);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ChunkCoord_WorldToLocal_Positive()
        {
            var local = ChunkCoord.WorldToLocal(new Vector3Int(17, 64, 5));
            Assert.AreEqual(1, local.x);
            Assert.AreEqual(0, local.y);   // 64 % 16 = 0
            Assert.AreEqual(5, local.z);
        }

        [Test]
        public void ChunkCoord_WorldToLocal_Negative()
        {
            var local = ChunkCoord.WorldToLocal(new Vector3Int(-1, 10, -3));
            Assert.AreEqual(15, local.x);
            Assert.AreEqual(10, local.y);
            Assert.AreEqual(13, local.z);
        }

        // --- NEW: NativeArray lifecycle tests ---

        [Test]
        public void ChunkData_Dispose_NoException()
        {
            var data = new ChunkData();
            Assert.DoesNotThrow(() => data.Dispose());
        }

        [Test]
        public void ChunkData_DoubleDispose_NoException()
        {
            var data = new ChunkData();
            data.Dispose();
            Assert.DoesNotThrow(() => data.Dispose());
        }

        [Test]
        public void ChunkData_AfterDispose_GetBlock_ReturnsAir()
        {
            var data = new ChunkData();
            data.SetBlock(5, 10, 7, BlockType.Stone);
            data.Dispose();
            Assert.AreEqual(BlockType.Air, data.GetBlock(5, 10, 7));
        }

        [Test]
        public void ChunkData_GetRawBlocks_ReturnsCopy()
        {
            var data = new ChunkData();
            try
            {
                data.SetBlock(0, 0, 0, BlockType.Grass);
                byte[] raw1 = data.GetRawBlocks();
                byte[] raw2 = data.GetRawBlocks();

                // Should be separate copies (different references)
                Assert.AreNotSame(raw1, raw2);
                // But identical content
                Assert.AreEqual(raw1[0], raw2[0]);
                Assert.AreEqual(BlockType.Grass, raw1[0]);
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void ChunkData_SetRawBlocks_CopiesData()
        {
            var data = new ChunkData();
            try
            {
                byte[] source = new byte[VoxelConstants.ChunkBlockCount];
                source[0] = BlockType.Stone;
                source[100] = BlockType.Dirt;

                data.SetRawBlocks(source);

                Assert.AreEqual(BlockType.Stone, data.GetBlock(0, 0, 0));
                // index 100 = x=100%16=4, remainder=100/16=6, y=6%256=6, z=6/256=0 → GetBlock(4, 6, 0)
                // Actually let's just check via GetRawBlocks
                byte[] result = data.GetRawBlocks();
                Assert.AreEqual(BlockType.Stone, result[0]);
                Assert.AreEqual(BlockType.Dirt, result[100]);
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void ChunkData_IsDirty_AfterSetBlock()
        {
            var data = new ChunkData();
            try
            {
                Assert.IsFalse(data.IsDirty);
                data.SetBlock(0, 0, 0, BlockType.Stone);
                Assert.IsTrue(data.IsDirty);
                data.ClearDirty();
                Assert.IsFalse(data.IsDirty);
            }
            finally { data.Dispose(); }
        }
    }
}
