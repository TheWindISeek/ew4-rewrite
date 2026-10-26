using System;
using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>Apply 的结果：新状态、通知、需要玩家处理的阻塞请求（Flow）以及展示事件。</summary>
public sealed record EngineResult(GameSession Session, IReadOnlyList<WorldNotice> Notices)
{
    /// <summary>本次 Apply 新产生（且仍未处理）的阻塞请求，按触发顺序排列；引擎状态本身不再持有它们。</summary>
    public IReadOnlyList<FlowRequest> Flow { get; init; } = Array.Empty<FlowRequest>();

    public IReadOnlyList<EngineEvent> Events { get; init; } = Array.Empty<EngineEvent>();
}

public sealed record NewGameOptions;
