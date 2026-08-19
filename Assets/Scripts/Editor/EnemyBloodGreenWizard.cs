using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把敌人（丘丘人）的受击/喷血特效与滴血特效全部换成绿色（幂等可重跑）：
///   - bloodSmashPrefab：Red/Blood_Smash_Small_Red → Green/Blood_Smash_Small_Green
///   - bloodDrippingPrefab：Red/Blood_Dripping_Red → Green/Blood_Dripping_Green
///
/// 同时处理两处（缺哪个补哪个）：
///   1. 敌人 prefab 资产：Assets/Resource/Prefabs/丘丘人.prefab
///   2. 当前打开的**场景**：Game 场景已 Unpack（对象脱离 prefab），改 prefab 不会同步，
///      必须对场景里所有 ZombieEnemy 直接替换引用。
///
/// 换回红色或其它颜色：改 Red/Green 常量即可（Effects/Hurts/prefab 下有 Red/RedBright/RedDark/Blue/Lava/Black 等全套）。
/// 结果写日志 `_Diagnostics_EnemyBloodGreen.log`（同时打印 Console）。
///
/// 菜单：Tools → 玩家 → 敌人喷血特效换绿色（受击+滴血）
/// </summary>
public static class EnemyBloodGreenWizard
{
    private const string ReportPath = "../_Diagnostics_EnemyBloodGreen.log";
    private const string EnemyPrefabPath = "Assets/Resource/Prefabs/丘丘人.prefab";
    private const string SmashGreenPath = "Assets/Resource/Effects/Hurts/prefab/Green/Blood_Smash_Small_Green.prefab";
    private const string DrippingGreenPath = "Assets/Resource/Effects/Hurts/prefab/Green/Blood_Dripping_Green.prefab";

    [MenuItem("Tools/玩家/敌人喷血特效换绿色（受击+滴血）")]
    public static void SwapToGreen()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L("===== 敌人受击/滴血特效 → 绿色 =====");

        GameObject smash = AssetDatabase.LoadAssetAtPath<GameObject>(SmashGreenPath);
        GameObject dripping = AssetDatabase.LoadAssetAtPath<GameObject>(DrippingGreenPath);
        if (smash == null || dripping == null)
        {
            L($"❌ 找不到 Green 特效：\n  {SmashGreenPath}\n  {DrippingGreenPath}");
            return;
        }

        // 1. 敌人 prefab 资产
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        if (root == null)
        {
            L($"❌ 打不开敌人 prefab：{EnemyPrefabPath}");
            return;
        }
        try
        {
            var enemy = root.GetComponentInChildren<ZombieEnemy>(true);
            if (enemy == null)
            {
                L($"❌ {EnemyPrefabPath} 里找不到 ZombieEnemy");
                return;
            }
            SwapEnemy(L, enemy, smash, dripping, "prefab");
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2. 活动场景（Game 已 Unpack，需直接替换场景对象）
        SwapScene(L, smash, dripping);

        L("--- 完成：敌人受击/滴血已全部换成绿色，无需再次运行（重复运行幂等）---");

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }

    private static void SwapScene(System.Action<string> L, GameObject smash, GameObject dripping)
    {
        var enemies = Object.FindObjectsOfType<ZombieEnemy>(true);
        if (enemies == null || enemies.Length == 0)
        {
            L("⚠️ 场景里没找到 ZombieEnemy（是否没打开 Game 场景？）");
            return;
        }

        foreach (var enemy in enemies)
        {
            SwapEnemy(L, enemy, smash, dripping, $"场景({enemy.gameObject.name})");
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        L($"✅ 场景已保存（共 {enemies.Length} 个敌人替换）");
    }

    private static void SwapEnemy(System.Action<string> L, ZombieEnemy enemy, GameObject smash, GameObject dripping, string where)
    {
        bool changed = false;
        if (enemy.bloodSmashPrefab != smash)
        {
            enemy.bloodSmashPrefab = smash;
            changed = true;
        }
        if (enemy.bloodDrippingPrefab != dripping)
        {
            enemy.bloodDrippingPrefab = dripping;
            changed = true;
        }
        L($"{(changed ? "✅" : "（已绿）")} {enemy.gameObject.name}（{where}）：bloodSmash={smash.name} / bloodDripping={dripping.name}");
    }
}
