using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 一键解开当前打开场景中的**全部预制体实例**（Unpack，脱离 prefab 关联）。
///
/// 用途：场景进入「自由编辑」模式——改场景不再影响 prefab 资产，改 prefab 也不会同步到场景；
/// 也不再受「prefab 实例内不能 SetParent / 需 RecordPrefabInstancePropertyModifications」等限制。
///
/// 实现：找到场景里所有「最外层 prefab 实例根」，用 `PrefabUnpackMode.Completely` 解开
/// （嵌套的预制体实例（如 Weapon_Hunter_AR03 嵌在 Hunter 里）也会一并解开）。
///
/// ⚠️ 后果：场景对象彻底脱离 prefab 资产。prefab 资产本身仍在（可随时重新拖进场景）。
///   依赖「按 prefab 路径识别对象」的工具会失效（如 AddHunterToGameWizard 的移除/去重、按
///   GetCorrespondingObjectFromOriginalSource 判断的角色），改由「场景根名 / 组件类型」识别兜底。
///
/// 菜单：Tools → 场景 → 解开全部预制体（Unpack）
/// </summary>
public static class UnpackPrefabsInSceneWizard
{
    [MenuItem("Tools/场景/解开全部预制体（Unpack，脱离 prefab 关联）")]
    public static void UnpackAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Play 模式下不可解开预制体，请退出 Play。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 收集所有「最外层 prefab 实例根」（含嵌套实例由 Completely 一并解开），去重后统一解
        var roots = new HashSet<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.NotAPrefab) continue;
            var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (outer != null) roots.Add(outer);
        }

        if (roots.Count == 0)
        {
            Debug.Log("当前场景没有预制体实例，无需解开。");
            return;
        }

        if (!EditorUtility.DisplayDialog("解开全部预制体",
                $"将解开当前场景 {roots.Count} 个预制体实例（含嵌套），场景对象将**彻底脱离 prefab 关联**。\n\n" +
                "prefab 资产仍保留，可随时重新拖回场景。确定继续吗？",
                "继续", "取消"))
            return;

        int count = 0;
        foreach (var r in roots)
        {
            if (r == null) continue;
            PrefabUtility.UnpackPrefabInstance(r, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            count++;
            Debug.Log($"  已解开：{r.name}");
        }

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"完成 ✅ 已解开 {count} 个预制体实例（含嵌套），场景已保存。\n" +
                  "现在场景对象完全独立：改场景不影响 prefab，改 prefab 不同步场景。" +
                  "之后用「按场景根名 / 组件类型」识别的向导仍可正常操作（如 Hunter 清理）。");
    }
}
