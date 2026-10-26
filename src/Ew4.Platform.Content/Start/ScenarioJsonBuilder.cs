using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 读取剧本 JSON，经内部 BTL 生成后由 <see cref="StaticDataScenarioHost"/> 构建开局状态。
/// </summary>
public sealed class ScenarioJsonBuilder : IScenarioBuilder
{
    public BuiltScenario Build(ResolvedContent content, string scenarioId, ScenarioSelection selection)
    {
        if (!content.Source.Exists(ScenarioModAccess.ScenarioJsonPath(scenarioId)))
        {
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        }

        int mode = ScenarioStartHelpers.ResolveMode(scenarioId);
        var bootstrap = ModRuntimeBootstrapCache.Get(content);
        StaticDataBattle battle = ScenarioModAccess.OpenBattle(content.Source, scenarioId);
        if (battle.CountryIds != null && battle.Factions.Any(f => bootstrap.Countries.Get(f.CountryId) == null))
        {
            throw new RuleViolation("country.reference", "CNTR references an unregistered global country in " + scenarioId);
        }

        var account = ScenarioStartHelpers.SmokeAccount(bootstrap.Catalogs.Accounts.SourceRevision);
        StaticDataScenarioRequest request = BuildRequest(scenarioId, mode, battle, bootstrap);
        StaticDataScenarioBuild built = StaticDataScenarioHost.BuildInitial(
            battle,
            bootstrap.Maps,
            bootstrap.Catalogs,
            request,
            account);

        return ScenarioExtensionPostProcessor.Apply(built, battle, scenarioId, mode, bootstrap);
    }

    internal static StaticDataScenarioRequest BuildRequest(
        string scenarioId,
        int mode,
        StaticDataBattle battle,
        ModRuntimeBootstrap bootstrap)
    {
        string? selectedCountry = mode == 2 ? ScenarioStartHelpers.SelectConquestCountry(battle) : null;
        return new StaticDataScenarioRequest
        {
            Id = scenarioId,
            Mode = mode,
            SelectedCountry = selectedCountry,
            CityNames = bootstrap.CityNamesByScenario.TryGetValue(scenarioId, out var names) ? names : bootstrap.Cities,
            Warzone = ScenarioStartHelpers.ResolveCampaignWarzone(scenarioId),
            CountryNames = battle.Factions.Select(f => f.CountryId).Distinct()
                .ToDictionary(c => c, bootstrap.CountryName),
            CountryUnitVariants = battle.Factions.Where(f => bootstrap.Countries.Get(f.CountryId)?.UnitVariant is string variant
                    && variant != f.Variant
                    && !(variant == "others" && !bootstrap.Catalogs.Runtime.Variants.ContainsKey(f.Variant)))
                .GroupBy(f => f.CountryId)
                .ToDictionary(g => g.Key, g => bootstrap.Countries.Get(g.Key)!.UnitVariant!),
            CombatSeed = mode == 4 ? 100 : (ulong)Random.Shared.NextInt64(1, long.MaxValue),
        };
    }

}
