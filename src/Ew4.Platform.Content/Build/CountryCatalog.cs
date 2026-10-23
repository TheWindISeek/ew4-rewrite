using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

public sealed record CountryDefinition(string Name, string? Flag = null, string? FlagImage = null, string? Color = null,
    string? UnitVariant = null, string? VisualVariant = null);

public sealed class CountryCatalog
{
    public IReadOnlyDictionary<string, CountryDefinition> Countries { get; }
    public IReadOnlyDictionary<string, string> Aliases { get; }
    private sealed record File(int Schema, Dictionary<string, CountryDefinition> Countries,
        Dictionary<string, string>? Aliases = null, Dictionary<string, string>? SourceSha256 = null);
    private CountryCatalog(Dictionary<string, CountryDefinition> countries, Dictionary<string, string>? aliases = null)
    {
        Countries = new ReadOnlyDictionary<string, CountryDefinition>(countries);
        Aliases = new ReadOnlyDictionary<string, string>(aliases ?? new(StringComparer.Ordinal));
    }
    public static CountryCatalog Load(Func<string, byte[]> read)
    {
        byte[] bytes;
        try { bytes = read("client/countries.json"); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or KeyNotFoundException) { return new(new()); }
        var file = ExtensionLoader.ParseJson<File>(bytes, caseSensitiveKeys: true);
        if (file.Schema != 1 || file.Countries == null) throw new RuleViolation("country.schema", "Unsupported global country catalog");
        if (file.SourceSha256 != null)
            foreach (var (source, hash) in file.SourceSha256)
            {
                if (source is not ("def_army.xml" or "stringtable_cn.ini")) throw new RuleViolation("country.source", "Unsupported country source: " + source);
                if (!Convert.ToHexString(SHA256.HashData(read("original/" + source))).Equals(hash, StringComparison.OrdinalIgnoreCase)) return new(new());
            }
        foreach (var (id, entry) in file.Countries)
        {
            if (!ScenarioFactions.CountryId(id) || entry == null || string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 128 || entry.Name.Any(char.IsControl)
                || entry.UnitVariant != null && !ScenarioFactions.Code(entry.UnitVariant)
                || entry.VisualVariant != null && !ScenarioFactions.Code(entry.VisualVariant))
                throw new RuleViolation("country.definition", "Invalid global country: " + id);
            ScenarioFactions.ValidateAppearance(new(entry.Flag, entry.FlagImage, entry.Color));
            if (entry.FlagImage is string image)
            {
                var png = read(image);
                if (png.Length > 4 * 1024 * 1024 || png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                    throw new RuleViolation("country.flag", "Invalid country PNG: " + id);
            }
        }
        var aliases = file.Aliases ?? new(StringComparer.Ordinal);
        foreach (var (alias, target) in aliases)
            if (!ScenarioFactions.CountryId(alias) || !ScenarioFactions.CountryId(target)
                || file.Countries.ContainsKey(alias) || !file.Countries.ContainsKey(target))
                throw new RuleViolation("country.alias", "Invalid or ambiguous country alias: " + alias);
        return new(file.Countries, aliases);
    }

    public string Canonical(string id) => Countries.ContainsKey(id) ? id : Aliases.GetValueOrDefault(id, id);
    public CountryDefinition? Get(string id) => Countries.GetValueOrDefault(Canonical(id));
    public void ValidateRuntime(OriginalRuntimeCatalog runtime)
    {
        foreach (var (id, entry) in Countries)
            if (entry.UnitVariant != null && !runtime.Variants.ContainsKey(entry.UnitVariant))
                throw new RuleViolation("country.unit_variant", "Unknown combat variant for " + id + ": " + entry.UnitVariant);
    }
    public ScenarioEffect Resolve(ScenarioEffect effect)
    {
        if (effect.Country == null) return effect;
        string canonical = Canonical(effect.Country);
        if (effect.Type != "faction_create" || !Countries.TryGetValue(canonical, out var country))
            throw new RuleViolation("country.reference", "Unknown global country: " + effect.Country);
        return effect with { Country = canonical, Name = effect.Name ?? country.Name, UnitVariant = effect.UnitVariant ?? country.UnitVariant,
            Flag = effect.Flag ?? (effect.FlagImage == null ? country.Flag : null),
            FlagImage = effect.FlagImage ?? (effect.Flag == null ? country.FlagImage : null), Color = effect.Color ?? country.Color };
    }
    public ScriptedScenarioEvent Resolve(ScriptedScenarioEvent ev) => ev with {
        Effects = ev.Effects.Select(Resolve).ToArray(), InsufficientResourcesEffects = ev.InsufficientResourcesEffects.Select(Resolve).ToArray(),
        Popup = ev.Popup == null ? null : ev.Popup with { Choices = ev.Popup.Choices.Select(c => c with { Effects = c.Effects.Select(Resolve).ToArray() }).ToArray() } };
}
