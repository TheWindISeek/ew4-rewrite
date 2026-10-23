using Ew4.Platform.Content;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;

namespace Ew4.Editor.Campaign;

/// <summary>
/// 结构化剧本编辑：只改 <see cref="ScenarioDocument.Layers"/>，再经 <see cref="ScenarioBtlCodec.ApplyLayers"/> 同步 sections。
/// </summary>
public static class ScenarioEditPatch
{
    public static ScenarioDocument PatchArmyCell(ScenarioDocument document, int armyIndex, short newCell)
    {
        IReadOnlyList<OriginalArmy> armies = document.Layers.Armies;
        if (armyIndex < 0 || armyIndex >= armies.Count)
        {
            throw new ContentError(
                "scenario.patch.army",
                $"Army index {armyIndex} is out of range (count={armies.Count}).");
        }

        var nextArmies = armies.ToArray();
        nextArmies[armyIndex] = nextArmies[armyIndex] with { Cell = newCell };
        ScenarioLayers layers = document.Layers with { Armies = nextArmies };
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = layers });
    }

    public static ScenarioDocument PatchSiteCell(ScenarioDocument document, int siteIndex, short newCell)
    {
        IReadOnlyList<OriginalSite> sites = document.Layers.Sites;
        if (siteIndex < 0 || siteIndex >= sites.Count)
        {
            throw new ContentError(
                "scenario.patch.site",
                $"Site index {siteIndex} is out of range (count={sites.Count}).");
        }

        var nextSites = sites.ToArray();
        nextSites[siteIndex] = nextSites[siteIndex] with { Cell = newCell };
        ScenarioLayers layers = document.Layers with { Sites = nextSites };
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = layers });
    }

    public static ScenarioDocument PatchFactionResources(
        ScenarioDocument document,
        int factionIndex,
        long money,
        long industry,
        long food)
    {
        if (document.Layers.FactionRows.Count > 0)
        {
            IReadOnlyList<ScenarioFactionRow> rows = document.Layers.FactionRows;
            if (factionIndex < 0 || factionIndex >= rows.Count)
            {
                throw new ContentError(
                    "scenario.patch.faction",
                    $"Faction index {factionIndex} is out of range (count={rows.Count}).");
            }

            var nextRows = rows.ToArray();
            OriginalFaction value = nextRows[factionIndex].Value;
            nextRows[factionIndex] = nextRows[factionIndex] with
            {
                Value = value with { Resources = new Resources(money, industry, food) },
            };
            ScenarioLayers layers = document.Layers with
            {
                FactionRows = nextRows,
                Factions = nextRows.Select(r => r.Value).ToArray(),
            };
            return ScenarioBtlCodec.ApplyLayers(document with { Layers = layers });
        }

        IReadOnlyList<OriginalFaction> factions = document.Layers.Factions;
        if (factionIndex < 0 || factionIndex >= factions.Count)
        {
            throw new ContentError(
                "scenario.patch.faction",
                $"Faction index {factionIndex} is out of range (count={factions.Count}).");
        }

        var nextFactions = factions.ToArray();
        nextFactions[factionIndex] = nextFactions[factionIndex] with
        {
            Resources = new Resources(money, industry, food),
        };
        ScenarioLayers layersOnly = document.Layers with { Factions = nextFactions };
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = layersOnly });
    }

    public static ScenarioDocument PatchArmyFields(ScenarioDocument document, int armyIndex, ArmyFieldPatch patch)
    {
        IReadOnlyList<OriginalArmy> armies = document.Layers.Armies;
        if (armyIndex < 0 || armyIndex >= armies.Count)
        {
            throw new ContentError("scenario.patch.army", $"Army index {armyIndex} is out of range (count={armies.Count}).");
        }

        OriginalArmy current = armies[armyIndex];
        var next = current with
        {
            Cell = patch.Cell ?? current.Cell,
            Unit = patch.Unit ?? current.Unit,
            Grade = patch.Grade ?? current.Grade,
            Health = patch.Health ?? current.Health,
            MaximumHealth = patch.MaximumHealth ?? current.MaximumHealth,
            Commander = patch.Commander ?? current.Commander,
            Movement = patch.Movement ?? current.Movement,
            Morale = patch.Morale ?? current.Morale,
            MoraleDuration = patch.MoraleDuration ?? current.MoraleDuration,
        };
        var nextArmies = armies.ToArray();
        nextArmies[armyIndex] = next;
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = document.Layers with { Armies = nextArmies } });
    }

    public static ScenarioDocument PatchSiteFields(ScenarioDocument document, int siteIndex, SiteFieldPatch patch)
    {
        IReadOnlyList<OriginalSite> sites = document.Layers.Sites;
        if (siteIndex < 0 || siteIndex >= sites.Count)
        {
            throw new ContentError("scenario.patch.site", $"Site index {siteIndex} is out of range (count={sites.Count}).");
        }

        OriginalSite current = sites[siteIndex];
        var next = current with
        {
            Cell = patch.Cell ?? current.Cell,
            CityId = patch.CityId ?? current.CityId,
            BuildingKind = patch.BuildingKind ?? current.BuildingKind,
            BuildingLevel = patch.BuildingLevel ?? current.BuildingLevel,
            InstallationKind = patch.InstallationKind ?? current.InstallationKind,
        };
        var nextSites = sites.ToArray();
        nextSites[siteIndex] = next;
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = document.Layers with { Sites = nextSites } });
    }

    public static ScenarioDocument PatchFactionFields(ScenarioDocument document, int factionIndex, FactionFieldPatch patch)
    {
        if (document.Layers.FactionRows.Count > 0)
        {
            IReadOnlyList<ScenarioFactionRow> rows = document.Layers.FactionRows;
            if (factionIndex < 0 || factionIndex >= rows.Count)
            {
                throw new ContentError("scenario.patch.faction", $"Faction index {factionIndex} is out of range (count={rows.Count}).");
            }

            OriginalFaction current = rows[factionIndex].Value;
            OriginalFaction next = ApplyFactionPatch(current, patch);
            var nextRows = rows.ToArray();
            nextRows[factionIndex] = nextRows[factionIndex] with { Value = next };
            ScenarioLayers layers = document.Layers with
            {
                FactionRows = nextRows,
                Factions = nextRows.Select(r => r.Value).ToArray(),
            };
            return ScenarioBtlCodec.ApplyLayers(document with { Layers = layers });
        }

        IReadOnlyList<OriginalFaction> factions = document.Layers.Factions;
        if (factionIndex < 0 || factionIndex >= factions.Count)
        {
            throw new ContentError("scenario.patch.faction", $"Faction index {factionIndex} is out of range (count={factions.Count}).");
        }

        var nextFactions = factions.ToArray();
        nextFactions[factionIndex] = ApplyFactionPatch(factions[factionIndex], patch);
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = document.Layers with { Factions = nextFactions } });
    }

    public static ScenarioDocument PatchEventFields(ScenarioDocument document, int eventIndex, EventFieldPatch patch)
    {
        if (document.Layers.EventRows.Count > 0)
        {
            IReadOnlyList<ScenarioEventRow> rows = document.Layers.EventRows;
            if (eventIndex < 0 || eventIndex >= rows.Count)
            {
                throw new ContentError("scenario.patch.event", $"Event index {eventIndex} is out of range (count={rows.Count}).");
            }

            OriginalEvent current = rows[eventIndex].Value;
            OriginalEvent next = ApplyEventPatch(current, patch);
            var nextRows = rows.ToArray();
            nextRows[eventIndex] = nextRows[eventIndex] with { Value = next };
            ScenarioLayers layers = document.Layers with
            {
                EventRows = nextRows,
                Events = nextRows.Select(r => r.Value).ToArray(),
            };
            return ScenarioBtlCodec.ApplyLayers(document with { Layers = layers });
        }

        IReadOnlyList<OriginalEvent> events = document.Layers.Events;
        if (eventIndex < 0 || eventIndex >= events.Count)
        {
            throw new ContentError("scenario.patch.event", $"Event index {eventIndex} is out of range (count={events.Count}).");
        }

        var nextEvents = events.ToArray();
        nextEvents[eventIndex] = ApplyEventPatch(events[eventIndex], patch);
        return ScenarioBtlCodec.ApplyLayers(document with { Layers = document.Layers with { Events = nextEvents } });
    }

    public static ScenarioDocument PatchMapInfo(ScenarioDocument document, MapInfoPatch patch) =>
        ScenarioBtlCodec.ApplyLayers(document with
        {
            MapId = patch.MapId ?? document.MapId,
            Width = patch.Width ?? document.Width,
            Height = patch.Height ?? document.Height,
        });

    private static OriginalFaction ApplyFactionPatch(OriginalFaction current, FactionFieldPatch patch)
    {
        Resources resources = current.Resources;
        if (patch.Money != null || patch.Industry != null || patch.Food != null)
        {
            resources = new Resources(
                patch.Money ?? resources.Money,
                patch.Industry ?? resources.Industry,
                patch.Food ?? resources.Food);
        }

        return current with
        {
            Resources = resources,
            Camp = patch.Camp ?? current.Camp,
            CountryId = patch.CountryId ?? current.CountryId,
            Variant = patch.Variant ?? current.Variant,
            Ai = patch.Ai ?? current.Ai,
        };
    }

    private static OriginalEvent ApplyEventPatch(OriginalEvent current, EventFieldPatch patch) =>
        current with
        {
            Id = patch.Id ?? current.Id,
            Condition = patch.Condition ?? current.Condition,
            Effect = patch.Effect ?? current.Effect,
            Round = patch.Round ?? current.Round,
            Dialogue = patch.Dialogue ?? current.Dialogue,
            Cell = patch.Cell ?? current.Cell,
            Country = patch.Country ?? current.Country,
        };
}
