using System.Text.Json;
using Ew4.Interface.Common;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Ew4.Interface.Plans;
using Ew4.Service.Plans;
using Xunit;

namespace Ew4.Test.Service;

public sealed class PlanTests
{
    private static readonly PlanKey<int> Number = new("t.number");
    private static readonly PlanKey<string> Secret = new("t.secret");
    private static readonly PlanKey<string> View = new("t.view", new("view"));
    private static readonly PlanKey<string> Cue = new("t.cue", new("effects", SlotKind.Append));

    private static readonly OperationDescriptor ListA = new EditorOperationDescriptor(EditorOperationKind.WorkspaceList);
    private static readonly OperationDescriptor ListB = new EditorOperationDescriptor(EditorOperationKind.WorkspaceDelete);
    private static readonly OperationPayload Empty = new EditorWorkspacePayload();

    private static string A => RequestNames.For(EditorOperationKind.WorkspaceList);

    private static (PlanRunner Runner, PlanRegistry Registry) Create(Action<PlanRegistry> configure)
    {
        var registry = new PlanRegistry();
        configure(registry);
        registry.Validate();
        return (new PlanRunner(registry), registry);
    }

    private static JsonElement Parse(OperationResponse response) => JsonDocument.Parse(response.TextContent!).RootElement;

