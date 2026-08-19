using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 给两把枪挂载可替换音效组件（幂等可重跑）：
///   - 荧（M4A1）→ AR_1p_01 / AR_1p_02
///   - 芙宁娜（AK47）→ AutoGun_1p_01 / AutoGun_1p_02
///
/// 同时处理两处（缺哪个补哪个）：
///   1. 角色 prefab 资产：Lumine FBX.prefab（Weapon_Lumine）/ Pilot Furina.prefab（Weapon-Furina）
///   2. 当前打开的**场景**：Game 场景已 Unpack（对象已脱离 prefab），改 prefab 不会同步进场景，
///      必须对场景里带 PlayerWeapon 的武器物体直接补挂（按物体名包含 Lumine/Furina 区分两把枪）。
///
/// 以后想换枪声：直接在对应 WeaponAudio 的 Inspector 里替换/增删 fireClips 数组即可，无需再跑本工具。
/// 结果写日志 `_Diagnostics_WeaponAudio.log`（同时打印 Console）。
///
/// 菜单：Tools → 玩家 → 给两把枪挂载音效组件（M4 / AK）
/// </summary>
public static class WeaponAudioWizard
{
    private const string ReportPath = "../_Diagnostics_WeaponAudio.log";
    private const string AudioDir = "Assets/PostApocalypseGuns/AssaultRifles";

    private static readonly string[] LumineClips = { "AR_1p_01.wav", "AR_1p_02.wav" };
    private static readonly string[] FurinaClips = { "AutoGun_1p_01.wav", "AutoGun_1p_02.wav" };

    [MenuItem("Tools/玩家/给两把枪挂载音效组件（M4 / AK）")]
    public static void AttachWeaponAudio()
    {
        var sb = new StringBuilder();
        void L(string s) { sb.AppendLine(s); Debug.Log(s); }

        L("===== 武器音效挂载 | M4A1（荧）/ AK47（芙宁娜）=====");

        // 1. prefab 资产
        AttachToPrefab(L, "Assets/Resource/Prefabs/Lumine FBX.prefab", "荧（M4A1）", LumineClips);
        AttachToPrefab(L, "Assets/Resource/Prefabs/Pilot Furina.prefab", "芙宁娜（AK47）", FurinaClips);

        // 2. 活动场景（Game 场景已 Unpack，改 prefab 不同步，需直接补挂场景对象）
        AttachToScene(L);

        L("--- 完成：音效已挂载，无需再次运行（重复运行幂等）---");

        try
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
            L($"📄 日志已写入：{abs}");
        }
        catch (System.Exception ex) { Debug.LogError($"写日志失败：{ex.Message}"); }
    }

    private static void AttachToPrefab(System.Action<string> L, string prefabPath, string label, string[] clipNames)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            L($"❌ {label}：打不开 prefab {prefabPath}");
            return;
        }

        try
        {
            // 找到挂 PlayerWeapon 的武器物体（两把枪各只有一个）
            PlayerWeapon weapon = null;
            foreach (var pw in root.GetComponentsInChildren<PlayerWeapon>(true))
            {
                weapon = pw;
                break;
            }

            if (weapon == null)
            {
                L($"❌ {label}：prefab 里找不到 PlayerWeapon（{prefabPath}）");
                return;
            }

            AttachAudio(L, weapon.gameObject, label, clipNames, "prefab");
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AttachToScene(System.Action<string> L)
    {
        var weapons = Object.FindObjectsOfType<PlayerWeapon>(true);
        if (weapons == null || weapons.Length == 0)
        {
            L("⚠️ 场景里没找到任何 PlayerWeapon（是否没打开 Game 场景？）");
            return;
        }

        int handled = 0;
        foreach (var pw in weapons)
        {
            string name = pw.gameObject.name;
            string[] clips = name.Contains("Lumine") ? LumineClips
                          : name.Contains("Furina") ? FurinaClips
                          : null;
            if (clips == null)
            {
                L($"  （跳过场景武器 {name}：不是两把枪）");
                continue;
            }

            AttachAudio(L, pw.gameObject, name, clips, "场景");
            handled++;
        }

        if (handled > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            L($"✅ 场景已保存（{handled} 处武器补挂音效）");
        }
    }

    /// <summary>给武器物体补挂 WeaponAudio + AudioSource 并写入 fireClips（幂等）。</summary>
    private static void AttachAudio(System.Action<string> L, GameObject weaponGO, string label, string[] clipNames, string where)
    {
        WeaponAudio audio = weaponGO.GetComponent<WeaponAudio>();
        if (audio == null) audio = weaponGO.AddComponent<WeaponAudio>();
        if (weaponGO.GetComponent<AudioSource>() == null) weaponGO.AddComponent<AudioSource>();

        audio.fireClips = new AudioClip[clipNames.Length];
        for (int i = 0; i < clipNames.Length; i++)
        {
            string clipPath = $"{AudioDir}/{clipNames[i]}";
            audio.fireClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (audio.fireClips[i] == null)
                L($"  ⚠️ {label}（{where}）：找不到音效 {clipPath}");
        }

        L($"✅ {label}（{weaponGO.name}，{where}）：WeaponAudio 已挂载，fireClips = [{string.Join(", ", clipNames)}]");
    }
}
