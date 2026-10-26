using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Service.Host;
using Ew4.Service.Localization;
using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Persistence.Serialization;

namespace Ew4.Service.Play;

/// <summary>从当前激活 Mod 快照构建游玩会话。</summary>
public static class PlaySessionFactory
{
    public static PlaySession Create(HostActivationInfo activation, IEngine engine, string? language = null)
    {
        PhysicalFiles.EnsureDirectory(activation.SaveRoot);
        ResolvedContent snapshot = activation.Content;
        Func<string, byte[]> read = path =>
        {
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
        var playContent = new PlayContent(read, battleFiles, id => ScenarioModAccess.OpenBattle(snapshot.Source, id),
            scenarioDisplayNames, verifySourceHashes: verifySources,
            localized: LocalizedStrings.Load(path => snapshot.TryReadRuntimeAsset(path, out byte[] bytes) ? bytes : null),
            language: language ?? new UserSettings(activation.StateRoot).Language);
        var envelope = new SlotSaveEnvelope.Binding(
            snapshot.Manifest.Id,
            snapshot.Revision,
            engine.Info);
        return new PlaySession(playContent, activation.SaveRoot, engine, envelope);
    }
}
