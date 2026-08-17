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
/// 修复 Game 场景 NavMesh「0 三角面 + 角色 not close enough」。
///
/// 根因（已诊断）：
///   ① 所有环境物体（含 Ground 地面）被误加 NavMeshModifier(ignoreFromBuild)=True
///      ——之前「标记可能走上天」工具把 1000×1000 的大平面也判成"浮空"排除，烘焙层被清空 → 0 三角面；
///   ② 地面 Ground 在 y=-5，而角色/楼梯/Cube 都在 y≈0.3（地面被挪低约 5 米）。
///
/// 修复步骤：
///   1. 移除场景内所有 NavMeshModifier(ignoreFromBuild)；
///   2. 找到地面（水平包围盒 >100m 的平面），抬升使其表面 = 角色脚底平均 Y；
///   3. 清空并重新烘焙所有 NavMeshSurface；
///   4. 把每个玩家/敌人吸附到最近 NavMesh 点（Scene 模式直接改 Transform，场景已 Unpack）；
///   5. 保存场景并写日志到项目根 _Diagnostics_NavMeshFix.log（同时打印 Console）。
///
/// 幂等可重跑。菜单：Tools → 玩家 → 修复 NavMesh 排除标记并校正地面（重烘焙）
/// </summary>
public static class FixNavMeshAndGroundWizard
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string ReportPath = "../_Diagnostics_NavMeshFix.log";

    [MenuItem("Tools/玩家/修复 NavMesh 排除标记并校正地面（重烘焙）")]
    public static void Fix()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        L($"===== NavMesh 修复开始 | 场景: {scene.name} =====");

        // ---- 1. 移除 ignoreFromBuild 标记 ----
        try
        {
            var mods = Object.FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None)
                .Where(m => m != null && m.ignoreFromBuild).ToArray();
            L($"--- 1. 移除 NavMeshModifier(ignoreFromBuild)：{mods.Length} 个 ---");
            foreach (var m in mods)
            {
                L($"  移除 {m.gameObject.name}（路径={PathOf(m.transform)}）");
                Object.DestroyImmediate(m);
            }
        }
        catch (System.Exception e) { L($"  [1] 异常：{e.Message}\n{e.StackTrace}"); }

        // ---- 2. 校正地面 Y：地面表面 = 角色脚底平均 Y ----
        try
        {
            var players = Object.FindObjectsByType<PlayerModel>(FindObjectsSortMode.None)
                .Where(p => p != null && p.cc != null).ToArray();
            if (players.Length > 0)
            {
                float avgFootY = players.Average(p => p.transform.position.y + p.cc.center.y - p.cc.height * 0.5f + p.cc.skinWidth);
                L($"--- 2. 校正地面：角色脚底平均 Y={avgFootY:F2} ---");
                foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (mr == null) continue;
                    var b = mr.bounds;
                    if (b.size.x > 100f && b.size.z > 100f && b.size.y < 1f) // 大面积、扁平的"地面"
                    {
                        float surfaceY = b.center.y;
                        float delta = avgFootY - surfaceY;
                        if (Mathf.Abs(delta) > 0.05f)
                        {
                            mr.transform.position += Vector3.up * delta;
                            L($"  地面 {mr.gameObject.name}：Y {surfaceY:F2} → {avgFootY:F2}（上移 {delta:F2}m）");
                        }
                        else L($"  地面 {mr.gameObject.name} 已对齐（Y={surfaceY:F2}）");
                    }
                }
            }
            else L("  ⚠️ 未找到带 CharacterController 的玩家，跳过地面校正。");
        }
        catch (System.Exception e) { L($"  [2] 异常：{e.Message}\n{e.StackTrace}"); }

        // ---- 3. 清空 + 重新烘焙 ----
        try
        {
            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            L($"--- 3. 重新烘焙：{surfaces.Length} 个 NavMeshSurface ---");
            NavMesh.RemoveAllNavMeshData();
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                s.RemoveData();
                s.navMeshData = null;
                s.BuildNavMesh();
                var d = s.navMeshData;
                L($"  [{s.gameObject.name}] 烘焙完成 bounds={(d != null ? d.sourceBounds.ToString() : "NULL")}");
            }
        }
        catch (System.Exception e) { L($"  [3] 异常：{e.Message}\n{e.StackTrace}"); }

        // ---- 4. 角色吸附到最近 NavMesh 点 ----
        try
        {
            L("--- 4. 角色吸附到 NavMesh ---");
            var comps = Object.FindObjectsByType<PlayerModel>(FindObjectsSortMode.None).Cast<Component>()
                .Concat(Object.FindObjectsByType<ZombieEnemy>(FindObjectsSortMode.None).Cast<Component>()).ToArray();
            foreach (var pm in comps)
            {
                var pos = pm.transform.position;
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(pos, hit.position) > 0.05f)
                    {
                        pm.transform.position = hit.position;
                        L($"  {pm.name}：{pos} → {hit.position}（移动 {Vector3.Distance(pos, hit.position):F2}m）");
                    }
                    else L($"  {pm.name}：已在网格上（{pos}）");
                }
                else L($"  {pm.name}：❌ 20m 内无 NavMesh，位置 {pos}（请手动放置到可走面）");
            }
        }
        catch (System.Exception e) { L($"  [4] 异常：{e.Message}\n{e.StackTrace}"); }

        // ---- 5. 汇总 + 保存 + 写日志 ----
        var tri = NavMesh.CalculateTriangulation();
        L($"--- 烘焙后三角面：{tri.vertices.Length / 3} 个 ---");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }

    private static string PathOf(Transform t)
    {
        if (t == null) return "NULL";
        var names = new System.Collections.Generic.List<string>();
        while (t != null) { names.Insert(0, t.name); t = t.parent; }
        return string.Join("/", names);
    }
}
