using Ew4.Platform.Persistence;
using System;

namespace Ew4.Platform.Persistence.Packages;

public static class ModPath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new PersistenceError("mod.path", "Path is required.");
        }

        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new PersistenceError("mod.path", "Path must not contain '..'.");
        }

        return normalized;
    }
}
