using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public sealed record ScenarioSelection(string ScenarioId);

public sealed record BuiltSessionAttach(
    IReadOnlyList<RosterDeploymentState> RosterDeployments,
    IReadOnlyList<SessionMarket> Markets,
    IReadOnlyList<TavernCandidate> TavernCandidates,
    string? SessionContentRevision);

public sealed record BuiltScenario(WorldState InitialWorld, RuleSet Rules, BuiltSessionAttach? Session = null);

/// <summary>
/// 从已解析内容构建开局所需的 <see cref="WorldState"/> 与 <see cref="RuleSet"/>。
/// </summary>
public interface IScenarioBuilder
{
    BuiltScenario Build(ResolvedContent content, string scenarioId, ScenarioSelection selection);
}
