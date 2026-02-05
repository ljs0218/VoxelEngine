using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core;
using VoxelEngine.Utilities;

namespace VoxelEngine.Meshing
{
    /// <summary>
    /// Burst-compiled job that generates chunk mesh data using face culling.
    /// Produces vertices, triangles, and UVs identical to the managed MeshGenerator.
    /// Operates on blittable NativeArrays only — no managed references.
    /// </summary>
    [BurstCompile]
    public struct MeshGenJob : IJob
    {
        // Input: this chunk's block data (length = ChunkBlockCount)
        [ReadOnly] public NativeArray<byte> blocks;

        // Input: blittable block definitions indexed by block ID (length = 256)
        [ReadOnly] public NativeArray<BlockInfo> blockInfos;

        // Input: neighbor chunk block data (posX = +X, negX = -X, posZ = +Z, negZ = -Z)
        // Safety restriction disabled because these may be default (uninitialized)
        // when no neighbor chunk is loaded. Use hasNeighborX flags to guard access.
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborPosX;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborNegX;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborPosZ;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborNegZ;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborPosY;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborNegY;

        // Input: this chunk's lightMap data (same layout as blocks)
        // Safety restriction disabled because this may be a dummy empty array when no light data exists.
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> lightMap;
        public bool hasLightMap;

        // Input: neighbor chunk lightMap data
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightPosX;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightNegX;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightPosZ;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightNegZ;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightPosY;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> neighborLightNegY;

        // Flags indicating whether each neighbor NativeArray is valid
        public bool hasNeighborPosX;
        public bool hasNeighborNegX;
        public bool hasNeighborPosZ;
        public bool hasNeighborNegZ;
        public bool hasNeighborPosY;
        public bool hasNeighborNegY;

        // Output: mesh data
        public NativeList<float3> vertices;
        public NativeList<int> triangles;
        public NativeList<float2> uvs;
        public NativeList<float4> colors;
        public NativeList<float3> normals;

        // Lighting constant
        public float minBrightness;

        // Constants (passed in because Burst cannot access static fields)
        public int chunkWidth;
        public int chunkHeight;
        public int chunkDepth;
        public int tilesPerRow;
        public float normalizedTileSize;
        public float uvPadding;

        public void Execute()
        {
            for (int x = 0; x < chunkWidth; x++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    for (int z = 0; z < chunkDepth; z++)
                    {
                        byte blockType = blocks[GetIndex(x, y, z)];
                        if (blockType == 0) // Air
                            continue;

                        BlockInfo info = blockInfos[blockType];
                        if (!info.isSolid && !info.isTransparent)
                            continue;

                        // Check each of 6 face directions
                        // 0=Up(+Y), 1=Down(-Y), 2=North(+Z), 3=South(-Z), 4=East(+X), 5=West(-X)
                        for (int d = 0; d < 6; d++)
                        {
                            byte neighborType = GetNeighborBlockType(x, y, z, d);
                            BlockInfo neighborInfo = blockInfos[neighborType];

                            // Skip this face if neighbor is solid and not transparent
                            if (neighborInfo.isSolid && !neighborInfo.isTransparent)
                                continue;

                            AddFace(x, y, z, d, info);
                        }
                    }
                }
            }
        }

