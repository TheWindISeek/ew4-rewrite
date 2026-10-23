using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// 目录或 zip 的统一读取。
/// </summary>
public interface IPackSource
{
    PackManifest Manifest { get; }

    bool Exists(string path);

    byte[] Read(string path);

    IEnumerable<string> List(string prefix);
}
