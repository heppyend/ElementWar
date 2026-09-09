using System.Text.Json;
using ElementWar.Net;

namespace ElementWar.Server;

/// <summary>服务器读取 Unity 手动烘焙出的纯 JSON；找不到时由调用者决定是否使用旧地图回退。</summary>
public static class PvpCollisionProfileFileLoader
{
    public static bool TryLoad(string? explicitPath, out PvpArenaCollisionProfile? profile, out string error)
    {
        profile = null;
        string[] candidates = string.IsNullOrWhiteSpace(explicitPath)
            ?
            [
                Path.Combine(Environment.CurrentDirectory, "..", "Assets", "Resources", "PVP", "PvpArenaCollisionProfile.json"),
                Path.Combine(Environment.CurrentDirectory, "Assets", "Resources", "PVP", "PvpArenaCollisionProfile.json"),
                Path.Combine(AppContext.BaseDirectory, "PvpArenaCollisionProfile.json")
            ]
            : [explicitPath];

        string? path = candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (path is null)
        {
            error = "未找到 PvpArenaCollisionProfile.json";
            return false;
        }
        try
        {
            profile = JsonSerializer.Deserialize<PvpArenaCollisionProfile>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            if (profile == null || profile.schemaVersion != PvpArenaCollisionProfile.CurrentSchemaVersion
                || profile.movementVolumes == null || profile.movementVolumes.Length == 0)
            {
                profile = null;
                error = $"Profile 无效或 schemaVersion 不兼容：{path}";
                return false;
            }
            error = path;
            return true;
        }
        catch (Exception ex)
        {
            error = $"读取 Profile 失败：{ex.Message}";
            return false;
        }
    }
}
