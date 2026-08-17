using UnityEngine;

namespace FPS
{
    /// <summary>
    /// FPS 角色状态枚举（与 Animator 状态一一对应，但驱动参数由混合树负责丝滑衔接）
    /// </summary>
    public enum FPSState
    {
        Idle,      // 待机
        Move,      // 移动（Locomotion 混合树 Jog 段）
        Sprint,    // 冲刺（Locomotion 混合树 Sprint 段）
        Air,       // 空中（跳跃/下落）
        Slide,     // 滑铲
        Aim        // 瞄准（肩射/开镜/腰射）
    }

    /// <summary>
    /// 瞄准模式：腰射 / 肩射 / 开镜
    /// 框架就绪，后续补枪械动画后各模式自行生效
    /// </summary>
    public enum FPSAimMode
    {
        HipFire,   // 腰射：自由相机，武器在腰间
        Shoulder,  // 肩射：过肩视角，FOV 微缩
        Ads        // 开镜：瞄准相机拉近 FOV
    }

    /// <summary>
    /// Animator 参数名集中管理，避免状态代码里散落魔法字符串。
    /// 混合树节点/参数名若在 Animator 中改动，只需改这里。
    /// </summary>
    public static class FPSAnimatorParams
    {
        /// <summary>Locomotion 混合树速度参数（0=Idle / 0.33=Walk / 0.66=Jog / 1.0=Sprint）</summary>
        public const string Speed = "Speed";
        /// <summary>空中垂直速度（正=上升，负=下降，驱动 Jump_Up/Jump_Down 混合）</summary>
        public const string VerticalSpeed = "VerticalSpeed";
        /// <summary>是否落地（Air→Locomotion 过渡条件）</summary>
        public const string IsGrounded = "IsGrounded";
        /// <summary>是否冲刺</summary>
        public const string IsSprinting = "IsSprinting";
        /// <summary>是否瞄准（Aim 状态过渡条件）</summary>
        public const string IsAiming = "IsAiming";
        /// <summary>瞄准模式：0 腰射 / 1 肩射 / 2 开镜</summary>
        public const string AimMode = "AimMode";
        /// <summary>瞄准横向移动</summary>
        public const string AimingX = "AimingX";
        /// <summary>瞄准纵向移动</summary>
        public const string AimingY = "AimingY";

        public static readonly int SpeedHash = Animator.StringToHash(Speed);
        public static readonly int VerticalSpeedHash = Animator.StringToHash(VerticalSpeed);
        public static readonly int IsGroundedHash = Animator.StringToHash(IsGrounded);
        public static readonly int IsSprintingHash = Animator.StringToHash(IsSprinting);
        public static readonly int IsAimingHash = Animator.StringToHash(IsAiming);
        public static readonly int AimModeHash = Animator.StringToHash(AimMode);
        public static readonly int AimingXHash = Animator.StringToHash(AimingX);
        public static readonly int AimingYHash = Animator.StringToHash(AimingY);
    }
}
