using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ew4.Service.Localization;

/// <summary>玩家本机设置：<c>&lt;user&gt;/settings.json</c>，跨 mod 共享（目前只有语言）。损坏的文件当作没设置。</summary>
public sealed class UserSettings(string stateRoot)
{
    private string FilePath => Path.Combine(stateRoot, "settings.json");

    public string Language
    {
        get
        {
            try
            {
                if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) is JsonObject root)
                {
                    return Languages.Normalize(root["language"]?.GetValue<string>());
                }
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
            {
            }

            return Languages.Default;
        }
    }

    public void SetLanguage(string language)
    {
        Directory.CreateDirectory(stateRoot);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, new JsonObject { ["language"] = Languages.Normalize(language) }.ToJsonString(), new UTF8Encoding(false));
        File.Move(temp, FilePath, overwrite: true);
    }
}
