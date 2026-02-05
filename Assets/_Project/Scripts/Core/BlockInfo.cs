namespace VoxelEngine.Core
{
    /// <summary>
    /// Blittable struct for block definition data, usable in Burst/Jobs.
    /// Mirrors relevant fields from BlockDefinitionSO without managed references.
    /// </summary>
    public struct BlockInfo
    {
        public bool isSolid;
        public bool isTransparent;
        public int topTileIndex;
        public int sideTileIndex;
        public int bottomTileIndex;
        public byte emission;
        public float hardness;
        public byte preferredToolType;

        /// <summary>
        /// Creates a BlockInfo from a BlockDefinitionSO.
        /// </summary>
        public static BlockInfo FromDefinition(BlockDefinitionSO def)
        {
            if (def == null)
            {
                return new BlockInfo
                {
                    isSolid = false,
                    isTransparent = true,
                    topTileIndex = 0,
                    sideTileIndex = 0,
                    bottomTileIndex = 0,
                    emission = 0,
                    hardness = 0,
                    preferredToolType = 0
                };
            }

            return new BlockInfo
            {
                isSolid = def.isSolid,
                isTransparent = def.isTransparent,
                topTileIndex = def.topTileIndex,
                sideTileIndex = def.sideTileIndex,
                bottomTileIndex = def.bottomTileIndex,
                emission = def.lightEmission,
                hardness = def.hardness,
                preferredToolType = (byte)def.preferredToolType
            };
        }
    }
}
