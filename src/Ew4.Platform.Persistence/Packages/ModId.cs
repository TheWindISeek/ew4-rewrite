using Ew4.Platform.Persistence;
using System.Text.RegularExpressions;

namespace Ew4.Platform.Persistence.Packages;

public static partial class ModId
{
    private static readonly Regex Pattern = ModIdRegex();

    public const string Base = "base";

    public static string Validate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new PersistenceError("mod.id", "Pack id is required.");
        }

        if (id.Length > 64)
        {
            throw new PersistenceError("mod.id", "Pack id must be at most 64 characters.");
        }

        if (!Pattern.IsMatch(id))
        {
            throw new PersistenceError("mod.id", "Pack id may only use lowercase letters, digits, '.', '-', and '_'.");
        }

        return id;
    }

    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex ModIdRegex();
}
