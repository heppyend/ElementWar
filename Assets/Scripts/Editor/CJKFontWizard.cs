using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;   // GlyphRenderMode（TMP 的 CreateFontAsset 用的引擎枚举，不在 TMPro 命名空间）

namespace ElementWarEditor
{
    /// <summary>
    /// 中文 UI 乱码修复（口口口，TMP 默认字体 LiberationSans 无 CJK 字形）。
    /// 烘焙一个 Dynamic TMP 字体资产（印品抹茶体.ttf，运行时按需生成字形），设为 TMP 默认字体，
    /// 并扫描全项目场景/预制体把"仍用旧默认字体/null"的 TMP 组件换成中文字体。
    /// Tools/玩家 菜单（幂等可重跑）。
    /// </summary>
    public static class CJKFontWizard
    {
        private const string SourceFontPath = "Assets/Resource/字体/印品抹茶体.ttf";
        private const string TargetDir = "Assets/Resource/Fonts";
        private const string TargetAssetPath = TargetDir + "/CJK_Default_SDF.asset";
        private const string OldDefaultPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string OldFallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

        [MenuItem("Tools/玩家/烘焙中文默认字体（修复口口口）")]
        public static void BakeCJKDefaultFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[CJKFont] 找不到源字体 {SourceFontPath}");
                return;
            }

            // 删除旧的重新生成（幂等）
            AssetDatabase.DeleteAsset(TargetAssetPath);
            if (!AssetDatabase.IsValidFolder(TargetDir))
                AssetDatabase.CreateFolder("Assets/Resource", "Fonts");

            // Dynamic 字体：运行时按需生成字形，任意中文字都能显示
            var tmpFont = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 72,
                atlasPadding: 4,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
            if (tmpFont == null)
            {
                Debug.LogError("[CJKFont] CreateFontAsset 失败（字体导入设置需勾选 Include Font Data）");
                return;
            }

            // CreateFontAsset 建的图集纹理是 0x0（运行时按需填充）；先初始化为 atlas 尺寸，
            // 否则 0x0 纹理存为资产会有问题。
            if (tmpFont.atlasTextures != null && tmpFont.atlasTextures.Length > 0 && tmpFont.atlasTextures[0] != null)
            {
                var tex = tmpFont.atlasTextures[0];
                tex.Reinitialize(tmpFont.atlasWidth, tmpFont.atlasHeight);
                tex.Apply();
            }

            AssetDatabase.CreateAsset(tmpFont, TargetAssetPath);

            // ⚠️ 图集纹理/材质必须存为字体资产的子资产——否则编辑器重载后引用悬空，
            //    运行时 TMP 用它时报 MissingReferenceException: Texture2D destroyed。
            foreach (var tex in tmpFont.atlasTextures)
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, tmpFont);
            if (tmpFont.material != null)
                AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);
            // SaveAssets 会刷新资产库（官方 TMP 创建流程也只到 SaveAssets，不要 Refresh——
            // Refresh 会重载资产使内存里的 tmpFont 引用失效，后续设默认/扫场景会拿到陈旧引用）
            AssetDatabase.SaveAssets();

            // 设为 TMP 默认字体 → 运行时自建 UI（PVP 大厅/HUD/信息窗）自动生效。
            // ⚠️ defaultFontAsset 是只读静态属性（无 setter），需改私有序列化字段 m_defaultFontAsset
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = tmpFont;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[CJKFont] 烘焙完成并设为 TMP 默认字体：{TargetAssetPath}");

            // 顺手扫描场景/预制体：把仍用旧默认字体/null 的 TMP 换成中文（含主菜单场景内置 TMP）
            ReplaceAllTmpFonts(tmpFont);
        }

        [MenuItem("Tools/玩家/替换全项目 TMP 字体为中文（场景/预制体）")]
        public static void ReplaceAllTmpFonts()
        {
            ReplaceAllTmpFonts(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetAssetPath));
        }

        /// <summary>扫描 Assets/Scenes + Assets/Resource 的场景/预制体，把"无字体或旧默认字体"的 TMP 换成中文字体。</summary>
        public static void ReplaceAllTmpFonts(TMP_FontAsset newFont)
        {
            if (newFont == null)
            {
                Debug.LogError($"[CJKFont] 请先运行「烘焙中文默认字体」，或资产不存在：{TargetAssetPath}");
                return;
            }

            // 旧默认字体集合：只替换这些（或 null），保留故意用的艺术字体
            var oldDefaults = new HashSet<TMP_FontAsset>();
            foreach (var p in new[] { OldDefaultPath, OldFallbackPath })
            {
                var old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                if (old != null) oldDefaults.Add(old);
            }

            int replaced = 0;

            // ① 场景（Build 设置 + Assets/Scenes 全部；资源包示例场景不扫，无用且慢）。
            // Additive 打开逐个处理：不打断当前场景，处理完保存并关闭；当前激活场景就地替换保存。
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            scenes.AddRange(Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories));
            var activePath = SceneManager.GetActiveScene().path;
            foreach (var scenePath in scenes.Distinct())
            {
                try
                {
                    if (scenePath == activePath)
                    {
                        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                            replaced += ReplaceInObject(root.transform, newFont, oldDefaults);
                        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    }
                    else
                    {
                        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                        foreach (var root in scene.GetRootGameObjects())
                            replaced += ReplaceInObject(root.transform, newFont, oldDefaults);
                        EditorSceneManager.SaveScene(scene);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CJKFont] 处理场景失败（跳过）：{scenePath} {e.Message}");
                }
            }

            // ② 预制体（只扫游戏资源目录；资源包示例 prefab 不扫）
            foreach (var prefabPath in Directory.GetFiles("Assets/Resource", "*.prefab", SearchOption.AllDirectories))
            {
                try
                {
                    var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                    replaced += ReplaceInObject(contents.transform, newFont, oldDefaults);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CJKFont] 处理预制体失败（跳过）：{prefabPath} {e.Message}");
                }
            }

            Debug.Log($"[CJKFont] 已替换 {replaced} 个 TMP 组件字体为中文默认");
        }

        /// <summary>递归替换某物体树中"无字体或旧默认字体"的 TMP 文本组件，返回替换数。</summary>
        private static int ReplaceInObject(Transform t, TMP_FontAsset newFont, HashSet<TMP_FontAsset> oldDefaults)
        {
            int n = 0;
            foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
            {
                var current = text.font;
                if (current == null || current == newFont || oldDefaults.Contains(current))
                {
                    if (current != newFont)
                    {
                        text.font = newFont;
                        n++;
                    }
                }
            }
            return n;
        }
    }
}
