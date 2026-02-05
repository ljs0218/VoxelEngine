using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Meshing;
using VoxelEngine.World;

namespace VoxelEngine.Tests.EditMode
{
    [TestFixture]
    public class MeshGeneratorTests
    {
        private BlockRegistry CreateTestRegistry()
        {
            var go = new GameObject("TestRegistry");
            var registry = go.AddComponent<BlockRegistry>();

            // Create test block definitions
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
            stoneDef.topTileIndex = 3;
            stoneDef.bottomTileIndex = 3;
            stoneDef.sideTileIndex = 3;

            registry.SetDefinitions(new BlockDefinitionSO[] { airDef, grassDef, stoneDef });
            return registry;
        }

        // ==================== V1 Managed Path Tests ====================

        [Test]
        public void SingleBlock_Generates24Vertices36Indices()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            try
            {
                data.SetBlock(8, 8, 8, BlockType.Grass);

                Mesh mesh = MeshGenerator.GenerateMesh(data, registry);

                Assert.IsNotNull(mesh);
                Assert.AreEqual(24, mesh.vertexCount);
                Assert.AreEqual(36, mesh.triangles.Length);
            }
            finally
            {
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void TwoAdjacentBlocks_ReducedFaceCount()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            try
            {
                data.SetBlock(8, 8, 8, BlockType.Grass);
                data.SetBlock(9, 8, 8, BlockType.Grass);

                Mesh mesh = MeshGenerator.GenerateMesh(data, registry);

                Assert.IsNotNull(mesh);
                Assert.AreEqual(40, mesh.vertexCount);
                Assert.AreEqual(60, mesh.triangles.Length);
            }
            finally
            {
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void EmptyChunk_ReturnsNull()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            try
            {
                Mesh mesh = MeshGenerator.GenerateMesh(data, registry);
                Assert.IsNull(mesh);
            }
            finally
            {
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void UVs_WithinValidRange()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            try
            {
                data.SetBlock(0, 0, 0, BlockType.Grass);

                Mesh mesh = MeshGenerator.GenerateMesh(data, registry);

                Assert.IsNotNull(mesh);
                Vector2[] uvs = mesh.uv;
                foreach (Vector2 uv in uvs)
                {
                    Assert.GreaterOrEqual(uv.x, 0f, "UV x should be >= 0");
                    Assert.LessOrEqual(uv.x, 1f, "UV x should be <= 1");
                    Assert.GreaterOrEqual(uv.y, 0f, "UV y should be >= 0");
                    Assert.LessOrEqual(uv.y, 1f, "UV y should be <= 1");
                }
            }
            finally
            {
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        // ==================== V2 Burst/Jobs Path Tests ====================

        [Test]
        public void BurstPath_SingleBlock_MatchesManagedPath()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                data.SetBlock(8, 8, 8, BlockType.Grass);

                // Managed path
                Mesh managedMesh = MeshGenerator.GenerateMesh(data, registry);

                // Burst path (no neighbors)
                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);
                Mesh burstMesh = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);

                Assert.IsNotNull(managedMesh, "Managed mesh should not be null");
                Assert.IsNotNull(burstMesh, "Burst mesh should not be null");
                Assert.AreEqual(managedMesh.vertexCount, burstMesh.vertexCount,
                    "Vertex count should match between managed and burst paths");
                Assert.AreEqual(managedMesh.triangles.Length, burstMesh.triangles.Length,
                    "Triangle count should match between managed and burst paths");
                Assert.AreEqual(managedMesh.uv.Length, burstMesh.uv.Length,
                    "UV count should match between managed and burst paths");

                // Verify actual vertex positions match
                Vector3[] managedVerts = managedMesh.vertices;
                Vector3[] burstVerts = burstMesh.vertices;
                for (int i = 0; i < managedVerts.Length; i++)
                {
                    Assert.AreEqual(managedVerts[i].x, burstVerts[i].x, 0.001f,
                        $"Vertex[{i}].x mismatch");
                    Assert.AreEqual(managedVerts[i].y, burstVerts[i].y, 0.001f,
                        $"Vertex[{i}].y mismatch");
                    Assert.AreEqual(managedVerts[i].z, burstVerts[i].z, 0.001f,
                        $"Vertex[{i}].z mismatch");
                }

                // Verify UV values match
                Vector2[] managedUVs = managedMesh.uv;
                Vector2[] burstUVs = burstMesh.uv;
                for (int i = 0; i < managedUVs.Length; i++)
                {
                    Assert.AreEqual(managedUVs[i].x, burstUVs[i].x, 0.001f,
                        $"UV[{i}].x mismatch");
                    Assert.AreEqual(managedUVs[i].y, burstUVs[i].y, 0.001f,
                        $"UV[{i}].y mismatch");
                }
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void BurstPath_TwoAdjacentBlocks_MatchesManagedPath()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                data.SetBlock(8, 8, 8, BlockType.Grass);
                data.SetBlock(9, 8, 8, BlockType.Grass);

                // Managed path
                Mesh managedMesh = MeshGenerator.GenerateMesh(data, registry);

                // Burst path
                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);
                Mesh burstMesh = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);

