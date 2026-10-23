using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>
/// Core 规则输入：汇总一局所需的全部规则参数（只增不改字段语义）。
/// </summary>
public sealed record RuleSet(
    WorldTurnRules Turns,
    WorldMoveRules Movement,
    WorldBattleRules Battle,
    WorldAiRules Ai,
    IReadOnlyDictionary<string, WorldProductionRules> Production,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, RecruitmentOffer>> Recruitment,
    WorldCommanderRules Commanders,
    RosterDeploymentRules Roster,
    TrainingRules Training,
    BuildingUpgradeRules Upgrades,
    IReadOnlyDictionary<string, InstallationBuildRules> Installations,
    TransportPurchaseRules Transport,
    TavernRules Tavern,
    SessionTradingRules Trading,
    DisbandRules Disband,
    IReadOnlyCollection<string> Compat);
