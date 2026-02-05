#!/usr/bin/env python3
from PIL import Image
import os

# Texture specifications
ATLAS_SIZE = 256
TILE_SIZE = 16
TILES_PER_ROW = 16

# Texture color definitions
TEXTURES = [
    {"index": 0, "suffix": "air", "color": (200, 200, 200)},
    {"index": 1, "suffix": "grass_top", "color": (76, 153, 0)},
    {"index": 2, "suffix": "dirt", "color": (139, 90, 43)},
    {"index": 3, "suffix": "stone", "color": (136, 136, 136)},
    {"index": 4, "suffix": "wood_side", "color": (160, 110, 50)},
    {"index": 5, "suffix": "leaves", "color": (34, 120, 34)},
    {"index": 6, "suffix": "sand", "color": (219, 211, 160)},
    {"index": 7, "suffix": "water", "color": (30, 100, 200)},
    {"index": 8, "suffix": "glass", "color": (200, 230, 255)},
    {"index": 9, "suffix": "cobblestone", "color": (120, 120, 120)},
    {"index": 10, "suffix": "planks", "color": (180, 140, 80)},
    {"index": 11, "suffix": "brick", "color": (170, 80, 60)},
    {"index": 12, "suffix": "gravel", "color": (140, 130, 125)},
    {"index": 13, "suffix": "iron", "color": (200, 200, 200)},
    {"index": 14, "suffix": "gold", "color": (255, 215, 0)},
    {"index": 15, "suffix": "diamond", "color": (80, 220, 220)},
    {"index": 16, "suffix": "coal", "color": (50, 50, 50)},
    {"index": 17, "suffix": "obsidian", "color": (20, 15, 30)},
    {"index": 18, "suffix": "snow", "color": (240, 240, 255)},
    {"index": 19, "suffix": "ice", "color": (170, 210, 240)},
    {"index": 20, "suffix": "clay", "color": (160, 165, 175)},
    {"index": 21, "suffix": "sandstone_top", "color": (220, 205, 155)},
    {"index": 22, "suffix": "wool", "color": (235, 235, 235)},
    {"index": 23, "suffix": "bedrock", "color": (85, 85, 85)},
    {"index": 24, "suffix": "torch", "color": (255, 200, 50)},
    {"index": 25, "suffix": "flower", "color": (255, 80, 80)},
    {
        "index": 26,
        "suffix": "grass_side",
        "color1": (76, 153, 0),
        "color2": (139, 90, 43),
    },
    {"index": 27, "suffix": "wood_top", "color": (140, 100, 45)},
    {"index": 28, "suffix": "sandstone_side", "color": (195, 180, 130)},
    {"index": 29, "suffix": "sandstone_bottom", "color": (200, 190, 150)},
    {
        "index": 30,
        "suffix": "snow_side",
        "color1": (240, 240, 255),
        "color2": (139, 90, 43),
    },
]


def create_tile(index, suffix, color=None, color1=None, color2=None):
    """Create a 16x16 tile with specified color(s)"""
    tile = Image.new("RGBA", (TILE_SIZE, TILE_SIZE), (0, 0, 0, 0))

    if color:
        # Solid color tile
        for x in range(TILE_SIZE):
            for y in range(TILE_SIZE):
                tile.putpixel((x, y), color + (255,))

    elif color1 and color2:
        # Dual color tile (top 8 rows color1, bottom 8 rows color2)
        for x in range(TILE_SIZE):
            for y in range(TILE_SIZE):
                color = color1 + (255,) if y < TILE_SIZE // 2 else color2 + (255,)
                tile.putpixel((x, y), color)

    return tile


def generate_textures():
    # Create output directories
    individual_dir = "Assets/_Project/Art/Textures/Blocks/Individual"
    atlas_dir = "Assets/_Project/Art/Textures/Blocks"
    os.makedirs(individual_dir, exist_ok=True)
    os.makedirs(atlas_dir, exist_ok=True)

    # Create BlockAtlas
    block_atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE), (0, 0, 0, 0))

    # Generate individual tiles and place in atlas
    for texture in TEXTURES:
        index = texture["index"]
        suffix = texture["suffix"]

        # Create tile
        if "color1" in texture:
            tile = create_tile(
                index, suffix, color1=texture["color1"], color2=texture["color2"]
            )
        else:
            tile = create_tile(index, suffix, color=texture["color"])

        # Save individual tile
        individual_tile_path = os.path.join(
            individual_dir, f"tile_{index}_{suffix}.png"
        )
        tile.save(individual_tile_path)

        # Calculate atlas position (Unity-compatible coordinate system)
        atlas_x = (index % TILES_PER_ROW) * TILE_SIZE
        atlas_y = (ATLAS_SIZE - TILE_SIZE) - ((index // TILES_PER_ROW) * TILE_SIZE)

        # Place tile in atlas
        block_atlas.paste(tile, (atlas_x, atlas_y))

    # Save BlockAtlas
    block_atlas_path = os.path.join(atlas_dir, "BlockAtlas.png")
    block_atlas.save(block_atlas_path)

    print(f"Generated {len(TEXTURES)} textures:")
    print(f"- Individual tiles saved in {individual_dir}")
    print(f"- Block atlas saved as {block_atlas_path}")


if __name__ == "__main__":
    generate_textures()
