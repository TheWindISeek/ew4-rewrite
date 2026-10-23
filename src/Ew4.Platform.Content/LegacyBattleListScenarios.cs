using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Ew4.Platform.Content;

/// <summary>
/// 从 <c>def_battlelist.xml</c> 补全 legacy BTL 剧本 id（overlay Mod 常只有部分 <c>scenarios/*.json</c> 索引）。
/// </summary>
public static class LegacyBattleListScenarios
{
    public static void AddIds(IPackSource source, ISet<string> ids)
    {
        if (!TryReadBattleList(source, out XDocument? document) || document?.Root == null)
        {
            return;
        }

        foreach (XElement battle in document.Root.Elements("battle"))
        {
            string? file = battle.Attribute("file")?.Value;
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            string id = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(id) || ids.Contains(id))
            {
                continue;
            }

            if (HasLegacyBtl(source, id))
            {
                ids.Add(id);
            }
        }
    }

    private static bool TryReadBattleList(IPackSource source, out XDocument? document)
    {
        foreach (string path in new[] { "assets/tables/def_battlelist.xml", "assets/original/def_battlelist.xml" })
        {
            if (!source.Exists(path))
            {
                continue;
            }

            try
            {
                document = ParseUtf8Xml(source.Read(path));
                return true;
            }
            catch (System.Xml.XmlException)
            {
            }
        }

        document = null;
        return false;
    }

    /// <summary>与 <see cref="ClientContent"/> 的 StreamReader(BOM) 行为一致，避免 UTF-8 BOM 导致根节点解析失败。</summary>
    internal static XDocument ParseUtf8Xml(byte[] utf8)
    {
        using var reader = new StreamReader(new MemoryStream(utf8), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return XDocument.Parse(reader.ReadToEnd());
    }

    private static bool HasLegacyBtl(IPackSource source, string scenarioId)
    {
        string file = scenarioId + ".btl";
        foreach (string path in new[]
                 {
                     "source/original/" + file,
                     "assets/original/" + file,
                     "original/" + file,
                 })
        {
            if (source.Exists(path))
            {
                return true;
            }
        }

        return false;
    }
}
