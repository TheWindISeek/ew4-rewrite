using System.Linq;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Service;
using Ew4.Platform.Session.Persistence;

namespace Ew4.Service;

/// <summary>
/// 绑定当前激活 Pack 的 <see cref="ClientGame"/>，对外提供封存 <c>/api/*</c> 语义。
/// </summary>
public sealed class PlaySessionHost : IHostActivationListener
{
    private readonly object _gate = new();
    private readonly IEngine _engine;
    private ClientGame? _game;
    private HostActivationInfo? _activation;

    public PlaySessionHost(IEngine engine)
    {
        _engine = engine;
    }

    public void OnActivated(HostActivationInfo info)
    {
        lock (_gate)
        {
            _activation = info;
            _game = null;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _game = null;
        }
    }

    public string Dispatch(ResolvedContent content, string path, string body)
    {
        lock (_gate)
        {
            if (_activation == null || _activation.Content.Fingerprint != content.Fingerprint)
            {
                throw new InvalidOperationException("Play session is not synchronized with active content.");
            }

            _game ??= CreateGame(_activation);
            return _game.Dispatch(path, body);
        }
    }

    private ClientGame CreateGame(HostActivationInfo activation)
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
        var battleFiles = PackScenarioIds.List(activation.Content).Select(id => id + ".btl").ToArray();
        bool verifySources = string.IsNullOrWhiteSpace(activation.Content.Manifest.Parent);
        var clientContent = new ClientContent(read, battleFiles, verifySourceHashes: verifySources);
        var envelope = new SlotSaveEnvelope.Binding(
            activation.Content.Manifest.Id,
            activation.Content.Fingerprint,
            _engine.Info);
        return new ClientGame(clientContent, activation.SaveRoot, envelope);
    }
}