                Assert.IsNotNull(managedMesh);
                Assert.IsNotNull(burstMesh);
                Assert.AreEqual(managedMesh.vertexCount, burstMesh.vertexCount,
                    "Vertex count should match for two adjacent blocks");
                Assert.AreEqual(managedMesh.triangles.Length, burstMesh.triangles.Length,
                    "Triangle count should match for two adjacent blocks");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void BurstPath_EmptyChunk_ReturnsNull()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);
                Mesh mesh = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);

                Assert.IsNull(mesh, "Empty chunk should return null from Burst path");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void BurstPath_UVs_WithinValidRange()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                data.SetBlock(0, 0, 0, BlockType.Stone);

                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);
                Mesh mesh = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);

                Assert.IsNotNull(mesh);
                Vector2[] uvs = mesh.uv;
                foreach (Vector2 uv in uvs)
                {
                    Assert.GreaterOrEqual(uv.x, 0f, "Burst UV x should be >= 0");
                    Assert.LessOrEqual(uv.x, 1f, "Burst UV x should be <= 1");
                    Assert.GreaterOrEqual(uv.y, 0f, "Burst UV y should be >= 0");
                    Assert.LessOrEqual(uv.y, 1f, "Burst UV y should be <= 1");
                }
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [Test]
        public void BurstPath_WithNeighborChunk_CullsSharedFace()
        {
            var registry = CreateTestRegistry();
            var data = new ChunkData();
            var neighborData = new ChunkData();
            NativeArray<BlockInfo> blockInfos = default;
            try
            {
                // Place block at east edge of chunk (x=15)
                data.SetBlock(15, 8, 8, BlockType.Grass);

                // Place block at west edge of neighbor (x=0) — should cull the shared +X/-X face
                neighborData.SetBlock(0, 8, 8, BlockType.Stone);

                blockInfos = registry.BuildBlockInfoArray(Allocator.TempJob);

                // Without neighbor: 6 faces = 24 vertices
                Mesh meshWithout = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    default, default, default, default);
                Assert.IsNotNull(meshWithout);
                Assert.AreEqual(24, meshWithout.vertexCount,
                    "Without neighbor: single block should have 24 vertices (6 faces)");

                // With +X neighbor: 5 faces = 20 vertices (shared face culled)
                Mesh meshWith = MeshGenerator.GenerateMesh(
                    data.Blocks, blockInfos,
                    neighborData.Blocks, default, default, default);
                Assert.IsNotNull(meshWith);
                Assert.AreEqual(20, meshWith.vertexCount,
                    "With +X neighbor: shared face should be culled, leaving 20 vertices (5 faces)");
            }
            finally
            {
                if (blockInfos.IsCreated) blockInfos.Dispose();
                neighborData.Dispose();
                data.Dispose();
                Object.DestroyImmediate(registry.gameObject);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any leftover GameObjects
            foreach (var go in Object.FindObjectsByType<BlockRegistry>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(go.gameObject);
            }
        }
    }
}
