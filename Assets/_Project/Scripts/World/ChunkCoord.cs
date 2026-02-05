using System;
using UnityEngine;
using VoxelEngine.Utilities;

namespace VoxelEngine.World
{
    [System.Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int x;
        public int y;
        public int z;

        public ChunkCoord(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public ChunkCoord(int x, int z)
        {
            this.x = x;
            this.y = 0;
            this.z = z;
        }

        public static ChunkCoord FromWorldPosition(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt(worldPos.x / VoxelConstants.ChunkWidth);
            int cy = Mathf.FloorToInt(worldPos.y / VoxelConstants.ChunkHeight);
            int cz = Mathf.FloorToInt(worldPos.z / VoxelConstants.ChunkDepth);
            return new ChunkCoord(cx, cy, cz);
        }

        public static Vector3Int WorldToLocal(Vector3Int worldPos)
        {
            int lx = ((worldPos.x % VoxelConstants.ChunkWidth) + VoxelConstants.ChunkWidth) % VoxelConstants.ChunkWidth;
            int ly = ((worldPos.y % VoxelConstants.ChunkHeight) + VoxelConstants.ChunkHeight) % VoxelConstants.ChunkHeight;
            int lz = ((worldPos.z % VoxelConstants.ChunkDepth) + VoxelConstants.ChunkDepth) % VoxelConstants.ChunkDepth;
            return new Vector3Int(lx, ly, lz);
        }

        public Vector3 GetWorldPosition()
        {
            return new Vector3(x * VoxelConstants.ChunkWidth, y * VoxelConstants.ChunkHeight, z * VoxelConstants.ChunkDepth);
        }

        public bool Equals(ChunkCoord other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (x * 397) ^ z;
                hash = (hash * 397) ^ y;
                return hash;
            }
        }

        public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
        public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);

        public override string ToString() => $"({x}, {y}, {z})";
    }
}
