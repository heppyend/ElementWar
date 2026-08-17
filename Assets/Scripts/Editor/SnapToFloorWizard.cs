using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 批量把选中物体「落到地面」——每个物体的底面自动吸附到它正下方第一个表面（射线检测）。
/// 适用于：换地板后大量 cube 悬空，一次性全部放下。
///
/// 原理：对每个选中物体，
///   1. 取其全部 Renderer（无 Renderer 则取 Collider）的世界包围盒，算出底面 Y；
///   2. 从物体上方 (topY + 20m) 向下打 `RaycastAll`，跳过物体自身/子物体的碰撞体；
///   3. 命中第一个表面 → 移动物体，使底面 Y = 命中点 Y（XZ 不动，只改 Y）。
///
/// ⚠️ 依赖地面有 Collider（Unity 的 Plane 原始体默认无碰撞体，需手动加 MeshCollider）。
/// 结果写日志 `_Diagnostics_SnapToFloor.log`（同时打印 Console）。
///
/// 菜单：Tools → 场景 → 把选中物体落到地面（吸附到表面）
/// </summary>
public static class SnapToFloorWizard
{
    private const string ReportPath = "../_Diagnostics_SnapToFloor.log";

    /// <summary>射线检测层级（默认全层，命中正下方第一个表面）。</summary>
    public static int snapLayerMask = ~0;
    /// <summary>物体离地超过该距离仍吸附（保护：太远可能是设计上的悬浮）。</summary>
    public const float MaxSnapDistance = 50f;

    [MenuItem("Tools/场景/把选中物体落到地面（吸附到表面）")]
    public static void SnapSelectedToFloor()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            L("⚠️ 请先在 Hierarchy 选中要落地的物体（可多选）。");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        L($"===== 落到地面 | 选中 {selected.Length} 个物体 =====");

        int placed = 0, skipped = 0;
        foreach (var go in selected)
        {
            if (go == null) continue;
            try
            {
                // ---- 计算世界包围盒（优先 Renderer，兜底 Collider）----
                var renders = go.GetComponentsInChildren<Renderer>(true);
                var colliders = go.GetComponentsInChildren<Collider>(true);
                Bounds? b = null;
                if (renders != null && renders.Length > 0)
                {
                    b = renders[0].bounds;
                    foreach (var r in renders) if (r != null) b = Merge(b.Value, r.bounds);
                }
                else if (colliders != null && colliders.Length > 0)
                {
                    b = colliders[0].bounds;
                    foreach (var c in colliders) if (c != null) b = Merge(b.Value, c.bounds);
                }

                if (!b.HasValue)
                {
                    L($"  {go.name}：无 Renderer/Collider，跳过。");
                    skipped++; continue;
                }

                float bottomY = b.Value.min.y;
                float topY = b.Value.max.y;
                Vector3 origin = new Vector3(go.transform.position.x, topY + 20f, go.transform.position.z);
                float rayDist = topY + 20f + MaxSnapDistance;

                // ---- RaycastAll 向下找第一个表面（跳过自身/子物体）----
                var hits = Physics.RaycastAll(origin, Vector3.down, rayDist, snapLayerMask, QueryTriggerInteraction.Ignore);
                var valid = hits.Where(h => h.collider != null && !h.collider.transform.IsChildOf(go.transform))
                                .OrderBy(h => h.distance).FirstOrDefault();

                if (valid.collider == null)
                {
                    L($"  {go.name}：❌ 下方无表面（检查地面是否有 Collider）pos={go.transform.position}");
                    skipped++; continue;
                }

                float delta = valid.point.y - bottomY;
                if (Mathf.Abs(delta) < 0.0001f)
                {
                    L($"  {go.name}：已在地面（底面 {bottomY:F2}）");
                    continue;
                }
                if (delta > MaxSnapDistance)
                {
                    L($"  {go.name}：❌ 距表面 {delta:F2}m（> {MaxSnapDistance}m），跳过（可能设计悬浮）。");
                    skipped++; continue;
                }

                go.transform.position += Vector3.up * delta;
                L($"  {go.name}：底面 {bottomY:F2} → {valid.point.y:F2}（下移 {delta:F2}m，命中 {valid.collider.name}）");
                placed++;
            }
            catch (System.Exception e)
            {
                L($"  {go.name}：异常 {e.Message}");
                skipped++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        L($"--- 完成：落地 {placed} 个，跳过 {skipped} 个 ---");

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }

    private static Bounds Merge(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }
}
