using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public enum FlowRequestKind { Dialogue, ScenarioPopup }

/// <summary>引擎规则要求玩家先处理的信息框（对白或剧情选择），由 Runtime 的 FlowQueue 持有。</summary>
public sealed record FlowRequest(FlowRequestKind Kind, string Id);

public abstract record EngineEvent;

public sealed record UnitMovedEvent(string UnitId, IReadOnlyList<int> Path) : EngineEvent;

public sealed record AttackResolvedEvent(BattlePreview Preview) : EngineEvent;

/// <summary>规则代码内部仍用 PendingDialogue/QueuedDialogues 暂存；Apply 前后在这里借出与取回，持久状态不保留它们。</summary>
public static class FlowExtraction
{
    public static WorldState Hydrate(WorldState world, IReadOnlyList<string> ids) => ids.Count == 0 ? world : world with
    {
        PendingDialogue = ids[0],
        QueuedDialogues = ids.Skip(1).ToArray(),
    };

    public static (WorldState World, IReadOnlyList<FlowRequest> Flow) Take(WorldState world)
    {
        if (world.PendingDialogue == null) return (world, Array.Empty<FlowRequest>());
        var flow = new[] { world.PendingDialogue }.Concat(world.QueuedDialogues)
            .Select(id => new FlowRequest(ScenarioRules.IsPopup(id) ? FlowRequestKind.ScenarioPopup : FlowRequestKind.Dialogue, id))
            .ToArray();
        return (world with { PendingDialogue = null, QueuedDialogues = Array.Empty<string>() }, flow);
    }
}
