using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core;
using VoxelEngine.Utilities;
using VoxelEngine.World;

namespace VoxelEngine.Meshing
{
    /// <summary>
    /// Generates chunk meshes using simple face culling.
    /// Only renders faces that are adjacent to air or transparent blocks.
    /// Supports both managed (legacy) and Burst/Jobs (NativeArray) paths.
    /// </summary>
    public static class MeshGenerator
    {
        // Direction offsets: Up, Down, North, South, East, West
        private static readonly Vector3Int[] DirectionOffsets = new Vector3Int[]
        {
            new Vector3Int(0, 1, 0),   // Up
            new Vector3Int(0, -1, 0),  // Down
            new Vector3Int(0, 0, 1),   // North (+Z)
            new Vector3Int(0, 0, -1),  // South (-Z)
            new Vector3Int(1, 0, 0),   // East  (+X)
            new Vector3Int(-1, 0, 0),  // West  (-X)
        };

        // Face vertex offsets for each direction (4 vertices per face, CCW winding)
        private static readonly Vector3[][] FaceVertices = new Vector3[][]
        {
            // Up (+Y)
            new Vector3[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // Down (-Y)
            new Vector3[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            // North (+Z)
            new Vector3[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            // South (-Z)
            new Vector3[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
            // East (+X)
            new Vector3[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            // West (-X)
            new Vector3[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
        };

        // Triangle indices for a quad (2 triangles, 6 indices referencing 4 vertices)
        private static readonly int[] QuadTriangles = new int[] { 0, 1, 2, 0, 2, 3 };

        [StructLayout(LayoutKind.Sequential)]
        private struct MeshVertex
        {
            public float3 position;
            public float3 normal;
            public float2 uv;
            public float4 color;
        }

        /// <summary>
        /// Generates a mesh for the given chunk data using the managed path (V1 legacy).
        /// </summary>
        /// <param name="chunkData">The chunk's block data.</param>
        /// <param name="registry">Block definitions for looking up solid/transparent and UV info.</param>
        /// <param name="getNeighborBlock">Optional callback to check blocks in adjacent chunks.
        /// Takes world-relative offset (localX + offsetX, y, localZ + offsetZ) and returns block type.
        /// If null, out-of-bounds blocks are treated as Air.</param>
        /// <returns>A new Mesh, or null if chunk is empty.</returns>
        public static Mesh GenerateMesh(ChunkData chunkData, BlockRegistry registry,
            Func<int, int, int, byte> getNeighborBlock = null)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();

            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelConstants.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                    {
                        byte blockType = chunkData.GetBlock(x, y, z);
                        if (blockType == BlockType.Air)
                            continue;

                        BlockDefinitionSO blockDef = registry.GetDefinition(blockType);
                        if (blockDef == null)
                            continue;

                        // Check each face direction
                        for (int d = 0; d < 6; d++)
                        {
                            Vector3Int offset = DirectionOffsets[d];
                            int nx = x + offset.x;
                            int ny = y + offset.y;
                            int nz = z + offset.z;

                            byte neighborType;
                            if (chunkData.IsInBounds(nx, ny, nz))
                            {
                                neighborType = chunkData.GetBlock(nx, ny, nz);
                            }
                            else if (getNeighborBlock != null)
                            {
                                neighborType = getNeighborBlock(nx, ny, nz);
                            }
                            else
                            {
                                neighborType = BlockType.Air;
                            }

                            // Skip this face if neighbor is solid and not transparent
                            if (registry.IsSolid(neighborType) && !registry.IsTransparent(neighborType))
                                continue;

                            // Add face
                            AddFace(vertices, triangles, uvs, x, y, z, d, blockDef);

                            // Managed path: directional face shading (no lightMap)
                            // Top=1.0, Side=0.8, Bottom=0.6
                            float faceBrightness = d == 0 ? 1.0f : (d == 1 ? 0.6f : 0.8f);
                            Color faceColor = new Color(faceBrightness, faceBrightness, faceBrightness, 1f);
                            colors.Add(faceColor);
                            colors.Add(faceColor);
                            colors.Add(faceColor);
                            colors.Add(faceColor);
                        }
                    }
                }
            }

            if (vertices.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.indexFormat = vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Generates a mesh using the Burst/Jobs path with NativeArrays.
        /// Schedules MeshGenJob synchronously (Complete) and returns the built Mesh.
        /// </summary>
        /// <param name="blocks">This chunk's block data (NativeArray from ChunkData.Blocks).</param>
        /// <param name="blockInfos">Blittable block definitions (from BlockRegistry.BuildBlockInfoArray).</param>
        /// <param name="neighborPosX">+X neighbor chunk blocks, or default if not loaded.</param>
        /// <param name="neighborNegX">-X neighbor chunk blocks, or default if not loaded.</param>
        /// <param name="neighborPosZ">+Z neighbor chunk blocks, or default if not loaded.</param>
        /// <param name="neighborNegZ">-Z neighbor chunk blocks, or default if not loaded.</param>
        /// <param name="lightMap">This chunk's lightMap, or default if not available.</param>
        /// <param name="neighborLightPosX">+X neighbor chunk lightMap, or default.</param>
        /// <param name="neighborLightNegX">-X neighbor chunk lightMap, or default.</param>
        /// <param name="neighborLightPosZ">+Z neighbor chunk lightMap, or default.</param>
        /// <param name="neighborLightNegZ">-Z neighbor chunk lightMap, or default.</param>
        /// <returns>A new Mesh, or null if no faces were generated.</returns>
        public static Mesh GenerateMesh(
            NativeArray<byte> blocks,
            NativeArray<BlockInfo> blockInfos,
            NativeArray<byte> neighborPosX,
            NativeArray<byte> neighborNegX,
            NativeArray<byte> neighborPosZ,
            NativeArray<byte> neighborNegZ,
            NativeArray<byte> neighborPosY = default,
            NativeArray<byte> neighborNegY = default,
            NativeArray<byte> lightMap = default,
            NativeArray<byte> neighborLightPosX = default,
            NativeArray<byte> neighborLightNegX = default,
            NativeArray<byte> neighborLightPosZ = default,
            NativeArray<byte> neighborLightNegZ = default,
            NativeArray<byte> neighborLightPosY = default,
            NativeArray<byte> neighborLightNegY = default)
        {
            var vertices = new NativeList<float3>(4096, Allocator.TempJob);
            var triangles = new NativeList<int>(6144, Allocator.TempJob);
            var uvs = new NativeList<float2>(4096, Allocator.TempJob);
            var colors = new NativeList<float4>(4096, Allocator.TempJob);
            var normals = new NativeList<float3>(4096, Allocator.TempJob);

            // Burst requires all NativeArrays to be valid (created), even if unused.
            // Allocate a small dummy array when no lightMap is provided.
            bool hasLightMap = lightMap.IsCreated;
            NativeArray<byte> dummyLight = default;
            if (!hasLightMap)
            {
                dummyLight = new NativeArray<byte>(1, Allocator.TempJob);
            }

            try
            {
                var job = new MeshGenJob
                {
                    blocks = blocks,
                    blockInfos = blockInfos,
                    neighborPosX = neighborPosX,
                    neighborNegX = neighborNegX,
                    neighborPosZ = neighborPosZ,
                    neighborNegZ = neighborNegZ,
                    neighborPosY = neighborPosY,
                    neighborNegY = neighborNegY,
                    lightMap = hasLightMap ? lightMap : dummyLight,
                    hasLightMap = hasLightMap,
                    neighborLightPosX = neighborLightPosX,
                    neighborLightNegX = neighborLightNegX,
                    neighborLightPosZ = neighborLightPosZ,
                    neighborLightNegZ = neighborLightNegZ,
                    neighborLightPosY = neighborLightPosY,
                    neighborLightNegY = neighborLightNegY,
                    hasNeighborPosX = neighborPosX.IsCreated,
                    hasNeighborNegX = neighborNegX.IsCreated,
                    hasNeighborPosZ = neighborPosZ.IsCreated,
                    hasNeighborNegZ = neighborNegZ.IsCreated,
                    hasNeighborPosY = neighborPosY.IsCreated,
                    hasNeighborNegY = neighborNegY.IsCreated,
                    vertices = vertices,
                    triangles = triangles,
                    uvs = uvs,
                    colors = colors,
                    normals = normals,
                    minBrightness = 0.05f,
                    chunkWidth = VoxelConstants.ChunkWidth,
                    chunkHeight = VoxelConstants.ChunkHeight,
                    chunkDepth = VoxelConstants.ChunkDepth,
                    tilesPerRow = VoxelConstants.TilesPerRow,
                    normalizedTileSize = VoxelConstants.NormalizedTileSize,
                    uvPadding = VoxelConstants.UVPadding
                };

                job.Schedule().Complete();

                if (vertices.Length == 0)
                    return null;

                return BuildMesh(vertices, triangles, uvs, colors, normals);
            }
            finally
            {
                vertices.Dispose();
                triangles.Dispose();
                uvs.Dispose();
                colors.Dispose();
                normals.Dispose();
                if (dummyLight.IsCreated) dummyLight.Dispose();
            }
        }

        private static Mesh BuildMesh(NativeList<float3> vertices, NativeList<int> triangles,
            NativeList<float2> uvs, NativeList<float4> colors, NativeList<float3> normals)
        {
            int vertexCount = vertices.Length;
            int indexCount = triangles.Length;

            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            meshData.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, stream: 3));

            bool useUInt32 = vertexCount > 65535;
            meshData.SetIndexBufferParams(indexCount,
                useUInt32 ? IndexFormat.UInt32 : IndexFormat.UInt16);

            // Write each stream directly from NativeLists (zero managed allocation)
            var posStream = meshData.GetVertexData<float3>(0);
            NativeArray<float3>.Copy(vertices.AsArray(), posStream, vertexCount);

            var normalStream = meshData.GetVertexData<float3>(1);
            NativeArray<float3>.Copy(normals.AsArray(), normalStream, vertexCount);

            var uvStream = meshData.GetVertexData<float2>(2);
            NativeArray<float2>.Copy(uvs.AsArray(), uvStream, vertexCount);

            var colorStream = meshData.GetVertexData<float4>(3);
            NativeArray<float4>.Copy(colors.AsArray(), colorStream, vertexCount);

            if (useUInt32)
            {
                var indexData = meshData.GetIndexData<int>();
                NativeArray<int>.Copy(triangles.AsArray(), indexData, indexCount);
            }
            else
            {
                var indexData = meshData.GetIndexData<ushort>();
                for (int i = 0; i < indexCount; i++)
                    indexData[i] = (ushort)triangles[i];
            }

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vertexCount
            });

            var mesh = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            int x, int y, int z, int direction, BlockDefinitionSO blockDef)
        {
            int vertexStart = vertices.Count;
            Vector3 blockPos = new Vector3(x, y, z);

            // Add 4 vertices for this face
            Vector3[] faceVerts = FaceVertices[direction];
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(blockPos + faceVerts[i]);
            }

            // Add 6 indices (2 triangles)
            for (int i = 0; i < 6; i++)
            {
                triangles.Add(vertexStart + QuadTriangles[i]);
            }

            // Calculate UV from atlas tile index
            int tileIndex = GetTileIndex(direction, blockDef);
            AddFaceUVs(uvs, tileIndex);
        }

