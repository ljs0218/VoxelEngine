using System.Collections.Generic;
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
    public class MeshJobScheduler
    {
        private struct PendingJob
        {
            public JobHandle handle;
            public NativeList<float3> vertices;
            public NativeList<int> triangles;
            public NativeList<float2> uvs;
            public NativeList<float4> colors;
            public NativeList<float3> normals;
            public NativeArray<byte> dummyLight;
            public NativeArray<byte> blocksCopy;
            public NativeArray<byte> lightMapCopy;
            public Chunk chunk;
        }

        private readonly Dictionary<ChunkCoord, PendingJob> pendingJobs = new Dictionary<ChunkCoord, PendingJob>();
        private readonly List<ChunkCoord> completedKeys = new List<ChunkCoord>();

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MeshVertex
        {
            public float3 position;
            public float3 normal;
            public float2 uv;
            public float4 color;
        }

        public void ScheduleMeshJob(Chunk chunk, NativeArray<BlockInfo> blockInfos, VoxelWorld world)
        {
            ChunkCoord coord = chunk.Coord;

            if (pendingJobs.ContainsKey(coord))
            {
                CompletePendingJob(coord);
            }

            var chunkData = chunk.ChunkData;
            if (chunkData == null || !chunkData.Blocks.IsCreated) return;

            var blocksCopy = new NativeArray<byte>(chunkData.Blocks, Allocator.TempJob);
            bool hasLight = chunkData.LightMap.IsCreated;
            var lightMapCopy = hasLight
                ? new NativeArray<byte>(chunkData.LightMap, Allocator.TempJob)
                : default;

            NativeArray<byte> neighborPosX = world.GetChunkBlocks(new ChunkCoord(coord.x + 1, coord.y, coord.z));
            NativeArray<byte> neighborNegX = world.GetChunkBlocks(new ChunkCoord(coord.x - 1, coord.y, coord.z));
            NativeArray<byte> neighborPosZ = world.GetChunkBlocks(new ChunkCoord(coord.x, coord.y, coord.z + 1));
            NativeArray<byte> neighborNegZ = world.GetChunkBlocks(new ChunkCoord(coord.x, coord.y, coord.z - 1));
            NativeArray<byte> neighborPosY = world.GetChunkBlocks(new ChunkCoord(coord.x, coord.y + 1, coord.z));
            NativeArray<byte> neighborNegY = world.GetChunkBlocks(new ChunkCoord(coord.x, coord.y - 1, coord.z));

            NativeArray<byte> lightPosX = world.GetChunkLightMap(new ChunkCoord(coord.x + 1, coord.y, coord.z));
            NativeArray<byte> lightNegX = world.GetChunkLightMap(new ChunkCoord(coord.x - 1, coord.y, coord.z));
            NativeArray<byte> lightPosZ = world.GetChunkLightMap(new ChunkCoord(coord.x, coord.y, coord.z + 1));
            NativeArray<byte> lightNegZ = world.GetChunkLightMap(new ChunkCoord(coord.x, coord.y, coord.z - 1));
            NativeArray<byte> lightPosY = world.GetChunkLightMap(new ChunkCoord(coord.x, coord.y + 1, coord.z));
            NativeArray<byte> lightNegY = world.GetChunkLightMap(new ChunkCoord(coord.x, coord.y - 1, coord.z));

            var vertices = new NativeList<float3>(4096, Allocator.TempJob);
            var triangles = new NativeList<int>(6144, Allocator.TempJob);
            var uvs = new NativeList<float2>(4096, Allocator.TempJob);
            var colors = new NativeList<float4>(4096, Allocator.TempJob);
            var normals = new NativeList<float3>(4096, Allocator.TempJob);

            NativeArray<byte> dummyLight = default;
            bool hasLightMap = hasLight;
            if (!hasLightMap)
            {
                dummyLight = new NativeArray<byte>(1, Allocator.TempJob);
            }

            var job = new MeshGenJob
            {
                blocks = blocksCopy,
                blockInfos = blockInfos,
                neighborPosX = neighborPosX,
                neighborNegX = neighborNegX,
                neighborPosZ = neighborPosZ,
                neighborNegZ = neighborNegZ,
                neighborPosY = neighborPosY,
                neighborNegY = neighborNegY,
                lightMap = hasLightMap ? lightMapCopy : dummyLight,
                hasLightMap = hasLightMap,
                neighborLightPosX = lightPosX,
                neighborLightNegX = lightNegX,
                neighborLightPosZ = lightPosZ,
                neighborLightNegZ = lightNegZ,
                neighborLightPosY = lightPosY,
                neighborLightNegY = lightNegY,
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

            JobHandle handle = job.Schedule();

            pendingJobs[coord] = new PendingJob
            {
                handle = handle,
                vertices = vertices,
                triangles = triangles,
                uvs = uvs,
                colors = colors,
                normals = normals,
                dummyLight = dummyLight,
                blocksCopy = blocksCopy,
                lightMapCopy = lightMapCopy,
                chunk = chunk
            };
        }

        public void Update(int maxCompletionsPerFrame = int.MaxValue)
        {
            completedKeys.Clear();
            int completionsThisFrame = 0;

            foreach (var kvp in pendingJobs)
            {
                if (completionsThisFrame >= maxCompletionsPerFrame)
                    break;

                if (kvp.Value.handle.IsCompleted)
                {
                    completedKeys.Add(kvp.Key);
                    completionsThisFrame++;
                }
            }

            foreach (var key in completedKeys)
            {
                var pending = pendingJobs[key];
                pending.handle.Complete();

                if (pending.chunk != null)
                {
                    Mesh mesh = null;
                    if (pending.vertices.Length > 0)
                    {
                        mesh = BuildMesh(pending.vertices, pending.triangles, pending.uvs,
                            pending.colors, pending.normals);
                    }
                    pending.chunk.ApplyMesh(mesh);
                }

                DisposePendingJob(pending);
                pendingJobs.Remove(key);
            }
        }

        public void CompleteAndRemove(ChunkCoord coord)
        {
            if (pendingJobs.TryGetValue(coord, out var pending))
            {
                pending.handle.Complete();
                DisposePendingJob(pending);
                pendingJobs.Remove(coord);
            }
        }

        public void CompleteAll()
        {
            foreach (var kvp in pendingJobs)
            {
                kvp.Value.handle.Complete();
                DisposePendingJob(kvp.Value);
            }
            pendingJobs.Clear();
        }

        private void CompletePendingJob(ChunkCoord coord)
        {
            if (pendingJobs.TryGetValue(coord, out var pending))
            {
                pending.handle.Complete();
                DisposePendingJob(pending);
                pendingJobs.Remove(coord);
            }
        }

        private static void DisposePendingJob(PendingJob pending)
        {
            if (pending.vertices.IsCreated) pending.vertices.Dispose();
            if (pending.triangles.IsCreated) pending.triangles.Dispose();
            if (pending.uvs.IsCreated) pending.uvs.Dispose();
            if (pending.colors.IsCreated) pending.colors.Dispose();
            if (pending.normals.IsCreated) pending.normals.Dispose();
            if (pending.dummyLight.IsCreated) pending.dummyLight.Dispose();
            if (pending.blocksCopy.IsCreated) pending.blocksCopy.Dispose();
            if (pending.lightMapCopy.IsCreated) pending.lightMapCopy.Dispose();
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

        public int PendingCount => pendingJobs.Count;
    }
}
