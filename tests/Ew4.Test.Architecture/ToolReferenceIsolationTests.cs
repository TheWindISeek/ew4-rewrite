using System.Xml.Linq;
using Xunit;

namespace Ew4.Test.Architecture;

/// <summary>
/// <c>Ew4.Tool</c> 为离线 CLI 叶子：src 内其它程序集不得引用 Tool；测试仅 <c>Ew4.Test.Tool</c> 可引用。
/// </summary>
public sealed class ToolReferenceIsolationTests
{
    [Fact]
    public void SourceProjectsDoNotReferenceTool()
    {
        string src = Path.Combine(RepoRoot(), "src");
        var violations = new List<string>();
        foreach (string projectDir in Directory.GetDirectories(src, "Ew4.*"))
        {
            string name = Path.GetFileName(projectDir)["Ew4.".Length..];
            if (name == "Tool" || name == "Android")
            {
                continue;
            }

            string csproj = Path.Combine(projectDir, "Ew4." + name + ".csproj");
            if (ReferencesTool(csproj))
            {
                violations.Add(name);
            }
        }

        Assert.True(violations.Count == 0, "src 项目不得引用 Ew4.Tool：" + string.Join(", ", violations));
    }

    [Fact]
    public void OnlyEw4TestToolMayReferenceToolAmongTests()
    {
        string tests = Path.Combine(RepoRoot(), "tests");
        var violations = new List<string>();
        foreach (string projectDir in Directory.GetDirectories(tests, "Ew4.*"))
        {
            string name = Path.GetFileName(projectDir)["Ew4.".Length..];
            string csproj = Path.Combine(projectDir, "Ew4." + name + ".csproj");
            if (name != "Test.Tool" && ReferencesTool(csproj))
            {
                violations.Add(name);
            }
        }

        Assert.True(violations.Count == 0, "仅 Ew4.Test.Tool 可引用 Tool，违规：" + string.Join(", ", violations));
    }

    private static bool ReferencesTool(string csproj) =>
        XDocument.Load(csproj).Descendants("ProjectReference")
            .Any(e => ((string)e.Attribute("Include")!).Replace('\\', '/').Contains("Ew4.Tool", StringComparison.Ordinal));

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ew4.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        return dir ?? throw new InvalidOperationException("Ew4.sln not found");
    }
}
