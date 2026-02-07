using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Meshing;
using VoxelEngine.Serialization;
using VoxelEngine.Utilities;

namespace VoxelEngine.World
{
    /// <summary>
    /// Indicates the origin of a block change for network synchronization.
    /// </summary>
    public enum BlockChangeSource
    {
        /// <summary>Local player action — fires OnBlockChanged event for network broadcast.</summary>
        Local,
        /// <summary>Received from network — applies change WITHOUT firing OnBlockChanged (prevents broadcast loop).</summary>
        Network,
        /// <summary>Host-authoritative confirmation — applies change and fires OnBlockChanged for broadcast.</summary>
        Authority
    }

    /// <summary>
    /// Central world manager that handles chunk loading, unloading, and block access.
    /// Maintains a dictionary of loaded chunks indexed by ChunkCoord.
    /// Supports save/load with RLE-compressed binary chunk files and world metadata.
    /// </summary>
    public class VoxelWorld : MonoBehaviour
    {
        [SerializeField] private BlockRegistry blockRegistry;
        [SerializeField] private Material voxelMaterial;
        [SerializeField] private int loadRadius = 4;
        [SerializeField] private int unloadRadius = 6;

        [Header("Streaming")]
        [Tooltip("Transform to track for chunk streaming. If null, no dynamic loading/unloading occurs.")]
        [SerializeField] private Transform trackTarget;

        [Header("Save/Load")]
        [Tooltip("Name of the world save folder.")]
        [SerializeField] private string worldName = "DefaultWorld";

        [Header("Performance")]
        [SerializeField] private int maxMeshBuildsPerFrame = 4;

        private Dictionary<ChunkCoord, Chunk> loadedChunks = new Dictionary<ChunkCoord, Chunk>();
        private MeshJobScheduler meshScheduler;
        private ChunkPool chunkPool;
        private readonly ConcurrentQueue<(ChunkCoord coord, byte[] blocks)> asyncLoadResults =
            new ConcurrentQueue<(ChunkCoord, byte[])>();

        public MeshJobScheduler MeshScheduler => meshScheduler;

        /// <summary>
        /// Blittable block info array for Burst/Jobs mesh generation.
        /// Built once on Awake, shared by all chunks. Disposed on OnDestroy.
        /// </summary>
        private NativeArray<BlockInfo> blockInfos;

        private WorldMetadata metadata;
        private float sessionStartTime;

        /// <summary>
        /// The world-space position used for chunk streaming decisions.
        /// Set via SetStreamingAnchor() — typically the camera's focal point on the ground,
        /// NOT the camera's transform.position (which is far away in isometric view).
        /// Falls back to trackTarget.position if not explicitly set.
        /// </summary>
        private Vector3 streamingAnchor;
        private bool hasStreamingAnchor;

        /// <summary>
        /// Cached chunk coordinate from the previous frame.
        /// Streaming only triggers when the anchor moves to a different chunk.
        /// </summary>
        private ChunkCoord lastStreamingCoord;
        private bool lastStreamingCoordValid;

        public BlockRegistry BlockRegistry => blockRegistry;

        // ==================== Network Events ====================

        /// <summary>Fired when a block is changed locally or by authority. NOT fired for Network source (prevents broadcast loop).</summary>
        public event Action<Vector3Int, byte, byte> OnBlockChanged;

        /// <summary>Fired when a chunk finishes loading (from disk or network).</summary>
        public event Action<ChunkCoord> OnChunkLoaded;

        /// <summary>Fired when a chunk is unloaded.</summary>
        public event Action<ChunkCoord> OnChunkUnloaded;

        // ==================== Multiplayer State ====================

        /// <summary>
        /// When true, this instance acts as a multiplayer client.
        /// Disables local chunk streaming and saving (controlled by network manager instead).
        /// </summary>
        public bool IsMultiplayerClient { get; private set; }

        public void SetMultiplayerClient(bool value)
        {
            IsMultiplayerClient = value;
        }

