namespace ElementWar.Server;

/// <summary>
/// 复制策略：2 人局全量复制（每个客户端都收到所有玩家的状态）。
/// 保留结构以便未来做距离分级（18m 高频 / 45m 低频 / 出范围剔除）。
/// </summary>
public static class ClientReplicator
{
    /// <summary>计算某客户端当前应复制的玩家 id 列表。</summary>
    public static int[] ComputeReplicatedIds(GameWorld world)
        => world.Players.Keys.OrderBy(x => x).ToArray();

    /// <summary>该玩家是否在本快照中给该客户端更新状态（2 人局恒为 true）。</summary>
    public static bool ShouldSendState(int recipientPlayerId, int subjectPlayerId)
        => true;
}
