using System.Text.Json;
using Ew4.Interface.Common;
using Ew4.Service.Audio;
using Ew4.Service.Plans;
using Xunit;

namespace Ew4.Test.Service;

public sealed class AudioTests
{
    private const string Table = """
        { "schema": 1, "default": "music/a.ogg", "countries": { "fra": "music/b.mp3" } }
        """;

    [Fact]
    public void Country_track_wins_and_default_is_the_fallback()
    {
        BgmCatalog catalog = BgmCatalog.Parse(Table);

        Assert.Equal("music/b.mp3", catalog.Pick("fra")!.Track);
        Assert.Equal("music/a.ogg", catalog.Pick("ger")!.Track);
        Assert.Equal("music/a.ogg", catalog.Pick(null)!.Track);
        Assert.True(catalog.Pick("fra")!.Loop);
    }

    [Fact]
    public void User_preferences_override_the_mod_per_country_and_fall_back_in_order()
    {
        BgmCatalog mod = BgmCatalog.Parse(Table);
        BgmCatalog user = BgmCatalog.Empty.With("fra", "music/mine.ogg").With("ger", "music/g.ogg");

        Assert.Equal("music/mine.ogg", mod.Pick("fra", user)!.Track);
        Assert.Equal("music/g.ogg", mod.Pick("ger", user)!.Track);
        Assert.Equal("music/a.ogg", mod.Pick("rus", user)!.Track);

        BgmCatalog withDefault = user.With(null, "music/d.ogg");
        Assert.Equal("music/d.ogg", mod.Pick("rus", withDefault)!.Track);
        Assert.Equal("music/b.mp3", mod.Pick("fra", user.With("fra", null))!.Track);
    }

    [Fact]
    public void Preferences_round_trip_through_the_save_root_and_ignore_corrupt_files()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-bgm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var prefs = new BgmPreferences(root);
            Assert.Empty(prefs.Load().Countries);

            prefs.Set("fra", "music/x.ogg");
            Assert.Equal("music/x.ogg", new BgmPreferences(root).Load().Countries["fra"]);

            File.WriteAllText(Path.Combine(root, "music.json"), "{not json");
            Assert.Empty(prefs.Load().Countries);
            Assert.Throws<OperationException>(() => prefs.Set("fra", "../evil.ogg"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Empty_table_picks_nothing()
    {
        Assert.Null(BgmCatalog.Empty.Pick("fra"));
        Assert.Null(BgmCatalog.Parse("""{ "schema": 1 }""").Pick("fra"));
    }

    [Theory]
    [InlineData("../x.ogg")]
    [InlineData("/abs.ogg")]
    [InlineData("music\\a.ogg")]
    [InlineData("http://x/a.ogg")]
    [InlineData("music/a.exe")]
    public void Unsafe_or_unknown_tracks_are_rejected(string track)
    {
        string json = "{\"default\":\"" + track.Replace("\\", "\\\\") + "\"}";
        Assert.Throws<OperationException>(() => BgmCatalog.Parse(json));
    }

    [Fact]
    public void Player_country_comes_from_the_player_faction_then_start_country()
    {
        const string withFactions = """
            { "ok": true, "data": { "playerFaction": "f2", "startCountry": "x",
              "factions": [ { "id": "f1", "country": "ger" }, { "id": "f2", "country": "fra" } ] } }
            """;
        Assert.Equal("fra", AudioSteps.PlayerCountry(withFactions));
        Assert.Equal("x", AudioSteps.PlayerCountry("""{ "ok": true, "data": { "startCountry": "x" } }"""));
        Assert.Null(AudioSteps.PlayerCountry("""{ "ok": false, "code": "c" }"""));
    }

    [Fact]
    public void Audio_is_merged_into_successful_envelopes_only()
    {
        const string ok = """{ "ok": true, "data": { "revision": 3 } }""";
        using JsonDocument merged = JsonDocument.Parse(AudioSteps.WithAudio(ok, new BgmChoice("music/a.ogg")));
        JsonElement bgm = merged.RootElement.GetProperty("data").GetProperty("audio").GetProperty("bgm");
        Assert.Equal("music/a.ogg", bgm.GetProperty("track").GetString());
        Assert.True(bgm.GetProperty("loop").GetBoolean());
        Assert.Equal(3, merged.RootElement.GetProperty("data").GetProperty("revision").GetInt32());

        using JsonDocument silent = JsonDocument.Parse(AudioSteps.WithAudio(ok, null));
        Assert.Equal(JsonValueKind.Null, silent.RootElement.GetProperty("data").GetProperty("audio").GetProperty("bgm").ValueKind);

        const string failure = """{ "ok": false, "code": "c", "message": "m" }""";
        Assert.Equal(failure, AudioSteps.WithAudio(failure, new BgmChoice("music/a.ogg")));
    }
}
