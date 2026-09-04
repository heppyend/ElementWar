using System.Text.Json;
using ElementWar.Rules;

namespace ElementWar.Server;

/// <summary>服务器启动时读取唯一规则文件并拒绝非法配置；不回退到隐式默认值。</summary>
public static class GameRulesFileLoader
{
    private const string RelativePath = "Assets/StreamingAssets/Rules/GameRules.v1.json";

    public static bool TryLoadDefault(out GameRulesDocument document, out string contentHash, out string error)
    {
        document = null!;
        contentHash = string.Empty;
        error = string.Empty;

        string? path = FindFrom(Directory.GetCurrentDirectory());
        path ??= FindFrom(AppContext.BaseDirectory);
        if (path is null)
        {
            error = $"规则文件不存在：{RelativePath}";
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            document = JsonSerializer.Deserialize<GameRulesDocument>(bytes, options)!;
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

    private static string? FindFrom(string start)
    {
        var directory = new DirectoryInfo(start);
        for (int i = 0; i < 10 && directory is not null; i++)
        {
            string candidate = Path.Combine(directory.FullName, RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
