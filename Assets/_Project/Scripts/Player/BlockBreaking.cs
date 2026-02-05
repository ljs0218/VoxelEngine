using UnityEngine;
using VoxelEngine.Core;

namespace VoxelEngine.Player
{
    public class BlockBreaking : MonoBehaviour
    {
        private bool isBreaking;
        private Vector3Int targetBlockPos;
        private float currentBreakTime;
        private float requiredBreakTime;
        private int currentStage;

        public bool IsBreaking => isBreaking;
        public Vector3Int TargetBlockPos => targetBlockPos;
        public int CurrentStage => currentStage;
        public bool IsComplete => isBreaking && requiredBreakTime > 0 && currentBreakTime >= requiredBreakTime;

        public static float CalculateBreakTime(BlockInfo blockInfo)
        {
            if (blockInfo.hardness < 0) return -1f;
            if (blockInfo.hardness == 0) return 0f;
            return blockInfo.hardness * 5.0f;
        }

        public bool StartBreaking(Vector3Int pos, BlockInfo blockInfo)
        {
            float breakTime = CalculateBreakTime(blockInfo);

            if (breakTime < 0) return false;

            if (breakTime == 0)
            {
                isBreaking = true;
                targetBlockPos = pos;
                requiredBreakTime = 0;
                currentBreakTime = 0;
                currentStage = 9;
                return true;
            }

            isBreaking = true;
            targetBlockPos = pos;
            requiredBreakTime = breakTime;
            currentBreakTime = 0;
            currentStage = 0;
            return true;
        }

        public void StopBreaking()
        {
            isBreaking = false;
            currentBreakTime = 0;
            requiredBreakTime = 0;
            currentStage = 0;
        }

        public void UpdateBreaking()
        {
            if (!isBreaking || requiredBreakTime <= 0) return;

            currentBreakTime += Time.deltaTime;

            float progress = currentBreakTime / requiredBreakTime;
            currentStage = Mathf.Clamp(Mathf.FloorToInt(progress * 10f), 0, 9);
        }
    }
}
