using Unity.Collections;
using UnityEngine;

namespace VoxelEngine.Core
{
    /// <summary>
    /// Central registry that provides O(1) lookup of block definitions by block ID.
    /// Assign all BlockDefinitionSO assets in the Inspector.
    /// </summary>
    public class BlockRegistry : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign all block definition assets here. Order does not matter; lookup is by blockId.")]
        private BlockDefinitionSO[] definitions;

        private BlockDefinitionSO[] lookup;

        private void Awake()
        {
            BuildLookup();
        }

        /// <summary>
        /// Builds the flat lookup array indexed by blockId.
        /// Call this if definitions change at runtime (editor only).
        /// </summary>
        public void BuildLookup()
        {
            lookup = new BlockDefinitionSO[256];

            if (definitions == null) return;

            foreach (var def in definitions)
            {
                if (def == null) continue;

                if (lookup[def.blockId] != null)
                {
                    Debug.LogWarning($"[BlockRegistry] Duplicate blockId {def.blockId}: " +
                                     $"'{lookup[def.blockId].blockName}' and '{def.blockName}'. Using last.");
                }

                lookup[def.blockId] = def;
            }
        }

        /// <summary>
        /// Returns the block definition for the given block ID, or null if not defined.
        /// </summary>
        public BlockDefinitionSO GetDefinition(byte blockId)
        {
            if (lookup == null) BuildLookup();
            return lookup[blockId];
        }

        /// <summary>
        /// Returns whether the block is solid (blocks face rendering).
        /// Air (0) and undefined blocks are not solid.
        /// </summary>
        public bool IsSolid(byte blockId)
        {
            if (lookup == null) BuildLookup();
            var def = lookup[blockId];
            return def != null && def.isSolid;
        }

        /// <summary>
        /// Returns whether the block is transparent.
        /// Air (0) and undefined blocks are considered transparent.
        /// </summary>
        public bool IsTransparent(byte blockId)
        {
            if (lookup == null) BuildLookup();
            var def = lookup[blockId];
            return def == null || def.isTransparent;
        }

        /// <summary>
        /// Returns the total number of assigned definitions (for testing/debug).
        /// </summary>
        public int DefinitionCount
        {
            get
            {
                if (definitions == null) return 0;
                return definitions.Length;
            }
        }

        /// <summary>
        /// Editor/test helper: set definitions array directly without Inspector.
        /// </summary>
        public void SetDefinitions(BlockDefinitionSO[] defs)
        {
            definitions = defs;
            BuildLookup();
        }

        /// <summary>
        /// Builds a NativeArray of BlockInfo structs for use in Burst/Jobs.
        /// Index corresponds to block ID (0-255). Caller must dispose.
        /// </summary>
        public NativeArray<BlockInfo> BuildBlockInfoArray(Allocator allocator)
        {
            if (lookup == null) BuildLookup();

            var blockInfos = new NativeArray<BlockInfo>(256, allocator);
            for (int i = 0; i < 256; i++)
            {
                blockInfos[i] = BlockInfo.FromDefinition(lookup[i]);
            }
            return blockInfos;
        }
    }
}
