using System.Text.Json;
using System.Text.Json.Serialization;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.Original;

namespace Ew4.Platform.Content.ScenarioEdit;

/// <summary>
/// 剧本可编辑文档（schema 2）。Importer / Exporter / Authoring 共用。
/// </summary>
public sealed record ScenarioDocument
{
    public int Schema { get; init; } = 2;
    public required string Id { get; init; }
    public int MapId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int[] Header { get; init; } = [];
    public IReadOnlyDictionary<string, byte[]> Sections { get; init; } = new Dictionary<string, byte[]>();
    public byte[] ExtensionTrailer { get; init; } = [];
    public ScenarioLayers Layers { get; init; } = new();
}

public sealed record ScenarioFactionRow(OriginalFaction Value, byte[] Raw);

public sealed record ScenarioEventRow(OriginalEvent Value, byte[] Raw);

public sealed record ScenarioLayers
{
    public IReadOnlyList<ScenarioFactionRow> FactionRows { get; init; } = Array.Empty<ScenarioFactionRow>();
    public IReadOnlyList<ScenarioEventRow> EventRows { get; init; } = Array.Empty<ScenarioEventRow>();
    public IReadOnlyList<OriginalFaction> Factions { get; init; } = Array.Empty<OriginalFaction>();
    public IReadOnlyList<OriginalSite> Sites { get; init; } = Array.Empty<OriginalSite>();
    public IReadOnlyList<OriginalArmy> Armies { get; init; } = Array.Empty<OriginalArmy>();
    public IReadOnlyList<OriginalEvent> Events { get; init; } = Array.Empty<OriginalEvent>();

    public int FactionCount => FactionRows.Count > 0 ? FactionRows.Count : Factions.Count;
    public int SiteCount => Sites.Count;
    public int ArmyCount => Armies.Count;
    public int EventCount => EventRows.Count > 0 ? EventRows.Count : Events.Count;
}

public static class ScenarioDocumentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static byte[] Serialize(ScenarioDocument document)
    {
        var dto = ScenarioDocumentDto.From(document);
        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }

    public static ScenarioDocument Deserialize(byte[] json)
    {
        ScenarioDocumentDto? dto = JsonSerializer.Deserialize<ScenarioDocumentDto>(json, Options)
            ?? throw new ContentError("scenario.document", "Empty scenario document JSON");
        return dto.ToModel();
    }

    private sealed class ScenarioDocumentDto
    {
        public int Schema { get; set; }
        public string Id { get; set; } = "";
        public int MapId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Header { get; set; } = [];
        public Dictionary<string, string> Sections { get; set; } = new(StringComparer.Ordinal);
        public string ExtensionTrailer { get; set; } = "";
        public ScenarioLayersDto Layers { get; set; } = new();

        public static ScenarioDocumentDto From(ScenarioDocument document) => new()
        {
            Schema = document.Schema,
            Id = document.Id,
            MapId = document.MapId,
            Width = document.Width,
            Height = document.Height,
            Header = document.Header.ToArray(),
            Sections = document.Sections.ToDictionary(
                p => p.Key,
                p => Convert.ToBase64String(p.Value),
                StringComparer.Ordinal),
            ExtensionTrailer = Convert.ToBase64String(document.ExtensionTrailer),
            Layers = ScenarioLayersDto.From(document.Layers),
        };

        public ScenarioDocument ToModel() => new()
        {
            Schema = Schema,
            Id = Id,
            MapId = MapId,
            Width = Width,
            Height = Height,
            Header = Header,
            Sections = Sections.ToDictionary(
                p => p.Key,
                p => Convert.FromBase64String(p.Value),
                StringComparer.Ordinal),
            ExtensionTrailer = string.IsNullOrEmpty(ExtensionTrailer) ? [] : Convert.FromBase64String(ExtensionTrailer),
            Layers = Layers.ToModel(),
        };
    }

    private sealed class ScenarioLayersDto
    {
        public List<ScenarioFactionRowDto> FactionRows { get; set; } = [];
        public List<OriginalFaction> Factions { get; set; } = [];
        public List<OriginalSite> Sites { get; set; } = [];
        public List<OriginalArmy> Armies { get; set; } = [];
        public List<ScenarioEventRowDto> EventRows { get; set; } = [];
        public List<OriginalEvent> Events { get; set; } = [];

        public static ScenarioLayersDto From(ScenarioLayers layers) => new()
        {
            FactionRows = layers.FactionRows.Select(r => new ScenarioFactionRowDto
            {
                Value = r.Value,
                Raw = Convert.ToBase64String(r.Raw),
            }).ToList(),
            Factions = (layers.FactionRows.Count > 0
                ? layers.FactionRows.Select(r => r.Value)
                : layers.Factions).ToList(),
            Sites = layers.Sites.ToList(),
            Armies = layers.Armies.ToList(),
            EventRows = layers.EventRows.Select(r => new ScenarioEventRowDto
            {
                Value = r.Value,
                Raw = Convert.ToBase64String(r.Raw),
            }).ToList(),
            Events = (layers.EventRows.Count > 0
                ? layers.EventRows.Select(r => r.Value)
                : layers.Events).ToList(),
        };

        public ScenarioLayers ToModel()
        {
            var rows = FactionRows.Select(r => new ScenarioFactionRow(
                r.Value,
                string.IsNullOrEmpty(r.Raw) ? [] : Convert.FromBase64String(r.Raw))).ToList();
            var eventRows = EventRows.Select(r => new ScenarioEventRow(
                r.Value,
                string.IsNullOrEmpty(r.Raw) ? [] : Convert.FromBase64String(r.Raw))).ToList();
            return new()
            {
                FactionRows = rows,
                Factions = rows.Count > 0 ? rows.Select(r => r.Value).ToArray() : Factions,
                EventRows = eventRows,
                Sites = Sites,
                Armies = Armies,
                Events = eventRows.Count > 0 ? eventRows.Select(r => r.Value).ToArray() : Events,
            };
        }
    }

    private sealed class ScenarioFactionRowDto
    {
        public OriginalFaction Value { get; set; } = null!;
        public string Raw { get; set; } = "";
    }

    private sealed class ScenarioEventRowDto
    {
        public OriginalEvent Value { get; set; } = null!;
        public string Raw { get; set; } = "";
    }
}
