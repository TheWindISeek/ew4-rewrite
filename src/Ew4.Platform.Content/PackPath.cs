using System;

namespace Ew4.Platform.Content;

public static class PackPath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ContentError("pack.path", "Path is required.");
        }

        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ContentError("pack.path", "Path must not contain '..'.");
        }

        return normalized;
    }
}
