using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.World;

namespace VoxelEngine.Player
{
    /// <summary>
    /// Highlights the voxel block currently under the mouse cursor.
    /// Renders a wireframe outline (12 edges as thin quads) using a MeshRenderer.
    /// Fully URP-compatible — no GL calls, no face fill, edges only.
    /// </summary>
    public class BlockHighlight : MonoBehaviour
    {
        [SerializeField] private VoxelWorld world;
        [SerializeField] private float maxRayDistance = 1000f;
        [SerializeField] private LayerMask voxelLayer = ~0;

        [Header("Highlight Appearance")]
        [SerializeField] private Color edgeColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);

        [Tooltip("Thickness of edge lines in world units.")]
        [SerializeField] private float edgeThickness = 0.02f;

        [Tooltip("Outset so edges sit slightly outside the block.")]
        [SerializeField] private float padding = 0.002f;

        private Camera mainCamera;
        private GameObject highlightObj;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material edgeMaterial;

        /// <summary>
        /// The world-space integer position of the currently highlighted block, or null if none.
        /// </summary>
        public Vector3Int? HighlightedBlockPos { get; private set; }

        private void Start()
        {
            mainCamera = Camera.main;
            CreateEdgeMesh();
        }

        private void Update()
        {
            if (world == null || mainCamera == null)
            {
                HighlightedBlockPos = null;
                SetVisible(false);
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, voxelLayer))
            {
                Vector3 blockPoint = hit.point - hit.normal * 0.5f;
                Vector3Int blockPos = Vector3Int.FloorToInt(blockPoint);

                byte blockType = world.GetBlock(blockPos);
                if (blockType != BlockType.Air)
                {
                    HighlightedBlockPos = blockPos;
                    highlightObj.transform.position = new Vector3(blockPos.x, blockPos.y, blockPos.z);
                    SetVisible(true);
                    return;
                }
            }

