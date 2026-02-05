using UnityEngine;

namespace VoxelEngine.Player
{
    public class BreakOverlay : MonoBehaviour
    {
        [SerializeField] private Texture2D[] crackStages;

        private GameObject overlayObj;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material crackMaterial;
        private int currentStage = -1;

        private void Start()
        {
            CreateOverlay();
        }

        private void CreateOverlay()
        {
            overlayObj = new GameObject("BreakOverlay");
            overlayObj.layer = 2; // "Ignore Raycast" — prevents raycast interference

            meshFilter = overlayObj.AddComponent<MeshFilter>();
            meshRenderer = overlayObj.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("VoxelEngine/CrackOverlay");
            crackMaterial = new Material(shader);
            crackMaterial.hideFlags = HideFlags.HideAndDontSave;

            meshRenderer.material = crackMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            meshFilter.mesh = BuildCubeMesh(1.002f);

            overlayObj.SetActive(false);
        }

        public void Show(Vector3Int blockPos)
        {
            overlayObj.transform.position = new Vector3(blockPos.x, blockPos.y, blockPos.z);
            overlayObj.SetActive(true);
            currentStage = -1;
        }

        public void Hide()
        {
            overlayObj.SetActive(false);
            currentStage = -1;
        }

        public void SetStage(int stage)
        {
            stage = Mathf.Clamp(stage, 0, 9);
            if (stage == currentStage) return;
            currentStage = stage;

            if (crackStages != null && stage < crackStages.Length && crackStages[stage] != null)
            {
                crackMaterial.SetTexture("_CrackTex", crackStages[stage]);
            }
        }

        // 24 vertices (4 per face × 6 faces), slightly oversized to prevent z-fighting
        private Mesh BuildCubeMesh(float size)
        {
            float offset = (size - 1f) * 0.5f;
            float lo = -offset;
            float hi = 1f + offset;

            var vertices = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            // Front face (Z+)
            vertices[0] = new Vector3(lo, lo, hi);
            vertices[1] = new Vector3(hi, lo, hi);
            vertices[2] = new Vector3(hi, hi, hi);
            vertices[3] = new Vector3(lo, hi, hi);

            // Back face (Z-)
            vertices[4] = new Vector3(hi, lo, lo);
            vertices[5] = new Vector3(lo, lo, lo);
            vertices[6] = new Vector3(lo, hi, lo);
            vertices[7] = new Vector3(hi, hi, lo);

            // Top face (Y+)
            vertices[8] = new Vector3(lo, hi, hi);
            vertices[9] = new Vector3(hi, hi, hi);
            vertices[10] = new Vector3(hi, hi, lo);
            vertices[11] = new Vector3(lo, hi, lo);

            // Bottom face (Y-)
            vertices[12] = new Vector3(lo, lo, lo);
            vertices[13] = new Vector3(hi, lo, lo);
            vertices[14] = new Vector3(hi, lo, hi);
            vertices[15] = new Vector3(lo, lo, hi);

            // Right face (X+)
            vertices[16] = new Vector3(hi, lo, hi);
            vertices[17] = new Vector3(hi, lo, lo);
            vertices[18] = new Vector3(hi, hi, lo);
            vertices[19] = new Vector3(hi, hi, hi);

            // Left face (X-)
            vertices[20] = new Vector3(lo, lo, lo);
            vertices[21] = new Vector3(lo, lo, hi);
            vertices[22] = new Vector3(lo, hi, hi);
            vertices[23] = new Vector3(lo, hi, lo);

            // UVs — each face maps the full crack texture
            for (int face = 0; face < 6; face++)
            {
                int i = face * 4;
                uvs[i + 0] = new Vector2(0f, 0f);
                uvs[i + 1] = new Vector2(1f, 0f);
                uvs[i + 2] = new Vector2(1f, 1f);
                uvs[i + 3] = new Vector2(0f, 1f);
            }

            // Triangles — two per face, CCW winding
            for (int face = 0; face < 6; face++)
            {
                int i = face * 4;
                int t = face * 6;
                triangles[t + 0] = i + 0;
                triangles[t + 1] = i + 2;
                triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 0;
                triangles[t + 4] = i + 3;
                triangles[t + 5] = i + 2;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (overlayObj != null) Destroy(overlayObj);
            if (crackMaterial != null) DestroyImmediate(crackMaterial);
        }
    }
}
