using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>运行时只读加载器。烘焙产物由用户从 Unity 菜单生成至 Resources/PVP。</summary>
    public static class PvpArenaCollisionProfileRuntime
    {
        public const string ResourcePath = "PVP/PvpArenaCollisionProfile";

        public static bool TryLoad(out PvpArenaCollisionProfile profile, out string error)
        {
            profile = null;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                error = $"未找到 Resources/{ResourcePath}.json";
                return false;
            }

            profile = JsonUtility.FromJson<PvpArenaCollisionProfile>(asset.text);
            if (profile == null || profile.schemaVersion != PvpArenaCollisionProfile.CurrentSchemaVersion)
            {
                error = "碰撞 Profile 为空或 schemaVersion 不兼容";
                profile = null;
                return false;
            }
            if (profile.movementVolumes == null || profile.movementVolumes.Length == 0)
            {
                error = "碰撞 Profile 未包含角色移动体积";
                profile = null;
                return false;
            }

            error = "";
            return true;
        }
    }
}
