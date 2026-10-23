using System.Text.Json;
using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ScenarioPackAccessTests
{
    [Fact]
    public void Embedded_json_detected_by_schema_2()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"schema":2,"id":"x","sections":{}}""");
        Assert.True(ScenarioPackAccess.IsEmbeddedScenarioJson(doc.RootElement));
    }

    [Fact]
    public void Stub_json_is_not_embedded()
    {
        using JsonDocument doc = JsonDocument.Parse(
            """{"schema":1,"id":"x","legacy":{"btl":"source/original/x.btl"}}""");
        Assert.False(ScenarioPackAccess.IsEmbeddedScenarioJson(doc.RootElement));
    }
}