        private byte GetNeighborBlockType(int x, int y, int z, int direction)
        {
            int nx = x, ny = y, nz = z;

            switch (direction)
            {
                case 0: ny = y + 1; break; // Up
                case 1: ny = y - 1; break; // Down
                case 2: nz = z + 1; break; // North (+Z)
                case 3: nz = z - 1; break; // South (-Z)
                case 4: nx = x + 1; break; // East (+X)
                case 5: nx = x - 1; break; // West (-X)
            }

            // Within this chunk
            if (nx >= 0 && nx < chunkWidth && ny >= 0 && ny < chunkHeight && nz >= 0 && nz < chunkDepth)
            {
                return blocks[GetIndex(nx, ny, nz)];
            }

            // Y neighbors
            if (ny >= chunkHeight && hasNeighborPosY)
                return neighborPosY[GetIndex(nx, ny - chunkHeight, nz)];
            if (ny < 0 && hasNeighborNegY)
                return neighborNegY[GetIndex(nx, ny + chunkHeight, nz)];
            if (ny < 0 || ny >= chunkHeight)
                return 0;

            // X neighbors
            if (nx >= chunkWidth && hasNeighborPosX)
                return neighborPosX[GetIndex(0, ny, nz)];
            if (nx < 0 && hasNeighborNegX)
                return neighborNegX[GetIndex(chunkWidth - 1, ny, nz)];

            // Z neighbors
            if (nz >= chunkDepth && hasNeighborPosZ)
                return neighborPosZ[GetIndex(nx, ny, 0)];
            if (nz < 0 && hasNeighborNegZ)
                return neighborNegZ[GetIndex(nx, ny, chunkDepth - 1)];

            return 0;
        }

        /// <summary>
        /// Gets the light value of the neighbor block in the given direction.
        /// Mirrors GetNeighborBlockType but reads from lightMap/neighborLight arrays.
        /// Returns 0 if no lightMap data is available.
        /// </summary>
        private byte GetNeighborLight(int x, int y, int z, int direction)
        {
            int nx = x, ny = y, nz = z;

            switch (direction)
            {
                case 0: ny = y + 1; break; // Up
                case 1: ny = y - 1; break; // Down
                case 2: nz = z + 1; break; // North (+Z)
                case 3: nz = z - 1; break; // South (-Z)
                case 4: nx = x + 1; break; // East (+X)
                case 5: nx = x - 1; break; // West (-X)
            }

            if (nx >= 0 && nx < chunkWidth && ny >= 0 && ny < chunkHeight && nz >= 0 && nz < chunkDepth)
            {
                return lightMap[GetIndex(nx, ny, nz)];
            }

            if (ny >= chunkHeight && neighborLightPosY.IsCreated)
                return neighborLightPosY[GetIndex(nx, ny - chunkHeight, nz)];
            if (ny < 0 && neighborLightNegY.IsCreated)
                return neighborLightNegY[GetIndex(nx, ny + chunkHeight, nz)];
            if (ny < 0 || ny >= chunkHeight)
                return 0;

            if (nx >= chunkWidth && neighborLightPosX.IsCreated)
                return neighborLightPosX[GetIndex(0, ny, nz)];
            if (nx < 0 && neighborLightNegX.IsCreated)
                return neighborLightNegX[GetIndex(chunkWidth - 1, ny, nz)];

            if (nz >= chunkDepth && neighborLightPosZ.IsCreated)
                return neighborLightPosZ[GetIndex(nx, ny, 0)];
            if (nz < 0 && neighborLightNegZ.IsCreated)
                return neighborLightNegZ[GetIndex(nx, ny, chunkDepth - 1)];

            return 0;
        }

        private int GetIndex(int x, int y, int z)
        {
            return x + chunkWidth * (y + chunkHeight * z);
        }

