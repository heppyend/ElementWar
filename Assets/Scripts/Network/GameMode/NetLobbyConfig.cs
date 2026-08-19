namespace ElementWar.Net
{
    /// <summary>
    /// PVP 房间配置：主菜单 → PVP 场景 跨场景传递（静态类，进 PVPGame 场景后由 NetworkLauncher 读取）。
    /// 服务器是独立 .NET UDP 进程，Unity 端都是连接方（加入房间）。
    /// </summary>
    public static class NetLobbyConfig
    {
        public static string serverIp = "127.0.0.1";// 加入房间时填服务器 IP（同机联调用 127.0.0.1）
        public static int serverPort = 7777;        // UDP 端口
        public static int characterId;              // 0=荧 1=芙宁娜
        public static string playerName = "Player";
    }
}
