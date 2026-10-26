using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ew4.Platform.Persistence.Io;

/// <summary>Install / workspace 等路径上的唯一低级文件 API（其它程序集不得直接调用 <see cref="File"/> / <see cref="Directory"/>）。</summary>
public static class PhysicalFiles
{
    public static void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public static bool DirectoryExists(string path) => Directory.Exists(path);

    public static bool FileExists(string path) => File.Exists(path);

    public static byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public static string ReadAllText(string path) => File.ReadAllText(path);

    public static void WriteAllBytes(string path, byte[] data)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureDirectory(parent);
        }

        File.WriteAllBytes(path, data);
    }

    public static void WriteAllText(string path, string text)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureDirectory(parent);
        }

        File.WriteAllText(path, text);
    }

    public static void Move(string sourcePath, string destinationPath, bool overwrite = false) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    public static void DeleteDirectoryRecursive(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public static FileStream CreateWriteStream(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureDirectory(parent);
        }

        return File.Create(path);
    }

    public static IEnumerable<string> EnumerateDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            yield return directory;
        }
    }

    public static long MeasureDirectoryBytes(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
    }

    public static string GetFullPath(string path) => Path.GetFullPath(path);

    public static string Combine(params string[] segments) => Path.Combine(segments);
}
