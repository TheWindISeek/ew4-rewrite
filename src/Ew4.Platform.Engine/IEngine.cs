using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>
/// 规则引擎门面：宿主只依赖本接口，不直接调用分散的静态 World* API。
/// </summary>
public interface IEngine
{
    EngineInfo Info { get; }

    GameSession NewGame(WorldState initial, RuleSet rules, PlayerAccount account, NewGameOptions options);

    EngineResult Apply(GameSession session, RuleSet rules, EngineCommand command);

    IReadOnlyDictionary<int, ReachableCell> Reachable(WorldState world, RuleSet rules, string unitId);

    BattlePreview PreviewAttack(WorldState world, RuleSet rules, string attackerId, string targetId);

    AiStepPlan PlanAiStep(WorldState world, RuleSet rules);
}
