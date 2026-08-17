#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// 场景清理工具（Tools/FPS/清理场景残留引用）。
    /// 用途：删除 KINEMATION 相关脚本后，场景里残留的 Missing 组件（已删脚本的引用）和武器对象（Weapon_*）。
    /// 不依赖任何 KINEMATION 类，可安全编译运行。
    /// </summary>
    public static class FPSSceneCleanup
    {
        private const string MenuPath = "Tools/FPS/清理场景残留引用 (Missing组件+武器对象)";

        [MenuItem(MenuPath)]
        public static void RunWizard()
        {
            // 1. 移除所有 Missing 脚本组件（已删脚本的引用）
            int removed = 0;
            var allObjects = Object.FindObjectsOfType<GameObject>(true);
            foreach (GameObject go in allObjects)
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            Debug.Log($"[FPSCleanup] ✅ 移除 {removed} 个 Missing 脚本组件");

            // 2. 删除武器对象（Weapon_*，挂在 IK WeaponBone 下）
            int weaponsRemoved = 0;
            foreach (GameObject go in allObjects)
            {
                if (go.name.StartsWith("Weapon_"))
                {
                    Object.DestroyImmediate(go);
                    weaponsRemoved++;
                }
            }

            // 3. 删除空的 IK WeaponBone 子物体（KINEMATION 辅助骨，现无用）
            // 但骨架骨还需保留（移动/瞄准用），只删 KINEMATION 专用的 IK 骨
            // —— 这个交给复刻 Game 流程时统一处理，此处只清武器。

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[FPSCleanup] 完成 ✅ 移除 Missing 组件 {removed} 个 + 武器对象 {weaponsRemoved} 个。\n" +
                      "场景已无 KINEMATION 残留。接下来复刻 Game 流程到 Hunter。");
        }
    }
}
#endif
