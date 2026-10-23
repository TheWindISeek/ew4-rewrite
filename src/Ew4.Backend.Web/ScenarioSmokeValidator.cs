using Ew4.Platform.Content;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;

namespace Ew4.Backend.Web;

internal static class ScenarioSmokeValidator
{
    public static IReadOnlyList<Diagnostic> SmokeStart(
        ResolvedContent content,
        string scenarioId,
        IEngine engine,
        IScenarioBuilder builder)
    {
        ScenarioIndex? scenario = ScenarioCatalog.List(content.Source)
            .FirstOrDefault(s => string.Equals(s.Id, scenarioId, StringComparison.Ordinal));
        if (scenario == null)
        {
            return
            [
                new Diagnostic(
                    Severity.Error,
                    "scenario.missing",
                    "scenarios/" + scenarioId + ".json",
                    "Scenario not found in resolved pack: " + scenarioId),
            ];
        }

        int mode = ScenarioStartHelpers.ResolveMode(scenario.Id);
        if (!ScenarioStartHelpers.SupportsSmokeStart(mode))
        {
            return
            [
                new Diagnostic(
                    Severity.Warning,
                    "scenario.smoke_skip",
                    scenario.JsonPath,
                    "Smoke start not supported for scenario mode " + mode),
            ];
        }

        try
        {
            BuiltScenario built = builder.Build(content, scenario.Id, new ScenarioSelection(scenario.Id));
            PlayerAccount account = ScenarioStartHelpers.SmokeAccount(built.InitialWorld.ContentRevision);
            GameSession session = engine.NewGame(built.InitialWorld, built.Rules, account, new NewGameOptions());
            if (built.Session != null)
            {
                session = session with
                {
                    ContentRevision = built.Session.SessionContentRevision ?? session.ContentRevision,
                    RosterDeployments = built.Session.RosterDeployments,
                    Markets = built.Session.Markets,
                    TavernCandidates = built.Session.TavernCandidates,
                };
                session.Validate();
            }

            return [];
        }
        catch (ContentError ex)
        {
            return [new Diagnostic(Severity.Error, ex.Code, scenario.JsonPath, ex.Message)];
        }
        catch (RuleViolation ex)
        {
            return [new Diagnostic(Severity.Error, ex.Code, scenario.JsonPath, ex.Message)];
        }
        catch (Exception ex)
        {
            return
            [
                new Diagnostic(
                    Severity.Error,
                    "scenario.smoke",
                    scenario.JsonPath,
                    "Smoke start failed: " + ex.Message),
            ];
        }
    }

    public static ResolvedContent ResolveWorkspace(
        IModSnapshotLoader loader,
        FileModRepository repository,
        string workspaceRoot)
    {
        var primary = new DirectoryModSource(Path.GetFullPath(workspaceRoot));
        return loader.ResolveView(primary, ModMerge.OpenParent(primary, repository));
    }
}
