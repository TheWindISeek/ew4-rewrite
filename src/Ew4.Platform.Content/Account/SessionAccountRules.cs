using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public sealed record TavernRules
{
    public WorldCommanderRules Assignment { get; init; } = new();
    public IReadOnlyDictionary<string, CommanderRuntime> Templates { get; init; } = new Dictionary<string, CommanderRuntime>();
    public bool FreeForAi { get; init; } = true;
}
public sealed record SessionTradingRules
{
    public ItemTradingRules Items { get; init; } = new();
    public IReadOnlyDictionary<int, int> TitleCommerce { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<string, int> CommanderCommerce { get; init; } = new Dictionary<string, int>();
}

