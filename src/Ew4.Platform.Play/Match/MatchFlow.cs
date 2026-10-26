using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Play.Match;

/// <summary>
/// 单局阻塞状态的持有者：Engine 规则产生的对白/剧情选择请求在这里排队，
/// 队首即当前 Blocker。Engine 状态本身不再保存这些信息。
/// </summary>
public sealed class MatchFlow
{
    private FlowQueue _queue = FlowQueue.Empty;

    public bool Blocked => _queue.Blocked;

    public FlowItem? Head => _queue.Head;

    /// <summary>用于存档：队列内全部未处理项。</summary>
    public IReadOnlyList<FlowItem> Pending => _queue.Pending;

    /// <summary>全部待处理 Flow Id（含队首），按处理顺序；交给 Engine 以保持规则内的顺序。</summary>
    public IReadOnlyList<string> Waiting => _queue.Ordered.Select(i => i.Id).ToArray();

    public void Clear() => _queue = FlowQueue.Empty;

    public void Restore(IEnumerable<FlowItem>? pending) =>
        _queue = pending == null ? FlowQueue.Empty : FlowQueue.Restore(pending);

    /// <summary>登记一次 Apply 新产生的请求（同一次触发视为一组，保持触发顺序）。</summary>
    public void Raise(IReadOnlyList<FlowRequest> requests)
    {
        if (requests.Count == 0) return;
        string group = "g" + Guid.NewGuid().ToString("N");
        _queue = _queue.Enqueue(requests.Select(r => r.Kind == FlowRequestKind.ScenarioPopup
            ? FlowItem.ScenarioPopup(group, r.Id)
            : FlowItem.Dialogue(group, r.Id)));
    }

    /// <summary>断言队首是可确认的对白。</summary>
    public FlowItem RequireDialogue()
    {
        FlowItem head = _queue.Head ?? throw new RuleViolation("client.dialogue", "No dialogue is waiting");
        if (head.Kind == FlowKind.ScenarioPopup) throw new RuleViolation("scenario.choice_required", "Select an event choice");
        return head;
    }

    /// <summary>断言队首是剧情选择弹窗。</summary>
    public FlowItem RequirePopup()
    {
        FlowItem? head = _queue.Head;
        return head is { Kind: FlowKind.ScenarioPopup } ? head : throw new RuleViolation("client.event_choice", "No scenario choice is waiting");
    }

    /// <summary>Engine 处理完队首后返回新的完整待处理列表，以它为准重建队列。</summary>
    public void Replace(IReadOnlyList<FlowRequest> remaining)
    {
        _queue = FlowQueue.Empty;
        Raise(remaining);
    }
}
