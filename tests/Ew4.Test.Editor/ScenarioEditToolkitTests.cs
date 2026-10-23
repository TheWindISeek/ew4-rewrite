using Ew4.Editor.Campaign;
using Ew4.Platform.Content.ScenarioEdit;
using Xunit;

namespace Ew4.Test.Editor;

public sealed class ScenarioEditToolkitTests
{
    private static readonly IScenarioEditToolkit Toolkit = ScenarioEditToolkit.Default;

    [Fact]
    public void Json_roundtrip_preserves_export_bytes_when_btl_corpus_present()
    {
        string btl = Path.GetFullPath("mods/base/source/original/campaign1_01.btl");
        if (!File.Exists(btl))
        {
            return;
        }

        byte[] raw = File.ReadAllBytes(btl);
        ScenarioDocument document = Toolkit.ImportBtl("campaign1_01", raw);
        byte[] json = Toolkit.SerializeJson(document);
        ScenarioDocument restored = Toolkit.DeserializeJson(json);
        byte[] exported = Toolkit.ExportBtl(restored, ScenarioExportMode.PreserveSections);
        Assert.True(exported.AsSpan().SequenceEqual(raw));
    }

    [Fact]
    public void Validate_reports_all_checks_for_corpus_btl()
    {
        string btl = Path.GetFullPath("mods/base/source/original/campaign1_01.btl");
        if (!File.Exists(btl))
        {
            return;
        }

        byte[] raw = File.ReadAllBytes(btl);
        ScenarioBtlRoundtrip.Result result = Toolkit.ValidateBtlRoundtrip("campaign1_01", raw);
        Assert.True(result.OriginalClone);
        Assert.True(result.Document);
        Assert.True(result.DocumentJson);
        Assert.True(result.Layers);
    }
}
