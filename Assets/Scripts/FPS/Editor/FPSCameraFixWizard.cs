#if UNITY_EDITOR
using Cinemachine;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// FPS 相机抖动一键修复（Tools/FPS/相机轨道对齐）。
    /// 根因：freeLookCamera 与 aimingCamera 的 Orbits（Top/Middle/Bottom 轨道高度/半径）配置不一致，
    /// 每次右键/开镜切 Priority 时相机高度/半径瞬跳（本场景实测约上跳 1 米）→ 视角"上下严重抖动"。
    /// 修复：把 aimingCamera 的 Orbits 同步为 freeLookCamera 的，并把初始 Lens.FOV 也同步，
    /// 这样切换只保留瞄准的 FOV 收放（预期效果），无位置跳变。
    /// </summary>
    public static class FPSCameraFixWizard
    {
        private const string MenuPath = "Tools/FPS/相机轨道对齐 (消除瞄准抖动)";

        [MenuItem(MenuPath)]
        public static void RunWizard()
        {
            var controller = Object.FindObjectOfType<FPSController>();
            if (controller == null)
            {
                Debug.LogError("[FPSCameraFix] 场景中找不到 FPSController");
                return;
            }

            if (controller.freeLookCamera == null || controller.aimingCamera == null)
            {
                Debug.LogError("[FPSCameraFix] FPSController 上两架相机槽位为空，请先拖入");
                return;
            }

            var free = controller.freeLookCamera;
            var aiming = controller.aimingCamera;

            Undo.RegisterCompleteObjectUndo(aiming, "FPS Camera Orbit Align");

            // 同步 Orbits（轨道高度/半径，index 0=Top / 1=Middle / 2=Bottom）——切换不再跳位
            for (int i = 0; i < aiming.m_Orbits.Length && i < free.m_Orbits.Length; i++)
            {
                aiming.m_Orbits[i].m_Height = free.m_Orbits[i].m_Height;
                aiming.m_Orbits[i].m_Radius = free.m_Orbits[i].m_Radius;
            }

            // 同步 Lens（FOV + 视野参数）——两相机初始完全一致，切换只靠运行时 SetAimFov 动态收放
            aiming.m_Lens = free.m_Lens;

            // 同步 X/Y 轴当前值与限幅（两相机角度一致，切换无跳变）
            aiming.m_YAxis.Value = free.m_YAxis.Value;
            aiming.m_XAxis.Value = free.m_XAxis.Value;
            aiming.m_YAxis.m_MinValue = free.m_YAxis.m_MinValue;
            aiming.m_YAxis.m_MaxValue = free.m_YAxis.m_MaxValue;
            aiming.m_XAxis.m_MinValue = free.m_XAxis.m_MinValue;
            aiming.m_XAxis.m_MaxValue = free.m_XAxis.m_MaxValue;

            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FPSCameraFix] ✅ 已把 aimingCamera 对齐 freeLookCamera（Orbits 高度/半径 + 初始 FOV）。\n" +
                      $"  现在两相机切换只保留瞄准 FOV 收放（肩射 {controller.shoulderFov}° / 开镜 {controller.adsFov}°），无位置跳变。\n" +
                      "  Play 测右键/开镜：若仍有轻微上下感，那是 FOV 收放的预期效果，不是抖动。");
        }
    }
}
#endif
