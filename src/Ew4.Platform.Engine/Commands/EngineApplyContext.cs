namespace Ew4.Platform.Engine;

/// <summary>宿主在调用 <see cref="IEngine.Apply"/> 时提供的扩展与提交管道上下文。</summary>
public sealed class EngineApplyContext
{
    public WorldCommitContext? Commit { get; init; }

    public DisbandRules? DisbandOverride { get; init; }
}
