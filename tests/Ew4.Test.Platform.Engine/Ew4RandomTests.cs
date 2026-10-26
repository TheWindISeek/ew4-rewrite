using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class Ew4RandomTests
{
    [Fact]
    public void Same_seed_yields_same_sequence()
    {
        var a = new Ew4Random(42);
        var b = new Ew4Random(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.Next(1000), b.Next(1000));
        Assert.Equal(a.State, b.State);
    }

    [Fact]
    public void First_value_from_seed_zero_is_stable()
    {
        // 存档回放依赖该序列；改动算法会破坏旧存档，因此锁定首个值。
        Assert.Equal(38, new Ew4Random(0).Next(100));
    }

    [Fact]
    public void Next_stays_below_bound_and_zero_bound_does_not_advance_state()
    {
        var random = new Ew4Random(7);
        for (int i = 0; i < 500; i++) Assert.InRange(random.Next(6), 0, 5);

        ulong before = random.State;
        Assert.Equal(0, random.Next(0));
        Assert.Equal(before, random.State);
    }

    [Fact]
    public void Negative_bound_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ew4Random(1).Next(-1));
}
