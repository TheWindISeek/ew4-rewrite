using Ew4.Platform.Engine;

namespace Ew4.Platform.Play.Match;

/// <summary>
/// 单局信息框队列（对白 / 剧情弹窗 / 终局）。队首即当前 Blocker：队首未解除，回合循环不得继续。
/// 不可变：每次变更返回新实例，便于快照与 Undo。
/// </summary>
/// <remarks>
/// 防死锁不变量：
/// 1. 只有 <see cref="FlowKind.GameOver"/> 可以 <see cref="FlowResolution.None"/>，其余必须可解除；
/// 2. 解除只匹配队首，种类与 id 不符直接拒绝；
/// 3. 队列内同一 Group 不重复（已触发事件的跨时段去重由 ScenarioDirector 负责）；
/// 4. GameOver 入队后队列关闭，不再接收新项；
/// 5. 排序：组间按组内最高优先级（Kind 最小），同级按入队先后；组内严格保持入队顺序。
/// </remarks>
public sealed class FlowQueue
{
    private readonly FlowItem[] _pending;
    private readonly string[] _groupOrder;

    public static FlowQueue Empty { get; } = new(Array.Empty<FlowItem>(), Array.Empty<string>());

    private FlowQueue(FlowItem[] pending, string[] groupOrder)
    {
        _pending = pending;
        _groupOrder = groupOrder;
    }

    /// <summary>是否已有 GameOver 入队。</summary>
    public bool Closed => _pending.Any(i => i.Kind == FlowKind.GameOver);

    /// <summary>入队顺序的全部待处理项（存档用；可经 <see cref="Restore"/> 还原）。</summary>
    public IReadOnlyList<FlowItem> Pending => _pending;

    /// <summary>按显示优先级排好的全部待处理项。</summary>
    public IReadOnlyList<FlowItem> Ordered => Order().ToArray();

    /// <summary>当前队首；空队列为 null。</summary>
    public FlowItem? Head => Order().FirstOrDefault();

    /// <summary>是否有未解除的信息框。</summary>
    public bool Blocked => _pending.Length > 0;

    /// <summary>入队一组信息框；已关闭的队列忽略新项。</summary>
    public FlowQueue Enqueue(IEnumerable<FlowItem> items)
    {
        FlowItem[] group = items.ToArray();
        if (group.Length == 0) return this;
        string name = group[0].Group;
        foreach (FlowItem item in group)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Group))
                throw new RuleViolation("flow.item", "Flow item requires id and group");
            if (item.Group != name) throw new RuleViolation("flow.group", "Items of one enqueue must share a group");
            if (item.Resolution == FlowResolution.None && item.Kind != FlowKind.GameOver)
                throw new RuleViolation("flow.unresolvable", "Only GameOver may be unresolvable: " + item.Kind);
            if (item.Kind == FlowKind.GameOver && item.Resolution != FlowResolution.None)
                throw new RuleViolation("flow.unresolvable", "GameOver must not be resolvable");
        }

        if (Closed) return this;
        if (_groupOrder.Contains(name, StringComparer.Ordinal)) throw new RuleViolation("flow.duplicate_group", "Group already queued: " + name);

        return new FlowQueue(_pending.Concat(group).ToArray(), _groupOrder.Append(name).ToArray());
    }

    public FlowQueue Enqueue(params FlowItem[] items) => Enqueue((IEnumerable<FlowItem>)items);

    /// <summary>确认队首对白。</summary>
    public FlowQueue Acknowledge(string id) => Resolve(FlowResolution.Ack, id);

    /// <summary>处理队首剧情弹窗。</summary>
    public FlowQueue Choose(string id) => Resolve(FlowResolution.Choice, id);

    /// <summary>从存档还原；<paramref name="pending"/> 为 <see cref="Pending"/> 的原顺序。</summary>
    public static FlowQueue Restore(IEnumerable<FlowItem> pending)
    {
        FlowQueue queue = Empty;
        foreach (IGrouping<string, FlowItem> group in pending.GroupBy(i => i.Group, StringComparer.Ordinal))
        {
            // GroupBy 保持首次出现顺序，组内保持原顺序，与入队时一致。
            queue = queue.Enqueue(group);
        }

        return queue;
    }

    private FlowQueue Resolve(FlowResolution how, string id)
    {
        FlowItem? head = Head;
        if (head == null) throw new RuleViolation("flow.empty", "Nothing is waiting");
        if (head.Resolution == FlowResolution.None) throw new RuleViolation("flow.unresolvable", "Current flow item cannot be resolved");
        if (head.Resolution != how || head.Id != id)
            throw new RuleViolation("flow.mismatch", "Head is " + head.Kind + " '" + head.Id + "', expected " + how + " '" + id + "'");

        int index = Array.IndexOf(_pending, head);
        FlowItem[] rest = _pending.Where((_, i) => i != index).ToArray();
        string[] groups = rest.Any(i => i.Group == head.Group) ? _groupOrder : _groupOrder.Where(g => g != head.Group).ToArray();
        return new FlowQueue(rest, groups);
    }

    private IEnumerable<FlowItem> Order()
    {
        // 组优先级 = 组内最小 Kind；同级按组入队先后；组内保持入队顺序。
        return _groupOrder
            .Select((name, seq) => (name, seq, items: _pending.Where(i => i.Group == name).ToArray()))
            .Where(g => g.items.Length > 0)
            .OrderBy(g => g.items.Min(i => (int)i.Kind))
            .ThenBy(g => g.seq)
            .SelectMany(g => g.items);
    }
}
