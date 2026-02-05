using Unity.Collections;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Meshing;
using VoxelEngine.Utilities;

namespace VoxelEngine.World
{
    /// <summary>
    /// MonoBehaviour representing a single chunk in the scene.
    /// Holds chunk data, renders mesh, and provides block access.
    /// Uses the Burst/Jobs mesh generation path when blockInfos are available.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Chunk : MonoBehaviour
    {
        public ChunkCoord Coord { get; private set; }
        public ChunkData ChunkData { get; private set; }

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private BlockRegistry registry;
        private VoxelWorld world;

        /// <summary>
        /// Initializes the chunk with data and references.
        /// Optionally builds the mesh immediately (set buildMesh=false for deferred lighting flow).
        /// </summary>
        public void Initialize(ChunkCoord coord, ChunkData data, BlockRegistry registry,
            Material material, VoxelWorld world, bool buildMesh = true)
        {
            Coord = coord;
            ChunkData = data;
            this.registry = registry;
            this.world = world;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            meshRenderer.material = material;
            transform.position = coord.GetWorldPosition();

            if (buildMesh)
            {
                RebuildMesh();
            }
        }

        public void RebuildMesh()
        {
            if (world != null && world.BlockInfos.IsCreated && world.MeshScheduler != null)
            {
                world.MeshScheduler.ScheduleMeshJob(this, world.BlockInfos, world);
                return;
            }

            RebuildMeshSync();
        }

        public void RebuildMeshSync()
        {
            if (meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
            }

            Mesh mesh;

            if (world != null && world.BlockInfos.IsCreated)
            {
                NativeArray<byte> neighborPosX = world.GetChunkBlocks(new ChunkCoord(Coord.x + 1, Coord.y, Coord.z));
                NativeArray<byte> neighborNegX = world.GetChunkBlocks(new ChunkCoord(Coord.x - 1, Coord.y, Coord.z));
                NativeArray<byte> neighborPosZ = world.GetChunkBlocks(new ChunkCoord(Coord.x, Coord.y, Coord.z + 1));
                NativeArray<byte> neighborNegZ = world.GetChunkBlocks(new ChunkCoord(Coord.x, Coord.y, Coord.z - 1));
                NativeArray<byte> neighborPosY = world.GetChunkBlocks(new ChunkCoord(Coord.x, Coord.y + 1, Coord.z));
                NativeArray<byte> neighborNegY = world.GetChunkBlocks(new ChunkCoord(Coord.x, Coord.y - 1, Coord.z));

                NativeArray<byte> lightPosX = world.GetChunkLightMap(new ChunkCoord(Coord.x + 1, Coord.y, Coord.z));
                NativeArray<byte> lightNegX = world.GetChunkLightMap(new ChunkCoord(Coord.x - 1, Coord.y, Coord.z));
                NativeArray<byte> lightPosZ = world.GetChunkLightMap(new ChunkCoord(Coord.x, Coord.y, Coord.z + 1));
                NativeArray<byte> lightNegZ = world.GetChunkLightMap(new ChunkCoord(Coord.x, Coord.y, Coord.z - 1));
                NativeArray<byte> lightPosY = world.GetChunkLightMap(new ChunkCoord(Coord.x, Coord.y + 1, Coord.z));
                NativeArray<byte> lightNegY = world.GetChunkLightMap(new ChunkCoord(Coord.x, Coord.y - 1, Coord.z));

                mesh = MeshGenerator.GenerateMesh(
                    ChunkData.Blocks, world.BlockInfos,
                    neighborPosX, neighborNegX, neighborPosZ, neighborNegZ,
                    neighborPosY, neighborNegY,
                    ChunkData.LightMap, lightPosX, lightNegX, lightPosZ, lightNegZ,
                    lightPosY, lightNegY);
            }
            else
            {
                mesh = MeshGenerator.GenerateMesh(ChunkData, registry, GetNeighborBlock);
            }

            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;
        }

        public void ApplyMesh(Mesh mesh)
        {
            if (meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
            }
            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (ChunkData != null)
            {
                ChunkData.Dispose();
                ChunkData = null;
            }
        }

        /// <summary>
        /// Sets a block at local coordinates and rebuilds the mesh.
        /// </summary>
        public void SetBlock(int localX, int localY, int localZ, byte blockType)
        {
            ChunkData.SetBlock(localX, localY, localZ, blockType);
            RebuildMesh();
        }

        /// <summary>
        /// Callback for MeshGenerator managed path to query blocks in neighboring chunks.
        /// Coordinates are in this chunk's local space but may be out of bounds.
        /// </summary>
        private byte GetNeighborBlock(int localX, int localY, int localZ)
        {
            if (ChunkData.IsInBounds(localX, localY, localZ))
            {
                return ChunkData.GetBlock(localX, localY, localZ);
            }

            if (world == null)
                return BlockType.Air;

            // Convert to world position and query the world
            Vector3Int worldPos = new Vector3Int(
                Coord.x * VoxelConstants.ChunkWidth + localX,
                localY,
                Coord.z * VoxelConstants.ChunkDepth + localZ
            );

            return world.GetBlock(worldPos);
        }
    }
}
