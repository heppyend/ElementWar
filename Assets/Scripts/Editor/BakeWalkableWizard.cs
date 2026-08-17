using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 把选中的物体「烘焙为可行走面」（斜坡/楼梯/平台等），并重烘焙 NavMesh。
///
/// 用途：NavMeshAgent 不做物理碰撞，只沿烘焙出的 NavMesh 表面走。
/// 要让随从/敌人真正走上斜坡/楼梯（Y 跟随斜面、不穿模），这些几何必须烘焙进 NavMesh 作为可行走面。
///
/// 行为：对每个选中物体：
///   1. 层 → 6 (Environment)（进入 NavMeshSurface 的 layerMask=64 收集范围）；
///   2. 移除 NavMeshModifier(ignoreFromBuild)（取消排除）；
///   3. 移除 NavMeshObstacle（它不是障碍，是可行走面，不能被挡）；
///   4. 移除 NavMeshModifier（默认按 Walkable 烘焙）。
///   然后清空 + 重烘焙所有 NavMeshSurface。
///
/// ⚠️ 只对「要走上去的斜坡/楼梯/平台」用；真正要挡路的墙，用 `给选中物体添加 NavMeshObstacle`。
/// 结果写日志 `_Diagnostics_BakeWalkable.log`。
///
/// 菜单：Tools → 场景 → 把选中物体烘焙为可行走（斜坡/楼梯）并重烘焙
/// </summary>
public static class BakeWalkableWizard
{
    private const string ReportPath = "../_Diagnostics_BakeWalkable.log";
    private const int EnvironmentLayer = 6;

    [MenuItem("Tools/场景/把选中物体烘焙为可行走（斜坡/楼梯）并重烘焙")]
    public static void BakeSelectedWalkable()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("⚠️ 请先在 Hierarchy 选中斜坡/楼梯/平台等要走上去的物体（可多选）。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }
        L($"===== 烘焙为可行走 | 选中 {selected.Length} 个 =====");

        int changed = 0, skipped = 0;
        foreach (var go in selected)
        {
            if (go == null) continue;
            if (go.GetComponent<NavMeshAgent>() != null)
            {
                L($"  {go.name}：是寻路代理（角色），跳过。");
                skipped++; continue;
            }

            bool did = false;
            // 1) 层 → 6
            if (go.layer != EnvironmentLayer)
            {
                go.layer = EnvironmentLayer;
                L($"  {go.name}：层 {go.layer} → {EnvironmentLayer} (Environment)");
                did = true;
            }
            // 2) 移除 ignoreFromBuild 的 NavMeshModifier
            var mods = go.GetComponents<NavMeshModifier>();
            foreach (var m in mods)
            {
                if (m == null) continue;
                if (m.ignoreFromBuild)
                {
                    Object.DestroyImmediate(m);
                    L($"  {go.name}：移除 NavMeshModifier(ignoreFromBuild)");
                    did = true;
                }
            }
            // 3) 移除 NavMeshObstacle（可行走面不能被挡）
            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs != null)
            {
                Object.DestroyImmediate(obs);
                L($"  {go.name}：移除 NavMeshObstacle");
                did = true;
            }
            // 4) 移除残余 NavMeshModifier（默认按 Walkable 烘焙）
            foreach (var m in go.GetComponents<NavMeshModifier>())
            {
                if (m == null) continue;
                Object.DestroyImmediate(m);
                L($"  {go.name}：移除 NavMeshModifier（恢复 Walkable）");
                did = true;
            }

            if (did) { EditorUtility.SetDirty(go); changed++; }
            else L($"  {go.name}：无需修改");
        }

        // 重烘焙
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        L($"--- 重烘焙 {surfaces.Length} 个 NavMeshSurface ---");
        NavMesh.RemoveAllNavMeshData();
        foreach (var s in surfaces)
        {
            if (s == null) continue;
            s.RemoveData();
            s.navMeshData = null;
            s.BuildNavMesh();
            var d = s.navMeshData;
            L($"  [{s.gameObject.name}] bounds={(d != null ? d.sourceBounds.ToString() : "NULL")}");
        }
        var tri = NavMesh.CalculateTriangulation();
        L($"--- 完成：处理 {changed} 个，跳过 {skipped} 个；烘焙后三角面 {tri.vertices.Length / 3} 个 ---");
        L("提示：真正的墙/柱子用 `Tools → 场景 → 给选中物体添加 NavMeshObstacle`，别用本工具。");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            Debug.Log($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }
}
