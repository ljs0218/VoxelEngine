using UnityEngine;

namespace VoxelEngine.Player
{
    /// <summary>
    /// Fixed-angle isometric camera with orthographic projection.
    /// Supports zoom via scroll wheel and WASD movement along the ground plane.
    /// Exposes FocalPoint for chunk streaming (the world XZ point the camera is looking at).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricCamera : MonoBehaviour
    {
        [Header("Isometric Settings")]
        [SerializeField] private float isometricAngleX = 30f;
        [SerializeField] private float isometricAngleY = 45f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 200f;
        [SerializeField] private float defaultZoom = 10f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 30f;
        [SerializeField] private float shiftMultiplier = 3f;

        private Camera cam;

        /// <summary>
        /// The world-space point on the ground (Y=focalY) the camera is looking at.
        /// Used by VoxelWorld for chunk streaming instead of transform.position.
        /// </summary>
        private Vector3 focalPoint;
        private float focalY = 64f;

        /// <summary>
        /// The world XZ ground point the camera is focused on.
        /// </summary>
        public Vector3 FocalPoint => focalPoint;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            SetupCamera();
        }

        private void SetupCamera()
        {
            cam.orthographic = true;
            cam.orthographicSize = defaultZoom;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            transform.rotation = Quaternion.Euler(isometricAngleX, isometricAngleY, 0f);
        }

        private void Update()
        {
            HandleMovement();
            HandleZoom();
        }

        private void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
                return;

            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speed *= shiftMultiplier;

            // Scale move speed with zoom so it feels consistent
            speed *= cam.orthographicSize / defaultZoom;

            // Move along the camera's right/up projected onto the XZ plane
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;

            // Project onto XZ (zero out Y, normalize)
            right.y = 0f;
            right.Normalize();
            forward.y = 0f;
            forward.Normalize();

            Vector3 move = (right * h + forward * v) * speed * Time.deltaTime;
            transform.position += move;

            UpdateFocalPoint();
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            float newSize = cam.orthographicSize - scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }

        /// <summary>
        /// Recalculates the focal point — the XZ ground position the camera looks at.
        /// Raycast from camera position along forward to Y = focalY plane.
        /// </summary>
        private void UpdateFocalPoint()
        {
            // Camera forward direction
            Vector3 fwd = transform.forward;

            // Avoid division by zero if camera looks perfectly horizontal
            if (Mathf.Approximately(fwd.y, 0f))
            {
                focalPoint = new Vector3(transform.position.x, focalY, transform.position.z);
                return;
            }

            // How far along forward to reach focalY
            float t = (focalY - transform.position.y) / fwd.y;
            focalPoint = transform.position + fwd * t;
            focalPoint.y = focalY;
        }

        /// <summary>
        /// Sets the camera position to look at a target world position from above.
        /// Positions the camera offset from the target along the isometric view direction.
        /// </summary>
        public void LookAt(Vector3 target, float distance = 100f)
        {
            focalY = target.y;
            transform.position = target - transform.forward * distance;
            UpdateFocalPoint();
        }

        /// <summary>
        /// Sets the camera position and orthographic size to view a large area.
        /// </summary>
        public void LookAt(Vector3 target, float distance, float orthoSize)
        {
            focalY = target.y;
            transform.position = target - transform.forward * distance;
            cam.orthographicSize = Mathf.Clamp(orthoSize, minZoom, maxZoom);
            UpdateFocalPoint();
        }
    }
}
