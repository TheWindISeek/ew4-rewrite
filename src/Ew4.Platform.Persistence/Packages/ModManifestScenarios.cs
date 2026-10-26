using System;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Persistence.Packages;

internal static class ModManifestScenarios
{
    public static ModManifestProfile.ModProfile? TryParseRoot(JsonElement root) =>
        root.TryGetProperty("scenarios", out JsonElement scenarios) && scenarios.ValueKind == JsonValueKind.Object
            ? ModManifestProfile.ParseScenariosSection(root, scenarios)
            : null;

    public static void MergeIntoManifest(string modRootDirectory, ModManifestProfile.ModProfile profile)
    {
        string root = Path.GetFullPath(modRootDirectory);
        ModManifestPaths.MigrateLegacyModJsonIfNeeded(root);
        string manifestPath = Path.Combine(root, ModManifestPaths.FileName);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "scenarios", StringComparison.Ordinal))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            WriteScenarios(writer, profile);
            writer.WriteEndObject();
        }

        File.WriteAllBytes(manifestPath, stream.ToArray());
    }

    public static void WriteScenarios(Utf8JsonWriter writer, ModManifestProfile.ModProfile profile)
    {
        writer.WritePropertyName("scenarios");
        writer.WriteStartObject();
        writer.WriteString("storage", profile.ScenarioStorage switch
        {
            ModManifestProfile.ScenarioStorageMode.EmbeddedJson => "embedded-json",
            ModManifestProfile.ScenarioStorageMode.LegacyBtl => "legacy-btl",
            _ => "auto",
        });
        writer.WriteNumber("documentSchema", profile.ScenarioDocumentSchema);
        writer.WriteEndObject();
    }
}
