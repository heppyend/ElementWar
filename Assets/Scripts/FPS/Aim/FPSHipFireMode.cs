using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 腰射模式：仅开火（不按右键）时使用自由相机，武器处于腰间姿态。
    /// </summary>
    public class FPSHipFireMode : FPSAimModeBase
    {
        public override void Enter()
        {
            // 切回自由相机（腰射视角）
            controller.SetAimCamera(false);
        }

        public override void Update()
        {
        }

        public override void Exit()
        {
        }
    }
}
