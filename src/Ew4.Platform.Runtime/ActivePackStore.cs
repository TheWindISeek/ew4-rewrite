using System;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Runtime;

public sealed class ActivePackStore
{
    private readonly string _path;

    public ActivePackStore(string stateDirectory)
    {
        Directory.CreateDirectory(stateDirectory);
        _path = Path.Combine(stateDirectory, "active.json");
    }

    public string? Read()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        using var stream = File.OpenRead(_path);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.TryGetProperty("packId", out var id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        return null;
    }

    public void Write(string? packId)
    {
        string json = JsonSerializer.Serialize(new { packId = packId ?? "base" });
        string temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, overwrite: true);
    }
}
