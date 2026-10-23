using System.Text;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ExtensionScriptPolicyTests
{
    [Fact]
    public void Api2_rejects_manifest_scripts_list()
    {
        string manifest = """
            {
              "schema": 1,
              "apiVersion": 2,
              "scripts": ["rewrite/scripts/abilities.lua"],
              "skills": ["rewrite/skills.json"]
            }
            """;
        string skills = """[{"id":"x.y","nameKey":"n","descriptionKey":"d","kind":"passive","target":"self","range":0,"cooldown":0,"cost":{"money":0,"industry":0,"food":0},"effects":[{"event":"turn.start","operation":"heal","value":1}]}]""";
        Assert.Throws<RuleViolation>(() => Load(manifest, skills));
    }

    [Fact]
    public void Api1_manifest_is_rejected()
    {
        string manifest = """
            {
              "schema": 1,
              "apiVersion": 1,
              "skills": ["rewrite/skills.json"]
            }
            """;
        string skills = """[{"id":"x.y","nameKey":"n","descriptionKey":"d","kind":"passive","target":"self","range":0,"cooldown":0,"cost":{"money":0,"industry":0,"food":0},"effects":[{"event":"turn.start","operation":"heal","value":1}]}]""";
        Assert.Throws<RuleViolation>(() => Load(manifest, skills));
    }

    private static ExtensionLoader Load(string manifest, string skillsJson)
    {
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest);
        byte[] skillsBytes = Encoding.UTF8.GetBytes(skillsJson);
        return ExtensionLoader.Load(path => path switch
        {
            "rewrite/extension.json" => manifestBytes,
            "rewrite/skills.json" => skillsBytes,
            "client/manifest.json" => """{"schema":1}"""u8.ToArray(),
            _ => throw new FileNotFoundException(path),
        });
    }
}
