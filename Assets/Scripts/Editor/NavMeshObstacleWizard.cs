using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 给选中物体批量添加/移除 NavMeshObstacle（阻挡 NavMeshAgent 寻路）。
///
/// 背景：随从/敌人用 NavMeshAgent 移动，**不理会物理 Collider**（只有主控的 CharacterController
/// 用物理碰撞）。所以场景里的 Cube 有 MeshCollider 但寻路代理照样穿墙。
/// 解决：给这些"墙/平台"加 NavMeshObstacle（carving=true 会在 NavMesh 上动态雕刻出障碍，
/// 代理绕行且走不上去）。静态墙更优解是烘焙成 Not Walkable，但 NavMeshObstacle 不用重烘焙、立即生效。
///
/// 行为：对选中的每个物体（自动跳过带 NavMeshAgent 的角色）：
///   1. 用其 Renderer/Collider 世界包围盒设置 NavMeshObstacle 的 shape=Box、size/center（局部空间）；
///   2. carving=true（动态雕刻避障）。
/// 结果写日志 `_Diagnostics_NavMeshObstacle.log`。
///
/// 菜单：
///   Tools → 场景 → 给选中物体添加 NavMeshObstacle（阻挡寻路）
///   Tools → 场景 → 移除选中物体的 NavMeshObstacle
/// </summary>
public static class NavMeshObstacleWizard
{
    private const string ReportPath = "../_Diagnostics_NavMeshObstacle.log";

    [MenuItem("Tools/场景/给选中物体添加 NavMeshObstacle（阻挡寻路）")]
    public static void AddObstacles()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("⚠️ 请先在 Hierarchy 选中要加障碍的物体（Cube 等墙体，可多选）。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }
        L($"===== 添加 NavMeshObstacle | 选中 {selected.Length} 个 =====");

        int added = 0, skipped = 0;
        foreach (var go in selected)
        {
            if (go == null) continue;
            if (go.GetComponent<NavMeshAgent>() != null)
            {
                L($"  {go.name}：是寻路代理（角色），跳过。");
                skipped++; continue;
            }

            var bounds = GetWorldBounds(go);
            if (!bounds.HasValue)
            {
                L($"  {go.name}：无 Renderer/Collider，无法定尺寸，跳过。");
                skipped++; continue;
            }

            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs == null) obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            // size/center 是局部空间：世界包围盒换算到本地
            obs.size = new Vector3(
                SafeDiv(bounds.Value.size.x, go.transform.lossyScale.x),
                SafeDiv(bounds.Value.size.y, go.transform.lossyScale.y),
                SafeDiv(bounds.Value.size.z, go.transform.lossyScale.z));
            obs.center = go.transform.InverseTransformPoint(bounds.Value.center);
            obs.carving = true;
            obs.carveOnlyStationary = true; // 静态墙专用，更稳
            EditorUtility.SetDirty(go);
            added++;
            L($"  {go.name}：+NavMeshObstacle 尺寸={obs.size} carving=true（路径={PathOf(go.transform)}）");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        L($"--- 完成：添加 {added} 个，跳过 {skipped} 个 ---");
        L("提示：如遇平台顶面仍可被走上，可对平台改用『烘焙成 Not Walkable』（见文档）。");
        WriteReport(sb);
    }

    [MenuItem("Tools/场景/移除选中物体的 NavMeshObstacle")]
    public static void RemoveObstacles()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("⚠️ 请先选中要移除障碍的物体。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }
        L($"===== 移除 NavMeshObstacle | 选中 {selected.Length} 个 =====");

        int removed = 0;
        foreach (var go in selected)
        {
            if (go == null) continue;
            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs == null) continue;
            Object.DestroyImmediate(obs);
            EditorUtility.SetDirty(go);
            removed++;
            L($"  {go.name}：已移除 NavMeshObstacle");
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        L($"--- 完成：移除 {removed} 个 ---");
        WriteReport(sb);
    }

    private static Bounds? GetWorldBounds(GameObject go)
    {
        var renders = go.GetComponentsInChildren<Renderer>(true);
        var cols = go.GetComponentsInChildren<Collider>(true);
        Bounds? b = null;
        if (renders != null && renders.Length > 0)
        {
            b = renders[0].bounds;
            foreach (var r in renders) if (r != null) b = Merge(b.Value, r.bounds);
        }
        if (cols != null && cols.Length > 0)
        {
            foreach (var c in cols)
            {
                if (c == null || c is CharacterController || c is TerrainCollider) continue;
                b = b.HasValue ? Merge(b.Value, c.bounds) : c.bounds;
            }
        }
        return b;
    }

    private static Bounds Merge(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
    private static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-6f ? 1f : a / b;

    private static string PathOf(Transform t)
    {
        if (t == null) return "NULL";
        var names = new System.Collections.Generic.List<string>();
        while (t != null) { names.Insert(0, t.name); t = t.parent; }
        return string.Join("/", names);
    }

    private static void WriteReport(StringBuilder sb)
    {
        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            Debug.Log($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }
}
