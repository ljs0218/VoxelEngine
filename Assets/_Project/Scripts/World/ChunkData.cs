using System;
using Unity.Collections;
using VoxelEngine.Core;
using VoxelEngine.Utilities;

namespace VoxelEngine.World
{
    /// <summary>
    /// Stores block data for a single sub-chunk (16x16x16) using NativeArrays.
    /// </summary>
    public class ChunkData : IDisposable
    {
        private NativeArray<byte> blocks;
        private NativeArray<byte> lightMap;

        /// <summary>
        /// Direct access to the underlying NativeArray for Jobs.
        /// </summary>
        public NativeArray<byte> Blocks => blocks;

        /// <summary>
        /// Direct access to the lightMap NativeArray for Jobs.
        /// Each byte stores sunlight (upper 4 bits) and block light (lower 4 bits).
        /// </summary>
        public NativeArray<byte> LightMap => lightMap;

        /// <summary>
        /// Whether this chunk has been modified since last save/clear.
        /// </summary>
        public bool IsDirty { get; private set; }

        public ChunkData()
        {
            blocks = new NativeArray<byte>(VoxelConstants.ChunkBlockCount, Allocator.Persistent);
            lightMap = new NativeArray<byte>(VoxelConstants.ChunkBlockCount, Allocator.Persistent);
        }

        /// <summary>
        /// Converts local 3D coordinates to a flat array index.
        /// Layout: x + ChunkWidth * (y + ChunkHeight * z)
        /// </summary>
        private int GetIndex(int x, int y, int z)
        {
            return x + VoxelConstants.ChunkWidth * (y + VoxelConstants.ChunkHeight * z);
        }

        /// <summary>
        /// Returns whether the given local coordinates are within chunk bounds.
        /// </summary>
        public bool IsInBounds(int x, int y, int z)
        {
            return x >= 0 && x < VoxelConstants.ChunkWidth &&
                   y >= 0 && y < VoxelConstants.ChunkHeight &&
                   z >= 0 && z < VoxelConstants.ChunkDepth;
        }

        /// <summary>
        /// Gets the block type at the given local coordinates.
        /// Returns Air (0) if coordinates are out of bounds or data is disposed.
        /// </summary>
        public byte GetBlock(int x, int y, int z)
        {
            if (!blocks.IsCreated || !IsInBounds(x, y, z))
                return BlockType.Air;

            return blocks[GetIndex(x, y, z)];
        }

        /// <summary>
        /// Sets the block type at the given local coordinates.
        /// Does nothing if coordinates are out of bounds or data is disposed.
        /// </summary>
        public void SetBlock(int x, int y, int z, byte blockType)
        {
            if (!blocks.IsCreated || !IsInBounds(x, y, z))
                return;

            blocks[GetIndex(x, y, z)] = blockType;
            IsDirty = true;
        }

        // ==================== Light Accessors ====================

        /// <summary>
        /// Gets the full light byte (sunlight | blockLight) at the given local coordinates.
        /// Returns 0 if coordinates are out of bounds or data is disposed.
        /// </summary>
        public byte GetLight(int x, int y, int z)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return 0;

            return lightMap[GetIndex(x, y, z)];
        }

        /// <summary>
        /// Gets the sunlight value (0-15) at the given local coordinates.
        /// Stored in the upper 4 bits of the light byte.
        /// </summary>
        public byte GetSunlight(int x, int y, int z)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return 0;

            return (byte)((lightMap[GetIndex(x, y, z)] >> 4) & 0xF);
        }

        /// <summary>
        /// Gets the block light value (0-15) at the given local coordinates.
        /// Stored in the lower 4 bits of the light byte.
        /// </summary>
        public byte GetBlockLight(int x, int y, int z)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return 0;

            return (byte)(lightMap[GetIndex(x, y, z)] & 0xF);
        }

        /// <summary>
        /// Sets the sunlight value (0-15) at the given local coordinates.
        /// Preserves the block light bits.
        /// </summary>
        public void SetSunlight(int x, int y, int z, byte val)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return;

            int idx = GetIndex(x, y, z);
            lightMap[idx] = (byte)((val << 4) | (lightMap[idx] & 0xF));
        }

        /// <summary>
        /// Sets the block light value (0-15) at the given local coordinates.
        /// Preserves the sunlight bits.
        /// </summary>
        public void SetBlockLight(int x, int y, int z, byte val)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return;

            int idx = GetIndex(x, y, z);
            lightMap[idx] = (byte)((lightMap[idx] & 0xF0) | (val & 0xF));
        }

        /// <summary>
        /// Sets the full light byte at the given local coordinates.
        /// </summary>
        public void SetLight(int x, int y, int z, byte val)
        {
            if (!lightMap.IsCreated || !IsInBounds(x, y, z))
                return;

            lightMap[GetIndex(x, y, z)] = val;
        }

        /// <summary>
        /// Clears all light values to 0 (complete darkness).
        /// </summary>
        public void ClearLightMap()
        {
            if (!lightMap.IsCreated) return;

            for (int i = 0; i < lightMap.Length; i++)
            {
                lightMap[i] = 0;
            }
        }

        // ==================== Block Queries ====================

        /// <summary>
        /// Returns true if all blocks in this chunk are Air.
        /// Used to skip mesh generation for empty chunks.
        /// </summary>
        public bool IsEmpty()
        {
            if (!blocks.IsCreated)
                return true;

            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != BlockType.Air)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns a managed byte[] copy of the block data for serialization.
        /// </summary>
        public byte[] GetRawBlocks()
        {
            if (!blocks.IsCreated)
                return new byte[VoxelConstants.ChunkBlockCount];

            return blocks.ToArray();
        }

        /// <summary>
        /// Copies data from a managed byte[] into the NativeArray (for deserialization).
        /// </summary>
        public void SetRawBlocks(byte[] rawBlocks)
        {
            if (rawBlocks != null && rawBlocks.Length == VoxelConstants.ChunkBlockCount && blocks.IsCreated)
            {
                blocks.CopyFrom(rawBlocks);
                IsDirty = true;
            }
        }

        /// <summary>
        /// Clears the dirty flag. Call after saving.
        /// </summary>
        public void ClearDirty()
        {
            IsDirty = false;
        }

        /// <summary>
        /// Disposes the NativeArray. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (blocks.IsCreated)
            {
                blocks.Dispose();
            }
            if (lightMap.IsCreated)
            {
                lightMap.Dispose();
            }
        }
    }
}