        private static int GetTileIndex(int direction, BlockDefinitionSO blockDef)
        {
            // direction: 0=Up, 1=Down, 2=North, 3=South, 4=East, 5=West
            switch (direction)
            {
                case 0: return blockDef.topTileIndex;      // Up
                case 1: return blockDef.bottomTileIndex;   // Down
                default: return blockDef.sideTileIndex;    // All sides
            }
        }

        private static void AddFaceUVs(List<Vector2> uvs, int tileIndex)
        {
            float tileSize = VoxelConstants.NormalizedTileSize;
            float padding = VoxelConstants.UVPadding;

            int tileX = tileIndex % VoxelConstants.TilesPerRow;
            int tileY = tileIndex / VoxelConstants.TilesPerRow;

            float uMin = tileX * tileSize + padding;
            float uMax = (tileX + 1) * tileSize - padding;
            float vMin = tileY * tileSize + padding;
            float vMax = (tileY + 1) * tileSize - padding;

            // UV coordinates for quad vertices (matching FaceVertices winding order)
            uvs.Add(new Vector2(uMin, vMin));
            uvs.Add(new Vector2(uMin, vMax));
            uvs.Add(new Vector2(uMax, vMax));
            uvs.Add(new Vector2(uMax, vMin));
        }
    }
}
