namespace ElementWar.Server;

/// <summary>
/// 可靠事件账本（每客户端一个）：死亡/重生/击杀/对局结束必须到达一次。
/// 服务器统一分配 eventId，未 ACK 则按间隔重发；收到 EventAck 移除。
/// </summary>
public sealed class ReliableEventLedger
{
    private sealed class PendingEvent
    {
        public long EventId;
        public string Message = string.Empty;   // 序列化后的 JSON 字符串
        public int ResendCount;
        public DateTime LastSentUtc;
    }

    private readonly LinkedList<PendingEvent> _pending = new();
    private readonly List<(string json, long eventId)> _dueResends = new();
    private const int MaxResendCount = 20;
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>入队一条可靠事件，返回分配的事件 id。</summary>
    public long QueueInitial(object wireMessage, long eventId, string json, DateTime? nowUtc = null)
    {
        _pending.AddLast(new PendingEvent
        {
            EventId = eventId,
            Message = json,
            LastSentUtc = nowUtc ?? DateTime.UtcNow,
        });
        return eventId;
    }

    /// <summary>收集本次应重发的条目（供发送），并返回各条 eventId。</summary>
    public IEnumerable<(string json, long eventId)> CollectDueResends(DateTime? nowUtc = null)
    {
        _dueResends.Clear();
        DateTime now = nowUtc ?? DateTime.UtcNow;
        var node = _pending.First;
        while (node != null)
        {
            var next = node.Next;
            var e = node.Value;
            if (e.ResendCount >= MaxResendCount)
            {
                _pending.Remove(node);
            }
            else if (now - e.LastSentUtc >= ResendInterval)
            {
                e.ResendCount++;
                e.LastSentUtc = now;
                _dueResends.Add((e.Message, e.EventId));
            }
            node = next;
        }
        return _dueResends;
    }

    /// <summary>ACK：移除对应事件。</summary>
    public bool Acknowledge(long eventId)
    {
        var node = _pending.First;
        while (node != null)
        {
            if (node.Value.EventId == eventId)
            {
                _pending.Remove(node);
                return true;
            }
            node = node.Next;
        }
        return false;
    }

    public int PendingCount => _pending.Count;
    public void Clear() => _pending.Clear();
}
