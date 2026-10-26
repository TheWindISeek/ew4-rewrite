namespace Ew4.Interface.Host;

/// <summary>Backend 启动参数解析结果；交给 <c>AddEw4GameHost</c>，不含 Platform 类型。</summary>
public sealed record Ew4HostLaunchOptions(string RepositoryRoot, string InstallHome);
