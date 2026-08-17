using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 开镜模式（开镜键，默认 V）：瞄准相机大幅拉近 FOV，精准射击。
    /// </summary>
    public class FPSAdsMode : FPSAimModeBase
    {
        public override void Enter()
        {
            controller.SetAimCamera(true);
        }

        public override void Update()
        {
            // FOV 平滑收至开镜值（从瞄准相机当前值平滑，快速切枪/开镜不跳变）
            if (controller.aimingCamera != null)
            {
                float fov = Mathf.Lerp(controller.aimingCamera.m_Lens.FieldOfView,
                    controller.adsFov, 8f * Time.deltaTime);
                controller.SetAimFov(fov);
            }
        }

        public override void Exit()
        {
            controller.SetAimCamera(false);
            // 退出瞄准：恢复瞄准相机 FOV 到自由相机基准（避免下次进入从残值跳变）
            controller.SetAimFov(controller.freeLookCamera != null ? controller.freeLookCamera.m_Lens.FieldOfView : 60f);
        }
    }
}
