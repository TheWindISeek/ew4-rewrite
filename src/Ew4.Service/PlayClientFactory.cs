using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Platform.Session.Persistence;

namespace Ew4.Service;

/// <summary>从当前激活 Pack 构建封存游玩用的 <see cref="ClientGame"/>。</summary>
public static class PlayClientFactory
{
    public static ClientGame Create(HostActivationInfo activation, IEngine engine)
    {
        Directory.CreateDirectory(activation.SaveRoot);
        IPackSource packSource = activation.Content.Source;
        Func<string, byte[]> baseRead = PackAssetReader.Open(packSource);
        Func<string, byte[]> read = path =>
        {
            if (path.StartsWith("original/", StringComparison.Ordinal)
                && path.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
            {
                string scenarioId = Path.GetFileNameWithoutExtension(path["original/".Length..]);
                return ScenarioBtlResolver.ResolveBtlBytes(packSource, scenarioId);
            }

            return baseRead(path);
        };
        string[] battleFiles = PackScenarioIds.List(activation.Content).Select(id => id + ".btl").ToArray();
        bool verifySources = string.IsNullOrWhiteSpace(activation.Content.Manifest.Parent);
        IReadOnlyDictionary<string, string> scenarioDisplayNames =
            PackScenarioCatalog.ReadOverlayDisplayNames(packSource);
        var clientContent = new ClientContent(read, battleFiles, scenarioDisplayNames, verifySourceHashes: verifySources);
        var envelope = new SlotSaveEnvelope.Binding(
            activation.Content.Manifest.Id,
            activation.Content.Fingerprint,
            engine.Info);
        return new ClientGame(clientContent, activation.SaveRoot, envelope);
    }
}
