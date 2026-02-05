using UnityEngine;
using VoxelEngine.Player;

namespace VoxelEngine.World
{
    /// <summary>
    /// Game initialization manager. Sets up the world, generates the starting island,
    /// positions the camera, and feeds the camera focal point to VoxelWorld each frame
    /// for dynamic chunk streaming.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private VoxelWorld world;
        [SerializeField] private IsometricCamera isometricCamera;

        [Header("Mass Island Test")]
        [Tooltip("Number of islands per axis (total = count x count).")]
        [SerializeField] private int islandGridCount = 10;

        [Tooltip("Spacing between island centers in blocks.")]
        [SerializeField] private int islandSpacing = 16;

        [Header("Showcase")]
        [Tooltip("Generate a showcase island displaying all block types instead of skyblock islands.")]
        [SerializeField] private bool generateShowcase;

        private void Start()
        {
            if (world == null)
            {
                Debug.LogError("[GameManager] VoxelWorld reference not set!");
                return;
            }

            // Always set camera as tracking target — streaming uses focal point, not camera position
            if (isometricCamera != null)
            {
                world.SetTrackTarget(isometricCamera.transform);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (generateShowcase)
            {
                ShowcaseIsland.Generate(world);
                sw.Stop();
                Debug.Log($"[GameManager] Generated showcase island in {sw.ElapsedMilliseconds}ms. " +
                          $"Loaded chunks: {world.LoadedChunkCount}");
            }
            else
            {
                int totalIslands = 0;
                for (int ix = 0; ix < islandGridCount; ix++)
                {
                    for (int iz = 0; iz < islandGridCount; iz++)
                    {
                        int centerX = (ix - islandGridCount / 2) * islandSpacing;
                        int centerZ = (iz - islandGridCount / 2) * islandSpacing;
                        SkyblockIsland.GenerateAt(world, centerX, centerZ);
                        totalIslands++;
                    }
                }

                sw.Stop();
                Debug.Log($"[GameManager] Generated {totalIslands} skyblock islands in {sw.ElapsedMilliseconds}ms. " +
                          $"Loaded chunks: {world.LoadedChunkCount}");
            }

            // Position the camera to look at the center
            if (isometricCamera != null)
            {
                int halfExtent = (islandGridCount * islandSpacing) / 2;
                Vector3 center = new Vector3(0f, 64f, 0f);
                float viewDistance = halfExtent * 2f;
                float orthoSize = halfExtent * 0.8f;
                isometricCamera.LookAt(center, Mathf.Max(viewDistance, 100f), Mathf.Max(orthoSize, 30f));
            }
        }

        private void Update()
        {
            // Feed the camera's ground-level focal point to VoxelWorld each frame.
            // This drives chunk streaming — loads chunks around the focal point,
            // unloads chunks far from it.
            if (world != null && isometricCamera != null)
            {
                world.SetStreamingAnchor(isometricCamera.FocalPoint);
            }
        }
    }
}
