using Ew4.Interface.Mod;

namespace Ew4.Platform.Mod.Activation;

/// <summary>已安装 mod 的激活与安装；由 <c>HostApp</c> 实现，供 Mod 层 catalog/import 编排。</summary>
public interface IModHost
{
    IModSnapshot? ActiveSnapshot { get; }

    void Activate(string? modId);

    void Remove(string modId);

    void InstallFromArchive(string archivePath, string modId);
}
