using UnityEngine;

namespace VoxelEngine.Networking
{
    /// <summary>
    /// Visual marker showing a remote player's camera focal point position.
    /// Lerps smoothly between received positions.
    /// </summary>
    public class RemotePlayerIndicator : MonoBehaviour
    {
        [SerializeField] private float lerpSpeed = 10f;

        private Vector3 targetPosition;
        private MeshRenderer meshRenderer;
        private Material instanceMaterial;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            // Smooth interpolation to target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
        }

        /// <summary>
        /// Updates the target position for smooth interpolation.
        /// </summary>
        public void UpdatePosition(Vector3 focalPoint)
        {
            targetPosition = focalPoint;
        }

        /// <summary>
        /// Sets the indicator color (unique per player).
        /// </summary>
        public void SetColor(Color color)
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer != null)
            {
                // Use instance material to avoid shared material modification
                if (instanceMaterial == null)
                {
                    instanceMaterial = new Material(meshRenderer.sharedMaterial);
                    meshRenderer.material = instanceMaterial;
                }
                instanceMaterial.color = color;
            }
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
            {
                Destroy(instanceMaterial);
            }
        }
    }
}
