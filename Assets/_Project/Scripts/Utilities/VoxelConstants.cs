namespace VoxelEngine.Utilities
{
    public static class VoxelConstants
    {
        // Chunk dimensions (sub-chunk: 16x16x16)
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 16;  // Sub-chunk height (was 256)
        public const int ChunkDepth = 16;
        public const int ChunkBlockCount = ChunkWidth * ChunkHeight * ChunkDepth; // 4096

        // Column dimensions (full vertical stack of sub-chunks)
        public const int SubChunksPerColumn = 16;
        public const int ColumnHeight = SubChunksPerColumn * ChunkHeight; // 256

        // Texture atlas
        public const int AtlasSize = 256;       // pixels (total atlas texture size)
        public const int TileSize = 16;          // pixels per tile
        public const int TilesPerRow = AtlasSize / TileSize; // 16
        public const float NormalizedTileSize = 1f / TilesPerRow; // 0.0625f
        public const float UVPadding = 0.5f / AtlasSize; // ~0.00195f

        // Lighting
        public const int MaxLightLevel = 15;
    }
}
