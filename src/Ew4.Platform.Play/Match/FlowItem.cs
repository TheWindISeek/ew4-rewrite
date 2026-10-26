namespace Ew4.Platform.Play.Match;

/// <summary>
/// 单局内需要玩家先处理的信息框种类。数值越小越先显示；新增种类必须在此登记优先级。
/// </summary>
public enum FlowKind
{
    /// <summary>终局，压过一切，不可解除。</summary>
    GameOver = 0,

    /// <summary>剧情弹窗，可带分支。</summary>
    ScenarioPopup = 10,

    /// <summary>普通对白。</summary>
    Dialogue = 20,
}

/// <summary>FlowItem 的唯一合法解除方式。</summary>
public enum FlowResolution
{
    /// <summary>不可解除（仅 <see cref="FlowKind.GameOver"/>）。</summary>
    None = 0,

    /// <summary>玩家点击确认。</summary>
    Ack = 1,

    /// <summary>玩家选择分支。</summary>
    Choice = 2,
}

/// <param name="Kind">信息框种类，决定组间优先级。</param>
/// <param name="Id">对白 key 或剧情弹窗 id；前端解除操作以它匹配。</param>
/// <param name="Group">所属事件组；同组内严格保持入队顺序。</param>
/// <param name="Resolution">解除方式。</param>
public sealed record FlowItem(FlowKind Kind, string Id, string Group, FlowResolution Resolution)
{
    public static FlowItem Dialogue(string group, string id) => new(FlowKind.Dialogue, id, group, FlowResolution.Ack);

    public static FlowItem ScenarioPopup(string group, string id) => new(FlowKind.ScenarioPopup, id, group, FlowResolution.Choice);

    public static FlowItem GameOver(string group = "game-over") => new(FlowKind.GameOver, group, group, FlowResolution.None);
}
