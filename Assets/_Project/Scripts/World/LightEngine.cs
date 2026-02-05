using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Utilities;

namespace VoxelEngine.World
{
    /// <summary>
    /// Burst-compiled job for single-chunk sunlight initialization.
    /// Phase 1: Column fill — scan Y top→bottom, set sunlight=15 until opaque.
    /// Phase 2: Horizontal BFS spread with level-1 decay.
    /// Downward propagation from sunlight==15 has NO decay.
    /// </summary>
    [BurstCompile]
    public struct SunlightInitJob : IJob
    {
        [ReadOnly] public NativeArray<byte> blocks;
        [ReadOnly] public NativeArray<BlockInfo> blockInfos;
        public NativeArray<byte> lightMap;

        private int GetIndex(int x, int y, int z)
        {
            return x + VoxelConstants.ChunkWidth * (y + VoxelConstants.ChunkHeight * z);
        }

        private byte GetSunlight(int idx)
        {
            return (byte)((lightMap[idx] >> 4) & 0xF);
        }

        private void SetSunlight(int idx, byte val)
        {
            lightMap[idx] = (byte)((val << 4) | (lightMap[idx] & 0xF));
        }

        public void Execute()
        {
            // Pack x,y,z,level into int4 for NativeQueue
            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);

            // 6-directional offsets: +X, -X, +Y, -Y, +Z, -Z
            var dirs = new NativeArray<int3>(6, Allocator.Temp);
            dirs[0] = new int3(1, 0, 0);
            dirs[1] = new int3(-1, 0, 0);
            dirs[2] = new int3(0, 1, 0);
            dirs[3] = new int3(0, -1, 0);
            dirs[4] = new int3(0, 0, 1);
            dirs[5] = new int3(0, 0, -1);

            // Phase 1: Column fill
            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                {
                    for (int y = VoxelConstants.ChunkHeight - 1; y >= 0; y--)
                    {
                        int idx = GetIndex(x, y, z);
                        byte blockType = blocks[idx];
                        BlockInfo info = blockInfos[blockType];

                        if (info.isSolid && !info.isTransparent)
                        {
                            break;
                        }

                        SetSunlight(idx, (byte)VoxelConstants.MaxLightLevel);
                        bfsQueue.Enqueue(new int4(x, y, z, VoxelConstants.MaxLightLevel));
                    }
                }
            }

            // Phase 2: Horizontal BFS spread (within this chunk only)
            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                int cx = current.x, cy = current.y, cz = current.z;
                int level = current.w;
                if (level <= 1) continue;

