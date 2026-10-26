using Ew4.Interface.Operations;
using Ew4.Interface.Play;
using Xunit;

namespace Ew4.Test.Interface;

public sealed class PlayDescriptorsTests
{
    [Fact]
    public void Every_play_route_has_a_descriptor()
    {
        foreach (string route in PlayRoutes.All)
        {
            var descriptor = PlayDescriptors.Describe(route);
            Assert.Equal(route, descriptor.Route);
        }
    }

    [Fact]
    public void Play_action_describes_as_engine_route()
    {
        Assert.IsType<PlayEngineRouteOperation>(PlayDescriptors.Describe(PlayRoutes.Action, PlayActionOps.Move));
    }

    [Fact]
    public void Unknown_route_throws()
    {
        Assert.Throws<ArgumentException>(() => PlayDescriptors.Describe("nope"));
    }

    [Fact]
    public void Catalog_lists_all_play_action_ops()
    {
        int actionLeaves = OperationCatalog.All.Count(s =>
            s.Id.StartsWith("play.engine.action.", StringComparison.Ordinal));
        Assert.Equal(PlayActionOps.All.Length, actionLeaves);
    }
}
