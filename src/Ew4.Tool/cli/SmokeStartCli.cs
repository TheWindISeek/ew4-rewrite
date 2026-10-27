using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Service;
using Ew4.Service.Host;

namespace Ew4.Tool.Cli;

internal static class SmokeStartCli
{
    internal static int Run(string[] args)
    {
        string packs = "mods";
        string scenario = "campaign1_01";
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length)
            {
                packs = args[++i];
            }
            else if (args[i] == "--scenario" && i + 1 < args.Length)
            {
                scenario = args[++i];
            }
        }

        string stateDir = Path.Combine(Path.GetTempPath(), "ew4-cli-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDir);
        var engine = new global::Ew4.Platform.Engine.Engine();
        var app = HostAppFactory.FromPaths(new HostPaths(Path.GetFullPath(packs), stateDir), engine);
        app.Activate("base");
        var game = new CliEngineGame(
            app.ActiveContent ?? throw new InvalidOperationException("No active content."),
            engine,
            new ScenarioJsonBuilder());
        CliGameView view = game.Start(new CliStartRequest(scenario));
        Console.WriteLine($"smoke-start ok: scenario={scenario} revision={view.Revision}");
        return 0;
    }
}
