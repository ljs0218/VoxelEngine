using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using VoxelEngine.Core;

namespace VoxelEngine.Editor
{
    public static class AtlasGenerator
    {
        private const int AtlasSize = 256;
        private const int TileSize = 16;
        private const int TilesPerRow = AtlasSize / TileSize; // 16

        // Block tile colors (index -> RGBA) — fallback when no texture is assigned
        // Index 0 = Air (transparent/unused)
        // Index 1 = Grass (green)
        // Index 2 = Dirt (brown)
        // Index 3 = Stone (grey)
        // Index 4 = Wood (brown-ish)
        // Index 5 = Leaves (dark green)
        private static readonly Color32[] TileColors = new Color32[]
        {
            new Color32(200, 200, 200, 255), // 0: Air
            new Color32(76, 153, 0, 255),    // 1: Grass top
            new Color32(139, 90, 43, 255),   // 2: Dirt
            new Color32(136, 136, 136, 255), // 3: Stone
            new Color32(160, 110, 50, 255),  // 4: Wood side
            new Color32(34, 120, 34, 255),   // 5: Leaves
            new Color32(219, 211, 160, 255), // 6: Sand
            new Color32(30, 100, 200, 255),  // 7: Water
            new Color32(200, 230, 255, 255), // 8: Glass
            new Color32(120, 120, 120, 255), // 9: Cobblestone
            new Color32(180, 140, 80, 255),  // 10: Planks
            new Color32(170, 80, 60, 255),   // 11: Brick
            new Color32(140, 130, 125, 255), // 12: Gravel
            new Color32(200, 200, 200, 255), // 13: Iron
            new Color32(255, 215, 0, 255),   // 14: Gold
            new Color32(80, 220, 220, 255),  // 15: Diamond
            new Color32(50, 50, 50, 255),    // 16: Coal
            new Color32(20, 15, 30, 255),    // 17: Obsidian
            new Color32(240, 240, 255, 255), // 18: Snow
            new Color32(170, 210, 240, 255), // 19: Ice
            new Color32(160, 165, 175, 255), // 20: Clay
            new Color32(220, 205, 155, 255), // 21: Sandstone top
            new Color32(235, 235, 235, 255), // 22: Wool
            new Color32(85, 85, 85, 255),    // 23: Bedrock
            new Color32(255, 200, 50, 255),  // 24: Torch
            new Color32(255, 80, 80, 255),   // 25: Flower
            new Color32(76, 153, 0, 255),    // 26: Grass side (upper half in Python)
            new Color32(140, 100, 45, 255),  // 27: Wood top
            new Color32(195, 180, 130, 255), // 28: Sandstone side
            new Color32(200, 190, 150, 255), // 29: Sandstone bottom
            new Color32(240, 240, 255, 255), // 30: Snow side (upper half in Python)
        };

        [MenuItem("VoxelEngine/Generate Block Atlas")]
        public static void GenerateAtlas()
        {
            // Create texture (RGBA32, y=0 is BOTTOM in Unity)
            Texture2D atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;

            // Fill entire atlas with default grey
            Color32 defaultColor = new Color32(200, 200, 200, 255);
            Color32[] allPixels = new Color32[AtlasSize * AtlasSize];
            for (int i = 0; i < allPixels.Length; i++)
                allPixels[i] = defaultColor;
            atlas.SetPixels32(allPixels);

            // Paint fallback color tiles
            for (int tileIndex = 0; tileIndex < TileColors.Length; tileIndex++)
            {
                PaintTile(atlas, tileIndex, TileColors[tileIndex]);
            }

            // Find all BlockDefinitionSO assets and blit textures into atlas tiles
            int textureCount = BlitBlockTextures(atlas);

            atlas.Apply();

            // Save as PNG
            string path = "Assets/_Project/Art/Textures/Blocks/BlockAtlas.png";
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            byte[] pngData = atlas.EncodeToPNG();
            File.WriteAllBytes(path, pngData);

            // Reimport with correct settings
            AssetDatabase.Refresh();

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.spriteImportMode = SpriteImportMode.None;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }

