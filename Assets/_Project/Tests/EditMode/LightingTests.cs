using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Utilities;
using VoxelEngine.World;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class LightingTests
    {
        // ==================== LightMap Packing Tests ====================

        [Test]
        public void LightMap_PackingUnpacking_4Bit()
        {
            var data = new ChunkData();
            try
            {
                data.SetSunlight(5, 10, 7, 12);
                data.SetBlockLight(5, 10, 7, 8);

                Assert.AreEqual(12, data.GetSunlight(5, 10, 7), "Sunlight should be 12");
                Assert.AreEqual(8, data.GetBlockLight(5, 10, 7), "Block light should be 8");
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void LightMap_SetSunlight_DoesNotAffectBlockLight()
        {
            var data = new ChunkData();
            try
            {
                data.SetBlockLight(3, 10, 3, 7);
                data.SetSunlight(3, 10, 3, 15);

                Assert.AreEqual(15, data.GetSunlight(3, 10, 3), "Sunlight should be 15");
                Assert.AreEqual(7, data.GetBlockLight(3, 10, 3), "Block light should still be 7");
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void LightMap_SetBlockLight_DoesNotAffectSunlight()
        {
            var data = new ChunkData();
            try
            {
                data.SetSunlight(3, 10, 3, 10);
                data.SetBlockLight(3, 10, 3, 5);

                Assert.AreEqual(10, data.GetSunlight(3, 10, 3), "Sunlight should still be 10");
                Assert.AreEqual(5, data.GetBlockLight(3, 10, 3), "Block light should be 5");
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void LightMap_ClearLightMap_ZerosAll()
        {
            var data = new ChunkData();
            try
            {
                data.SetSunlight(0, 0, 0, 15);
                data.SetBlockLight(5, 5, 5, 14);

                data.ClearLightMap();

                Assert.AreEqual(0, data.GetSunlight(0, 0, 0));
                Assert.AreEqual(0, data.GetBlockLight(5, 5, 5));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void LightMap_GetLight_OutOfBounds_ReturnsZero()
        {
            var data = new ChunkData();
            try
            {
                Assert.AreEqual(0, data.GetSunlight(-1, 0, 0));
                Assert.AreEqual(0, data.GetBlockLight(0, -1, 0));
                Assert.AreEqual(0, data.GetLight(16, 0, 0));
            }
            finally { data.Dispose(); }
        }

        [Test]
        public void LightMap_SetLight_FullByte()
        {
            var data = new ChunkData();
            try
            {
                // Pack: sunlight=12 (upper), blockLight=8 (lower) = (12 << 4) | 8 = 200
                data.SetLight(5, 10, 7, 200);

                Assert.AreEqual(12, data.GetSunlight(5, 10, 7));
                Assert.AreEqual(8, data.GetBlockLight(5, 10, 7));
                Assert.AreEqual(200, data.GetLight(5, 10, 7));
            }
            finally { data.Dispose(); }
        }

        // ==================== Sunlight Propagation Tests ====================

        [Test]
        public void Sunlight_EmptyChunk_AllMaxLight()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArray();

                LightEngine.InitializeSunlight(data, blockInfos);

                // Every air block should have sunlight = 15
                Assert.AreEqual(15, data.GetSunlight(0, 0, 0));
                Assert.AreEqual(15, data.GetSunlight(8, 8, 8));
                Assert.AreEqual(15, data.GetSunlight(15, 15, 15));
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        [Test]
        public void Sunlight_OpaqueBlockAtY100_BlocksSunlight()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArray();

                // Place solid block at Y=10
                data.SetBlock(8, 10, 8, BlockType.Stone);

                LightEngine.InitializeSunlight(data, blockInfos);

                // Above: sunlight = 15
                Assert.AreEqual(15, data.GetSunlight(8, 15, 8));
                Assert.AreEqual(15, data.GetSunlight(8, 11, 8));

                // The opaque block itself: sunlight = 0
                Assert.AreEqual(0, data.GetSunlight(8, 10, 8));

                // Below (directly under): should be 0 or very low due to no direct sunlight
                // (may get some horizontal spread, but the direct column is blocked)
                Assert.LessOrEqual(data.GetSunlight(8, 9, 8), 14,
                    "Below opaque block should not have max sunlight");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        [Test]
        public void Sunlight_HorizontalSpread_DecaysBy1()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArray();

                // Fill entire chunk with solid at Y=10, except center column
                for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
                {
                    for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                    {
                        if (x != 8 || z != 8) // Leave (8,8) column open
                        {
                            data.SetBlock(x, 10, z, BlockType.Stone);
                        }
                    }
                }

                LightEngine.InitializeSunlight(data, blockInfos);

                // The open column should have full sunlight below Y=10
                Assert.AreEqual(15, data.GetSunlight(8, 9, 8));

                // Adjacent blocks below the roof (horizontal spread from column)
                byte adjacentSun = data.GetSunlight(9, 9, 8);
                Assert.AreEqual(14, adjacentSun, "Adjacent to sunlit column should be 14 (15-1 decay)");

                // Two blocks away
                byte twoAway = data.GetSunlight(10, 9, 8);
                Assert.AreEqual(13, twoAway, "Two blocks from sunlit column should be 13");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        [Test]
        public void Sunlight_DownwardNoDecay_Remains15()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArray();

                LightEngine.InitializeSunlight(data, blockInfos);

                // In an empty chunk, sunlight should be 15 at every Y level
                // because downward propagation from 15 has no decay
                Assert.AreEqual(15, data.GetSunlight(8, 0, 8));
                Assert.AreEqual(15, data.GetSunlight(8, 8, 8));
                Assert.AreEqual(15, data.GetSunlight(8, 15, 8));
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        // ==================== Block Light Tests ====================

        [Test]
        public void BlockLight_TorchEmission14_SpreadsBFS()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArrayWithTorch();

                // Place torch at center
                data.SetBlock(8, 8, 8, BlockType.Torch);
                data.SetBlockLight(8, 8, 8, 14);

                // Manually propagate within chunk (no VoxelWorld needed for single chunk test)
                PropagateSingleChunkBlockLight(data, blockInfos);

                // Source should have emission level
                Assert.AreEqual(14, data.GetBlockLight(8, 8, 8));

                // Neighbors should have 13
                Assert.AreEqual(13, data.GetBlockLight(9, 8, 8));
                Assert.AreEqual(13, data.GetBlockLight(7, 8, 8));
                Assert.AreEqual(13, data.GetBlockLight(8, 9, 8));

                // 2 blocks away should have 12
                Assert.AreEqual(12, data.GetBlockLight(10, 8, 8));
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        [Test]
        public void BlockLight_StopsAtOpaqueBlock()
        {
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = CreateBlockInfoArrayWithTorch();

                // Place torch and a wall next to it
                data.SetBlock(8, 8, 8, BlockType.Torch);
                data.SetBlockLight(8, 8, 8, 14);
                data.SetBlock(9, 8, 8, BlockType.Stone); // Wall

                PropagateSingleChunkBlockLight(data, blockInfos);

                // Torch should have full emission
                Assert.AreEqual(14, data.GetBlockLight(8, 8, 8));

                // The solid block itself should have 0 block light
                Assert.AreEqual(0, data.GetBlockLight(9, 8, 8),
                    "Solid block itself should have 0 block light");

                // Block behind the wall gets light via wrap-around paths (above, below, sides)
                // but should have LESS light than it would without the wall.
                // Without wall: (10,8,8) would be 12 (14-2). With wall it wraps around.
                byte behindWall = data.GetBlockLight(10, 8, 8);
                Assert.Less(behindWall, 12,
                    "Light behind wall should be less than direct-path brightness (12)");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
            }
        }

        // ==================== Vertex Color Tests ====================

        [Test]
        public void MeshVertexColors_WithLightMap_MatchLightValues()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);

                // Place a single block
                data.SetBlock(8, 8, 8, BlockType.Grass);

                // Set sunlight on neighboring air blocks (above the block)
                data.SetSunlight(8, 9, 8, 15); // Above

                Mesh mesh = VoxelEngine.Meshing.MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default,
                    default, default,
                    data.LightMap);

                Assert.IsNotNull(mesh, "Mesh should not be null");

                Color[] colors = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, colors.Length,
                    "Colors count should match vertex count");

                // At least some vertices should have brightness = 1.0 (from sunlight 15)
                bool hasFullBright = false;
                foreach (var c in colors)
                {
                    if (c.r >= 0.99f) hasFullBright = true;
                }
                Assert.IsTrue(hasFullBright, "Some vertices should have full brightness from sunlight");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void MeshVertexColors_NoLightMap_DirectionalFaceShading()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);
                data.SetBlock(8, 8, 8, BlockType.Grass);

                // No lightMap passed (default)
                Mesh mesh = VoxelEngine.Meshing.MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);

                Assert.IsNotNull(mesh);

                Color[] colors = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, colors.Length);

                // Without lightMap, base brightness=1.0 but directional face shading applies:
                // Top=1.0, Side=0.8, Bottom=0.6
                // A single isolated block at Y=8 has 6 visible faces.
                // Verify that at least 3 distinct brightness levels exist.
                var uniqueBrightness = new System.Collections.Generic.HashSet<float>();
                foreach (var c in colors)
                {
                    // Round to 1 decimal place for comparison
                    uniqueBrightness.Add(Mathf.Round(c.r * 10f) / 10f);
                }
                Assert.GreaterOrEqual(uniqueBrightness.Count, 3,
                    "Should have at least 3 brightness levels (top=1.0, side=0.8, bottom=0.6)");
                Assert.IsTrue(uniqueBrightness.Contains(1.0f), "Top face should have brightness 1.0");
                Assert.IsTrue(uniqueBrightness.Contains(0.8f), "Side faces should have brightness 0.8");
                Assert.IsTrue(uniqueBrightness.Contains(0.6f), "Bottom face should have brightness 0.6");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        // ==================== Helper Methods ====================

        private NativeArray<BlockInfo> CreateBlockInfoArray()
        {
            var infos = new NativeArray<BlockInfo>(256, Allocator.TempJob);

            // Air (0): not solid, transparent
            infos[BlockType.Air] = new BlockInfo
            {
                isSolid = false,
                isTransparent = true,
                emission = 0
            };

            // Stone (3): solid, not transparent
            infos[BlockType.Stone] = new BlockInfo
            {
                isSolid = true,
                isTransparent = false,
                emission = 0
            };

            // Grass (1): solid, not transparent
            infos[BlockType.Grass] = new BlockInfo
            {
                isSolid = true,
                isTransparent = false,
                topTileIndex = 1,
                sideTileIndex = 1,
                bottomTileIndex = 2,
                emission = 0
            };

            return infos;
        }

        private NativeArray<BlockInfo> CreateBlockInfoArrayWithTorch()
        {
            var infos = CreateBlockInfoArray();

            // Torch (24): not solid, transparent, emission=14
            infos[BlockType.Torch] = new BlockInfo
            {
                isSolid = false,
                isTransparent = true,
                emission = 14
            };

            return infos;
        }

        /// <summary>
        /// Simple single-chunk BFS block light propagation for testing (no VoxelWorld needed).
        /// </summary>
        private void PropagateSingleChunkBlockLight(ChunkData data, NativeArray<BlockInfo> blockInfos)
        {
            var queue = new System.Collections.Generic.Queue<(int x, int y, int z, byte level)>();

            // Find all sources
            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelConstants.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                    {
                        byte bl = data.GetBlockLight(x, y, z);
                        if (bl > 0)
                        {
                            queue.Enqueue((x, y, z, bl));
                        }
                    }
                }
            }

            int[][] dirs = new int[][]
            {
                new int[]{1,0,0}, new int[]{-1,0,0},
                new int[]{0,1,0}, new int[]{0,-1,0},
                new int[]{0,0,1}, new int[]{0,0,-1}
            };

            while (queue.Count > 0)
            {
                var (cx, cy, cz, level) = queue.Dequeue();
                if (level <= 1) continue;
                byte newLevel = (byte)(level - 1);

                foreach (var dir in dirs)
                {
                    int nx = cx + dir[0], ny = cy + dir[1], nz = cz + dir[2];
                    if (!data.IsInBounds(nx, ny, nz)) continue;

                    byte blockType = data.GetBlock(nx, ny, nz);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte current = data.GetBlockLight(nx, ny, nz);
                    if (newLevel > current)
                    {
                        data.SetBlockLight(nx, ny, nz, newLevel);
                        queue.Enqueue((nx, ny, nz, newLevel));
                    }
                }
            }
        }

        private BlockRegistry CreateTestRegistry()
        {
            var go = new GameObject("TestRegistry");
            var registry = go.AddComponent<BlockRegistry>();

            var airDef = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            airDef.blockId = BlockType.Air;
            airDef.blockName = "Air";
            airDef.isSolid = false;
            airDef.isTransparent = true;

            var grassDef = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            grassDef.blockId = BlockType.Grass;
            grassDef.blockName = "Grass";
            grassDef.isSolid = true;
            grassDef.isTransparent = false;
            grassDef.topTileIndex = 1;
            grassDef.bottomTileIndex = 2;
            grassDef.sideTileIndex = 1;

            var stoneDef = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            stoneDef.blockId = BlockType.Stone;
            stoneDef.blockName = "Stone";
            stoneDef.isSolid = true;
            stoneDef.isTransparent = false;

            registry.SetDefinitions(new BlockDefinitionSO[] { airDef, grassDef, stoneDef });
            return registry;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsByType<BlockRegistry>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(go.gameObject);
            }
        }
    }
}
