using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;

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

            _game ??= PlayClientFactory.Create(_activation, _engine);
            return _game.Dispatch(path, body);
        }
    }
}