            HighlightedBlockPos = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (highlightObj != null && highlightObj.activeSelf != visible)
            {
                highlightObj.SetActive(visible);
            }
        }

        private void CreateEdgeMesh()
        {
            highlightObj = new GameObject("BlockHighlight");
            highlightObj.layer = LayerMask.NameToLayer("Ignore Raycast");

            meshFilter = highlightObj.AddComponent<MeshFilter>();
            meshRenderer = highlightObj.AddComponent<MeshRenderer>();

            // Create an unlit transparent material
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            edgeMaterial = new Material(shader);
            edgeMaterial.hideFlags = HideFlags.HideAndDontSave;
            edgeMaterial.color = edgeColor;

            // Enable transparency
            edgeMaterial.SetFloat("_Surface", 1); // Transparent
            edgeMaterial.SetFloat("_Blend", 0);   // Alpha
            edgeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            edgeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            edgeMaterial.SetInt("_ZWrite", 0);
            edgeMaterial.SetFloat("_AlphaClip", 0);
            edgeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            edgeMaterial.SetOverrideTag("RenderType", "Transparent");
            edgeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            edgeMaterial.SetColor("_BaseColor", edgeColor);

            meshRenderer.material = edgeMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            meshFilter.mesh = BuildWireframeMesh();
            SetVisible(false);
        }

        /// <summary>
        /// Builds a mesh of 12 thin rectangular "beams" forming the edges of a unit cube.
        /// The cube spans [0,0,0] to [1,1,1] with padding outset.
        /// </summary>
        private Mesh BuildWireframeMesh()
        {
            float t = edgeThickness * 0.5f;
            float p = padding;
            float lo = 0f - p;
            float hi = 1f + p;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 4 bottom edges (Y = lo)
            AddBeamX(verts, tris, lo, hi, lo, lo, t);  // along X at (y=lo, z=lo)
            AddBeamX(verts, tris, lo, hi, lo, hi, t);  // along X at (y=lo, z=hi)
            AddBeamZ(verts, tris, lo, hi, lo, lo, t);  // along Z at (y=lo, x=lo)
            AddBeamZ(verts, tris, lo, hi, lo, hi, t);  // along Z at (y=lo, x=hi)

            // 4 top edges (Y = hi)
            AddBeamX(verts, tris, lo, hi, hi, lo, t);  // along X at (y=hi, z=lo)
            AddBeamX(verts, tris, lo, hi, hi, hi, t);  // along X at (y=hi, z=hi)
            AddBeamZ(verts, tris, lo, hi, hi, lo, t);  // along Z at (y=hi, x=lo)
            AddBeamZ(verts, tris, lo, hi, hi, hi, t);  // along Z at (y=hi, x=hi)

            // 4 vertical edges (Y direction)
            AddBeamY(verts, tris, lo, hi, lo, lo, t);  // at (x=lo, z=lo)
            AddBeamY(verts, tris, lo, hi, hi, lo, t);  // at (x=hi, z=lo)
            AddBeamY(verts, tris, lo, hi, lo, hi, t);  // at (x=lo, z=hi)
            AddBeamY(verts, tris, lo, hi, hi, hi, t);  // at (x=hi, z=hi)

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Beam along X axis at position (y, z), from xMin to xMax
        private static void AddBeamX(List<Vector3> verts, List<int> tris,
            float xMin, float xMax, float y, float z, float t)
        {
            int i = verts.Count;
            // 8 vertices of a thin box
            verts.Add(new Vector3(xMin, y - t, z - t));
            verts.Add(new Vector3(xMin, y + t, z - t));
            verts.Add(new Vector3(xMin, y + t, z + t));
            verts.Add(new Vector3(xMin, y - t, z + t));
            verts.Add(new Vector3(xMax, y - t, z - t));
            verts.Add(new Vector3(xMax, y + t, z - t));
            verts.Add(new Vector3(xMax, y + t, z + t));
            verts.Add(new Vector3(xMax, y - t, z + t));
            AddBoxTris(tris, i);
        }

        // Beam along Y axis at position (x, z), from yMin to yMax
        private static void AddBeamY(List<Vector3> verts, List<int> tris,
            float yMin, float yMax, float x, float z, float t)
        {
            int i = verts.Count;
            verts.Add(new Vector3(x - t, yMin, z - t));
            verts.Add(new Vector3(x + t, yMin, z - t));
            verts.Add(new Vector3(x + t, yMin, z + t));
            verts.Add(new Vector3(x - t, yMin, z + t));
            verts.Add(new Vector3(x - t, yMax, z - t));
            verts.Add(new Vector3(x + t, yMax, z - t));
            verts.Add(new Vector3(x + t, yMax, z + t));
            verts.Add(new Vector3(x - t, yMax, z + t));
            AddBoxTris(tris, i);
        }

        // Beam along Z axis at position (y, x), from zMin to zMax
        private static void AddBeamZ(List<Vector3> verts, List<int> tris,
            float zMin, float zMax, float y, float x, float t)
        {
            int i = verts.Count;
            verts.Add(new Vector3(x - t, y - t, zMin));
            verts.Add(new Vector3(x + t, y - t, zMin));
            verts.Add(new Vector3(x + t, y + t, zMin));
            verts.Add(new Vector3(x - t, y + t, zMin));
            verts.Add(new Vector3(x - t, y - t, zMax));
            verts.Add(new Vector3(x + t, y - t, zMax));
            verts.Add(new Vector3(x + t, y + t, zMax));
            verts.Add(new Vector3(x - t, y + t, zMax));
            AddBoxTris(tris, i);
        }

        // 12 triangles (6 faces × 2 tris) for a box defined by 8 vertices
        private static void AddBoxTris(List<int> tris, int i)
        {
            // Front  (v0 v1 v5 v4)
            tris.Add(i + 0); tris.Add(i + 1); tris.Add(i + 5);
            tris.Add(i + 0); tris.Add(i + 5); tris.Add(i + 4);
            // Back   (v2 v3 v7 v6)
            tris.Add(i + 2); tris.Add(i + 3); tris.Add(i + 7);
            tris.Add(i + 2); tris.Add(i + 7); tris.Add(i + 6);
            // Top    (v1 v2 v6 v5)
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 6);
            tris.Add(i + 1); tris.Add(i + 6); tris.Add(i + 5);
            // Bottom (v3 v0 v4 v7)
            tris.Add(i + 3); tris.Add(i + 0); tris.Add(i + 4);
            tris.Add(i + 3); tris.Add(i + 4); tris.Add(i + 7);
            // Left   (v3 v2 v1 v0)
            tris.Add(i + 3); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 3); tris.Add(i + 1); tris.Add(i + 0);
            // Right  (v4 v5 v6 v7)
            tris.Add(i + 4); tris.Add(i + 5); tris.Add(i + 6);
            tris.Add(i + 4); tris.Add(i + 6); tris.Add(i + 7);
        }

        private void OnDestroy()
        {
            if (highlightObj != null) Destroy(highlightObj);
            if (edgeMaterial != null) DestroyImmediate(edgeMaterial);
        }
    }
}