        private void AddFace(int x, int y, int z, int direction, BlockInfo info)
        {
            int vertexStart = vertices.Length;
            float3 blockPos = new float3(x, y, z);

            AddFaceVertices(blockPos, direction);
            AddFaceNormals(direction);

            // Add 6 indices (2 triangles): 0,1,2, 0,2,3
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 1);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart + 3);

            // Calculate UV from atlas tile index
            int tileIndex = GetTileIndex(direction, info);
            AddFaceUVs(tileIndex);

            // Calculate vertex colors from lightMap + directional face shading
            float brightness;
            if (hasLightMap)
            {
                byte lightValue = GetNeighborLight(x, y, z, direction);
                byte sun = (byte)((lightValue >> 4) & 0xF);
                byte block = (byte)(lightValue & 0xF);
                brightness = math.max((float)sun, (float)block) / 15f;
                brightness = math.max(brightness, minBrightness);
            }
            else
            {
                brightness = 1f; // No lightMap = full brightness
            }

            // Apply directional face shading for visual depth
            // Top=1.0, Side(N/S/E/W)=0.8, Bottom=0.6
            float faceFactor = direction == 0 ? 1.0f : (direction == 1 ? 0.6f : 0.8f);
            brightness *= faceFactor;

            float4 color = new float4(brightness, brightness, brightness, 1f);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }

        private void AddFaceVertices(float3 pos, int direction)
        {
            // Vertex offsets per face direction (4 vertices, CCW winding)
            // Must match MeshGenerator.FaceVertices exactly
            switch (direction)
            {
                case 0: // Up (+Y)
                    vertices.Add(pos + new float3(0, 1, 0));
                    vertices.Add(pos + new float3(0, 1, 1));
                    vertices.Add(pos + new float3(1, 1, 1));
                    vertices.Add(pos + new float3(1, 1, 0));
                    break;
                case 1: // Down (-Y)
                    vertices.Add(pos + new float3(0, 0, 1));
                    vertices.Add(pos + new float3(0, 0, 0));
                    vertices.Add(pos + new float3(1, 0, 0));
                    vertices.Add(pos + new float3(1, 0, 1));
                    break;
                case 2: // North (+Z)
                    vertices.Add(pos + new float3(1, 0, 1));
                    vertices.Add(pos + new float3(1, 1, 1));
                    vertices.Add(pos + new float3(0, 1, 1));
                    vertices.Add(pos + new float3(0, 0, 1));
                    break;
                case 3: // South (-Z)
                    vertices.Add(pos + new float3(0, 0, 0));
                    vertices.Add(pos + new float3(0, 1, 0));
                    vertices.Add(pos + new float3(1, 1, 0));
                    vertices.Add(pos + new float3(1, 0, 0));
                    break;
                case 4: // East (+X)
                    vertices.Add(pos + new float3(1, 0, 0));
                    vertices.Add(pos + new float3(1, 1, 0));
                    vertices.Add(pos + new float3(1, 1, 1));
                    vertices.Add(pos + new float3(1, 0, 1));
                    break;
                case 5: // West (-X)
                    vertices.Add(pos + new float3(0, 0, 1));
                    vertices.Add(pos + new float3(0, 1, 1));
                    vertices.Add(pos + new float3(0, 1, 0));
                    vertices.Add(pos + new float3(0, 0, 0));
                    break;
            }
        }

        private void AddFaceNormals(int direction)
        {
            float3 n;
            switch (direction)
            {
                case 0:  n = new float3(0, 1, 0); break;
                case 1:  n = new float3(0, -1, 0); break;
                case 2:  n = new float3(0, 0, 1); break;
                case 3:  n = new float3(0, 0, -1); break;
                case 4:  n = new float3(1, 0, 0); break;
                case 5:  n = new float3(-1, 0, 0); break;
                default: n = new float3(0, 1, 0); break;
            }
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
        }

        private int GetTileIndex(int direction, BlockInfo info)
        {
            switch (direction)
            {
                case 0: return info.topTileIndex;      // Up
                case 1: return info.bottomTileIndex;    // Down
                default: return info.sideTileIndex;     // All sides
            }
        }

        private void AddFaceUVs(int tileIndex)
        {
            int tileX = tileIndex % tilesPerRow;
            int tileY = tileIndex / tilesPerRow;

            float uMin = tileX * normalizedTileSize + uvPadding;
            float uMax = (tileX + 1) * normalizedTileSize - uvPadding;
            float vMin = tileY * normalizedTileSize + uvPadding;
            float vMax = (tileY + 1) * normalizedTileSize - uvPadding;

            uvs.Add(new float2(uMin, vMin));
            uvs.Add(new float2(uMin, vMax));
            uvs.Add(new float2(uMax, vMax));
            uvs.Add(new float2(uMax, vMin));
        }
    }
}
