using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// 已安装包管理（不含会话）。
/// </summary>
public interface IPackRepository
{
    IReadOnlyList<PackInfo> List();

    PackInfo Install(IPackSource source, InstallOptions options);

    void Remove(string packId);

    IPackSource Open(string packId);
}
