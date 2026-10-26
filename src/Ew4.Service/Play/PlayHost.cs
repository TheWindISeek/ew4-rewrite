using Ew4.Interface.Play;
using Ew4.Platform.Engine;
using Ew4.Service.Host;

namespace Ew4.Service.Play;

/// <summary>游玩门面：注册表转发 + 按平面拆分的 JSON 桥。</summary>
public sealed class PlayHost : IPlayFacade, IPlayJsonInvoker, IHostActivationListener
{
    private readonly PlayCoordinator _coordinator;
    private readonly PlayRouteRegistry _registry;

    public PlayHost(IEngine engine)
    {
        _coordinator = new PlayCoordinator(engine);
        _registry = PlayRouteRegistry.CreateDefault();
        Json = this;
        Persistence = new RoutedInvoker(this, PlayPersistenceRoutes);
        Content = new RoutedInvoker(this, PlayContentRoutes);
        Engine = new EngineInvoker(this);
    }

    public IPlayJsonInvoker Json { get; }

    public IPlayPersistence Persistence { get; }

    public IPlayContentRpc Content { get; }

    public IPlayEngine Engine { get; }

    /// <summary>Plan 的 Step 在会话锁内访问 <see cref="PlaySession"/>；与整条 route 共用同一把锁。</summary>
    internal T WithSession<T>(string contentRevision, Func<PlaySession, T> action) =>
        _coordinator.With(contentRevision, action);

    /// <summary>当前激活内容对应的存档根目录（玩家本机数据所在）；未激活时为 null。</summary>
    public string? SaveRoot { get; private set; }

    /// <summary>玩家本机设置所在目录（<c>user/</c>）；未激活时为 null。</summary>
    public string? StateRoot { get; private set; }

    /// <summary>保存语言并重建游玩内容（局内会话被清掉，进度以自动存档为准）。</summary>
    public void SetLanguage(string language)
    {
        new Localization.UserSettings(StateRoot ?? throw new InvalidOperationException("No content pack is active."))
            .SetLanguage(language);
        _coordinator.Reset();
    }

    public void OnActivated(HostActivationInfo info)
    {
        SaveRoot = info.SaveRoot;
        StateRoot = info.StateRoot;
        _coordinator.OnActivated(info);
    }

    public void Reset() => _coordinator.Reset();

    public string InvokeEnvelope(string contentRevision, string route, string body)
    {
        _coordinator.EnsureSynchronized(contentRevision);
        return _registry.Invoke(_coordinator, route, body);
    }

    private static readonly HashSet<string> PlayPersistenceRoutes = new(StringComparer.Ordinal)
    {
        PlayRoutes.Save,
        PlayRoutes.Load,
        PlayRoutes.Saves,
    };

    private static readonly HashSet<string> PlayContentRoutes = new(StringComparer.Ordinal)
    {
        PlayRoutes.Catalog,
        PlayRoutes.CountryRegistry,
        PlayRoutes.Countries,
        PlayRoutes.LocalPlayers,
        PlayRoutes.Start,
        PlayRoutes.Leave,
        PlayRoutes.Headquarters,
        PlayRoutes.Deployment,
        PlayRoutes.UnitInfo,
        PlayRoutes.Commander,
        PlayRoutes.Commerce,
        PlayRoutes.Items,
        PlayRoutes.Exchange,
    };

    private sealed class RoutedInvoker : IPlayPersistence, IPlayContentRpc
    {
        private readonly PlayHost _host;
        private readonly HashSet<string> _allowed;

        public RoutedInvoker(PlayHost host, HashSet<string> allowed)
        {
            _host = host;
            _allowed = allowed;
        }

        public string InvokeEnvelope(string contentRevision, string route, string body)
        {
            if (!_allowed.Contains(route))
            {
                throw new ArgumentException("Route not allowed for this play plane: " + route, nameof(route));
            }

            return _host.InvokeEnvelope(contentRevision, route, body);
        }
    }

    private sealed class EngineInvoker : IPlayEngine
    {
        private readonly PlayHost _host;

        public EngineInvoker(PlayHost host) => _host = host;

        public string InvokeEnvelope(string contentRevision, string body, string? actionOp = null) =>
            _host.InvokeEnvelope(contentRevision, PlayRoutes.Action, body);
    }
}
