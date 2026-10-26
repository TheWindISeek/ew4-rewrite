using Ew4.Interface.Host;
using Ew4.Interface.Plans;
using Ew4.Interface.Play;
using Ew4.Interface.Common;
using System.Text.Json;
using Ew4.Platform.Engine;
using Ew4.Service.Host;
using Ew4.Service.Play;

namespace Ew4.Service.Plans;

/// <summary>游玩域的 Step 与 Plan（目前只有局内 action）。</summary>
public sealed class PlayPlans : IPlanModule
{
    public static readonly PlanKey<string> Body = new("play.body");
    public static readonly PlanKey<string> Revision = new("play.content-revision");

    internal static readonly PlanKey<PlayCommand> Command = new("play.command");
    internal static readonly PlanKey<StaticDataScenarioStart> Opened = new("play.opened");
    internal static readonly PlanKey<PlaySlot> Slot = new("play.slot");

    /// <summary>引擎返回的原始信封 JSON（start/load 在并入音频前的形态）。</summary>
    public static readonly PlanKey<string> Envelope = new("play.envelope");

    private readonly IRuntimeHost _host;
    private readonly PlayHost _play;

    public PlayPlans(IRuntimeHost host, PlayHost play)
    {
        _host = host;
        _play = play;
    }

    public void Register(PlanRegistry registry)
    {
        MusicPlan(registry);
        BodyPlan(registry, PlayRoutes.Language, "language", "language.config", body => LanguageSteps.Config(_host, _play, body));
        BodyPlan(registry, PlayRoutes.ExportApk, "export", "export.apk", _ => LanguageSteps.ExportApkUnimplemented());
        foreach (string route in PlayRoutes.All)
        {
            string name = RequestNames.ForRoute(route);
            Func<string, string, string> call = route switch
            {
                PlayRoutes.Action => (revision, body) => _play.Engine.InvokeEnvelope(revision, body),
                PlayRoutes.Save or PlayRoutes.Load or PlayRoutes.Saves => (revision, body) => _play.Persistence.InvokeEnvelope(revision, route, body),
                _ => (revision, body) => _play.Content.InvokeEnvelope(revision, route, body),
            };

            PlanBuilder plan = registry.Request(name)
                .Resource("session")
                .Step(Steps.Of("play.require-body", [], [Body], ctx =>
                    ctx.Set(Body, ctx.Get(PlanKeys.Payload) is PlayBodyPayload play
                        ? play.Body
                        : throw new ArgumentException("PlayBodyPayload required."))))
                .Step(Steps.Of("play.require-revision", [], [Revision], ctx =>
                    ctx.Set(Revision, _host.ActiveSnapshot?.Revision
                        ?? throw new InvalidOperationException("No content pack is active."))));

            if (route is PlayRoutes.Start or PlayRoutes.Load)
            {
                plan.OnFailure(OnPlayFailure);
            }

            if (route == PlayRoutes.Start)
            {
                StartPlan(plan);
            }
            else if (route == PlayRoutes.Load)
            {
                LoadPlan(plan);
            }
            else
            {
                plan.Step(Steps.Of("play." + route, [Body, Revision], [PlanKeys.Response], ctx =>
                    ctx.Set(PlanKeys.Response, OperationResponse.Json(call(ctx.Get(Revision), ctx.Get(Body))))));
            }

            plan.Done();
        }
    }

    private static void BodyPlan(PlanRegistry registry, string route, string resource, string stepId, Func<string, string> run) =>
        registry.Request(RequestNames.ForRoute(route))
            .Resource(resource)
            .Step(Steps.Of("play.require-body", [], [Body], ctx =>
                ctx.Set(Body, ctx.Get(PlanKeys.Payload) is PlayBodyPayload play
                    ? play.Body
                    : throw new ArgumentException("PlayBodyPayload required."))))
            .Step(Steps.Of(stepId, [Body], [PlanKeys.Response], ctx =>
                ctx.Set(PlanKeys.Response, OperationResponse.Json(run(ctx.Get(Body))))))
            .Done();

