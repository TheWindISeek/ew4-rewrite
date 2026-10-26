using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Persistence.User;

/// <summary>读写 <c>user/active.json</c>（当前激活的 mod id）。</summary>
public sealed class ActiveModStore
{
    private readonly string _path;

    public ActiveModStore(string stateDirectory)
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
        if (document.RootElement.TryGetProperty("modId", out var id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        if (document.RootElement.TryGetProperty("packId", out var legacy) && legacy.ValueKind == JsonValueKind.String)
        {
            return legacy.GetString();
        }

        return null;
    }

    public void Write(string? modId)
    {
        string json = JsonSerializer.Serialize(new { modId = modId ?? "base", packId = modId ?? "base" });
        string temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, overwrite: true);
    }
}
