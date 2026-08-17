using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 红狼玫瑰 FBX 导入后处理：
/// 将 FBX 内嵌的 Phong 材质（导入为内置 Standard shader）替换为 URP/Lit，
/// 并把贴图重链到 <模型目录>/textures 下的外部 tga 文件，
/// 避免 Standard shader 在 URP 下显示为紫色。
/// </summary>
public class RedWolfRoseFBXFixer : AssetPostprocessor
{
    private const string TargetFBX = "红狼_玫瑰.fbx";

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.EndsWith(TargetFBX))
            return;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("[红狼玫瑰] 未找到 URP/Lit shader，跳过材质修复");
            return;
        }

        string texturesDir = Path.Combine(Path.GetDirectoryName(assetPath), "textures");
        // 预加载外部贴图（按文件名小写索引），用于重链
        Dictionary<string, Texture2D> externalTextures = new Dictionary<string, Texture2D>();
        if (Directory.Exists(texturesDir))
        {
            foreach (string file in Directory.GetFiles(texturesDir, "*.tga"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string relPath = "Assets" + file.Substring(Application.dataPath.Length);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(relPath);
                if (tex != null)
                    externalTextures[name] = tex;
            }
        }

        int fixedCount = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null)
                    continue;

                // 记录旧 Standard 材质上的贴图引用
                Texture oldMain = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture oldBump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Texture oldEmission = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;

                // 换 shader
                mat.shader = urpLit;

                // 重链：优先用外部 tga（文件名与 FBX 内嵌贴图一致），否则保留内嵌
                mat.SetTexture("_BaseMap", ResolveExternal(oldMain, externalTextures) ?? oldMain);
                if (oldBump != null)
                    mat.SetTexture("_NormalMap", ResolveExternal(oldBump, externalTextures) ?? oldBump);
                if (oldEmission != null)
                {
                    mat.SetTexture("_EmissionMap", ResolveExternal(oldEmission, externalTextures) ?? oldEmission);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                fixedCount++;
            }
        }

        Debug.Log($"[红狼玫瑰] FBX 导入后处理完成，修复 {fixedCount} 个材质为 URP/Lit");
    }

    /// <summary>
    /// 从外部贴图字典中查找与指定贴图同名的 Texture2D。
    /// 匹配时忽略扩展名差异（FBX 内嵌名可能为 "xxx.tga"）。
    /// </summary>
    private static Texture2D ResolveExternal(Texture tex, Dictionary<string, Texture2D> externalTextures)
    {
        if (tex == null || externalTextures.Count == 0)
            return null;

        string texName = tex.name;
        // 去掉常见后缀：.001 之类的点号序号
        int dot = texName.IndexOf('.');
        if (dot > 0)
            texName = texName.Substring(0, dot);

        Texture2D result;
        if (externalTextures.TryGetValue(texName, out result))
            return result;

        return null;
    }
}
