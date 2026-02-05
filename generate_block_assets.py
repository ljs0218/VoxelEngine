import uuid

blocks = [
    {
        "name": "Water",
        "id": 7,
        "solid": 0,
        "transparent": 1,
        "top": 7,
        "side": 7,
        "bottom": 7,
        "emission": 0,
        "hardness": -1,
        "tool": 0,
    },
    {
        "name": "Glass",
        "id": 8,
        "solid": 1,
        "transparent": 1,
        "top": 8,
        "side": 8,
        "bottom": 8,
        "emission": 0,
        "hardness": 0.3,
        "tool": 0,
    },
    {
        "name": "Cobblestone",
        "id": 9,
        "solid": 1,
        "transparent": 0,
        "top": 9,
        "side": 9,
        "bottom": 9,
        "emission": 0,
        "hardness": 2,
        "tool": 1,
    },
    {
        "name": "Planks",
        "id": 10,
        "solid": 1,
        "transparent": 0,
        "top": 10,
        "side": 10,
        "bottom": 10,
        "emission": 0,
        "hardness": 2,
        "tool": 2,
    },
    {
        "name": "Brick",
        "id": 11,
        "solid": 1,
        "transparent": 0,
        "top": 11,
        "side": 11,
        "bottom": 11,
        "emission": 0,
        "hardness": 2,
        "tool": 1,
    },
    {
        "name": "Gravel",
        "id": 12,
        "solid": 1,
        "transparent": 0,
        "top": 12,
        "side": 12,
        "bottom": 12,
        "emission": 0,
        "hardness": 0.6,
        "tool": 3,
    },
    {
        "name": "Iron",
        "id": 13,
        "solid": 1,
        "transparent": 0,
        "top": 13,
        "side": 13,
        "bottom": 13,
        "emission": 0,
        "hardness": 3,
        "tool": 1,
    },
    {
        "name": "Gold",
        "id": 14,
        "solid": 1,
        "transparent": 0,
        "top": 14,
        "side": 14,
        "bottom": 14,
        "emission": 0,
        "hardness": 3,
        "tool": 1,
    },
    {
        "name": "Diamond",
        "id": 15,
        "solid": 1,
        "transparent": 0,
        "top": 15,
        "side": 15,
        "bottom": 15,
        "emission": 0,
        "hardness": 3,
        "tool": 1,
    },
    {
        "name": "Coal",
        "id": 16,
        "solid": 1,
        "transparent": 0,
        "top": 16,
        "side": 16,
        "bottom": 16,
        "emission": 0,
        "hardness": 3,
        "tool": 1,
    },
    {
        "name": "Obsidian",
        "id": 17,
        "solid": 1,
        "transparent": 0,
        "top": 17,
        "side": 17,
        "bottom": 17,
        "emission": 0,
        "hardness": 50,
        "tool": 1,
    },
    {
        "name": "Snow",
        "id": 18,
        "solid": 1,
        "transparent": 0,
        "top": 18,
        "side": 30,
        "bottom": 2,
        "emission": 0,
        "hardness": 0.2,
        "tool": 3,
    },
    {
        "name": "Ice",
        "id": 19,
        "solid": 1,
        "transparent": 1,
        "top": 19,
        "side": 19,
        "bottom": 19,
        "emission": 0,
        "hardness": 0.5,
        "tool": 1,
    },
    {
        "name": "Clay",
        "id": 20,
        "solid": 1,
        "transparent": 0,
        "top": 20,
        "side": 20,
        "bottom": 20,
        "emission": 0,
        "hardness": 0.6,
        "tool": 3,
    },
    {
        "name": "Sandstone",
        "id": 21,
        "solid": 1,
        "transparent": 0,
        "top": 21,
        "side": 28,
        "bottom": 29,
        "emission": 0,
        "hardness": 0.8,
        "tool": 1,
    },
    {
        "name": "Wool",
        "id": 22,
        "solid": 1,
        "transparent": 0,
        "top": 22,
        "side": 22,
        "bottom": 22,
        "emission": 0,
        "hardness": 0.8,
        "tool": 4,
    },
    {
        "name": "Bedrock",
        "id": 23,
        "solid": 1,
        "transparent": 0,
        "top": 23,
        "side": 23,
        "bottom": 23,
        "emission": 0,
        "hardness": -1,
        "tool": 0,
    },
    {
        "name": "Flower",
        "id": 25,
        "solid": 0,
        "transparent": 1,
        "top": 25,
        "side": 25,
        "bottom": 25,
        "emission": 0,
        "hardness": 0,
        "tool": 0,
    },
]

guids = [
    "eff441eceee9439f8613d1e04c109cae",
    "2aa2682e1cd347f8b3f9affc1a32dc25",
    "a701f2bca8524314b208b0eaffe9e1c4",
    "f39ec1c49cfb4542a0a885f5b5c0057b",
    "e67e1b5a0f6349c6945bdd58af67aad3",
    "eecf3916c73040eb8546768495f78fe5",
    "3ee3f8b22ec74ba1b5dea97c41ed81be",
    "98b7267a79f8422aa09ca13b42873f5e",
    "bfc180317d244c1ba08ab96c58509873",
    "78fd64fb53aa40049238a35329e1d7f5",
    "c2fd5fabea1f4a9496dc8c73febdee2d",
    "4a94192e85ca4d43833e986cf12ee765",
    "d6b2dd0856e34daa960ac73fd0622e1d",
    "e7f7515632e842268d3cfee73e0e07d4",
    "da7244822b9c494586136099424a76b0",
    "3fbd913a87cc46278e77dc942e14822a",
    "ed5724392cfb459c9935bf0478fa7c82",
    "76481855fa67425d92fcdaac1cf01e37",
]

base_template = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 0fed08878a38d04459f1f2881b12ea1e, type: 3}}
  m_Name: Block_{name}
  m_EditorClassIdentifier: 
  blockId: {id}
  blockName: {name}
  isSolid: {solid}
  isTransparent: {transparent}
  topTileIndex: {top}
  bottomTileIndex: {bottom}
  sideTileIndex: {side}
  topTexture: {{fileID: 0}}
  sideTexture: {{fileID: 0}}
  bottomTexture: {{fileID: 0}}
  lightEmission: {emission}
  hardness: {hardness}
  preferredToolType: {tool}
"""

base_meta_template = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: """

for block, guid in zip(blocks, guids):
    asset_path = f"/Users/ljs0218/VoxelEngine/Assets/_Project/Data/Blocks/Block_{block['name']}.asset"
    meta_path = f"{asset_path}.meta"

    with open(asset_path, "w") as f:
        f.write(base_template.format(**block) + "\n")

    with open(meta_path, "w") as f:
        f.write(base_meta_template.format(guid=guid) + "\n")

print("Generated all block assets and meta files.")
