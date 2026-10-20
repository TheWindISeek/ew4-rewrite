using System.Text;

namespace Ew4.Mods;

internal static class LegacyXml
{
    // Some TinyXML-authored packs use a version="1" declaration or adjacent quoted attributes.
    // Keep valid bytes stable for source hashes; completed children before a trailing comment survive.
    internal static byte[] Normalize(byte[] bytes, out bool changed)
    {
        string source = ModFiles.Text(bytes);
        var output = new StringBuilder(source.Length);
        int position = 0;
        bool tag = false;
        int tagStart = 0;
        var open = new Stack<string>();
        char quote = '\0';
        while (position < source.Length)
        {
            if (!tag && source.AsSpan(position).StartsWith("<?xml", StringComparison.Ordinal)
                && position + 5 < source.Length && char.IsWhiteSpace(source[position + 5]))
            {
                int end = source.IndexOf("?>", position + 5, StringComparison.Ordinal);
                if (end < 0) throw new InvalidDataException("XML声明未结束");
                string declaration = source[position..(end + 2)];
                if (!System.Text.RegularExpressions.Regex.IsMatch(declaration, "\\bversion\\s*=\\s*(['\"])1\\1"))
                    output.Append(declaration);
                position = end + 2; continue;
            }
            if (!tag && (source.AsSpan(position).StartsWith("<!--", StringComparison.Ordinal)
                || source.AsSpan(position).StartsWith("<![CDATA[", StringComparison.Ordinal)))
            {
                string terminator = source[position + 2] == '-' ? "-->" : "]]>";
                int end = source.IndexOf(terminator, position + 4, StringComparison.Ordinal);
                if (end < 0)
                {
                    if (terminator == "-->" && open.Count == 1)
                    {
                        // TinyXML retains the completed children before an unfinished trailing comment.
                        output.Append("</").Append(open.Pop()).Append('>');
                        break;
                    }
                    throw new InvalidDataException("XML注释或CDATA未结束");
                }
                end += terminator.Length; output.Append(source, position, end - position); position = end; continue;
            }
            char current = source[position++]; output.Append(current);
            if (!tag) { if (current == '<') { tag = true; tagStart = position; } continue; }
            if (quote != '\0')
            {
                if (current != quote) continue;
                quote = '\0';
                if (position < source.Length && (char.IsLetter(source[position]) || source[position] is '_' or ':')) output.Append(' ');
            }
            else if (current is '\'' or '"') quote = current;
            else if (current == '>')
            {
                tag = false;
                string token = source[tagStart..(position - 1)].Trim();
                if (token.Length == 0 || token[0] is '?' or '!') continue;
                if (token[0] == '/')
                {
                    if (open.Count > 0 && open.Peek() == token[1..].Trim()) open.Pop();
                }
                else if (!token.EndsWith('/'))
                    open.Push(token.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0]);
            }
        }
        string normalized = output.ToString(); changed = normalized != source;
        return changed ? Encoding.UTF8.GetBytes(normalized) : bytes;
    }
}
