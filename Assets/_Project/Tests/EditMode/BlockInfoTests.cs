using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Core;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class BlockInfoTests
    {
        [Test]
        public void BlockInfo_FromDefinition_MapsCorrectly()
        {
            var def = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            def.blockId = 1;
            def.blockName = "Grass";
            def.isSolid = true;
            def.isTransparent = false;
            def.topTileIndex = 1;
            def.bottomTileIndex = 2;
            def.sideTileIndex = 3;

            BlockInfo info = BlockInfo.FromDefinition(def);

            Assert.IsTrue(info.isSolid);
            Assert.IsFalse(info.isTransparent);
            Assert.AreEqual(1, info.topTileIndex);
            Assert.AreEqual(2, info.bottomTileIndex);
            Assert.AreEqual(3, info.sideTileIndex);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void BlockInfo_FromNull_ReturnsAirLike()
        {
            BlockInfo info = BlockInfo.FromDefinition(null);

            Assert.IsFalse(info.isSolid);
            Assert.IsTrue(info.isTransparent);
            Assert.AreEqual(0, info.topTileIndex);
            Assert.AreEqual(0, info.sideTileIndex);
            Assert.AreEqual(0, info.bottomTileIndex);
        }

        [Test]
        public void BlockInfoArray_Lookup_MatchesRegistry()
        {
            var go = new GameObject("TestRegistry");
            var registry = go.AddComponent<BlockRegistry>();

            var airDef = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            airDef.blockId = 0;
            airDef.isSolid = false;
            airDef.isTransparent = true;

            var grassDef = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            grassDef.blockId = 1;
            grassDef.isSolid = true;
            grassDef.isTransparent = false;
            grassDef.topTileIndex = 1;
            grassDef.sideTileIndex = 1;
            grassDef.bottomTileIndex = 2;

            registry.SetDefinitions(new BlockDefinitionSO[] { airDef, grassDef });

            NativeArray<BlockInfo> blockInfos = registry.BuildBlockInfoArray(Allocator.Temp);
            try
            {
                // Air (ID 0)
                Assert.IsFalse(blockInfos[0].isSolid);
                Assert.IsTrue(blockInfos[0].isTransparent);

                // Grass (ID 1)
                Assert.IsTrue(blockInfos[1].isSolid);
                Assert.IsFalse(blockInfos[1].isTransparent);
                Assert.AreEqual(1, blockInfos[1].topTileIndex);
                Assert.AreEqual(1, blockInfos[1].sideTileIndex);
                Assert.AreEqual(2, blockInfos[1].bottomTileIndex);

                // Undefined (ID 2) should be air-like
                Assert.IsFalse(blockInfos[2].isSolid);
                Assert.IsTrue(blockInfos[2].isTransparent);
            }
            finally
            {
                blockInfos.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(airDef);
                Object.DestroyImmediate(grassDef);
            }
        }
    }
}
