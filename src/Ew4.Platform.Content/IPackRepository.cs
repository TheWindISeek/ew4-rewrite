using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// 已安装包管理（不含会话）。
/// </summary>
public interface IModRepository
{
    IReadOnlyList<ModInfo> List();

    ModInfo Install(IModSource source, InstallOptions options);

    void Remove(string modId);

    IModSource Open(string modId);
}