            Object.DestroyImmediate(atlas);

            Debug.Log($"[AtlasGenerator] Block atlas generated at {path} with {TileColors.Length} color tiles + {textureCount} texture tiles. y=0 is texture bottom (UV v=0).");
        }

        /// <summary>
        /// Finds all BlockDefinitionSO assets and blits their textures into the atlas.
        /// Textures override color-based tiles. Returns the number of texture tiles blitted.
        /// </summary>
        private static int BlitBlockTextures(Texture2D atlas)
        {
            string[] guids = AssetDatabase.FindAssets("t:BlockDefinitionSO");
            int textureCount = 0;
            var processedTiles = new HashSet<int>();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                BlockDefinitionSO def = AssetDatabase.LoadAssetAtPath<BlockDefinitionSO>(assetPath);
                if (def == null) continue;

                // Blit top texture into topTileIndex
                if (def.topTexture != null && !processedTiles.Contains(def.topTileIndex))
                {
                    BlitTexture(atlas, def.topTileIndex, def.topTexture);
                    processedTiles.Add(def.topTileIndex);
                    textureCount++;
                }

                // Blit side texture into sideTileIndex
                if (def.sideTexture != null && !processedTiles.Contains(def.sideTileIndex))
                {
                    BlitTexture(atlas, def.sideTileIndex, def.sideTexture);
                    processedTiles.Add(def.sideTileIndex);
                    textureCount++;
                }

                // Blit bottom texture into bottomTileIndex
                if (def.bottomTexture != null && !processedTiles.Contains(def.bottomTileIndex))
                {
                    BlitTexture(atlas, def.bottomTileIndex, def.bottomTexture);
                    processedTiles.Add(def.bottomTileIndex);
                    textureCount++;
                }
            }

            return textureCount;
        }

        /// <summary>
        /// Blits a source texture into the atlas at the given tile index.
        /// Resizes the source to TileSize x TileSize using bilinear sampling.
        /// Source texture must be readable (isReadable = true in import settings).
        /// </summary>
        private static void BlitTexture(Texture2D atlas, int tileIndex, Texture2D source)
        {
            int tileX = tileIndex % TilesPerRow;
            int tileY = tileIndex / TilesPerRow;
            int pixelStartX = tileX * TileSize;
            int pixelStartY = tileY * TileSize;

            // Read source pixels, resizing to TileSize x TileSize
            Color32[] tilePixels = GetResizedPixels(source, TileSize, TileSize);
            if (tilePixels == null)
            {
                Debug.LogWarning($"[AtlasGenerator] Could not read texture '{source.name}' for tile {tileIndex}. " +
                                 "Ensure the texture is set to Read/Write in import settings.");
                return;
            }

            atlas.SetPixels32(pixelStartX, pixelStartY, TileSize, TileSize, tilePixels);
        }

        /// <summary>
        /// Resizes a source texture to the target dimensions using a temporary RenderTexture blit.
        /// Works even if the source is not readable by using GPU blitting.
        /// </summary>
        private static Color32[] GetResizedPixels(Texture2D source, int targetWidth, int targetHeight)
        {
            // Use RenderTexture blit to handle non-readable textures
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);

            Texture2D resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resized.Apply();

            Color32[] pixels = resized.GetPixels32();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(resized);

            return pixels;
        }

        private static void PaintTile(Texture2D atlas, int tileIndex, Color32 color)
        {
            // tileX/tileY match UV calculation in MeshGenerator
            // tileY=0 means bottom row (Unity UV v=0 = pixel y=0 = bottom)
            int tileX = tileIndex % TilesPerRow;
            int tileY = tileIndex / TilesPerRow;

            int pixelStartX = tileX * TileSize;
            int pixelStartY = tileY * TileSize; // y=0 is bottom in Unity Texture2D

            Color32[] tilePixels = new Color32[TileSize * TileSize];
            for (int i = 0; i < tilePixels.Length; i++)
                tilePixels[i] = color;

            atlas.SetPixels32(pixelStartX, pixelStartY, TileSize, TileSize, tilePixels);
        }
    }
}
