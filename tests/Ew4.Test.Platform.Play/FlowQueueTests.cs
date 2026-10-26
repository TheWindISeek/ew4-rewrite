using Ew4.Platform.Engine;
using Ew4.Platform.Play.Match;
using Xunit;

namespace Ew4.Test.Platform.Play;

public sealed class FlowQueueTests
{
    [Fact]
    public void Empty_queue_is_not_blocked()
    {
        Assert.False(FlowQueue.Empty.Blocked);
        Assert.Null(FlowQueue.Empty.Head);
    }

    [Fact]
    public void Popup_group_outranks_dialogue_group()
    {
        FlowQueue queue = FlowQueue.Empty
            .Enqueue(FlowItem.Dialogue("a", "d1"))
            .Enqueue(FlowItem.ScenarioPopup("b", "p1"));

        Assert.Equal(FlowKind.ScenarioPopup, queue.Head!.Kind);
    }

    [Fact]
    public void Items_inside_one_group_keep_script_order()
    {
        FlowQueue queue = FlowQueue.Empty.Enqueue(
            FlowItem.Dialogue("e", "d1"),
            FlowItem.ScenarioPopup("e", "p1"),
            FlowItem.Dialogue("e", "d2"));

        Assert.Equal(new[] { "d1", "p1", "d2" }, queue.Ordered.Select(i => i.Id));
        Assert.Equal("d1", queue.Head!.Id);
    }

    [Fact]
    public void Same_priority_groups_are_first_in_first_out()
    {
        FlowQueue queue = FlowQueue.Empty
            .Enqueue(FlowItem.Dialogue("a", "d1"))
            .Enqueue(FlowItem.Dialogue("b", "d2"));

        Assert.Equal(new[] { "d1", "d2" }, queue.Ordered.Select(i => i.Id));
    }

    [Fact]
    public void Acknowledge_pops_only_matching_head()
    {
        FlowQueue queue = FlowQueue.Empty.Enqueue(FlowItem.Dialogue("a", "d1"), FlowItem.Dialogue("a", "d2"));

        queue = queue.Acknowledge("d1");
        Assert.Equal("d2", queue.Head!.Id);
        queue = queue.Acknowledge("d2");
        Assert.False(queue.Blocked);
    }

    [Fact]
    public void Wrong_resolution_or_id_is_rejected_with_clear_code()
    {
        FlowQueue popup = FlowQueue.Empty.Enqueue(FlowItem.ScenarioPopup("a", "p1"));
        FlowQueue dialogue = FlowQueue.Empty.Enqueue(FlowItem.Dialogue("a", "d1"));

        Assert.Equal("flow.mismatch", Assert.Throws<RuleViolation>(() => popup.Acknowledge("p1")).Code);
        Assert.Equal("flow.mismatch", Assert.Throws<RuleViolation>(() => dialogue.Choose("d1")).Code);
        Assert.Equal("flow.mismatch", Assert.Throws<RuleViolation>(() => dialogue.Acknowledge("other")).Code);
        Assert.Equal("flow.empty", Assert.Throws<RuleViolation>(() => FlowQueue.Empty.Acknowledge("d1")).Code);
    }

    [Fact]
    public void Only_game_over_may_be_unresolvable()
    {
        var bad = new FlowItem(FlowKind.Dialogue, "d1", "a", FlowResolution.None);
        Assert.Equal("flow.unresolvable", Assert.Throws<RuleViolation>(() => FlowQueue.Empty.Enqueue(bad)).Code);

        var badOver = new FlowItem(FlowKind.GameOver, "over", "g", FlowResolution.Ack);
        Assert.Equal("flow.unresolvable", Assert.Throws<RuleViolation>(() => FlowQueue.Empty.Enqueue(badOver)).Code);
    }

    [Fact]
    public void Game_over_outranks_everything_and_cannot_be_resolved()
    {
        FlowQueue queue = FlowQueue.Empty
            .Enqueue(FlowItem.ScenarioPopup("a", "p1"))
            .Enqueue(FlowItem.GameOver());

        Assert.Equal(FlowKind.GameOver, queue.Head!.Kind);
        Assert.Equal("flow.unresolvable", Assert.Throws<RuleViolation>(() => queue.Acknowledge("game-over")).Code);
    }

    [Fact]
    public void Closed_queue_ignores_new_items()
    {
        FlowQueue queue = FlowQueue.Empty.Enqueue(FlowItem.GameOver());
        FlowQueue after = queue.Enqueue(FlowItem.Dialogue("late", "d1"));

        Assert.True(after.Closed);
        Assert.Single(after.Pending);
    }

    [Fact]
    public void Duplicate_group_in_queue_is_rejected()
    {
        FlowQueue queue = FlowQueue.Empty.Enqueue(FlowItem.Dialogue("a", "d1"));
        Assert.Equal("flow.duplicate_group", Assert.Throws<RuleViolation>(() => queue.Enqueue(FlowItem.Dialogue("a", "d2"))).Code);
    }

    [Fact]
    public void Mixed_groups_in_one_enqueue_are_rejected()
    {
        Assert.Equal("flow.group", Assert.Throws<RuleViolation>(() =>
            FlowQueue.Empty.Enqueue(FlowItem.Dialogue("a", "d1"), FlowItem.Dialogue("b", "d2"))).Code);
    }

    [Fact]
    public void Restore_reproduces_the_same_order()
    {
        FlowQueue queue = FlowQueue.Empty
            .Enqueue(FlowItem.Dialogue("a", "d1"), FlowItem.Dialogue("a", "d2"))
            .Enqueue(FlowItem.ScenarioPopup("b", "p1"));
        FlowQueue restored = FlowQueue.Restore(queue.Pending);

        Assert.Equal(queue.Ordered, restored.Ordered);
    }

    [Fact]
    public void Every_resolvable_head_can_always_be_cleared_without_deadlock()
    {
        FlowQueue queue = FlowQueue.Empty
            .Enqueue(FlowItem.Dialogue("a", "d1"), FlowItem.ScenarioPopup("a", "p1"), FlowItem.Dialogue("a", "d2"))
            .Enqueue(FlowItem.ScenarioPopup("b", "p2"))
            .Enqueue(FlowItem.Dialogue("c", "d3"));

        int guard = 0;
        while (queue.Blocked)
        {
            FlowItem head = queue.Head!;
            queue = head.Resolution == FlowResolution.Ack ? queue.Acknowledge(head.Id) : queue.Choose(head.Id);
            Assert.True(++guard < 20);
        }

        Assert.Equal(5, guard);
    }
}