        /// <summary>
        /// The shared NativeArray of BlockInfo for Burst mesh generation.
        /// </summary>
        public NativeArray<BlockInfo> BlockInfos => blockInfos;

        /// <summary>
        /// The current world metadata, or null if not loaded/created.
        /// </summary>
        public WorldMetadata Metadata => metadata;

        /// <summary>
        /// Sets the transform to track for chunk loading/unloading.
        /// </summary>
        public void SetTrackTarget(Transform target)
        {
            trackTarget = target;
        }

        /// <summary>
        /// Returns the save directory path for the current world.
        /// </summary>
        public string SaveDirectory => Path.Combine(Application.persistentDataPath, "Worlds", worldName);

        private string MetadataFilePath => Path.Combine(SaveDirectory, "world.meta");

        private string GetChunkFilePath(ChunkCoord coord)
        {
            return Path.Combine(SaveDirectory, "chunks", $"chunk_{coord.x}_{coord.y}_{coord.z}.vkc");
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        private void Awake()
        {
            if (blockRegistry != null)
            {
                blockInfos = blockRegistry.BuildBlockInfoArray(Allocator.Persistent);
            }
            meshScheduler = new MeshJobScheduler();
            chunkPool = new ChunkPool(transform, gameObject.layer);

            sessionStartTime = Time.realtimeSinceStartup;

            // Try to load existing metadata, or create new
            metadata = WorldMetadata.LoadFromFile(MetadataFilePath);
            if (metadata == null)
            {
                metadata = WorldMetadata.CreateNew(worldName);
            }
        }

        private void OnDestroy()
        {
            meshScheduler?.CompleteAll();
            chunkPool?.DestroyAll();
            if (blockInfos.IsCreated)
            {
                blockInfos.Dispose();
            }
        }

        private void OnApplicationQuit()
        {
            if (!IsMultiplayerClient)
            {
                SaveWorld();
            }
        }

        private void Update()
        {
            meshScheduler?.Update(maxMeshBuildsPerFrame);
            ProcessAsyncLoadResults();

            // Multiplayer clients don't do local chunk streaming — network manager controls it
            if (IsMultiplayerClient) return;

            if (trackTarget == null) return;

            Vector3 anchor = hasStreamingAnchor ? streamingAnchor : trackTarget.position;
            ChunkCoord currentCoord = ChunkCoord.FromWorldPosition(anchor);

            if (lastStreamingCoordValid && currentCoord.x == lastStreamingCoord.x && currentCoord.z == lastStreamingCoord.z)
                return;

            lastStreamingCoord = currentCoord;
            lastStreamingCoordValid = true;

            LoadChunksAround(anchor);
            UnloadDistantChunks(anchor);
        }

        private void ProcessAsyncLoadResults()
        {
            int processed = 0;
            while (processed < maxMeshBuildsPerFrame && asyncLoadResults.TryDequeue(out var result))
            {
                pendingAsyncLoads.TryRemove(result.coord, out _);

                if (loadedChunks.ContainsKey(result.coord)) continue;

                ChunkData data = new ChunkData();
                if (result.blocks != null)
                {
                    data.SetRawBlocks(result.blocks);
                    data.ClearDirty();
                }

                if (data.IsEmpty())
                {
                    data.Dispose();
                    continue;
                }

                GameObject chunkObj = chunkPool.Get($"Chunk_{result.coord.x}_{result.coord.y}_{result.coord.z}");
                Chunk chunk = chunkObj.GetComponent<Chunk>();
                if (chunk == null) chunk = chunkObj.AddComponent<Chunk>();

                chunk.Initialize(result.coord, data, blockRegistry, voxelMaterial, this, buildMesh: false);
                loadedChunks[result.coord] = chunk;

                if (blockInfos.IsCreated)
                {
                    LightEngine.InitializeSunlight(data, blockInfos);
                    LightEngine.InitializeBlockLights(data, blockInfos, result.coord, this);
                    LightEngine.PropagateSunlightCrossChunk(this, result.coord);
                }

                chunk.RebuildMesh();
                OnChunkLoaded?.Invoke(result.coord);
                processed++;
            }
        }

        /// <summary>
        /// Sets the world-space position used for chunk streaming decisions.
        /// Should be the camera's ground-level focal point, NOT the camera transform position.
        /// </summary>
        public void SetStreamingAnchor(Vector3 worldPos)
        {
            streamingAnchor = worldPos;
            hasStreamingAnchor = true;
        }

        /// <summary>
        /// Returns the chunk at the given coordinate, or null if not loaded.
        /// </summary>
        public Chunk GetChunk(ChunkCoord coord)
        {
            loadedChunks.TryGetValue(coord, out Chunk chunk);
            return chunk;
        }

        /// <summary>
        /// Loads (creates) a chunk at the given coordinate.
        /// If already loaded, returns the existing chunk.
        /// Attempts to load from disk first; creates empty chunk if no save exists.
        /// </summary>
        public Chunk LoadChunk(ChunkCoord coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk existing))
                return existing;

