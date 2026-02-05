using NUnit.Framework;
using VoxelEngine.Core;
using VoxelEngine.Player;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class BlockHardnessTests
    {
        [Test]
        public void Bedrock_IsUnbreakable()
        {
            var info = new BlockInfo { hardness = -1f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(-1f, breakTime);
        }

        [Test]
        public void Water_IsUnbreakable()
        {
            var info = new BlockInfo { hardness = -1f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(-1f, breakTime);
        }

        [Test]
        public void Torch_IsInstantBreak()
        {
            var info = new BlockInfo { hardness = 0f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(0f, breakTime);
        }

        [Test]
        public void Air_IsInstantBreak()
        {
            var info = new BlockInfo { hardness = 0f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(0f, breakTime);
        }

        [Test]
        public void Stone_BreakTime_IsHardnessTimes5()
        {
            var info = new BlockInfo { hardness = 1.5f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(7.5f, breakTime, 0.001f);
        }

        [Test]
        public void Wood_BreakTime_IsHardnessTimes5()
        {
            var info = new BlockInfo { hardness = 2.0f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(10.0f, breakTime, 0.001f);
        }

        [Test]
        public void Obsidian_BreakTime_IsHardnessTimes5()
        {
            var info = new BlockInfo { hardness = 50.0f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(250.0f, breakTime, 0.001f);
        }

        [Test]
        public void Grass_BreakTime_IsCorrect()
        {
            var info = new BlockInfo { hardness = 0.6f };
            float breakTime = BlockBreaking.CalculateBreakTime(info);
            Assert.AreEqual(3.0f, breakTime, 0.01f);
        }
    }
}
