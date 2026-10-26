using System.Text;
using Ew4.Interface.Common;
using Ew4.Service.Localization;
using Xunit;

namespace Ew4.Test.Service;

public sealed class LocalizationTests
{
    private static LocalizedStrings Load(Dictionary<string, string> files) =>
        LocalizedStrings.Load(path => files.TryGetValue(path, out string? json) ? Encoding.UTF8.GetBytes(json) : null);

    private static readonly Dictionary<string, string> Files = new()
    {
        ["client/lang/zh-Hans.json"] = """{ "schema": 1, "strings": { "ui.save": "存档", "ui.back": "返回" } }""",
        ["client/lang/en.json"] = """{ "schema": 1, "strings": { "ui.save": "Saves" } }""",
    };

    [Fact]
    public void Fallback_chain_is_language_then_default_then_legacy()
    {
        var legacy = new Dictionary<string, string> { ["name_Napoleon"] = "拿破仑", ["ui.save"] = "旧" };
        IReadOnlyDictionary<string, string> en = Load(Files).Resolve("en", legacy);

        Assert.Equal("Saves", en["ui.save"]);      // 当前语言
        Assert.Equal("返回", en["ui.back"]);        // 回退默认语言
        Assert.Equal("拿破仑", en["name_Napoleon"]); // 回退原版表
        Assert.Equal("存档", Load(Files).Resolve("zh-Hans", legacy)["ui.save"]);
    }

    [Fact]
    public void Available_lists_default_always_and_others_only_with_a_file()
    {
        Assert.Equal(["zh-Hans", "en"], Load(Files).Available().Select(l => l.Code).ToArray());
        Assert.Equal(["zh-Hans"], LocalizedStrings.Empty.Available().Select(l => l.Code).ToArray());
    }

    [Theory]
    [InlineData("""{ "strings": 1 }""")]
    [InlineData("""{ "schema": 1 }""")]
    [InlineData("""{ "strings": { "a": 2 } }""")]
    [InlineData("{not json")]
    public void Malformed_tables_are_rejected(string json) =>
        Assert.Throws<OperationException>(() => LocalizedStrings.Parse("en", json));

    [Fact]
    public void Unknown_language_normalizes_to_default()
    {
        Assert.Equal("zh-Hans", Languages.Normalize("fr"));
        Assert.Equal("zh-Hans", Languages.Normalize(null));
        Assert.Equal("en", Languages.Normalize("en"));
    }

    [Fact]
    public void User_settings_round_trip_and_survive_corruption()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-lang-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new UserSettings(root);
            Assert.Equal("zh-Hans", settings.Language);

            settings.SetLanguage("en");
            Assert.Equal("en", new UserSettings(root).Language);

            File.WriteAllText(Path.Combine(root, "settings.json"), "{broken");
            Assert.Equal("zh-Hans", settings.Language);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
