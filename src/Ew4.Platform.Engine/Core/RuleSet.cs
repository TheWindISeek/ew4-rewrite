using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>
/// Core 规则输入：汇总一局所需的全部规则参数（只增不改字段语义）。
/// 规则配置的合法性只在此处校验一次（<see cref="Validate"/>）：内容载入后由 Content/宿主调用，
/// <c>Engine.NewGame</c> 也会再校验。各规则函数不再逐次重复校验，只检查运行时状态。
/// </summary>
public sealed record RuleSet(
    WorldTurnRules Turns,
    WorldMoveRules Movement,
    WorldBattleRules Battle,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, RecruitmentOffer>> Recruitment,
    WorldCommanderRules Commanders,
    TrainingRules Training,
    BuildingUpgradeRules Upgrades,
    IReadOnlyDictionary<string, InstallationBuildRules> Installations,
    TransportPurchaseRules Transport,
    DisbandRules Disband,
    IReadOnlyCollection<string> Compat)
{
    public void Validate()
    {
        Turns.Validate();
        Battle.Validate();
        Commanders.Validate();
        Training.Validate();
        Upgrades.Validate();
        foreach (InstallationBuildRules installation in Installations.Values) installation.Purchase.Validate();
        Transport.Purchase.Validate();
    }
}