    /// <summary>背景音乐配置：<c>{op:"get"}</c> 看 mod 曲目表 + 玩家覆盖 + 可选曲目；<c>{op:"set",country,track}</c> 改覆盖。</summary>
    private void MusicPlan(PlanRegistry registry) =>
        registry.Request(RequestNames.ForRoute(PlayRoutes.Music))
            .Resource("music")
            .Step(Steps.Of("play.require-body", [], [Body], ctx =>
                ctx.Set(Body, ctx.Get(PlanKeys.Payload) is PlayBodyPayload play
                    ? play.Body
                    : throw new ArgumentException("PlayBodyPayload required."))))
            .Step(Steps.Of("audio.music-config", [Body], [PlanKeys.Response], ctx =>
                ctx.Set(PlanKeys.Response, OperationResponse.Json(AudioSteps.MusicConfig(_host, _play, ctx.Get(Body))))))
            .Done();

    // —— start / load：把 PlaySession 的阶段逐个暴露为 Step（计算类在前，落地类在后）。

    private void StartPlan(PlanBuilder plan)
    {
        plan.Step(ParseCommand(PlayCommandJson.Parse))
            .Step(Session("play.validate-local-selection", [Command], [], (s, c) => s.ValidateSelection(c.Get(Command))))
            .Step(Session("play.end-battle-profile", [], [], (s, _) => s.EndCurrentBattle()))
            .Step(Session("play.check-unlocked", [Command], [], (s, c) => s.CheckUnlocked(c.Get(Command))))
            .Step(Session("play.open-start", [Command], [Opened], (s, c) => c.Set(Opened, s.OpenStart(c.Get(Command)))))
            .Step(Session("play.install-session", [Command, Opened], [], (s, c) => s.InstallStarted(c.Get(Command), c.Get(Opened))))
            .Step(Session("play.autosave", [], [], (s, _) => s.SaveAuto()))
            .Step(RenderView())
            .Step(AudioSteps.PickBgm(_host, _play), optional: true)
            .Step(AudioSteps.Compose());
    }

    private void LoadPlan(PlanBuilder plan)
    {
        plan.Step(ParseCommand(PlayCommandJson.ParseOrEmpty))
            .Step(Session("play.end-battle-profile", [], [], (s, _) => s.EndCurrentBattle()))
            .Step(Session("play.read-slot", [Command], [Slot], (s, c) => c.Set(Slot, s.ReadSlotFor(c.Get(Command)))))
            .Step(Session("play.decode-slot", [Slot], [Opened], (s, c) => c.Set(Opened, s.DecodeSlot(c.Get(Slot)))))
            .Step(Session("play.install-session", [Slot, Opened], [], (s, c) => s.InstallLoaded(c.Get(Slot), c.Get(Opened))))
            .Step(Session("play.autosave", [], [], (s, _) => s.SaveAuto()))
            .Step(RenderView())
            .Step(AudioSteps.PickBgm(_host, _play), optional: true)
            .Step(AudioSteps.Compose());
    }

    private IStep ParseCommand(Func<string, PlayCommand> parse) =>
        Steps.Of("play.parse-command", [Body], [Command], ctx =>
            Guard(() => ctx.Set(Command, parse(ctx.Get(Body)))));

    private IStep RenderView() =>
        Session("play.render-view", [], [Envelope], (s, c) => c.Set(Envelope, s.ViewEnvelope()));

    /// <summary>在会话锁内运行的 Step；可预期失败转成游玩接口一贯的 <c>{ok:false}</c> 信封。</summary>
    private IStep Session(string id, PlanKey[] reads, PlanKey[] writes, Action<PlaySession, PlanContext> run) =>
        Steps.Of(id, [.. reads, Revision], writes, ctx =>
            Guard(() => _play.WithSession(ctx.Get(Revision), session =>
            {
                run(session, ctx);
                return 0;
            })));

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is RuleViolation or IOException or JsonException or InvalidOperationException
            or ArgumentException or KeyNotFoundException or OverflowException)
        {
            throw new PlayFailureException(error);
        }
    }

    /// <summary>start/load Plan 里的可预期失败；由 <see cref="OnPlayFailure"/> 转成失败信封（HTTP 200，与旧 route 一致）。</summary>
    private sealed class PlayFailureException(Exception inner) : Exception(inner.Message, inner)
    {
        public string Envelope => PlayJsonEnvelope.Failure(InnerException!);
    }

    private static OperationResponse? OnPlayFailure(Exception error) =>
        error is PlayFailureException failure ? OperationResponse.Json(failure.Envelope) : null;
}
