using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 读取剧本 JSON，经内部 BTL 生成后由 <see cref="OriginalScenarioHost"/> 构建开局状态。
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
        byte[] btlBytes = ScenarioBtlCache.GetOrAdd(
            content,
            scenarioId,
            () => ScenarioModAccess.ResolveBtlBytes(content.Source, scenarioId));
        OriginalBattle battle = bootstrap.ReadBattle(btlBytes);
        if (battle.CountryIds != null && battle.Factions.Any(f => bootstrap.Countries.Get(f.CountryId) == null))
        {
            throw new RuleViolation("country.reference", "CNTR references an unregistered global country in " + scenarioId);
        }

        var account = ScenarioStartHelpers.SmokeAccount(bootstrap.Catalogs.Accounts.SourceRevision);
        OriginalScenarioRequest request = BuildRequest(scenarioId, mode, battle, bootstrap);
        OriginalScenarioBuild built = OriginalScenarioHost.BuildInitial(
            battle,
            bootstrap.Maps,
            bootstrap.Catalogs,
            request,
            account);

        return ScenarioExtensionPostProcessor.Apply(built, battle, scenarioId, mode, bootstrap);
    }

    internal static OriginalScenarioRequest BuildRequest(
        string scenarioId,
        int mode,
        OriginalBattle battle,
        ModRuntimeBootstrap bootstrap)
    {
        string? selectedCountry = mode == 2 ? ScenarioStartHelpers.SelectConquestCountry(battle) : null;
        return new OriginalScenarioRequest
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
            AiSeed = (ulong)Random.Shared.NextInt64(1, long.MaxValue),
        };
    }

}
