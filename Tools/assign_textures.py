#!/usr/bin/env python3
"""
Assigns texture references (GUIDs) to all BlockDefinitionSO .asset files.
Each block's topTexture/sideTexture/bottomTexture fields will point to the
corresponding tile_*.png texture in Individual/.

Unity Texture2D reference format in YAML:
  topTexture: {fileID: 2800000, guid: <GUID>, type: 3}

Null reference:
  topTexture: {fileID: 0}
"""

import os
import re

BLOCKS_DIR = os.path.join(
    os.path.dirname(__file__), "..", "Assets", "_Project", "Data", "Blocks"
)
TEXTURES_DIR = os.path.join(
    os.path.dirname(__file__),
    "..",
    "Assets",
    "_Project",
    "Art",
    "Textures",
    "Blocks",
    "Individual",
)

# Tile index -> texture filename (without path)
TILE_FILES = {
    0: "tile_0_air.png",
    1: "tile_1_grass_top.png",
    2: "tile_2_dirt.png",
    3: "tile_3_stone.png",
    4: "tile_4_wood_side.png",
    5: "tile_5_leaves.png",
    6: "tile_6_sand.png",
    7: "tile_7_water.png",
    8: "tile_8_glass.png",
    9: "tile_9_cobblestone.png",
    10: "tile_10_planks.png",
    11: "tile_11_brick.png",
    12: "tile_12_gravel.png",
    13: "tile_13_iron.png",
    14: "tile_14_gold.png",
    15: "tile_15_diamond.png",
    16: "tile_16_coal.png",
    17: "tile_17_obsidian.png",
    18: "tile_18_snow.png",
    19: "tile_19_ice.png",
    20: "tile_20_clay.png",
    21: "tile_21_sandstone_top.png",
    22: "tile_22_wool.png",
    23: "tile_23_bedrock.png",
    24: "tile_24_torch.png",
    25: "tile_25_flower.png",
    26: "tile_26_grass_side.png",
    27: "tile_27_wood_top.png",
    28: "tile_28_sandstone_side.png",
    29: "tile_29_sandstone_bottom.png",
    30: "tile_30_snow_side.png",
}

# Block name -> (topTileIndex, sideTileIndex, bottomTileIndex)
# From the existing .asset data
BLOCK_TILE_MAP = {
    "Block_Air": (0, 0, 0),  # Air - no textures needed, skip
    "Block_Grass": (1, 26, 2),
    "Block_Dirt": (2, 2, 2),
    "Block_Stone": (3, 3, 3),
    "Block_Wood": (27, 4, 27),
    "Block_Leaves": (5, 5, 5),
    "Block_Sand": (6, 6, 6),
    "Block_Water": (7, 7, 7),
    "Block_Glass": (8, 8, 8),
    "Block_Cobblestone": (9, 9, 9),
    "Block_Planks": (10, 10, 10),
    "Block_Brick": (11, 11, 11),
    "Block_Gravel": (12, 12, 12),
    "Block_Iron": (13, 13, 13),
    "Block_Gold": (14, 14, 14),
    "Block_Diamond": (15, 15, 15),
    "Block_Coal": (16, 16, 16),
    "Block_Obsidian": (17, 17, 17),
    "Block_Snow": (18, 30, 2),
    "Block_Ice": (19, 19, 19),
    "Block_Clay": (20, 20, 20),
    "Block_Sandstone": (21, 28, 29),
    "Block_Wool": (22, 22, 22),
    "Block_Bedrock": (23, 23, 23),
    "Block_Torch": (24, 24, 24),
    "Block_Flower": (25, 25, 25),
}


def get_guid_from_meta(texture_path: str) -> str:
    """Read the GUID from a .meta file."""
    meta_path = texture_path + ".meta"
    if not os.path.exists(meta_path):
        raise FileNotFoundError(f"Meta file not found: {meta_path}")
    with open(meta_path, "r") as f:
        for line in f:
            if line.strip().startswith("guid:"):
                return line.strip().split("guid:")[1].strip()
    raise ValueError(f"No GUID found in {meta_path}")


def build_guid_map() -> dict:
    """Build tile_index -> GUID mapping."""
    guid_map = {}
    for tile_idx, filename in TILE_FILES.items():
        tex_path = os.path.join(TEXTURES_DIR, filename)
        if os.path.exists(tex_path):
            guid_map[tile_idx] = get_guid_from_meta(tex_path)
        else:
            print(f"  WARNING: {filename} not found, skipping tile {tile_idx}")
    return guid_map


def make_tex_ref(guid: str) -> str:
    """Create Unity texture reference YAML."""
    return f"{{fileID: 2800000, guid: {guid}, type: 3}}"


def update_asset(asset_path: str, block_name: str, guid_map: dict):
    """Update a single .asset file with texture references."""
    if block_name not in BLOCK_TILE_MAP:
        print(f"  SKIP: {block_name} not in tile map")
        return False

    top_idx, side_idx, bottom_idx = BLOCK_TILE_MAP[block_name]

    # Skip Air - no visible textures
    if block_name == "Block_Air":
        print(f"  SKIP: {block_name} (Air, no textures)")
        return False

    top_guid = guid_map.get(top_idx)
    side_guid = guid_map.get(side_idx)
    bottom_guid = guid_map.get(bottom_idx)

    if not all([top_guid, side_guid, bottom_guid]):
        print(
            f"  ERROR: Missing GUID for {block_name} (top={top_idx}, side={side_idx}, bottom={bottom_idx})"
        )
        return False

    with open(asset_path, "r") as f:
        content = f.read()

    # Replace texture references
    # Pattern: topTexture: {fileID: 0} or topTexture: {fileID: 2800000, guid: ..., type: 3}
    tex_pattern = r"\{fileID: [^}]*\}"

    content = re.sub(
        r"(topTexture: )" + tex_pattern, r"\1" + make_tex_ref(top_guid), content
    )
    content = re.sub(
        r"(sideTexture: )" + tex_pattern, r"\1" + make_tex_ref(side_guid), content
    )
    content = re.sub(
        r"(bottomTexture: )" + tex_pattern, r"\1" + make_tex_ref(bottom_guid), content
    )

    with open(asset_path, "w") as f:
        f.write(content)

    return True


def main():
    print("=== Assigning textures to BlockDefinitionSO assets ===\n")

    # Build GUID map
    print("Building GUID map from .meta files...")
    guid_map = build_guid_map()
    print(f"  Found {len(guid_map)} tile GUIDs\n")

    # Process each .asset file
    updated = 0
    skipped = 0
    for filename in sorted(os.listdir(BLOCKS_DIR)):
        if not filename.endswith(".asset") or not filename.startswith("Block_"):
            continue

        block_name = filename.replace(".asset", "")
        asset_path = os.path.join(BLOCKS_DIR, filename)
        print(f"Processing {block_name}...")

        if update_asset(asset_path, block_name, guid_map):
            top_idx, side_idx, bottom_idx = BLOCK_TILE_MAP[block_name]
            print(
                f"  OK: top=tile_{top_idx}, side=tile_{side_idx}, bottom=tile_{bottom_idx}"
            )
            updated += 1
        else:
            skipped += 1

    print(f"\n=== Done: {updated} updated, {skipped} skipped ===")


if __name__ == "__main__":
    main()