            ChunkData data = new ChunkData();

            // Try to load chunk data from disk
            byte[] savedBlocks = ChunkSerializer.LoadFromFile(GetChunkFilePath(coord));
            if (savedBlocks != null)
            {
                data.SetRawBlocks(savedBlocks);
                data.ClearDirty(); // Just loaded, not dirty
            }

            GameObject chunkObj = chunkPool.Get($"Chunk_{coord.x}_{coord.y}_{coord.z}");

            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null) chunk = chunkObj.AddComponent<Chunk>();

            chunk.Initialize(coord, data, blockRegistry, voxelMaterial, this, buildMesh: false);
            loadedChunks[coord] = chunk;

            if (blockInfos.IsCreated)
            {
                LightEngine.InitializeSunlight(data, blockInfos);
                LightEngine.InitializeBlockLights(data, blockInfos, coord, this);
                LightEngine.PropagateSunlightCrossChunk(this, coord);
            }

            chunk.RebuildMesh();
            OnChunkLoaded?.Invoke(coord);
            return chunk;
        }

        /// <summary>
        /// Loads a chunk from network-received data (no disk I/O).
        /// Used by clients to receive chunks from the host.
        /// </summary>
        public void LoadChunkFromNetwork(ChunkCoord coord, byte[] rawBlocks)
        {
            if (rawBlocks == null || rawBlocks.Length != VoxelConstants.ChunkBlockCount)
            {
                Debug.LogWarning($"[VoxelWorld] LoadChunkFromNetwork: Invalid block data for {coord}");
                return;
            }

            // If already loaded, update existing chunk data
            if (loadedChunks.TryGetValue(coord, out Chunk existing))
            {
                existing.ChunkData.SetRawBlocks(rawBlocks);
                existing.ChunkData.ClearDirty();
                if (blockInfos.IsCreated)
                {
                    LightEngine.InitializeSunlight(existing.ChunkData, blockInfos);
                    LightEngine.InitializeBlockLights(existing.ChunkData, blockInfos, coord, this);
                    LightEngine.PropagateSunlightCrossChunk(this, coord);
                }
                existing.RebuildMesh();
                return;
            }

            ChunkData data = new ChunkData();
            data.SetRawBlocks(rawBlocks);
            data.ClearDirty();

            if (data.IsEmpty())
            {
                data.Dispose();
                return;
            }

            GameObject chunkObj = chunkPool.Get($"Chunk_{coord.x}_{coord.y}_{coord.z}");
            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null) chunk = chunkObj.AddComponent<Chunk>();

            chunk.Initialize(coord, data, blockRegistry, voxelMaterial, this, buildMesh: false);
            loadedChunks[coord] = chunk;

            if (blockInfos.IsCreated)
            {
                LightEngine.InitializeSunlight(data, blockInfos);
                LightEngine.InitializeBlockLights(data, blockInfos, coord, this);
                LightEngine.PropagateSunlightCrossChunk(this, coord);
            }

            chunk.RebuildMesh();
            OnChunkLoaded?.Invoke(coord);
        }

        /// <summary>
        /// Unloads (destroys) the chunk at the given coordinate.
        /// Saves dirty chunks to disk before unloading.
        /// </summary>
        public void UnloadChunk(ChunkCoord coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk chunk))
            {
                OnChunkUnloaded?.Invoke(coord);
                meshScheduler?.CompleteAndRemove(coord);

                if (chunk.ChunkData != null && chunk.ChunkData.IsDirty)
                {
                    SaveChunk(coord, chunk);
                }

                loadedChunks.Remove(coord);
                if (chunk.ChunkData != null)
                {
                    chunk.ChunkData.Dispose();
                }
                chunkPool.Return(chunk.gameObject);
            }
        }

        /// <summary>
        /// Gets a block at the given world position.
        /// Returns Air if the chunk is not loaded or position is invalid.
        /// </summary>
        public byte GetBlock(Vector3Int worldPos)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight)
                return BlockType.Air;

            ChunkCoord coord = new ChunkCoord(
                FloorDiv(worldPos.x, VoxelConstants.ChunkWidth),
                FloorDiv(worldPos.y, VoxelConstants.ChunkHeight),
                FloorDiv(worldPos.z, VoxelConstants.ChunkDepth));
            Chunk chunk = GetChunk(coord);

            if (chunk == null)
                return BlockType.Air;

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            return chunk.ChunkData.GetBlock(local.x, local.y, local.z);
        }

        /// <summary>
        /// Sets a block at the given world position (defaults to Local source).
        /// Automatically updates lighting and rebuilds the affected chunk and any adjacent chunks.
        /// </summary>
        public void SetBlock(Vector3Int worldPos, byte blockType)
        {
            SetBlock(worldPos, blockType, BlockChangeSource.Local);
        }

        /// <summary>
        /// Sets a block at the given world position with a specified change source.
        /// Local/Authority sources fire OnBlockChanged; Network source does NOT (prevents broadcast loop).
        /// </summary>
        public void SetBlock(Vector3Int worldPos, byte blockType, BlockChangeSource source)
        {
            if (worldPos.y < 0 || worldPos.y >= VoxelConstants.ColumnHeight)
                return;

            ChunkCoord coord = new ChunkCoord(
                FloorDiv(worldPos.x, VoxelConstants.ChunkWidth),
                FloorDiv(worldPos.y, VoxelConstants.ChunkHeight),
                FloorDiv(worldPos.z, VoxelConstants.ChunkDepth));
            Chunk chunk = GetChunk(coord);

            if (chunk == null)
            {
                if (blockType != BlockType.Air)
                {
                    chunk = LoadChunk(coord);
                }
                else
                {
                    return;
                }
            }

            Vector3Int local = ChunkCoord.WorldToLocal(worldPos);
            byte oldBlockType = chunk.ChunkData.GetBlock(local.x, local.y, local.z);

            chunk.ChunkData.SetBlock(local.x, local.y, local.z, blockType);

            // Update lighting
            HashSet<ChunkCoord> dirtyChunks = new HashSet<ChunkCoord>();
            if (blockInfos.IsCreated)
            {
                dirtyChunks = LightEngine.OnBlockChanged(this, worldPos, oldBlockType, blockType, blockInfos);
            }

            chunk.RebuildMesh();

            foreach (var dirtyCoord in dirtyChunks)
            {
                if (dirtyCoord.Equals(coord)) continue;
                Chunk dirtyChunk = GetChunk(dirtyCoord);
                if (dirtyChunk != null) dirtyChunk.RebuildMesh();
            }

            RebuildAdjacentChunks(local, coord);

            // Fire event for Local and Authority sources (NOT for Network — prevents broadcast loop)
            if (source != BlockChangeSource.Network)
            {
                OnBlockChanged?.Invoke(worldPos, oldBlockType, blockType);
            }
        }

        public void LoadChunksAround(Vector3 worldPos)
        {
            ChunkCoord center = ChunkCoord.FromWorldPosition(worldPos);

            for (int x = -loadRadius; x <= loadRadius; x++)
            {
                for (int z = -loadRadius; z <= loadRadius; z++)
                {
                    for (int y = 0; y < VoxelConstants.SubChunksPerColumn; y++)
                    {
                        ChunkCoord coord = new ChunkCoord(center.x + x, y, center.z + z);
                        if (!loadedChunks.ContainsKey(coord) && !pendingAsyncLoads.ContainsKey(coord))
                        {
                            pendingAsyncLoads.TryAdd(coord, 0);
                            string filePath = GetChunkFilePath(coord);
                            ChunkCoord capturedCoord = coord;
                            ChunkSerializer.LoadFromFileAsync(filePath).ContinueWith(task =>
                            {
                                asyncLoadResults.Enqueue((capturedCoord, task.Result));
                            });
                        }
                    }
                }
            }
        }

        private readonly ConcurrentDictionary<ChunkCoord, byte> pendingAsyncLoads = new ConcurrentDictionary<ChunkCoord, byte>();

        private Chunk LoadChunkWithoutMesh(ChunkCoord coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk existing))
                return existing;

            ChunkData data = new ChunkData();

            byte[] savedBlocks = ChunkSerializer.LoadFromFile(GetChunkFilePath(coord));
            if (savedBlocks != null)
            {
                data.SetRawBlocks(savedBlocks);
                data.ClearDirty();
            }

            if (data.IsEmpty())
            {
                data.Dispose();
                return null;
            }

            GameObject chunkObj = chunkPool.Get($"Chunk_{coord.x}_{coord.y}_{coord.z}");

            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null) chunk = chunkObj.AddComponent<Chunk>();

            chunk.Initialize(coord, data, blockRegistry, voxelMaterial, this, buildMesh: false);

            loadedChunks[coord] = chunk;
            return chunk;
        }

        /// <summary>
        /// Checks if a block is on a chunk boundary and rebuilds neighboring chunk meshes.
        /// </summary>
        private void RebuildAdjacentChunks(Vector3Int local, ChunkCoord coord)
        {
            if (local.x == 0)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x - 1, coord.y, coord.z));
            else if (local.x == VoxelConstants.ChunkWidth - 1)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x + 1, coord.y, coord.z));

            if (local.y == 0)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x, coord.y - 1, coord.z));
            else if (local.y == VoxelConstants.ChunkHeight - 1)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x, coord.y + 1, coord.z));

            if (local.z == 0)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x, coord.y, coord.z - 1));
            else if (local.z == VoxelConstants.ChunkDepth - 1)
                RebuildNeighborIfLoaded(new ChunkCoord(coord.x, coord.y, coord.z + 1));
        }

        private void RebuildNeighborIfLoaded(ChunkCoord coord)
        {
            Chunk neighbor = GetChunk(coord);
            if (neighbor != null)
            {
                neighbor.RebuildMesh();
            }
        }

        /// <summary>
        /// Returns the NativeArray of blocks for a loaded chunk, or default if not loaded.
        /// Used by Chunk.RebuildMesh to pass neighbor data to MeshGenJob.
        /// </summary>
        public NativeArray<byte> GetChunkBlocks(ChunkCoord coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk chunk) && chunk.ChunkData != null)
            {
                return chunk.ChunkData.Blocks;
            }
            return default;
        }

        /// <summary>
        /// Returns the NativeArray of lightMap for a loaded chunk, or default if not loaded.
        /// Used by Chunk.RebuildMesh to pass neighbor light data to MeshGenJob.
        /// </summary>
        public NativeArray<byte> GetChunkLightMap(ChunkCoord coord)
        {
            if (loadedChunks.TryGetValue(coord, out Chunk chunk) && chunk.ChunkData != null)
            {
                return chunk.ChunkData.LightMap;
            }
            return default;
        }

        /// <summary>
        /// Unloads chunks that are farther than unloadRadius from the given position.
        /// </summary>
        private void UnloadDistantChunks(Vector3 worldPos)
        {
            ChunkCoord center = ChunkCoord.FromWorldPosition(worldPos);
            var toUnload = new List<ChunkCoord>();

            foreach (var kvp in loadedChunks)
            {
                int dx = kvp.Key.x - center.x;
                int dz = kvp.Key.z - center.z;
                // Distance check uses only XZ (all Y sub-chunks unload together)
                if (dx * dx + dz * dz > unloadRadius * unloadRadius)
                {
                    toUnload.Add(kvp.Key);
                }
            }

            foreach (var coord in toUnload)
            {
                UnloadChunk(coord);
            }
        }

        // ==================== Save/Load ====================

        /// <summary>
        /// Saves all dirty chunks and world metadata to disk.
        /// No-op for multiplayer clients (they don't own the world data).
        /// </summary>
        public void SaveWorld()
        {
            if (IsMultiplayerClient) return;

            int savedCount = 0;

            foreach (var kvp in loadedChunks)
            {
                if (kvp.Value.ChunkData != null && kvp.Value.ChunkData.IsDirty)
                {
                    SaveChunk(kvp.Key, kvp.Value);
                    savedCount++;
                }
            }

            // Update metadata
            if (metadata != null)
            {
                metadata.lastSavedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                metadata.playTimeSeconds += Time.realtimeSinceStartup - sessionStartTime;
                metadata.savedChunkCount = CountSavedChunks();
                metadata.SaveToFile(MetadataFilePath);
                sessionStartTime = Time.realtimeSinceStartup;
            }

            if (savedCount > 0)
            {
                Debug.Log($"[VoxelWorld] Saved {savedCount} dirty chunks to '{SaveDirectory}'.");
            }
        }

        private void SaveChunk(ChunkCoord coord, Chunk chunk)
        {
            if (chunk.ChunkData == null) return;

            byte[] rawBlocks = chunk.ChunkData.GetRawBlocks();
            string filePath = GetChunkFilePath(coord);

            // Ensure directory exists before async write
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            ChunkSerializer.SaveToFileAsync(filePath, rawBlocks);
            chunk.ChunkData.ClearDirty();
        }

        /// <summary>
        /// Checks if a saved chunk file exists for the given coordinate.
        /// </summary>
        public bool HasSavedChunk(ChunkCoord coord)
        {
            return File.Exists(GetChunkFilePath(coord));
        }

        /// <summary>
        /// Returns the number of chunk files on disk for the current world.
        /// </summary>
        private int CountSavedChunks()
        {
            string chunksDir = Path.Combine(SaveDirectory, "chunks");
            if (!Directory.Exists(chunksDir))
                return 0;

            return Directory.GetFiles(chunksDir, "chunk_*.vkc").Length;
        }

        /// <summary>
        /// Returns the number of currently loaded chunks.
        /// </summary>
        public int LoadedChunkCount => loadedChunks.Count;

        // ==================== Network Support ====================

        /// <summary>
        /// Returns all loaded chunks as RLE-serialized data.
        /// Used by host to stream chunks to late-joining clients.
        /// </summary>
        public Dictionary<ChunkCoord, byte[]> GetAllLoadedChunkData()
        {
            var result = new Dictionary<ChunkCoord, byte[]>();
            foreach (var kvp in loadedChunks)
            {
                if (kvp.Value.ChunkData != null)
                {
                    byte[] rawBlocks = kvp.Value.ChunkData.GetRawBlocks();
                    byte[] rleData = ChunkSerializer.Serialize(rawBlocks);
                    if (rleData != null)
                    {
                        result[kvp.Key] = rleData;
                    }
                }
            }
            return result;
        }
    }
}
