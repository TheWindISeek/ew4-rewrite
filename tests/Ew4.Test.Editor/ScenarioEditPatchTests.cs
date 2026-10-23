using System.Buffers.Binary;
using Ew4.Editor.Campaign;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;
using Xunit;

namespace Ew4.Test.Editor;

public sealed class ScenarioEditPatchTests
{
    private static readonly IScenarioEditToolkit Toolkit = ScenarioEditToolkit.Default;

    [Fact]
    public void PatchArmyCell_updates_layer_and_armies_section()
    {
        string btl = Path.GetFullPath("mods/base/source/original/campaign1_01.btl");
        if (!File.Exists(btl))
        {
            return;
        }

        byte[] raw = File.ReadAllBytes(btl);
        ScenarioDocument document = Toolkit.ImportBtl("campaign1_01", raw);
        if (document.Layers.Armies.Count == 0)
        {
            return;
        }

        short newCell = document.Layers.Armies[0].Cell == 0 ? (short)1 : (short)0;
        ScenarioDocument patched = Toolkit.PatchArmyCell(document, 0, newCell);
        Assert.Equal(newCell, patched.Layers.Armies[0].Cell);

        byte[] armiesSection = patched.Sections["armies"];
        short encoded = BinaryPrimitives.ReadInt16LittleEndian(armiesSection.AsSpan(0, 2));
        Assert.Equal(newCell, encoded);
    }

    [Fact]
    public void PatchArmyCell_preserves_btl_roundtrip_when_corpus_present()
    {
        string btl = Path.GetFullPath("mods/base/source/original/campaign1_01.btl");
        if (!File.Exists(btl))
        {
            return;
        }

        byte[] raw = File.ReadAllBytes(btl);
        ScenarioDocument document = Toolkit.ImportBtl("campaign1_01", raw);
        if (document.Layers.Armies.Count == 0)
        {
            return;
        }

        short originalCell = document.Layers.Armies[0].Cell;
        ScenarioDocument patched = Toolkit.PatchArmyCell(document, 0, originalCell);
        byte[] exported = Toolkit.ExportBtl(patched, ScenarioExportMode.PreserveSections);
        Assert.True(exported.AsSpan().SequenceEqual(raw));
    }
}
