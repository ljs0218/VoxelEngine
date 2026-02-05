import os

# Existing block GUIDs from the current SampleScene.unity
existing_guids = [
    "bd31c31dfd0b4314691f16ea7b3fe355",  # Air
    "84f7b997588994941b26694bd273b8ab",  # Grass
    "4de6099ccf668d241a3a91a599636089",  # Dirt
    "93e418a9880d5ad408f0f91355a37cd1",  # Stone
    "cba1318f6db55f1408a4c1f0f8469889",  # Wood
    "1fd60414ba05e0249889168eb881c194",  # Leaves
    "377ff86f72d4c7b44928c0b9dfc995ad",  # Torch
]

# New block GUIDs
new_block_guids = [
    "eff441eceee9439f8613d1e04c109cae",  # Water
    "2aa2682e1cd347f8b3f9affc1a32dc25",  # Glass
    "a701f2bca8524314b208b0eaffe9e1c4",  # Cobblestone
    "f39ec1c49cfb4542a0a885f5b5c0057b",  # Planks
    "e67e1b5a0f6349c6945bdd58af67aad3",  # Brick
    "eecf3916c73040eb8546768495f78fe5",  # Gravel
    "3ee3f8b22ec74ba1b5dea97c41ed81be",  # Iron
    "98b7267a79f8422aa09ca13b42873f5e",  # Gold
    "bfc180317d244c1ba08ab96c58509873",  # Diamond
    "78fd64fb53aa40049238a35329e1d7f5",  # Coal
    "c2fd5fabea1f4a9496dc8c73febdee2d",  # Obsidian
    "4a94192e85ca4d43833e986cf12ee765",  # Snow
    "d6b2dd0856e34daa960ac73fd0622e1d",  # Ice
    "e7f7515632e842268d3cfee73e0e07d4",  # Clay
    "da7244822b9c494586136099424a76b0",  # Sandstone
    "3fbd913a87cc46278e77dc942e14822a",  # Wool
    "ed5724392cfb459c9935bf0478fa7c82",  # Bedrock
    "76481855fa67425d92fcdaac1cf01e37",  # Flower
]

# Combine existing and new GUIDs
all_guids = existing_guids + new_block_guids

# Read the SampleScene.unity file
with open("/Users/ljs0218/VoxelEngine/Assets/Scenes/SampleScene.unity", "r") as f:
    lines = f.readlines()

# Find the line with 'definitions:' and replace the block definitions
for i, line in enumerate(lines):
    if line.strip() == "definitions:":
        # Replace the block definitions
        lines[i + 1 : i + 8] = [
            f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n" for guid in all_guids
        ]
        break

# Write the updated file
with open("/Users/ljs0218/VoxelEngine/Assets/Scenes/SampleScene.unity", "w") as f:
    f.writelines(lines)

print("Updated SampleScene.unity with new block definitions.")
