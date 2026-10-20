using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ew4.Mods;

public sealed record ModImportIdentity(string Id, string Name, string FileName, string Source);

internal static class ModIdentityReader
{
    internal static ModImportIdentity Read(string path, string fileName)
    {
        fileName = fileName.Replace('\\', '/').Split('/').Last();
        string name = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Mod";
        name = new string(name.Where(c => !char.IsControl(c)).Take(80).ToArray());
        string slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9_.-]+", "-").Trim('-', '.', '_');
        string id = name.Any(c => c > 127) || slug.Length == 0 ? "mod-" + ModFiles.Hash(Encoding.UTF8.GetBytes(name))[..12] : slug[..Math.Min(64, slug.Length)].TrimEnd('.');
        try { ModFiles.Identifier(id); } catch (InvalidDataException) { id = "mod-" + ModFiles.Hash(Encoding.UTF8.GetBytes(name))[..12]; }
        var fallback = new ModImportIdentity(id, name, fileName, "filename");
        using var source = new ImportSource(path);
        if (source.Contains("mod.json"))
        {
            var manifest = JsonSerializer.Deserialize<ModManifest>(source.Read("mod.json"), ModFiles.Json) ?? throw new InvalidDataException("Mod清单无效");
            ModFiles.Identifier(manifest.Id);
            if (string.IsNullOrWhiteSpace(manifest.Name)) throw new InvalidDataException("Mod清单缺少名称");
            return fallback with { Id = manifest.Id, Name = manifest.Name, Source = "manifest" };
        }
        if (!source.Contains("AndroidManifest.xml")) return fallback;
        try
        {
            var bytes = source.Read("AndroidManifest.xml");
            if (bytes.Length > 4 * 1024 * 1024) return fallback;
            var (package, label) = AndroidIdentity(bytes);
            if (package == null) return fallback;
            package = package.ToLowerInvariant(); ModFiles.Identifier(package);
            return fallback with { Id = package, Name = string.IsNullOrWhiteSpace(label) || label.StartsWith('@') ? name : label, Source = "apk" };
        }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or ArgumentException or OverflowException or DecoderFallbackException)
        { return fallback; }
    }

    // Android binary XML stores names in a string pool and typed attributes in start-element chunks.
    internal static (string? Package, string? Label) AndroidIdentity(byte[] bytes)
    {
        if (bytes.Length < 8) throw new InvalidDataException("APK清单过短");
        int U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        int U32(int offset) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        if (U16(0) != 3)
        {
            var xml = ModFiles.Xml(bytes); var ns = System.Xml.Linq.XNamespace.Get("http://schemas.android.com/apk/res/android");
            return ((string?)xml.Root?.Attribute("package"), (string?)xml.Root?.Element("application")?.Attribute(ns + "label"));
        }
        int end = U32(4); if (end > bytes.Length || end < 8) throw new InvalidDataException("APK清单长度无效");
        string[] strings = []; string? package = null, label = null;
        string? S(int index) => index >= 0 && index < strings.Length ? strings[index] : null;
        int Index(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        for (int at = U16(2); at + 8 <= end;)
        {
            int type = U16(at), header = U16(at + 2), size = U32(at + 4);
            if (header < 8 || size < header || size > end - at) throw new InvalidDataException("APK清单块长度无效");
            int limit = at + size;
            if (type == 1)
            {
                if (header < 28) throw new InvalidDataException("APK字符串池无效");
                int count = U32(at + 8), flags = U32(at + 16), start = U32(at + 20);
                if (count > 100000 || (long)header + count * 4L > size || start > size) throw new InvalidDataException("APK字符串数量无效");
                strings = new string[count];
                for (int i = 0; i < count; i++)
                {
                    int pos = checked(at + start + U32(at + header + i * 4));
                    int Length(bool utf8)
                    {
                        int unit = utf8 ? 1 : 2;
                        if (pos > limit - unit) throw new InvalidDataException("APK字符串越界");
                        int value = utf8 ? bytes[pos] : U16(pos); pos += unit;
                        int mask = utf8 ? 0x80 : 0x8000;
                        if ((value & mask) == 0) return value;
                        if (pos > limit - unit) throw new InvalidDataException("APK字符串越界");
                        int next = utf8 ? bytes[pos] : U16(pos); pos += unit;
                        return checked(((value & (mask - 1)) << (utf8 ? 8 : 16)) | next);
                    }
                    bool utf8 = (flags & 0x100) != 0;
                    int length = Length(utf8); if (utf8) length = Length(true); else length = checked(length * 2);
                    if (length > limit - pos) throw new InvalidDataException("APK字符串越界");
                    strings[i] = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetString(bytes, pos, length);
                }
            }
            else if (type == 0x102)
            {
                int ext = at + header;
                if (ext > limit - 20) throw new InvalidDataException("APK元素无效");
                string? element = S(Index(ext + 4));
                int offset = U16(ext + 8), stride = U16(ext + 10), count = U16(ext + 12);
                if (stride < 20 || offset < 20 || (long)ext + offset + count * (long)stride > limit) throw new InvalidDataException("APK属性越界");
                for (int i = 0; i < count; i++)
                {
                    int attr = ext + offset + i * stride;
                    string? key = S(Index(attr + 4)), value = S(Index(attr + 8));
                    if (value == null && bytes[attr + 15] == 3) value = S(Index(attr + 16));
                    if (element == "manifest" && key == "package") package = value;
                    if (element == "application" && key == "label") label = value;
                }
            }
            at += size;
        }
        return (package, label);
    }
}