    [Fact]
    public void Steps_pass_data_through_keys_and_only_exposed_keys_reach_the_response()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.make", [], [Number, Secret], c => { c.Set(Number, 7); c.Set(Secret, "internal"); }))
            .Step(Steps.Of("t.render", [Number], [View], c => c.Set(View, "n=" + c.Get(Number))))
            .Done());

        JsonElement body = Parse(runner.Run(ListA, Empty));

        Assert.Equal("n=7", body.GetProperty("view").GetString());
        Assert.False(body.TryGetProperty("t.secret", out _));
        Assert.False(body.TryGetProperty("secret", out _));
    }

    [Fact]
    public void Append_slot_collects_values_in_step_order()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.a", [], [Cue], c => c.Set(Cue, "one")))
            .Step(Steps.Of("t.b", [], [Cue], c => c.Set(Cue, "two")))
            .Done());

        JsonElement effects = Parse(runner.Run(ListA, Empty)).GetProperty("effects");

        Assert.Equal(["one", "two"], effects.EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public void Raw_response_is_returned_as_is()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.raw", [], [PlanKeys.Response], c => c.Set(PlanKeys.Response, OperationResponse.CreatedJson("{}"))))
            .Done());

        Assert.Equal(201, runner.Run(ListA, Empty).StatusCode);
    }

    [Fact]
    public void Required_step_failure_propagates()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.ok", [], [View], c => c.Set(View, "x")))
            .Step(Steps.Of("t.boom", c => throw new OperationException("t.code", "boom")))
            .Done());

        var error = Assert.Throws<OperationException>(() => runner.Run(ListA, Empty));
        Assert.Equal("t.code", error.Code);
    }

    [Fact]
    public void Optional_step_failure_goes_to_diagnostics_and_its_writes_are_dropped()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.main", [], [View], c => c.Set(View, "ok")))
            .Step(Steps.Of("t.music", [], [Cue], c => { c.Set(Cue, "half-written"); throw new OperationException("audio.none", "no bgm"); }),
                optional: true)
            .Done());

        JsonElement body = Parse(runner.Run(ListA, Empty));

        Assert.Equal("ok", body.GetProperty("view").GetString());
        Assert.False(body.TryGetProperty("effects", out _));
        JsonElement diagnostic = body.GetProperty("diagnostics")[0];
        Assert.Equal("t.music", diagnostic.GetProperty("step").GetString());
        Assert.Equal("audio.none", diagnostic.GetProperty("code").GetString());
    }

    [Fact]
    public void Step_cannot_use_undeclared_keys()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.make", [], [Number], c => c.Set(Number, 1)))
            .Step(Steps.Of("t.peek", [], [View], c => c.Set(View, c.Get(Number).ToString())))
            .Done());

        Assert.Throws<InvalidOperationException>(() => runner.Run(ListA, Empty));
    }

    [Fact]
    public void Validation_rejects_read_before_write()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.render", [Number], [View], c => { }))
            .Done()));
    }

    [Fact]
    public void Validation_rejects_two_writers_for_a_single_slot()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.one", [], [View], c => c.Set(View, "1")))
            .Step(Steps.Of("t.two", [], [View], c => c.Set(View, "2")))
            .Done()));
    }

    [Fact]
    public void Validation_rejects_a_later_step_depending_on_an_optional_writer()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.opt", [], [Number], c => c.Set(Number, 1)), optional: true)
            .Step(Steps.Of("t.use", [Number], [View], c => { }))
            .Done()));
    }

    [Fact]
    public void Optional_reads_see_an_optional_writer_when_it_succeeds_and_nothing_when_it_fails()
    {
        PlanKey<string> seen = View;
        var (ok, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.opt", [], [Number], c => c.Set(Number, 5)), optional: true)
            .Step(Steps.Of("t.use", [], [Number], [seen], c => c.Set(seen, c.TryGet(Number, out int n) ? "n=" + n : "none")))
            .Done());
        Assert.Equal("n=5", Parse(ok.Run(ListA, Empty)).GetProperty("view").GetString());

        var (failed, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.opt", [], [Number], c => throw new OperationException("t.boom", "x")), optional: true)
            .Step(Steps.Of("t.use", [], [Number], [seen], c => c.Set(seen, c.TryGet(Number, out int n) ? "n=" + n : "none")))
            .Done());
        Assert.Equal("none", Parse(failed.Run(ListA, Empty)).GetProperty("view").GetString());
    }

    [Fact]
    public void Conditional_step_runs_only_when_its_predicate_holds()
    {
        PlanKey<string> seen = View;
        Func<int, PlanRunner> build = flag => Create(r => r.Request(A)
            .Step(Steps.Of("t.flag", [], [Number], c => c.Set(Number, flag)))
            .Step(Steps.Of("t.cue", [Number], [Cue], c => c.Set(Cue, "cue")), when: c => c.Get(Number) > 0)
            .Step(Steps.Of("t.view", [Number], [seen], c => c.Set(seen, "done")))
            .Done()).Runner;

        JsonElement on = Parse(build(1).Run(ListA, Empty));
        Assert.Equal(["cue"], on.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!).ToArray());

        JsonElement off = Parse(build(0).Run(ListA, Empty));
        Assert.False(off.TryGetProperty("effects", out _));
        Assert.Equal("done", off.GetProperty("view").GetString());
        Assert.False(off.TryGetProperty("diagnostics", out _));
    }

    [Fact]
    public void Conditional_predicate_cannot_read_undeclared_keys()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.flag", [], [Number], c => c.Set(Number, 1)))
            .Step(Steps.Of("t.cue", [], [View], c => c.Set(View, "x")), when: c => c.Get(Number) > 0)
            .Done());

        Assert.Throws<InvalidOperationException>(() => runner.Run(ListA, Empty));
    }

    [Fact]
    public void Validation_treats_conditional_writes_as_maybe_present()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.cond", [], [Number], c => c.Set(Number, 1)), when: _ => true)
            .Step(Steps.Of("t.use", [Number], [View], c => { }))
            .Done()));
    }

    [Fact]
    public void Validation_rejects_optional_reads_nobody_writes()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.use", [], [Number], [View], c => { }))
            .Done()));
    }

    [Fact]
    public void Validation_rejects_duplicate_requests_and_duplicate_step_ids()
    {
        Assert.Throws<PlanDefinitionException>(() => Create(r =>
        {
            r.Request(A).Step(Steps.Of("t.x", c => { })).Done();
            r.Request(A).Step(Steps.Of("t.y", c => { })).Done();
        }));
        Assert.Throws<PlanDefinitionException>(() => Create(r => r.Request(A)
            .Step(Steps.Of("t.same", c => { }))
            .Step(Steps.Of("t.same", c => { }))
            .Done()));
    }

    [Fact]
    public async Task Same_resource_is_serialized_and_reject_policy_reports_busy()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var (runner, _) = Create(r => r.Request(A)
            .Resource("r1", ConflictPolicy.Reject)
            .Step(Steps.Of("t.hold", c => { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); }))
            .Done());

        Task first = Task.Run(() => runner.Run(ListA, Empty));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        Assert.Throws<PlanBusyException>(() => runner.Run(ListA, Empty));

        release.Set();
        await first;
    }

    [Fact]
    public async Task Different_resources_do_not_block_each_other()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        string b = RequestNames.For(EditorOperationKind.WorkspaceDelete);
        var (runner, _) = Create(r =>
        {
            r.Request(A).Resource("r1").Step(Steps.Of("t.hold", c => { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); })).Done();
            r.Request(b).Resource("r2").Step(Steps.Of("t.fast", [], [View], c => c.Set(View, "fast"))).Done();
        });

        Task first = Task.Run(() => runner.Run(ListA, Empty));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal("fast", Parse(runner.Run(ListB, Empty)).GetProperty("view").GetString());

        release.Set();
        await first;
    }

    [Fact]
    public void Step_timeout_is_a_failure()
    {
        var (runner, _) = Create(r => r.Request(A)
            .Step(Steps.Of("t.slow", [], [], c => Thread.Sleep(500), TimeSpan.FromMilliseconds(30)))
            .Done());

        Assert.Equal("plan.timeout", Assert.Throws<OperationException>(() => runner.Run(ListA, Empty)).Code);
    }

    [Fact]
    public void Request_names_cover_every_plan_backed_descriptor()
    {
        foreach (EditorOperationKind kind in Enum.GetValues<EditorOperationKind>())
        {
            Assert.False(string.IsNullOrEmpty(RequestNames.For(kind)));
        }
    }
}
