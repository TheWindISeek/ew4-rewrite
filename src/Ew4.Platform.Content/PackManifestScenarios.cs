using System;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Content;

internal static class PackManifestScenarios
{
    public static PackModProfile.ModProfile? TryParseRoot(JsonElement root) =>
        root.TryGetProperty("scenarios", out JsonElement scenarios) && scenarios.ValueKind == JsonValueKind.Object
            ? PackModProfile.ParseScenariosSection(root, scenarios)
            : null;

    public static void MergeIntoManifest(string packRootDirectory, PackModProfile.ModProfile profile)
    {
        string root = Path.GetFullPath(packRootDirectory);
        PackManifestPaths.MigrateLegacyPackJsonIfNeeded(root);
        string manifestPath = Path.Combine(root, PackManifestPaths.FileName);

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

    public static void WriteScenarios(Utf8JsonWriter writer, PackModProfile.ModProfile profile)
    {
        writer.WritePropertyName("scenarios");
        writer.WriteStartObject();
        writer.WriteString("storage", profile.ScenarioStorage switch
        {
            PackModProfile.ScenarioStorageMode.EmbeddedJson => "embedded-json",
            PackModProfile.ScenarioStorageMode.LegacyBtl => "legacy-btl",
            _ => "auto",
        });
        writer.WriteNumber("documentSchema", profile.ScenarioDocumentSchema);
        writer.WriteEndObject();
    }
}