                for (int d = 0; d < 6; d++)
                {
                    int3 dir = dirs[d];
                    int nx = cx + dir.x;
                    int ny = cy + dir.y;
                    int nz = cz + dir.z;

                    if (nx < 0 || nx >= VoxelConstants.ChunkWidth ||
                        ny < 0 || ny >= VoxelConstants.ChunkHeight ||
                        nz < 0 || nz >= VoxelConstants.ChunkDepth)
                        continue;

                    int nIdx = GetIndex(nx, ny, nz);
                    byte blockType = blocks[nIdx];
                    BlockInfo neighborInfo = blockInfos[blockType];

                    if (neighborInfo.isSolid && !neighborInfo.isTransparent) continue;

                    // Downward from sunlight 15 has no decay
                    byte newLevel;
                    if (dir.y == -1 && level == VoxelConstants.MaxLightLevel)
                    {
                        newLevel = (byte)VoxelConstants.MaxLightLevel;
                    }
                    else
                    {
                        newLevel = (byte)(level - 1);
                    }

                    byte currentSun = GetSunlight(nIdx);
                    if (newLevel > currentSun)
                    {
                        SetSunlight(nIdx, newLevel);
                        bfsQueue.Enqueue(new int4(nx, ny, nz, newLevel));
                    }
                }
            }

            dirs.Dispose();
            bfsQueue.Dispose();
        }
    }

    /// <summary>
    /// Static class that handles all lighting calculations for the voxel world.
    /// Uses flood-fill BFS for sunlight and block light propagation/removal.
    /// InitializeSunlight uses Burst-compiled SunlightInitJob.
    /// Cross-chunk methods run on main thread with NativeQueue-based BFS.
    /// </summary>
    public static class LightEngine
    {
        // 6-directional offsets: +X, -X, +Y, -Y, +Z, -Z
        private static readonly Vector3Int[] Directions = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        // ==================== Sunlight Propagation ====================

        /// <summary>
        /// Initialize sunlight for a single chunk using Burst-compiled SunlightInitJob.
        /// Phase 1: Column fill — for each (x,z), scan Y top→0, set sunlight=15 until opaque.
        /// Phase 2: Horizontal BFS spread with level-1 decay.
        /// Special: downward propagation from sunlight==15 has NO decay.
        /// </summary>
        public static void InitializeSunlight(ChunkData chunkData, NativeArray<BlockInfo> blockInfos)
        {
            if (chunkData == null || !blockInfos.IsCreated) return;

            var job = new SunlightInitJob
            {
                blocks = chunkData.Blocks,
                blockInfos = blockInfos,
                lightMap = chunkData.LightMap
            };
            job.Schedule().Complete();
        }

        public static HashSet<ChunkCoord> PropagateSunlightCrossChunk(VoxelWorld world, ChunkCoord coord)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null || chunk.ChunkData == null) return dirtyChunks;

            // int4: x=worldX, y=worldY, z=worldZ, w=level
            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);
            ChunkData data = chunk.ChunkData;
            NativeArray<BlockInfo> blockInfos = world.BlockInfos;

            for (int y = 0; y < VoxelConstants.ChunkHeight; y++)
            {
                for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                {
                    EnqueueEdge(data, coord, 0, y, z, ref bfsQueue);
                    EnqueueEdge(data, coord, VoxelConstants.ChunkWidth - 1, y, z, ref bfsQueue);
                }
                for (int x = 1; x < VoxelConstants.ChunkWidth - 1; x++)
                {
                    EnqueueEdge(data, coord, x, y, 0, ref bfsQueue);
                    EnqueueEdge(data, coord, x, y, VoxelConstants.ChunkDepth - 1, ref bfsQueue);
                }
            }

            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                {
                    EnqueueEdge(data, coord, x, 0, z, ref bfsQueue);
                    EnqueueEdge(data, coord, x, VoxelConstants.ChunkHeight - 1, z, ref bfsQueue);
                }
            }

            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte newLevel;
                    if (Directions[d].y == -1 && level == VoxelConstants.MaxLightLevel)
                    {
                        newLevel = (byte)VoxelConstants.MaxLightLevel;
                    }
                    else
                    {
                        newLevel = (byte)(level - 1);
                    }

                    byte currentSun = nChunk.ChunkData.GetSunlight(local.x, local.y, local.z);
                    if (newLevel > currentSun)
                    {
                        nChunk.ChunkData.SetSunlight(local.x, local.y, local.z, newLevel);
                        bfsQueue.Enqueue(new int4(npos.x, npos.y, npos.z, newLevel));

                        if (!nCoord.Equals(coord))
                        {
                            dirtyChunks.Add(nCoord);
                        }
                    }
                }
            }

            bfsQueue.Dispose();
            return dirtyChunks;
        }

        private static void EnqueueEdge(ChunkData data, ChunkCoord coord, int x, int y, int z,
            ref NativeQueue<int4> queue)
        {
            byte sun = data.GetSunlight(x, y, z);
            if (sun > 0)
            {
                queue.Enqueue(new int4(
                    coord.x * VoxelConstants.ChunkWidth + x,
                    coord.y * VoxelConstants.ChunkHeight + y,
                    coord.z * VoxelConstants.ChunkDepth + z,
                    sun));
            }
        }

        // ==================== Block Light Propagation ====================

        public static HashSet<ChunkCoord> PropagateBlockLight(VoxelWorld world, Vector3Int worldPos, byte emission)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            NativeArray<BlockInfo> blockInfos = world.BlockInfos;

            SetWorldBlockLight(world, worldPos, emission, dirtyChunks);

            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);
            bfsQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, emission));

            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                byte newLevel = (byte)(level - 1);

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte currentBlock = nChunk.ChunkData.GetBlockLight(local.x, local.y, local.z);
                    if (newLevel > currentBlock)
                    {
                        nChunk.ChunkData.SetBlockLight(local.x, local.y, local.z, newLevel);
                        dirtyChunks.Add(nCoord);
                        bfsQueue.Enqueue(new int4(npos.x, npos.y, npos.z, newLevel));
                    }
                }
            }

            bfsQueue.Dispose();
            return dirtyChunks;
        }

        public static void InitializeBlockLights(ChunkData chunkData, NativeArray<BlockInfo> blockInfos,
            ChunkCoord coord, VoxelWorld world)
        {
            if (chunkData == null || !blockInfos.IsCreated) return;

            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelConstants.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
                    {
                        byte blockType = chunkData.GetBlock(x, y, z);
                        BlockInfo info = blockInfos[blockType];
                        if (info.emission > 0)
                        {
                            Vector3Int worldPos = new Vector3Int(
                                coord.x * VoxelConstants.ChunkWidth + x,
                                coord.y * VoxelConstants.ChunkHeight + y,
                                coord.z * VoxelConstants.ChunkDepth + z);
                            PropagateBlockLight(world, worldPos, info.emission);
                        }
                    }
                }
            }
        }

        // ==================== Light Removal ====================

        public static HashSet<ChunkCoord> RemoveSunlight(VoxelWorld world, Vector3Int worldPos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            NativeArray<BlockInfo> blockInfos = world.BlockInfos;

            ChunkCoord srcCoord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk srcChunk = world.GetChunk(srcCoord);
            if (srcChunk == null || srcChunk.ChunkData == null) return dirtyChunks;

            Vector3Int srcLocal = ChunkCoord.WorldToLocal(worldPos);
            byte oldSun = srcChunk.ChunkData.GetSunlight(srcLocal.x, srcLocal.y, srcLocal.z);
            if (oldSun == 0) return dirtyChunks;

            var removeQueue = new NativeQueue<int4>(Allocator.Temp);
            var refillQueue = new NativeQueue<int4>(Allocator.Temp);

            srcChunk.ChunkData.SetSunlight(srcLocal.x, srcLocal.y, srcLocal.z, 0);
            dirtyChunks.Add(srcCoord);
            removeQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, oldSun));

            while (removeQueue.Count > 0)
            {
                int4 current = removeQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int oldLevel = current.w;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte neighborSun = nChunk.ChunkData.GetSunlight(local.x, local.y, local.z);

                    if (neighborSun == 0) continue;

                    bool removeIfMaximum = Directions[d].y == -1 &&
                                           oldLevel == VoxelConstants.MaxLightLevel &&
                                           neighborSun == VoxelConstants.MaxLightLevel;

                    if (neighborSun < oldLevel || removeIfMaximum)
                    {
                        nChunk.ChunkData.SetSunlight(local.x, local.y, local.z, 0);
                        dirtyChunks.Add(nCoord);
                        removeQueue.Enqueue(new int4(npos.x, npos.y, npos.z, neighborSun));
                    }
                    else if (neighborSun >= oldLevel)
                    {
                        refillQueue.Enqueue(new int4(npos.x, npos.y, npos.z, neighborSun));
                    }
                }
            }

            while (refillQueue.Count > 0)
            {
                int4 current = refillQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte newLevel;
                    if (Directions[d].y == -1 && level == VoxelConstants.MaxLightLevel)
                    {
                        newLevel = (byte)VoxelConstants.MaxLightLevel;
                    }
                    else
                    {
                        newLevel = (byte)(level - 1);
                    }

                    byte currentSun = nChunk.ChunkData.GetSunlight(local.x, local.y, local.z);
                    if (newLevel > currentSun)
                    {
                        nChunk.ChunkData.SetSunlight(local.x, local.y, local.z, newLevel);
                        dirtyChunks.Add(nCoord);
                        refillQueue.Enqueue(new int4(npos.x, npos.y, npos.z, newLevel));
                    }
                }
            }

            removeQueue.Dispose();
            refillQueue.Dispose();
            return dirtyChunks;
        }

        public static HashSet<ChunkCoord> RemoveBlockLight(VoxelWorld world, Vector3Int worldPos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            NativeArray<BlockInfo> blockInfos = world.BlockInfos;

            ChunkCoord srcCoord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk srcChunk = world.GetChunk(srcCoord);
            if (srcChunk == null || srcChunk.ChunkData == null) return dirtyChunks;

            Vector3Int srcLocal = ChunkCoord.WorldToLocal(worldPos);
            byte oldBlock = srcChunk.ChunkData.GetBlockLight(srcLocal.x, srcLocal.y, srcLocal.z);
            if (oldBlock == 0) return dirtyChunks;

            var removeQueue = new NativeQueue<int4>(Allocator.Temp);
            var refillQueue = new NativeQueue<int4>(Allocator.Temp);

            srcChunk.ChunkData.SetBlockLight(srcLocal.x, srcLocal.y, srcLocal.z, 0);
            dirtyChunks.Add(srcCoord);
            removeQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, oldBlock));

            while (removeQueue.Count > 0)
            {
                int4 current = removeQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int oldLevel = current.w;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte neighborBlock = nChunk.ChunkData.GetBlockLight(local.x, local.y, local.z);

                    if (neighborBlock == 0) continue;

                    if (neighborBlock < oldLevel)
                    {
                        nChunk.ChunkData.SetBlockLight(local.x, local.y, local.z, 0);
                        dirtyChunks.Add(nCoord);
                        removeQueue.Enqueue(new int4(npos.x, npos.y, npos.z, neighborBlock));
                    }
                    else
                    {
                        refillQueue.Enqueue(new int4(npos.x, npos.y, npos.z, neighborBlock));
                    }
                }
            }

            while (refillQueue.Count > 0)
            {
                int4 current = refillQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                byte newLevel = (byte)(level - 1);

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte currentBlock = nChunk.ChunkData.GetBlockLight(local.x, local.y, local.z);
                    if (newLevel > currentBlock)
                    {
                        nChunk.ChunkData.SetBlockLight(local.x, local.y, local.z, newLevel);
                        dirtyChunks.Add(nCoord);
                        refillQueue.Enqueue(new int4(npos.x, npos.y, npos.z, newLevel));
                    }
                }
            }

            removeQueue.Dispose();
            refillQueue.Dispose();
            return dirtyChunks;
        }

        // ==================== Block Change Orchestrator ====================

        public static HashSet<ChunkCoord> OnBlockChanged(VoxelWorld world, Vector3Int worldPos,
            byte oldBlockType, byte newBlockType, NativeArray<BlockInfo> blockInfos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            BlockInfo oldInfo = blockInfos[oldBlockType];
            BlockInfo newInfo = blockInfos[newBlockType];

            if (oldInfo.emission > 0)
            {
                dirtyChunks.UnionWith(RemoveBlockLight(world, worldPos));
            }

            if (newInfo.isSolid && !newInfo.isTransparent)
            {
                dirtyChunks.UnionWith(RemoveSunlight(world, worldPos));
                dirtyChunks.UnionWith(RemoveBlockLight(world, worldPos));
            }

            if (oldInfo.isSolid && !oldInfo.isTransparent)
            {
                dirtyChunks.UnionWith(RecalculateSunlightColumn(world, worldPos, blockInfos));
                dirtyChunks.UnionWith(PropagateNeighborLight(world, worldPos, blockInfos));
            }

            if (newInfo.emission > 0)
            {
                dirtyChunks.UnionWith(PropagateBlockLight(world, worldPos, newInfo.emission));
            }

            return dirtyChunks;
        }

        private static HashSet<ChunkCoord> RecalculateSunlightColumn(VoxelWorld world, Vector3Int worldPos,
            NativeArray<BlockInfo> blockInfos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();

            Vector3Int abovePos = worldPos + Vector3Int.up;
            if (abovePos.y >= VoxelConstants.ColumnHeight) abovePos.y = VoxelConstants.ColumnHeight - 1;

            byte sunAbove = GetWorldSunlight(world, abovePos);
            if (sunAbove == 0)
            {
                dirtyChunks.UnionWith(PropagateNeighborSunlight(world, worldPos, blockInfos));
                return dirtyChunks;
            }

            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);

            byte newLevel;
            if (sunAbove == VoxelConstants.MaxLightLevel)
            {
                newLevel = (byte)VoxelConstants.MaxLightLevel;
            }
            else
            {
                newLevel = (byte)(sunAbove - 1);
            }

            SetWorldSunlight(world, worldPos, newLevel, dirtyChunks);
            bfsQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, newLevel));

            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte nLevel;
                    if (Directions[d].y == -1 && level == VoxelConstants.MaxLightLevel)
                    {
                        nLevel = (byte)VoxelConstants.MaxLightLevel;
                    }
                    else
                    {
                        nLevel = (byte)(level - 1);
                    }

                    byte currentSun = nChunk.ChunkData.GetSunlight(local.x, local.y, local.z);
                    if (nLevel > currentSun)
                    {
                        nChunk.ChunkData.SetSunlight(local.x, local.y, local.z, nLevel);
                        dirtyChunks.Add(nCoord);
                        bfsQueue.Enqueue(new int4(npos.x, npos.y, npos.z, nLevel));
                    }
                }
            }

            bfsQueue.Dispose();
            return dirtyChunks;
        }

        private static HashSet<ChunkCoord> PropagateNeighborSunlight(VoxelWorld world, Vector3Int worldPos,
            NativeArray<BlockInfo> blockInfos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);

            for (int d = 0; d < Directions.Length; d++)
            {
                Vector3Int npos = worldPos + Directions[d];
                if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                byte neighborSun = GetWorldSunlight(world, npos);
                if (neighborSun <= 1) continue;

                byte newLevel;
                if (Directions[d].y == 1 && neighborSun == VoxelConstants.MaxLightLevel)
                {
                    newLevel = (byte)VoxelConstants.MaxLightLevel;
                }
                else
                {
                    newLevel = (byte)(neighborSun - 1);
                }

                byte currentSun = GetWorldSunlight(world, worldPos);
                if (newLevel > currentSun)
                {
                    SetWorldSunlight(world, worldPos, newLevel, dirtyChunks);
                    bfsQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, newLevel));
                }
            }

            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte nLevel;
                    if (Directions[d].y == -1 && level == VoxelConstants.MaxLightLevel)
                    {
                        nLevel = (byte)VoxelConstants.MaxLightLevel;
                    }
                    else
                    {
                        nLevel = (byte)(level - 1);
                    }

                    byte currentSun = nChunk.ChunkData.GetSunlight(local.x, local.y, local.z);
                    if (nLevel > currentSun)
                    {
                        nChunk.ChunkData.SetSunlight(local.x, local.y, local.z, nLevel);
                        dirtyChunks.Add(nCoord);
                        bfsQueue.Enqueue(new int4(npos.x, npos.y, npos.z, nLevel));
                    }
                }
            }

            bfsQueue.Dispose();
            return dirtyChunks;
        }

        private static HashSet<ChunkCoord> PropagateNeighborLight(VoxelWorld world, Vector3Int worldPos,
            NativeArray<BlockInfo> blockInfos)
        {
            var dirtyChunks = new HashSet<ChunkCoord>();
            var bfsQueue = new NativeQueue<int4>(Allocator.Temp);

            for (int d = 0; d < Directions.Length; d++)
            {
                Vector3Int npos = worldPos + Directions[d];
                if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                byte neighborBlock = GetWorldBlockLight(world, npos);
                if (neighborBlock <= 1) continue;

                byte newLevel = (byte)(neighborBlock - 1);
                byte currentBlock = GetWorldBlockLight(world, worldPos);
                if (newLevel > currentBlock)
                {
                    SetWorldBlockLight(world, worldPos, newLevel, dirtyChunks);
                    bfsQueue.Enqueue(new int4(worldPos.x, worldPos.y, worldPos.z, newLevel));
                }
            }

            while (bfsQueue.Count > 0)
            {
                int4 current = bfsQueue.Dequeue();
                Vector3Int pos = new Vector3Int(current.x, current.y, current.z);
                int level = current.w;
                if (level <= 1) continue;

                byte newLevel = (byte)(level - 1);

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3Int npos = pos + Directions[d];
                    if (npos.y < 0 || npos.y >= VoxelConstants.ColumnHeight) continue;

                    ChunkCoord nCoord = ChunkCoord.FromWorldPosition(new Vector3(npos.x, npos.y, npos.z));
                    Chunk nChunk = world.GetChunk(nCoord);
                    if (nChunk == null || nChunk.ChunkData == null) continue;

                    Vector3Int local = ChunkCoord.WorldToLocal(npos);
                    byte blockType = nChunk.ChunkData.GetBlock(local.x, local.y, local.z);
                    BlockInfo info = blockInfos[blockType];
                    if (info.isSolid && !info.isTransparent) continue;

                    byte currentBlock = nChunk.ChunkData.GetBlockLight(local.x, local.y, local.z);
                    if (newLevel > currentBlock)
                    {
                        nChunk.ChunkData.SetBlockLight(local.x, local.y, local.z, newLevel);
                        dirtyChunks.Add(nCoord);
                        bfsQueue.Enqueue(new int4(npos.x, npos.y, npos.z, newLevel));
                    }
                }
            }

            bfsQueue.Dispose();
            return dirtyChunks;
        }

        private static byte GetWorldSunlight(VoxelWorld world, Vector3Int worldPos)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight) return 0;

            ChunkCoord coord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null || chunk.ChunkData == null) return 0;

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            return chunk.ChunkData.GetSunlight(local.x, local.y, local.z);
        }

        private static byte GetWorldBlockLight(VoxelWorld world, Vector3Int worldPos)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight) return 0;

            ChunkCoord coord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null || chunk.ChunkData == null) return 0;

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            return chunk.ChunkData.GetBlockLight(local.x, local.y, local.z);
        }

        private static void SetWorldSunlight(VoxelWorld world, Vector3Int worldPos, byte value,
            HashSet<ChunkCoord> dirtyChunks)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight) return;

            ChunkCoord coord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null || chunk.ChunkData == null) return;

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            chunk.ChunkData.SetSunlight(local.x, local.y, local.z, value);
            dirtyChunks.Add(coord);
        }

        private static void SetWorldBlockLight(VoxelWorld world, Vector3Int worldPos, byte value,
            HashSet<ChunkCoord> dirtyChunks)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight) return;

            ChunkCoord coord = ChunkCoord.FromWorldPosition(new Vector3(worldPos.x, worldPos.y, worldPos.z));
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null || chunk.ChunkData == null) return;

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            chunk.ChunkData.SetBlockLight(local.x, local.y, local.z, value);
            dirtyChunks.Add(coord);
        }
    }
}
