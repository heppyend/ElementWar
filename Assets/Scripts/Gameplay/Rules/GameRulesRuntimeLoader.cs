using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ElementWar.Rules
{

/// <summary>Unity 端从 StreamingAssets 读取与服务器相同的规则字节，不从 Inspector 取规则。</summary>
public static class GameRulesRuntimeLoader
{
    public const string RelativePath = "Rules/GameRules.v1.json";

    public static bool TryLoad(out GameRulesDocument document, out string contentHash, out string error)
    {
        document = null;
        contentHash = string.Empty;
        error = string.Empty;
        string path = Path.Combine(Application.streamingAssetsPath, RelativePath);
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            document = JsonUtility.FromJson<GameRulesDocument>(Encoding.UTF8.GetString(bytes));
            if (!GameRulesValidator.TryValidate(document, out error)) return false;
            contentHash = GameRulesHash.Compute(bytes);
            return true;
        }
        catch (Exception exception)
        {
            error = $"读取规则文件失败：{exception.Message}";
            return false;
        }
    }
}
}
