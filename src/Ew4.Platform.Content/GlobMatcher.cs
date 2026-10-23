using System;
using System.Text.RegularExpressions;

namespace Ew4.Platform.Content;

internal static class GlobMatcher
{
    public static bool IsMatch(string pattern, string path)
    {
        string regex = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(path, regex, RegexOptions.CultureInvariant);
    }
}
