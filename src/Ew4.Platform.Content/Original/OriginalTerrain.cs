using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public sealed record OriginalTerrainDefinition(string Kind, IReadOnlyDictionary<int, int> DensityBySprite);
public sealed record OriginalResolvedTerrain(MovementCell Movement, string DefenseTerrain, string FireTerrain, int Overlay);

public sealed class OriginalTerrainRules
{
    public IReadOnlyList<OriginalTerrainDefinition> Definitions { get; }
    public IReadOnlyDictionary<string, int> MovementCosts { get; }
    private OriginalTerrainRules(OriginalTerrainDefinition[] definitions, Dictionary<string, int> costs)
    { Definitions = Array.AsReadOnly(definitions); MovementCosts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(costs); }

    public static OriginalTerrainRules FromXml(string mapTerrainXml, string terrainTypesXml)
    {
        var types = XDocument.Parse(terrainTypesXml).Root ?? throw new RuleViolation("legacy.terrain_xml", "Missing terrain types");
        var definitions = XDocument.Parse(mapTerrainXml).Root ?? throw new RuleViolation("legacy.terrain_xml", "Missing terrain definitions");
        var costs = types.Elements("terraintype").ToDictionary(t => (string?)t.Attribute("type") ?? "", t => (int?)t.Attribute("movementcost") ?? 0, StringComparer.Ordinal);
        if (costs.Count == 0 || costs.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 1))
            throw new RuleViolation("legacy.terrain_xml", "Invalid terrain movement costs");
        var rows = definitions.Elements("terrain").Select(t => new OriginalTerrainDefinition((string?)t.Attribute("type") ?? "",
            new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(t.Elements("tile").ToDictionary(s => (int?)s.Attribute("idx") ?? -1, s => (int?)s.Attribute("n") ?? 0)))).ToArray();
        if (rows.Length == 0 || rows.Any(r => !costs.ContainsKey(r.Kind) || r.DensityBySprite.Any(p => p.Key < 0 || p.Value < 0))
            || !costs.ContainsKey("plant") || !costs.ContainsKey("forest"))
            throw new RuleViolation("legacy.terrain_xml", "Invalid terrain/sprite definitions");
        return new(rows, costs);
    }

    public OriginalResolvedTerrain Resolve(OriginalTerrainCell cell, bool active = true)
    {
        if (cell.BackgroundKind >= Definitions.Count || cell.ForegroundKind != 11 && cell.ForegroundKind >= Definitions.Count)
            throw new RuleViolation("legacy.terrain_kind", "Unknown terrain definition");
        if (cell.RiverEdges > 63 || cell.BlockedSeaEdges > 63 || cell.Unknown10 != 0 || cell.Unknown11 != 0)
            throw new RuleViolation("legacy.terrain_flags", "Unknown terrain flags cannot be discarded");
        var background = Definitions[cell.BackgroundKind];
        int Density(OriginalTerrainDefinition definition, byte index, bool unusedLand)
        {
            // Native land uses no background sprite, so its 255 sentinel is valid.
            if (unusedLand && index == 255) return 0;
            if (!definition.DensityBySprite.TryGetValue(index, out int density))
                throw new RuleViolation("legacy.terrain_sprite", "Terrain sprite is outside the definition; unsafe in the old runtime");
            return density;
        }
        int backgroundDensity = Density(background, cell.BackgroundIndex, cell.BackgroundKind == 0);
        int cost = MovementCosts[background.Kind], overlay = 0;
        if (cell.ForegroundKind != 11)
        {
            var foreground = Definitions[cell.ForegroundKind];
            int foregroundDensity = Density(foreground, cell.ForegroundIndex, false);
            if (background.Kind == "forest" && foreground.Kind == "forest")
            {
                overlay = (long)backgroundDensity + foregroundDensity < 3 ? 1 : 2;
                cost = MovementCosts[overlay == 1 ? "plant" : "forest"];
            }
            else
            {
                cost = Math.Max(cost, MovementCosts[foreground.Kind]);
                int density = background.Kind == "forest" ? backgroundDensity : foreground.Kind == "forest" ? foregroundDensity : 0;
                overlay = density > 2 ? 2 : density > 0 ? 1 : 0;
            }
        }
        return new(new() { Active = active, Sea = cell.BackgroundKind == 1, TerrainCost = cost,
            RiverEdges = cell.RiverEdges, BlockedSeaEdges = cell.BlockedSeaEdges },
            overlay == 1 ? "plant" : overlay == 2 ? "forest" : background.Kind,
            overlay == 1 ? "medium" : overlay == 2 ? "dense" : "open", overlay);
    }
}
