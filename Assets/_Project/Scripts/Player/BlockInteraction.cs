using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.World;

namespace VoxelEngine.Player
{
    public class BlockInteraction : MonoBehaviour
    {
        [SerializeField] private VoxelWorld world;
        [SerializeField] private BlockBreaking blockBreaking;
        [SerializeField] private BreakOverlay breakOverlay;
        [SerializeField] private float maxRayDistance = 1000f;
        [SerializeField] private LayerMask voxelLayer = ~0;

        [Header("Block Placement")]
        [Tooltip("The block type to place. Change in Inspector to switch blocks.")]
        public byte currentBlockType = BlockType.Grass;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay;

        private Camera mainCamera;
        private Vector3Int? currentBreakTarget;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (world == null || mainCamera == null) return;

            if (Input.GetMouseButtonDown(1))
            {
                TryPlaceBlock();
            }

            HandleBreaking();
        }

        private void HandleBreaking()
        {
            if (Input.GetMouseButtonUp(0))
            {
                CancelBreaking();
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * maxRayDistance, Color.red, 1f);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, voxelLayer))
            {
                CancelBreaking();
                return;
            }

            Vector3 destroyPoint = hit.point - hit.normal * 0.5f;
            Vector3Int blockPos = Vector3Int.FloorToInt(destroyPoint);
            byte blockType = world.GetBlock(blockPos);

            if (blockType == BlockType.Air)
            {
                CancelBreaking();
                return;
            }

            if (currentBreakTarget.HasValue && currentBreakTarget.Value != blockPos)
            {
                CancelBreaking();
            }

            if (!blockBreaking.IsBreaking)
            {
                BlockInfo blockInfo = BlockInfo.FromDefinition(world.BlockRegistry.GetDefinition(blockType));
                float breakTime = BlockBreaking.CalculateBreakTime(blockInfo);

                if (breakTime < 0) return;

                if (breakTime == 0)
                {
                    world.SetBlock(blockPos, BlockType.Air);
                    return;
                }

                blockBreaking.StartBreaking(blockPos, blockInfo);
                breakOverlay.Show(blockPos);
                currentBreakTarget = blockPos;
            }

            blockBreaking.UpdateBreaking();
            breakOverlay.SetStage(blockBreaking.CurrentStage);

            if (blockBreaking.IsComplete)
            {
                world.SetBlock(blockPos, BlockType.Air);
                CancelBreaking();
            }
        }

        private void CancelBreaking()
        {
            if (blockBreaking.IsBreaking)
            {
                blockBreaking.StopBreaking();
            }
            breakOverlay.Hide();
            currentBreakTarget = null;
        }

        private void TryPlaceBlock()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * maxRayDistance, Color.blue, 1f);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, voxelLayer))
            {
                Vector3 placePoint = hit.point + hit.normal * 0.5f;
                Vector3Int blockPos = Vector3Int.FloorToInt(placePoint);

                world.SetBlock(blockPos, currentBlockType);
            }
        }
    }
}
