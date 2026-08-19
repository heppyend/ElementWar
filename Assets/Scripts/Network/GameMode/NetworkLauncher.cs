using UnityEngine;
using ElementWar.Net;

/// <summary>
/// PVP 场景入口：读取 NetLobbyConfig，配置并启动 NetClient（连接 .NET 服务器）。
/// 挂在 PVPGame 场景的空物体上。
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    private void Start()
    {
        var netClient = FindObjectOfType<NetClient>();
        var spawner = FindObjectOfType<PlayerSpawner>();
        if (netClient == null)
        {
            Debug.LogError("[NetworkLauncher] 缺少 NetClient 组件");
            return;
        }

        // 确保 PVE 式双 FreeLook 相机存在（运行时自建，清理旧 PVPCameraFollow）
        if (FindObjectOfType<PVPCameraRig>() == null)
            new GameObject("PVPCameraRig").AddComponent<PVPCameraRig>();

        netClient.spawner = spawner;
        netClient.Configure(
            NetLobbyConfig.serverIp,
            NetLobbyConfig.serverPort,
            NetLobbyConfig.playerName,
            NetLobbyConfig.characterId);
        netClient.Connect();
    }
}
