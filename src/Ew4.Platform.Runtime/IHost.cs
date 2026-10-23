using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content;

namespace Ew4.Platform.Runtime;

public sealed record PackStatus(
    IReadOnlyList<PackInfo> Installed,
    string? ActivePackId,
    bool ImportInProgress);

public sealed record ImportToken(string TempDirectory, string ArchivePath, string PackId, string SuggestedName);

/// <summary>
/// 进程宿主：Pack 安装/激活，不含规则逻辑。
/// </summary>
public interface IHostApp
{
    PackStatus Status();

    void Activate(string? packId);

    ImportToken Prepare(Stream package, string fileName);

    void Import(ImportToken token, string id, string name);

    void Remove(string packId);
}
