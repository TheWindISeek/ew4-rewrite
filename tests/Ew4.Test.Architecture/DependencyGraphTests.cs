using System.Xml.Linq;
using Xunit;

namespace Ew4.Test.Architecture;

/// <summary>
/// 锁死程序集依赖图，对应 docs/arch/frozen/layering.md。
/// 改这里的白名单 = 改架构，只有维护者明确要求才能改。
/// </summary>
public sealed class DependencyGraphTests
{
    // 每个 src 项目允许直接引用的项目（短名，不含 Ew4. 前缀）。不在表里的引用一律失败。
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Front.Web"] = [],
        ["Interface"] = [],
        ["Platform.Engine"] = [],
        ["Platform.Ai"] = ["Platform.Engine"],
        ["Platform.Persistence"] = ["Interface", "Platform.Engine"],
        ["Platform.Mod"] = ["Interface", "Platform.Engine", "Platform.Persistence"],
        ["Platform.Play"] = ["Platform.Engine"],
        ["Platform.Content"] = ["Interface", "Platform.Ai", "Platform.Engine", "Platform.Mod", "Platform.Persistence"],
        ["Platform.Editor"] = ["Interface", "Platform.Content", "Platform.Engine", "Platform.Mod", "Platform.Persistence"],
        ["Service"] = ["Interface", "Platform.Ai", "Platform.Content", "Platform.Editor", "Platform.Engine", "Platform.Mod", "Platform.Persistence", "Platform.Play"],
        ["Backend.Web"] = ["Front.Web", "Interface", "Service"],
        ["Tool"] = ["Platform.Content", "Platform.Editor", "Platform.Engine", "Platform.Persistence", "Service"],
    };

    [Fact]
    public void ProjectReferencesMatchTheFrozenGraph()
    {
        string src = Path.Combine(RepoRoot(), "src");
        var violations = new List<string>();
        foreach (string project in Directory.GetDirectories(src, "Ew4.*"))
        {
            string name = Path.GetFileName(project)["Ew4.".Length..];
            if (name == "Android")
            {
                continue; // Java 壳，无 csproj
            }

            Assert.True(Allowed.ContainsKey(name), $"新项目 {name} 未登记到架构白名单（需维护者批准）");
            string csproj = Path.Combine(project, "Ew4." + name + ".csproj");
            foreach (string reference in References(csproj))
            {
                if (!Allowed[name].Contains(reference))
                {
                    violations.Add($"{name} → {reference}");
                }
            }
        }

        Assert.True(violations.Count == 0, "违反依赖图：" + string.Join("; ", violations));
    }

    [Fact]
    public void EngineAndInterfaceHaveNoPackageReferences()
    {
        foreach (string name in new[] { "Platform.Engine", "Interface" })
        {
            string csproj = Path.Combine(RepoRoot(), "src", "Ew4." + name, "Ew4." + name + ".csproj");
            Assert.Empty(XDocument.Load(csproj).Descendants("PackageReference"));
        }
    }

    [Fact]
    public void EngineStaysNetStandard()
    {
        string csproj = Path.Combine(RepoRoot(), "src", "Ew4.Platform.Engine", "Ew4.Platform.Engine.csproj");
        Assert.Equal("netstandard2.1", XDocument.Load(csproj).Descendants("TargetFramework").Single().Value);
    }

    private static IEnumerable<string> References(string csproj) =>
        XDocument.Load(csproj).Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(((string)e.Attribute("Include")!).Replace('\\', '/').Split('/').Last())["Ew4.".Length..]);

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ew4.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("找不到 Ew4.sln");
    }
}
