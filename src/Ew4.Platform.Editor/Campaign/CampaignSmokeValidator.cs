using Ew4.Interface.Mod;
using Ew4.Platform.Content;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Editor.Campaign;

public static class CampaignSmokeValidator
{
    public static IReadOnlyList<Diagnostic> SmokeStart(
        ResolvedContent content,
        string campaignId,
        IEngine engine,
        IScenarioBuilder builder)
    {
        ScenarioIndex? scenario = ScenarioCatalog.List(content.Source)
            .FirstOrDefault(s => string.Equals(s.Id, campaignId, StringComparison.Ordinal));
        if (scenario == null)
        {
            return
            [
                new Diagnostic(
                    Severity.Error,
                    "campaign.missing",
                    "scenarios/" + campaignId + ".json",
                    "Campaign not found in resolved pack: " + campaignId),
            ];
        }

        int mode = ScenarioStartHelpers.ResolveMode(scenario.Id);
        if (!ScenarioStartHelpers.SupportsSmokeStart(mode))
        {
            return
            [
                new Diagnostic(
                    Severity.Warning,
                    "campaign.smoke_skip",
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
                    "campaign.smoke",
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
        return loader.ResolveView(primary, ModMerge.OpenParent(primary, repository)).AsResolvedContent();
    }
}
