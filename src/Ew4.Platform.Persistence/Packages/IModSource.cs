using System.Collections.Generic;
using Ew4.Interface.Mod;
using Ew4.Platform.Persistence;

namespace Ew4.Platform.Persistence.Packages;

/// <summary>
/// 目录或 zip 的统一读取。
/// </summary>
public interface IModSource : IModPackHandle
{
    ModManifest Manifest { get; }

    bool Exists(string path);

    byte[] Read(string path);

    IEnumerable<string> List(string prefix);
}
