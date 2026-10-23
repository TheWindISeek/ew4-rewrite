using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ew4.Platform.Content;

internal static class PackManifestReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static PackManifest Read(byte[] jsonBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ContentError("pack.schema", "mod.json must be a JSON object.");
            }

            int schema = root.GetProperty("schema").GetInt32();
            string id = PackId.Validate(root.GetProperty("id").GetString() ?? "");
            string name = root.GetProperty("name").GetString() ?? "";
            string version = root.GetProperty("version").GetString() ?? "";
            string? parent = root.TryGetProperty("parent", out var parentEl) && parentEl.ValueKind == JsonValueKind.String
                ? parentEl.GetString()
                : null;

            if (!root.TryGetProperty("requires", out var requiresEl) || requiresEl.ValueKind != JsonValueKind.Object)
            {
                throw new ContentError("pack.requires", "mod.json requires a 'requires' object.");
            }

            string engine = requiresEl.GetProperty("engine").GetString() ?? "";
            var capabilities = ReadStringArray(requiresEl, "capabilities");

            if (!root.TryGetProperty("formats", out var formatsEl) || formatsEl.ValueKind != JsonValueKind.Object)
            {
                throw new ContentError("pack.formats", "mod.json requires a 'formats' object.");
            }

            var formats = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in formatsEl.EnumerateObject())
            {
                formats[property.Name] = property.Value.GetInt32();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ContentError("pack.name", "Pack name is required.");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ContentError("pack.version", "Pack version is required.");
            }

            PackModProfile.ModProfile? mod = PackManifestScenarios.TryParseRoot(root);

            return new PackManifest(
                schema,
                id,
                name,
                version,
                parent,
                new PackRequirements(engine, capabilities),
                formats,
                mod);
        }
        catch (ContentError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ContentError("pack.parse", "Failed to parse mod.json: " + ex.Message);
        }
    }

    public static PackManifest ReadFromSource(IPackSource source)
    {
        if (source.Exists(PackManifestPaths.FileName))
        {
            return Read(source.Read(PackManifestPaths.FileName));
        }

        if (source.Exists(PackManifestPaths.LegacyPackJson))
        {
            return Read(source.Read(PackManifestPaths.LegacyPackJson));
        }

        throw new ContentError("pack.missing", "Pack is missing mod.json.");
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
