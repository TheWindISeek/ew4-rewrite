using System.IO;
using System.IO.Compression;

namespace Ew4.Platform.Content;

public sealed class ZipModExporter : IModExporter
{
    public void Export(IModSource pack, Stream destination)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (string path in pack.List(""))
        {
            var entry = zip.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
            using Stream entryStream = entry.Open();
            byte[] bytes = pack.Read(path);
            entryStream.Write(bytes, 0, bytes.Length);
        }
    }
}
