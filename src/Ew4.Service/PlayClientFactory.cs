using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Platform.Session.Persistence;

namespace Ew4.Service;

/// <summary>从当前激活 Mod 快照构建封存游玩用的 <see cref="ClientGame"/>。</summary>
public static class PlayClientFactory
{
    public static ClientGame Create(HostActivationInfo activation, IEngine engine)
    {
        Directory.CreateDirectory(activation.SaveRoot);
        IModSnapshot snapshot = activation.Snapshot;
        Func<string, byte[]> read = path =>
        {
            if (path.StartsWith("original/", StringComparison.Ordinal)
                && path.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
            {
                string scenarioId = Path.GetFileNameWithoutExtension(path["original/".Length..]);
                return ScenarioBtlResolver.ResolveBtlBytes(snapshot.Source, scenarioId);
            }

            if (!snapshot.TryReadRuntimeAsset(path, out byte[] bytes))
            {
                throw new FileNotFoundException("Pack asset not found: " + path, path);
            }

            return bytes;
        };
        string[] battleFiles = snapshot.ListScenarioIds().Select(id => id + ".btl").ToArray();
        bool verifySources = string.IsNullOrWhiteSpace(snapshot.Manifest.Parent);
        IReadOnlyDictionary<string, string> scenarioDisplayNames =
            ModScenarioCatalog.ReadOverlayDisplayNames(snapshot.Source);
        var clientContent = new ClientContent(read, battleFiles, scenarioDisplayNames, verifySourceHashes: verifySources);
        var envelope = new SlotSaveEnvelope.Binding(
            snapshot.Manifest.Id,
            snapshot.Revision,
            engine.Info);
        return new ClientGame(clientContent, activation.SaveRoot, envelope);
    }
}
