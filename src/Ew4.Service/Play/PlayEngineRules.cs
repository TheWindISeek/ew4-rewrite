using System.Runtime.CompilerServices;
using Ew4.Platform.Content.StaticData;
using Ew4.Platform.Engine;

namespace Ew4.Service.Play;

internal static class PlayEngineRules
{
    private static readonly ConditionalWeakTable<StaticDataScenarioRules, RuleSet> Cache = new();

    /// <summary>每份内容规则只构建并校验一次；之后的 Apply/Reachable/Preview 复用同一个 <see cref="RuleSet"/>。</summary>
    public static RuleSet ToRuleSet(StaticDataScenarioRules rules) =>
        Cache.GetValue(rules, Build);

    private static RuleSet Build(StaticDataScenarioRules rules)
    {
        RuleSet result = new(
            rules.Turns,
            rules.Movement,
            rules.Battle,
            rules.Recruitment,
            rules.Commanders,
            rules.Training,
            rules.Upgrades,
            rules.Installations,
            rules.Transport,
            new DisbandRules(),
            Array.Empty<string>());
        result.Validate();
        rules.Roster.Validate();
        rules.Tavern.Assignment.Validate();
        rules.Trading.Items.Validate();
        return result;
    }
}
