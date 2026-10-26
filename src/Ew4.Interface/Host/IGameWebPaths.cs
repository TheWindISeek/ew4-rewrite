namespace Ew4.Interface.Host;

/// <summary>Web 宿主静态前端根目录（由 Service 解析 install / 开发态 wwwroot）。</summary>
public interface IGameWebPaths
{
    string WwwRootDirectory { get; }
}
