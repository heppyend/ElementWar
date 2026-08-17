using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 诊断 Game 场景 NavMesh 的「层/几何」根因：烘焙出 0 三角面、角色 "not close enough to the NavMesh"。
/// 输出写到项目根 _Diagnostics_NavMeshScene.log（同时打印 Console），直接读文件即可，无需复制粘贴。
///
/// 修复项（v2）：
///   - 空引用读取用 `x != null` 显式判断（Unity 假空对象上 `?.` 会抛 UnassignedReferenceException，v1 在此崩了）
///   - 对烘焙层（层6）物体打印 MeshRenderer.enabled / MeshFilter.sharedMesh / MeshCollider.sharedMesh /
///     Navigation Static 标记 / NavMeshModifier(ignoreFromBuild) —— 定位"为什么收不进几何"
///   - 每段 try/catch 独立保护，finally 必定写文件（部分输出也能保存）
///
/// 菜单：Tools → 玩家 → 诊断 NavMesh 层配置（写日志文件）
/// </summary>
public static class NavMeshLayerDiagWizard
{
    private const string ReportPath = "../_Diagnostics_NavMeshScene.log";
    private const int EnvironmentLayer = 6;

    [MenuItem("Tools/玩家/诊断 NavMesh 层配置（写日志文件）")]
    public static void Diagnose()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L($"===== NavMesh 层配置诊断 v2 | 场景: {SceneManager.GetActiveScene().name} =====");

        // ---- 1. NavMeshSurface 设置 ----
        try
        {
            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            if (surfaces.Length == 0) L("⚠️ 场景中没有 NavMeshSurface！");
            foreach (var s in surfaces)
            {
                var d = s.navMeshData;
                L($"[Surface] '{s.gameObject.name}' 自身层={s.gameObject.layer} collect={s.collectObjects} " +
                  $"size={s.size} center={s.center} layerMask={s.layerMask.value} geo={s.useGeometry} " +
                  $"agentType={s.agentTypeID} 已烘焙bounds={(d != null ? d.sourceBounds.ToString() : "NULL(无数据)")}");
            }
        }
        catch (System.Exception e) { L($"  [1] 异常：{e.Message}"); }

        // ---- 2. 烘焙层（层6）物体详情：为什么收不进几何 ----
        try
        {
            var layer6 = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g != null && g.layer == EnvironmentLayer).ToArray();
            L($"--- 层 {EnvironmentLayer} (Environment) 上的物体：{layer6.Length} 个 ---");
            foreach (var g in layer6)
            {
                var mr = g.GetComponent<MeshRenderer>();
                var mf = g.GetComponent<MeshFilter>();
                var mc = g.GetComponent<MeshCollider>();
                var mod = g.GetComponent<NavMeshModifier>();
                bool navStatic = (GameObjectUtility.GetStaticEditorFlags(g) & StaticEditorFlags.NavigationStatic) != 0;

                L($"  [{g.name}] active={g.activeInHierarchy} layer={g.layer} " +
                  $"renderer={(mr != null ? "有(enabled=" + mr.enabled + ")" : "无")} " +
                  $"meshFilter={(mf != null && mf.sharedMesh != null ? "有mesh" + (mf.sharedMesh != null ? "(三角形" + mf.sharedMesh.triangles.Length / 3 + ")" : "") : (mf != null ? "有但mesh=null" : "无"))} " +
                  $"meshCollider={(mc != null ? "有(convex=" + mc.convex + ", mesh=" + (mc.sharedMesh != null ? "有" : "NULL") + ")" : "无")} " +
                  $"NavStatic={navStatic} " +
                  $"NavMeshModifier={(mod != null ? ("ignoreFromBuild=" + mod.ignoreFromBuild + " area=" + mod.area) : "无")}");
            }
        }
        catch (System.Exception e) { L($"  [2] 异常：{e.Message}"); }

        // ---- 3. 所有 NavMeshModifier(ignoreFromBuild) ----
        try
        {
            var mods = Object.FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None)
                .Where(m => m != null && m.ignoreFromBuild).ToArray();
            L($"--- NavMeshModifier(ignoreFromBuild) 被排除物体：{mods.Length} 个 ---");
            foreach (var m in mods)
                L($"    {m.gameObject.name}（层={m.gameObject.layer} 路径={PathOf(m.transform)}）");
        }
        catch (System.Exception e) { L($"  [3] 异常：{e.Message}"); }

        // ---- 4. 场景内所有 MeshRenderer（其他层的，看地面是不是被挪到了别的层）----
        try
        {
            var mrs = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(m => m != null && m.gameObject.layer != EnvironmentLayer).ToArray();
            L($"--- 非层{EnvironmentLayer} 的 MeshRenderer：{mrs.Length} 个（按层分组）---");
            foreach (var g in mrs.Select(m => m.gameObject).GroupBy(g => LayerMask.LayerToName(g.layer)))
            {
                L($"[层:{g.Key} ({g.First().layer})] {g.Count()} 个");
                foreach (var go in g.Take(20))
                    L($"    {go.name}  bounds={go.GetComponent<MeshRenderer>().bounds}");
                if (g.Count() > 20) L($"    ... 共 {g.Count()} 个");
            }
        }
        catch (System.Exception e) { L($"  [4] 异常：{e.Message}"); }

        // ---- 5. 玩家/敌人：层 + 位置 + 脚下 10m 内是否有 NavMesh ----
        try
        {
            L("--- 角色接地（层 + 位置 + 脚下 10m NavMesh）---");
            var agents = Object.FindObjectsByType<PlayerModel>(FindObjectsSortMode.None).Cast<Component>()
                .Concat(Object.FindObjectsByType<ZombieEnemy>(FindObjectsSortMode.None).Cast<Component>()).ToArray();
            foreach (var pm in agents)
            {
                var na = pm.GetComponent<NavMeshAgent>();
                string aInfo = na != null ? $"agent.enabled={na.enabled} isOnNavMesh={na.isOnNavMesh}" : "无NavMeshAgent";
                L($"  {pm.name} 层={pm.gameObject.layer} pos={pm.transform.position} {aInfo}");
                if (NavMesh.SamplePosition(pm.transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                    L($"    → 最近面点 {hit.position} ΔY={pm.transform.position.y - hit.position.y:F2}");
                else
                    L($"    → ❌ 10m 内无 NavMesh");
            }
        }
        catch (System.Exception e) { L($"  [5] 异常：{e.Message}"); }

        // ---- 6. 运行时 NavMesh 汇总 ----
        try
        {
            var tri = NavMesh.CalculateTriangulation();
            L($"--- 运行时 NavMesh 汇总：三角面 {tri.vertices.Length / 3} 个 ---");
        }
        catch (System.Exception e) { L($"  [6] 异常：{e.Message}"); }

        // ---- 写文件（必定执行）----
        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 诊断已写入：{abs}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"写文件失败：{ex.Message}");
            Debug.Log(sb.ToString());
        }
    }

    private static string PathOf(Transform t)
    {
        if (t == null) return "NULL";
        var names = new System.Collections.Generic.List<string>();
        while (t != null) { names.Insert(0, t.name); t = t.parent; }
        return string.Join("/", names);
    }
}
