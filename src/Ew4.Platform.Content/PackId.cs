using System.Text.RegularExpressions;

namespace Ew4.Platform.Content;

public static partial class PackId
{
    private static readonly Regex Pattern = PackIdRegex();

    public const string Base = "base";

    public static string Validate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ContentError("pack.id", "Pack id is required.");
        }

        if (id.Length > 64)
        {
            throw new ContentError("pack.id", "Pack id must be at most 64 characters.");
        }

        if (!Pattern.IsMatch(id))
        {
            throw new ContentError("pack.id", "Pack id may only use lowercase letters, digits, '.', '-', and '_'.");
        }

        return id;
    }

    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex PackIdRegex();
}
