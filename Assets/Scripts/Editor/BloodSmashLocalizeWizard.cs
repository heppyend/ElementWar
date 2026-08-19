using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 让受击/喷血特效「停在命中点」：把 Green/Blood_Smash_Small_Green.prefab 的 Droplets 子发射器
/// （血滴）的重力和初速调小，血花从敌人中弹处喷发后不再掉落到地面和滴血特效混在一起。
///   重力：gravityModifier 1 → 0.2
///   初速：startSpeed 4~6 → 2（恒定）
///
/// 只改 prefab 资产（场景敌人通过 GUID 引用同一资产，改动自动生效），幂等可重跑。
/// 结果写日志 `_Diagnostics_BloodSmashLocalize.log`（同时打印 Console）。
///
/// 菜单：Tools → 玩家 → 受击血花停在命中点（减小重力+喷射）
/// </summary>
public static class BloodSmashLocalizeWizard
{
    private const string ReportPath = "../_Diagnostics_BloodSmashLocalize.log";
    private const string SmashPrefabPath = "Assets/Resource/Effects/Hurts/prefab/Green/Blood_Smash_Small_Green.prefab";

    [MenuItem("Tools/玩家/受击血花停在命中点（减小重力+喷射）")]
    public static void LocalizeSmash()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L("===== 受击血花停在命中点 =====");

        GameObject root = PrefabUtility.LoadPrefabContents(SmashPrefabPath);
        if (root == null)
        {
            L($"❌ 打不开 prefab：{SmashPrefabPath}");
            return;
        }

        try
        {
            ParticleSystem droplets = null;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name == "Droplets")
                {
                    droplets = ps;
                    break;
                }
            }

            if (droplets == null)
            {
                L($"❌ {SmashPrefabPath} 里找不到 Droplets 子发射器");
                return;
            }

            var main = droplets.main;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.2f);//血滴不再快速下坠
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f);//喷射距离缩短，血花留在命中点附近

            PrefabUtility.SaveAsPrefabAsset(root, SmashPrefabPath);
            L($"✅ {SmashPrefabPath}：Droplets 重力 1→0.2、初速 4~6→2（血花停在命中点）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
