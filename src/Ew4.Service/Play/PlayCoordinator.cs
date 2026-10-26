using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Service.Host;

namespace Ew4.Service.Play;

/// <summary>绑定激活 Mod 与懒创建的 <see cref="PlaySession"/> 局内会话。</summary>
internal sealed class PlayCoordinator
{
    private readonly object _gate = new();
    private readonly IEngine _engine;
    private HostActivationInfo? _activation;
    private PlaySession? _session;

    public PlayCoordinator(IEngine engine) => _engine = engine;

    public void OnActivated(HostActivationInfo info)
    {
        lock (_gate)
        {
            _activation = info;
            _session = null;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _session = null;
        }
    }

    public void EnsureSynchronized(string contentRevision)
    {
        lock (_gate)
        {
            if (_activation == null || _activation.Snapshot.Revision != contentRevision)
            {
                throw new InvalidOperationException("Play session is not synchronized with active content.");
            }
        }
    }

    public T With<T>(string contentRevision, Func<PlaySession, T> action)
    {
        lock (_gate)
        {
            EnsureSynchronizedLocked(contentRevision);
            return action(EnsureSessionInstance());
        }
    }

    private void EnsureSynchronizedLocked(string contentRevision)
    {
        if (_activation == null || _activation.Snapshot.Revision != contentRevision)
        {
            throw new InvalidOperationException("Play session is not synchronized with active content.");
        }
    }

    public string WithSession(Func<PlaySession, string> action)
    {
        lock (_gate)
        {
            PlaySession session = EnsureSessionInstance();
            return action(session);
        }
    }

    private PlaySession EnsureSessionInstance()
    {
        if (_activation == null)
        {
            throw new InvalidOperationException("Play session is not synchronized with active content.");
        }

        _session ??= PlaySessionFactory.Create(_activation, _engine);
        return _session;
    }
}
