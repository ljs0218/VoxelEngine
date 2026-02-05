#!/bin/bash

blocks=(
    "Air 0 1 0 0 0 0 0 0 0"
    "Grass 2 1 0 0 1 26 0 0.6 3"
    "Dirt 3 1 0 2 2 2 0 0.5 3"
    "Stone 4 1 0 16 16 16 0 1.5 1"
    "Wood 5 1 0 4 27 27 0 2.0 2"
    "Leaves 6 1 1 5 5 5 0 0.2 4"
    "Torch 24 1 1 24 24 24 14 0 0"
)

for block in "${blocks[@]}"; do
    read -r name id solid transparent top side bottom emission hardness tool <<< "$block"
    
    asset_path="/Users/ljs0218/VoxelEngine/Assets/_Project/Data/Blocks/Block_$name.asset"
    
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
done