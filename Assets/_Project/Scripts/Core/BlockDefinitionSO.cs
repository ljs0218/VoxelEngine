using UnityEngine;

namespace VoxelEngine.Core
{
    /// <summary>
    /// ScriptableObject that defines the properties of a single block type.
    /// Each block type has a unique ID, visual properties, and atlas tile indices.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlockDefinition", menuName = "VoxelEngine/Block Definition")]
    public class BlockDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique block ID matching BlockType constants. Must be unique across all definitions.")]
        public byte blockId;

        [Tooltip("Human-readable block name.")]
        public string blockName;

        [Header("Properties")]
        [Tooltip("Whether this block is solid (blocks visibility and movement).")]
        public bool isSolid = true;

        [Tooltip("Whether this block is transparent (adjacent faces still render).")]
        public bool isTransparent;

        [Header("Texture Atlas Tile Indices")]
        [Tooltip("Tile index in the atlas for the top face (+Y).")]
        public int topTileIndex;

        [Tooltip("Tile index in the atlas for the bottom face (-Y).")]
        public int bottomTileIndex;

        [Tooltip("Tile index in the atlas for all side faces (+X, -X, +Z, -Z).")]
        public int sideTileIndex;

        [Header("Textures (Optional)")]
        [Tooltip("Texture for the top face (+Y). If set, overrides color-based atlas tile.")]
        public Texture2D topTexture;

        [Tooltip("Texture for all side faces (+X, -X, +Z, -Z). If set, overrides color-based atlas tile.")]
        public Texture2D sideTexture;

        [Tooltip("Texture for the bottom face (-Y). If set, overrides color-based atlas tile.")]
        public Texture2D bottomTexture;

        [Header("Lighting")]
        [Tooltip("Light emission level (0-15). 0 = no emission, 14 = torch-level brightness.")]
        public byte lightEmission;

        [Header("Durability")]
        [Tooltip("Block hardness. -1 = unbreakable, 0 = instant break. Break time (hand) = hardness * 5.0")]
        public float hardness = 1.0f;

        [Tooltip("Preferred tool type for faster breaking.")]
        public ToolType preferredToolType = ToolType.None;
    }
}
