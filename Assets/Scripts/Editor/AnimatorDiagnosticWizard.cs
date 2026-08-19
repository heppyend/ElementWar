using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 诊断当前打开场景里所有 Animator 引用的控制器（写 _Diagnostics_Animators.log）。
/// 用于排查"改了 TPS_Movement.controller 但 PVE 场景动画没变"——确认场景对象实际指向哪个控制器。
/// 菜单：Tools → 玩家 → 诊断场景 Animator 控制器（写日志）
/// </summary>
public static class AnimatorDiagnosticWizard
{
    private const string ReportPath = "../_Diagnostics_Animators.log";

    [MenuItem("Tools/玩家/诊断场景 Animator 控制器（写日志）")]
    public static void Diagnose()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L("===== 场景 Animator 控制器诊断 =====");
        L($"活动场景: {SceneManager.GetActiveScene().name}");

        var animators = Object.FindObjectsOfType<Animator>(true);
        L($"共 {animators.Length} 个 Animator：");
        foreach (var anim in animators)
        {
            var ctrl = anim.runtimeAnimatorController;
            string ctrlPath = ctrl != null ? AssetDatabase.GetAssetPath(ctrl) : "(空/未分配)";
            string ctrlGuid = "";
            if (ctrl != null && !string.IsNullOrEmpty(ctrlPath))
            {
                string metaPath = ctrlPath + ".meta";
                if (File.Exists(metaPath))
                {
                    foreach (var ln in File.ReadAllLines(metaPath))
                        if (ln.StartsWith("guid:")) { ctrlGuid = ln.Substring(6).Trim(); break; }
                }
            }
            L($"{anim.gameObject.name} → {ctrlPath} [guid {ctrlGuid}]");
            var pm = anim.GetComponent<PlayerModel>();
            if (pm != null)
                L($"    PlayerModel: useFPSMovement={pm.useFPSMovement}, disableStateMachine={pm.disableStateMachine}, cc={(pm.cc != null ? "有" : "null")}");
        }

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }
}
