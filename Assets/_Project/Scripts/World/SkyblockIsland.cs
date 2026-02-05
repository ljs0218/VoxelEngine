using UnityEngine;
using VoxelEngine.Core;

namespace VoxelEngine.World
{
    /// <summary>
    /// Generates the initial skyblock starting island.
    /// Hardcoded small platform with a tree, placed at Y=64.
    /// </summary>
    public static class SkyblockIsland
    {
        private const int IslandY = 64;

        /// <summary>
        /// Generates the starting island at the world origin.
        /// Creates a 5x1x5 grass platform, 3 layers of dirt below,
        /// and a small tree in the center.
        /// </summary>
        public static void Generate(VoxelWorld world)
        {
            GenerateAt(world, 0, 0);
        }

        /// <summary>
        /// Generates a skyblock island at the given world XZ center position.
        /// </summary>
        public static void GenerateAt(VoxelWorld world, int centerX, int centerZ)
        {
            // Island platform: 5x4x5 (grass top, dirt below)
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    int wx = centerX + x;
                    int wz = centerZ + z;

                    // Grass layer on top
                    world.SetBlock(new Vector3Int(wx, IslandY, wz), BlockType.Grass);

                    // 3 layers of dirt below
                    world.SetBlock(new Vector3Int(wx, IslandY - 1, wz), BlockType.Dirt);
                    world.SetBlock(new Vector3Int(wx, IslandY - 2, wz), BlockType.Dirt);
                    world.SetBlock(new Vector3Int(wx, IslandY - 3, wz), BlockType.Dirt);
                }
            }

            // Tree trunk: 4 blocks of wood at center
            for (int y = 1; y <= 4; y++)
            {
                world.SetBlock(new Vector3Int(centerX, IslandY + y, centerZ), BlockType.Wood);
            }

            // Tree canopy: 3x3x3 leaves at top of trunk
            int canopyBase = IslandY + 4;
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    for (int y = 0; y <= 2; y++)
                    {
                        // Skip the trunk positions in the first layer
                        if (x == 0 && z == 0 && y == 0)
                            continue;

                        world.SetBlock(new Vector3Int(centerX + x, canopyBase + y, centerZ + z), BlockType.Leaves);
                    }
                }
            }

            // Extra leaves on top for a rounded look
            world.SetBlock(new Vector3Int(centerX, canopyBase + 3, centerZ), BlockType.Leaves);
        }
    }
}
