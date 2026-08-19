using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 清理滴血特效：滴到地面后只留一滩平面血，去掉血面上的喷血特效；滴血一次性、速度减半。
///
/// `Green/Blood_Dripping_Green.prefab` 的生成链（sub-emitter，properties=0 不继承父粒子颜色）：
///   DropletsWithBloodMarks（rate 8，碰撞开启）→ 血滴碰撞地面 → 生成 Cone → Cone 出生 → 生成 BloodMarks（血滩）
///   Droplets（rate 22，碰撞关闭）→ 滴落动画，直接穿过地面、不在血面残留
///   root（rate 0）→ 不自发射，仅作 EffectPool 存活锚点
///
/// 三处处理：
///   1. Cone / DropletsWithBloodMarks 粒子**透明化**（alpha=0）——不显示血面喷血/漂浮血块，但仍发射/碰撞保证血滩链不断
///   2. Droplets / DropletsWithBloodMarks 改为**一次性爆发**（loop=false，时长 1s）——第一次掉到地面生成血滩后滴血即停，不再持续 6 秒
///   3. Droplets / DropletsWithBloodMarks 初速**减半**（6~15 → 3~7.5）——滴血下落速度降一半
///
/// 只改 prefab 资产（场景通过 GUID 引用，改动自动生效），幂等可重跑。
/// 结果写日志 `_Diagnostics_BloodDripClean.log`（同时打印 Console）。
///
/// 菜单：Tools → 玩家 → 滴血特效只留血滩（去掉血面喷血）
/// </summary>
public static class BloodDripCleanWizard
{
    private const string ReportPath = "../_Diagnostics_BloodDripClean.log";
    private const string DripPrefabPath = "Assets/Resource/Effects/Hurts/prefab/Green/Blood_Dripping_Green.prefab";

    [MenuItem("Tools/玩家/滴血特效只留血滩（去掉血面喷血）")]
    public static void CleanDrip()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L("===== 滴血特效只留血滩 =====");

        GameObject root = PrefabUtility.LoadPrefabContents(DripPrefabPath);
        if (root == null)
        {
            L($"❌ 打不开 prefab：{DripPrefabPath}");
            return;
        }

        try
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null || systems.Length == 0)
            {
                L($"❌ {DripPrefabPath} 里没有 ParticleSystem");
                return;
            }

            foreach (var ps in systems)
            {
                switch (ps.gameObject.name)
                {
                    case "Blood_Dripping_Green":
                        // root：不自发射（rate 0），保留原样
                        L($"  root：不自发射，保留");
                        break;

                    case "Cone":
                        // Cone 出生时生成血滩 BloodMarks；粒子透明化隐藏漂浮血块，但仍发射以触发血滩
                        SetInvisible(ps);
                        L($"  Cone：粒子 alpha→0（隐藏漂浮血块，保留发射→生成血滩）");
                        break;

                    case "DropletsWithBloodMarks":
                        // 血滩生成源：透明化隐藏落地血滴 + 一次性爆发 + 速度减半
                        SetInvisible(ps);
                        MakeDripOneShotAndHalve(ps);
                        L($"  DropletsWithBloodMarks：透明化 + 一次性(1s) + 初速减半");
                        break;

                    case "Droplets":
                        // 可见滴落动画：一次性爆发 + 速度减半（穿过地面不残留）
                        MakeDripOneShotAndHalve(ps);
                        L($"  Droplets：一次性(1s) + 初速减半");
                        break;

                    case "BloodMarks":
                        // 地面血滩
                        L($"  BloodMarks：保留（地面血滩）");
                        break;

                    default:
                        L($"  （未知子粒子系统 {ps.gameObject.name}，保留）");
                        break;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, DripPrefabPath);
            L($"✅ {DripPrefabPath}：血面喷血已去掉，滴血一次性(1s)且速度减半，血滩保留");
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

    /// <summary>把粒子透明化（alpha=0），不影响发射/碰撞/子发射器触发。</summary>
    private static void SetInvisible(ParticleSystem ps)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0f));
    }

    /// <summary>
    /// 滴血改为一次性爆发（loop=false、时长 1s），并把初速减半（原 6~15 → 3~7.5）。
    /// </summary>
    private static void MakeDripOneShotAndHalve(ParticleSystem ps)
    {
        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        var speed = main.startSpeed;
        speed.mode = ParticleSystemCurveMode.TwoConstants;
        speed.constantMin = 3f;
        speed.constantMax = 7.5f;
        main.startSpeed = speed;
    }
}
