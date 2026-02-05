#!/bin/bash

blocks=(
    "Glass 8 1 1 8 8 8 0 0.3 0"
    "Cobblestone 9 1 0 9 9 9 0 2 1"
    "Planks 10 1 0 10 10 10 0 2 2"
    "Brick 11 1 0 11 11 11 0 2 1"
    "Gravel 12 1 0 12 12 12 0 0.6 3"
    "Iron 13 1 0 13 13 13 0 3 1"
    "Gold 14 1 0 14 14 14 0 3 1"
    "Diamond 15 1 0 15 15 15 0 3 1"
    "Coal 16 1 0 16 16 16 0 3 1"
    "Obsidian 17 1 0 17 17 17 0 50 1"
    "Snow 18 1 0 18 30 2 0 0.2 3"
    "Ice 19 1 1 19 19 19 0 0.5 1"
    "Clay 20 1 0 20 20 20 0 0.6 3"
    "Sandstone 21 1 0 21 28 29 0 0.8 1"
    "Wool 22 1 0 22 22 22 0 0.8 4"
    "Bedrock 23 1 0 23 23 23 0 -1 0"
    "Flower 25 0 1 25 25 25 0 0 0"
)

guids=(
    "2aa2682e1cd347f8b3f9affc1a32dc25"
    "a701f2bca8524314b208b0eaffe9e1c4"
    "f39ec1c49cfb4542a0a885f5b5c0057b"
    "e67e1b5a0f6349c6945bdd58af67aad3"
    "eecf3916c73040eb8546768495f78fe5"
    "3ee3f8b22ec74ba1b5dea97c41ed81be"
    "98b7267a79f8422aa09ca13b42873f5e"
    "bfc180317d244c1ba08ab96c58509873"
    "78fd64fb53aa40049238a35329e1d7f5"
    "c2fd5fabea1f4a9496dc8c73febdee2d"
    "4a94192e85ca4d43833e986cf12ee765"
    "d6b2dd0856e34daa960ac73fd0622e1d"
    "e7f7515632e842268d3cfee73e0e07d4"
    "da7244822b9c494586136099424a76b0"
    "3fbd913a87cc46278e77dc942e14822a"
    "ed5724392cfb459c9935bf0478fa7c82"
    "76481855fa67425d92fcdaac1cf01e37"
)

for i in "${!blocks[@]}"; do
    read -r name id solid transparent top side bottom emission hardness tool <<< "${blocks[i]}"
    guid="${guids[i]}"
    
    asset_path="/Users/ljs0218/VoxelEngine/Assets/_Project/Data/Blocks/Block_$name.asset"
    meta_path="$asset_path.meta"
    
    cat > "$asset_path" << EOF
%YAML 1.1
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
  m_Script: {fileID: 11500000, guid: 0fed08878a38d04459f1f2881b12ea1e, type: 3}
  m_Name: Block_$name
  m_EditorClassIdentifier: 
  blockId: $id
  blockName: $name
  isSolid: $solid
  isTransparent: $transparent
  topTileIndex: $top
  bottomTileIndex: $bottom
  sideTileIndex: $side
  topTexture: {fileID: 0}
  sideTexture: {fileID: 0}
  bottomTexture: {fileID: 0}
  lightEmission: $emission
  hardness: $hardness
  preferredToolType: $tool
EOF

    cat > "$meta_path" << EOF
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
done