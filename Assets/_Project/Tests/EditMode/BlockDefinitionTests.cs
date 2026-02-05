using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Core;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class BlockDefinitionTests
    {
        [Test]
        public void BlockInfo_FromDefinition_MapsHardness()
        {
            var def = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            def.hardness = 2.5f;
            def.preferredToolType = ToolType.Pickaxe;

            BlockInfo info = BlockInfo.FromDefinition(def);

            Assert.AreEqual(2.5f, info.hardness, 0.001f);
            Assert.AreEqual((byte)ToolType.Pickaxe, info.preferredToolType);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void BlockInfo_FromNull_HasZeroHardness()
        {
            BlockInfo info = BlockInfo.FromDefinition(null);
            Assert.AreEqual(0f, info.hardness);
            Assert.AreEqual(0, info.preferredToolType);
        }

        [Test]
        public void GrassSide_DifferentFromTop()
        {
            var def = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            def.blockId = (byte)BlockType.Grass;
            def.topTileIndex = 1;
            def.sideTileIndex = 26;
            def.bottomTileIndex = 2;

            Assert.AreNotEqual(def.topTileIndex, def.sideTileIndex);
            Assert.AreNotEqual(def.topTileIndex, def.bottomTileIndex);
            Assert.AreNotEqual(def.sideTileIndex, def.bottomTileIndex);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void WoodTop_DifferentFromSide()
        {
            var def = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            def.blockId = (byte)BlockType.Wood;
            def.topTileIndex = 27;
            def.sideTileIndex = 4;
            def.bottomTileIndex = 27;

            Assert.AreNotEqual(def.topTileIndex, def.sideTileIndex);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void ToolType_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (byte)ToolType.None);
            Assert.AreEqual(1, (byte)ToolType.Pickaxe);
            Assert.AreEqual(2, (byte)ToolType.Axe);
            Assert.AreEqual(3, (byte)ToolType.Shovel);
            Assert.AreEqual(4, (byte)ToolType.Shears);
        }

        [Test]
        public void BlockDefinitionSO_DefaultHardness_IsOne()
        {
            var def = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            Assert.AreEqual(1.0f, def.hardness, 0.001f);
            Assert.AreEqual(ToolType.None, def.preferredToolType);
            Object.DestroyImmediate(def);
        }
    }
}
