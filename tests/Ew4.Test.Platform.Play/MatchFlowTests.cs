using Ew4.Platform.Engine;
using Ew4.Platform.Play.Match;
using Xunit;

namespace Ew4.Test.Platform.Play;

public sealed class MatchFlowTests
{
    private static readonly FlowRequest Talk = new(FlowRequestKind.Dialogue, "d1");
    private static readonly FlowRequest Popup = new(FlowRequestKind.ScenarioPopup, "scenario.popup/e1");

    [Fact]
    public void Raise_keeps_trigger_order_and_blocks()
    {
        var flow = new MatchFlow();
        flow.Raise(new[] { Talk, Popup });
        Assert.True(flow.Blocked);
        Assert.Equal(new[] { "d1", "scenario.popup/e1" }, flow.Waiting);
    }

    [Fact]
    public void Dialogue_and_popup_heads_are_checked()
    {
        var flow = new MatchFlow();
        Assert.Equal("client.dialogue", Assert.Throws<RuleViolation>(flow.RequireDialogue).Code);
        flow.Raise(new[] { Popup });
        Assert.Equal("scenario.choice_required", Assert.Throws<RuleViolation>(flow.RequireDialogue).Code);
        Assert.Equal(FlowKind.ScenarioPopup, flow.RequirePopup().Kind);
    }

    [Fact]
    public void Replace_and_restore_round_trip()
    {
        var flow = new MatchFlow();
        flow.Raise(new[] { Talk, Popup });
        flow.Replace(new[] { Popup });
        Assert.Equal(new[] { "scenario.popup/e1" }, flow.Waiting);
        var loaded = new MatchFlow();
        loaded.Restore(flow.Pending);
        Assert.Equal(flow.Waiting, loaded.Waiting);
        flow.Replace(System.Array.Empty<FlowRequest>());
        Assert.False(flow.Blocked);
    }
}
