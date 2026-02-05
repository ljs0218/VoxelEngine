using UnityEngine;
using VoxelEngine.Core;

namespace VoxelEngine.World
{
    public static class ShowcaseIsland
    {
        private const int IslandY = 64;

        public static void Generate(VoxelWorld world)
        {
            GenerateAt(world, 0, 0);
        }

        public static void GenerateAt(VoxelWorld world, int centerX, int centerZ)
        {
            byte[] allBlocks = new byte[]
            {
                BlockType.Grass, BlockType.Dirt, BlockType.Stone, BlockType.Wood,
                BlockType.Leaves, BlockType.Sand, BlockType.Water, BlockType.Glass,
                BlockType.Cobblestone, BlockType.Planks, BlockType.Brick, BlockType.Gravel,
                BlockType.Iron, BlockType.Gold, BlockType.Diamond, BlockType.Coal,
                BlockType.Obsidian, BlockType.Snow, BlockType.Ice, BlockType.Clay,
                BlockType.Sandstone, BlockType.Wool, BlockType.Bedrock, BlockType.Torch,
                BlockType.Flower
            };

            int cols = 13;
            int rows = (allBlocks.Length + cols - 1) / cols;

            int platformWidth = cols * 2 + 2;
            int platformDepth = rows * 2 + 2;

            for (int x = 0; x < platformWidth; x++)
            {
                for (int z = 0; z < platformDepth; z++)
                {
                    int wx = centerX + x - platformWidth / 2;
                    int wz = centerZ + z - platformDepth / 2;
                    world.SetBlock(new Vector3Int(wx, IslandY - 1, wz), BlockType.Bedrock);
                }
            }

            for (int i = 0; i < allBlocks.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int wx = centerX + col * 2 - (cols - 1);
                int wz = centerZ + row * 2 - (rows - 1);
                world.SetBlock(new Vector3Int(wx, IslandY, wz), allBlocks[i]);
            }
        }
    }
}
