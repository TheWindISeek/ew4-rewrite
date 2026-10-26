using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class HexMapTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(-1, 4)]
    public void Constructor_rejects_non_positive_dimensions(int width, int height) =>
        Assert.Throws<RuleViolation>(() => new HexMap(width, height));

    [Fact]
    public void Index_maps_column_row_and_rejects_outside()
    {
        var map = new HexMap(4, 3);
        Assert.Equal(5, map.Index(1, 1));
        Assert.Equal(-1, map.Index(4, 0));
        Assert.Equal(-1, map.Index(0, 3));
        Assert.Equal(-1, map.Index(-1, 0));
    }

    [Fact]
    public void Neighbor_returns_minus_one_at_the_edge()
    {
        var map = new HexMap(4, 3);
        Assert.Equal(6, map.Neighbor(5, 0));
        Assert.Equal(-1, map.Neighbor(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Neighbor(0, 6));
        Assert.Throws<RuleViolation>(() => map.Neighbor(12, 0));
    }

    [Fact]
    public void Neighbor_is_always_at_distance_one()
    {
        var map = new HexMap(7, 6);
        for (int cell = 0; cell < map.Count; cell++)
            for (int direction = 0; direction < 6; direction++)
            {
                int next = map.Neighbor(cell, direction);
                if (next >= 0) Assert.Equal(1, map.Distance(cell, next));
            }
    }

    [Fact]
    public void Distance_is_symmetric_and_zero_for_same_cell()
    {
        var map = new HexMap(6, 6);
        Assert.Equal(0, map.Distance(7, 7));
        Assert.Equal(3, map.Distance(0, 3));
        Assert.Equal(2, new HexMap(4, 3).Distance(0, 8));
        for (int a = 0; a < map.Count; a++)
            for (int b = 0; b < map.Count; b++)
                Assert.Equal(map.Distance(a, b), map.Distance(b, a));
    }

    [Fact]
    public void Within_excludes_center_by_default_and_counts_six_neighbors()
    {
        var map = new HexMap(5, 5);
        int center = map.Index(2, 2);
        Assert.Equal(6, map.Within(center, 1).Count());
        Assert.Equal(7, map.Within(center, 1, includeCenter: true).Count());
        Assert.Empty(map.Within(center, 0));
        Assert.Throws<RuleViolation>(() => map.Within(center, -1).ToList());
    }
}
