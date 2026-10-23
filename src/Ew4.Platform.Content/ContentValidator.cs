using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public sealed class ContentValidator : IContentValidator
{
    private readonly IContentKindRegistry _registry;
    private readonly IEngine? _engine;
    private readonly IScenarioBuilder? _builder;

    public ContentValidator(IContentKindRegistry registry)
    {
        _registry = registry;
    }

    public ContentValidator(IContentKindRegistry registry, IEngine engine, IScenarioBuilder builder)
    {
        _registry = registry;
        _engine = engine;
        _builder = builder;
    }

    public IReadOnlyList<Diagnostic> Run(ResolvedContent content, ValidationLevel upTo)
    {
        var diagnostics = new List<Diagnostic>();
        if (upTo < ValidationLevel.Schema)
        {
            return diagnostics;
        }

        foreach (string path in content.Source.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (string.Equals(path, "pack.json", StringComparison.Ordinal)
                || string.Equals(path, PackModProfile.FilePath, StringComparison.Ordinal)
                || string.Equals(path, ".workspace.json", StringComparison.Ordinal))
            {
                continue;
            }

            if (path.Contains(".ew4scenario.", StringComparison.Ordinal))
            {
                continue;
            }

            IContentKind? kind = _registry.Match(path);
            if (kind == null)
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error,
                    "content.unknown-path",
                    path,
                    "No content kind is registered for this path."));
                continue;
            }

            byte[] bytes = content.Source.Read(path);
            if (bytes.Length > kind.Limits.MaxBytesPerFile)
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error,
                    "content.size",
                    path,
                    $"File exceeds limit of {kind.Limits.MaxBytesPerFile} bytes."));
                continue;
            }

            if (!content.Manifest.Formats.TryGetValue(kind.Name, out int schema))
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error,
                    "content.format",
                    path,
                    $"pack.json formats.{kind.Name} is required."));
                continue;
            }

            try
            {
                object model = kind.Load(bytes, schema);
                foreach (Diagnostic diagnostic in kind.Validate(model, new ValidationContext(content)))
                {
                    diagnostics.Add(diagnostic with { Path = string.IsNullOrEmpty(diagnostic.Path) ? path : diagnostic.Path });
                }
            }
            catch (ContentError ex)
            {
                diagnostics.Add(new Diagnostic(Severity.Error, ex.Code, path, ex.Message));
            }
        }

        if (upTo >= ValidationLevel.Reference)
        {
            diagnostics.AddRange(ValidateScenarioReferences(content));
        }

        if (upTo >= ValidationLevel.SmokeStart)
        {
            diagnostics.AddRange(ValidateSmokeStart(content));
        }

        return diagnostics;
    }

    private static IEnumerable<Diagnostic> ValidateScenarioReferences(ResolvedContent content)
    {
        foreach (ScenarioIndex scenario in ScenarioCatalog.List(content.Source))
        {
            if (scenario.IsEmbeddedDocument)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scenario.OriginalBtlPath))
            {
                yield return new Diagnostic(
                    Severity.Error,
                    "scenario.legacy",
                    scenario.JsonPath,
                    "Scenario '" + scenario.Id + "' is missing legacy.btl or embedded document.");
                continue;
            }

            if (!content.Source.Exists(scenario.OriginalBtlPath))
            {
                bool optional = scenario.Id.StartsWith("multiplay", StringComparison.Ordinal);
                yield return new Diagnostic(
                    optional ? Severity.Warning : Severity.Error,
                    "scenario.btl",
                    scenario.JsonPath,
                    optional
                        ? "Multiplayer scenario BTL is not bundled in base: " + scenario.OriginalBtlPath
                        : "BTL not found: " + scenario.OriginalBtlPath);
            }
        }
    }

    private IEnumerable<Diagnostic> ValidateSmokeStart(ResolvedContent content)
    {
        if (_engine == null || _builder == null)
        {
            return
            [
                new Diagnostic(
                    Severity.Error,
                    "content.smoke",
                    "pack",
                    "SmokeStart validation requires IEngine and IScenarioBuilder."),
            ];
        }

        var diagnostics = new List<Diagnostic>();
        foreach (ScenarioIndex scenario in ScenarioCatalog.List(content.Source))
        {
            int mode = ScenarioStartHelpers.ResolveMode(scenario.Id);
            if (!ScenarioStartHelpers.SupportsSmokeStart(mode))
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Info,
                    "scenario.smoke_skip",
                    scenario.JsonPath,
                    "Skipped smoke start for multiplayer-only scenario '" + scenario.Id + "'."));
                continue;
            }

            try
            {
                BuiltScenario built = _builder.Build(content, scenario.Id, new ScenarioSelection(scenario.Id));
                PlayerAccount account = ScenarioStartHelpers.SmokeAccount(built.InitialWorld.ContentRevision);
                GameSession session = _engine.NewGame(built.InitialWorld, built.Rules, account, new NewGameOptions());
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
            }
            catch (ContentError ex)
            {
                diagnostics.Add(new Diagnostic(Severity.Error, ex.Code, scenario.JsonPath, ex.Message));
            }
            catch (RuleViolation ex)
            {
                diagnostics.Add(new Diagnostic(Severity.Error, ex.Code, scenario.JsonPath, ex.Message));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error,
                    "scenario.smoke",
                    scenario.JsonPath,
                    "Smoke start failed for '" + scenario.Id + "': " + ex.Message));
            }
        }

        return diagnostics;
    }
}
