using UnityEngine;

/// <summary>
/// 人机跟随诊断：Play 后每帧打印每个非主控角色的 NavMeshAgent 状态，
/// 定位「随从原地播放动画」的根因。
///
/// 重点看：
///  - navMeshAgent.enabled：随从是否启用了寻路
///  - navMeshAgent.isOnNavMesh：随从是否在烘焙网格上（false=不在网格，SetDestination 被跳过）
///  - navMeshAgent.hasPath / destination / velocity：是否真的有寻路路径
///  - 当前状态机状态：是否卡在 Idle（没切 Move）
///
/// 用法：挂到场景任意物体，Play 后看 Console。
/// </summary>
public class FollowerDiag : MonoBehaviour
{
    [Tooltip("每 N 秒打印一次（默认 1 秒）")]
    public float interval = 1f;

    float next;

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + interval;

        if (PlayerController.INSTANCE == null) return;
        var models = FindObjectsOfType<PlayerModel>();
        foreach (var m in models)
        {
            bool isControl = m == PlayerController.INSTANCE.currentPlayerModel;
            var agent = m.navMeshAgent;
            string agentInfo = agent == null
                ? "无 NavMeshAgent"
                : $"enabled={agent.enabled} isOnNavMesh={agent.isOnNavMesh} hasPath={agent.hasPath} " +
                  $"dest={agent.destination} vel={agent.velocity.magnitude:F2}";

            string status = isControl ? "主控" : "随从";
            Debug.Log($"[FollowerDiag] {m.name} [{status}] 位置={m.transform.position} {agentInfo}");
        }
    }
}
