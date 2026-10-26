using Ew4.Platform.Persistence;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ew4.Platform.Persistence.Packages;

internal static class ModManifestReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ModManifest Read(byte[] jsonBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PersistenceError("mod.schema", "mod.json must be a JSON object.");
            }

            int schema = root.GetProperty("schema").GetInt32();
            string id = ModId.Validate(root.GetProperty("id").GetString() ?? "");
            string name = root.GetProperty("name").GetString() ?? "";
            string version = root.GetProperty("version").GetString() ?? "";
            string? parent = root.TryGetProperty("parent", out var parentEl) && parentEl.ValueKind == JsonValueKind.String
                ? parentEl.GetString()
                : null;

            if (!root.TryGetProperty("requires", out var requiresEl) || requiresEl.ValueKind != JsonValueKind.Object)
            {
                throw new PersistenceError("mod.requires", "mod.json requires a 'requires' object.");
            }

            string engine = requiresEl.GetProperty("engine").GetString() ?? "";
            var capabilities = ReadStringArray(requiresEl, "capabilities");

            if (!root.TryGetProperty("formats", out var formatsEl) || formatsEl.ValueKind != JsonValueKind.Object)
            {
                throw new PersistenceError("mod.formats", "mod.json requires a 'formats' object.");
            }

            var formats = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in formatsEl.EnumerateObject())
            {
                formats[property.Name] = property.Value.GetInt32();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PersistenceError("mod.name", "Pack name is required.");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new PersistenceError("mod.version", "Pack version is required.");
            }

            ModManifestProfile.ModProfile? mod = ModManifestScenarios.TryParseRoot(root);

            return new ModManifest(
                schema,
                id,
                name,
                version,
                parent,
                new ModRequirements(engine, capabilities),
                formats,
                mod);
        }
        catch (PersistenceError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PersistenceError("mod.parse", "Failed to parse mod.json: " + ex.Message);
        }
    }

    public static ModManifest ReadFromSource(IModSource source)
    {
        if (source.Exists(ModManifestPaths.FileName))
        {
            return Read(source.Read(ModManifestPaths.FileName));
        }

        if (source.Exists(ModManifestPaths.LegacyModJson))
        {
            return Read(source.Read(ModManifestPaths.LegacyModJson));
        }

        throw new PersistenceError("mod.missing", "Pack is missing mod.json.");
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value);
                }
            }
        }

        return list;
    }
}
