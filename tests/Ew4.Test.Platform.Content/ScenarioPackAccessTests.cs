using System.Text.Json;
using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ScenarioModAccessTests
{
    [Fact]
    public void Schema_3_is_scenario_json()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"schema":3,"id":"x"}""");
        Assert.True(ScenarioModAccess.IsScenarioJson(doc.RootElement));
    }

    [Theory]
    [InlineData("""{"schema":1,"id":"x","legacy":{"btl":"source/original/x.btl"}}""")]
    [InlineData("""{"schema":2,"id":"x","sections":{}}""")]
    public void Older_schemas_are_not_scenario_json(string text)
    {
        using JsonDocument doc = JsonDocument.Parse(text);
        Assert.False(ScenarioModAccess.IsScenarioJson(doc.RootElement));
    }
}
