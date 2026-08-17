using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键配置 Running Slide 动画资产：
/// 把 Y Bot@Running Slide.fbx 从 Generic 转为 Humanoid
/// （这样才能重定向到 Lumine/芙宁娜 的 Humanoid 骨骼），
/// 并把动画 clip 命名为 "RunningSlide"、关闭循环
/// （non-loop 才能用 normalizedTime>=1 判断动画播完）。
///
/// 用法：Unity 菜单栏 → Tools → 配置 Running Slide 动画 (Humanoid)
/// 运行后请选中该 FBX，检查 Rig 面板 Avatar 是否正常生成（无黄色警告）。
/// </summary>
public static class SetupRunningSlide
{
    private const string FbxPath = "Assets/Resource/Animations/Player/Y Bot@Running Slide.fbx";

    [MenuItem("Tools/配置 Running Slide 动画 (Humanoid)")]
    public static void ConfigureRunningSlide()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"找不到模型: {FbxPath}");
            return;
        }

        // 1. Rig → Humanoid，自动生成 Avatar（Mixamo 标准骨骼，Unity 可直接识别）
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.autoGenerateAvatarMappingIfUnspecified = true;

        // 2. 动画 clip：改名 RunningSlide + 关闭循环
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            clips = new[] { new ModelImporterClipAnimation() };
        clips[0].name = "RunningSlide";
        clips[0].loopTime = false;
        clips[0].loop = false;
        importer.clipAnimations = clips;

        // 3. 重新导入
        importer.SaveAndReimport();

        Debug.Log("Running Slide 已配置为 Humanoid，clip 名 = RunningSlide (non-loop)。");
        Debug.Log("请选中该 FBX，检查 Rig 面板的 Avatar 是否正常生成（如有黄色警告请报告）。");
    }
}
