using Ew4.Platform.Persistence;
using System.Collections.Generic;

namespace Ew4.Platform.Persistence.Packages;

/// <summary>
/// 已安装包管理（不含会话）。
/// </summary>
public interface IModRepository
{
    IReadOnlyList<ModInfo> List();

    ModInfo Install(IModSource source, InstallOptions options);

    void Remove(string modId);

    IModSource Open(string modId);

    /// <summary>已安装 mod 目录占用字节（不含未安装 staging）。</summary>
    long MeasureInstalledBytes(string modId);
}
