namespace Ew4.Interface.Play;

/// <summary>游玩 API 路由名（不含 <c>/api/</c> 前缀），与封存前端 <c>rpc(route)</c> 一致。</summary>
public static class PlayRoutes
{
    public const string Catalog = "catalog";
    public const string CountryRegistry = "country-registry";
    public const string Countries = "countries";
    public const string LocalPlayers = "local-players";
    public const string Headquarters = "headquarters";
    public const string Deployment = "deployment";
    public const string UnitInfo = "unit-info";
    public const string Commander = "commander";
    public const string Commerce = "commerce";
    public const string Items = "items";
    public const string Exchange = "exchange";
    public const string Start = "start";
    public const string Leave = "leave";
    public const string Action = "action";
    public const string Save = "save";
    public const string Saves = "saves";
    public const string Load = "load";

    /// <summary>背景音乐配置（读/改玩家的按势力曲目）。不属于局内会话，故不在 <see cref="All"/> 中。</summary>
    public const string Music = "music";

    /// <summary>语言设置（读/切换）。同 <see cref="Music"/>，不在 <see cref="All"/> 中。</summary>
    public const string Language = "language";

    /// <summary>导出 APK 的前端接口（占位，尚未实现）。</summary>
    public const string ExportApk = "export-apk";

    public static readonly string[] All =
    [
        Catalog,
        CountryRegistry,
        Countries,
        LocalPlayers,
        Headquarters,
        Deployment,
        UnitInfo,
        Commander,
        Commerce,
        Items,
        Exchange,
        Start,
        Leave,
        Action,
        Save,
        Saves,
        Load,
    ];
}
