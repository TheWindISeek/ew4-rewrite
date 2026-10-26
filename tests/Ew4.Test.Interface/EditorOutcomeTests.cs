using Ew4.Interface.Common;
using Ew4.Interface.Editor;
using Xunit;

namespace Ew4.Test.Interface;

public sealed class EditorOutcomeTests
{
    [Fact]
    public void Ok_carries_value()
    {
        var outcome = EditorOutcome<CommanderEditOutcome>.Ok(
            new CommanderEditOutcome(7, new EditorWriteOutcome("def_commander.xml", Array.Empty<Diagnostic>())));
        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Value);
        Assert.Equal(7, outcome.Value!.LegacyId);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void Fail_carries_error()
    {
        var outcome = EditorOutcome<string>.Fail("editor.roundtrip", "mismatch");
        Assert.False(outcome.Success);
        Assert.Null(outcome.Value);
        Assert.Equal("editor.roundtrip", outcome.Error!.Code);
    }
}
