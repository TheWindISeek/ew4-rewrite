// 操作模型契约（单文件入口，详见 docs/arch/operations.md、interface-contracts.md）
//
// 数据流：
//   Backend  — 只根据 URL/路由组装 OperationDescriptor（不知 Engine/Editor/Persistence）
//   Service  — 静态资源直接出字节；其余请求交给 PlanRunner，按注册表把 Request 展开成一串 Step（见 docs/arch/operation-plans.md）
//
// 类型分层：
//   OperationChannel          用户通道（Play / Editor / Platform / Static）
//   OperationDescriptor       请求身份（record 继承，与 HTTP 解耦）
//   OperationCatalog          前端终端能力注册表（ParentId 成树；供 Backend 注册表与测试）
//
// 路由字符串仍用 PlayRoutes、EditorRoutes、PlayApiRoutes；本文件描述「执行语义」。

using Ew4.Interface.Play;

namespace Ew4.Interface.Operations;

#region 通道

/// <summary>用户可见的操作通道。Backend 从 URL 前缀判定，例如 Play 对应 <c>/api/{route}</c>（目标 <c>/api/play/*</c>）。</summary>
public enum OperationChannel
{
    Play,       // POST /api/play/{route}（兼容 /api/{route}）— 选局、大本营、地图回合、槽位
    Editor,     // /api/editor/* — workspace、战役/将领编辑、预览绑定
    Platform,   // /api/mods、mod-prepare、mod-import — 安装目录内 mod 管理
    Static,     // GET /content/* 等 — Backend 经 Invoke 交给 Service 出字节
}

/// <summary>Editor HTTP 终端种类；与 <see cref="EditorRoutes"/> 及 Backend 路径模板对应。</summary>
public enum EditorOperationKind
{
    WorkspaceList,              // GET  /workspaces — 列出全部创作 workspace
    WorkspaceCreate,            // POST /workspaces — 从 parent mod 新建 workspace
    WorkspaceDelete,            // DELETE /workspaces/{id} — 删除 workspace 目录
    WorkspaceValidate,          // POST /workspaces/{id}/validate — 按级别 Smoke 校验合并包
    WorkspaceExport,            // POST /workspaces/{id}/export — 校验通过后打 zip 到 exports
    WorkspaceCampaignList,      // GET  /workspaces/{id}/campaigns — 按模式分组列出战役 id
    WorkspaceFileRead,          // GET  /workspaces/{id}/files/{path} — 读 overlay 二进制
    WorkspaceFileWrite,         // PUT  /workspaces/{id}/files/{path} — 写 overlay 文件
    WorkspaceCapabilities,      // GET  /workspaces/{id}/capabilities — 编辑模式与能力集
    MapList,                    // GET  /workspaces/{id}/maps — base 里可选的地图
    MapDescribe,                // GET  /workspaces/{id}/maps/{mapId} — 尺寸与 hash
    MapPreview,                 // GET  /workspaces/{id}/maps/{mapId}/preview?x&y&w&h — 裁剪区域地形预览
    CampaignCreate,             // POST /workspaces/{id}/campaign — 自由编辑：新建空场景
    CampaignGet,                // GET  …/campaign/{id} — 打开战役文档（BTL/JSON）
    CampaignPut,                // PUT  …/campaign/{id} — 整包保存战役字节（可选 smoke）
    CampaignEdit,               // POST …/campaign/{id}/edit — 结构化 patch 编辑并落盘
    CampaignRoundtrip,          // POST …/campaign/{id}/roundtrip — BTL 编解码往返报告
    CommanderList,              // GET  …/commanders — 将领表列表
    CommanderSkills,            // GET  …/commanders/skills — 可选技能目录
    CommanderGet,               // GET  …/commanders/{legacyId} — 单将领条目
    CommanderEdit,              // POST …/commanders/{legacyId}/edit — 改技能/兵种等并写表
    PreviewEnter,               // POST …/preview/enter — 用 workspace 合并结果进入游玩预览绑定
    PreviewRefresh,             // POST …/preview/refresh — 预览中刷新合并快照
    PreviewExit,                // POST /preview/exit — 退出预览，恢复已安装 mod 绑定
    PreviewStatus,              // GET  /preview — 当前是否在预览、绑定信息
    BtlToJson,                  // POST /campaign/btl-to-json — 导入：上传二进制剧本，经 Mod 转换器得到场景 JSON
}

/// <summary>Platform 通道终端种类（mods / mod-prepare / mod-import）。</summary>
public enum PlatformModOperationKind
{
    ListMods,       // GET/POST /api/mods（无 body 或空）— 返回已安装 mod 面板状态列表
    MutateMods,     // POST /api/mods + JSON：op=activate|delete|import|discard — 切换激活/删包/提交或放弃暂存导入
    PrepareImport,  // POST /api/mod-prepare（multipart file）— 暂存上传包，返回 token + 识别信息
    CommitImport,   // POST /api/mod-import（form token/id/name）— 将暂存包写入 mods 目录并刷新面板
}

#endregion

#region 请求身份（Descriptor）

/// <summary>与 HTTP 解耦的操作身份；由 Backend 从 path + route 组装后交给 Service。</summary>
/// <param name="Channel">所属用户通道。</param>
public abstract record OperationDescriptor(OperationChannel Channel);

/// <summary>游玩 RPC：<c>POST /api/{route}</c>。</summary>
/// <param name="Route"><see cref="PlayRoutes"/> 中的 route 名。</param>
public abstract record PlayOperationDescriptor(string Route) : OperationDescriptor(OperationChannel.Play);

/// <summary>分类为 Content 平面的 play route（选局 / HQ / 商店等）。</summary>
public sealed record PlayContentRouteOperation(string Route) : PlayOperationDescriptor(Route);

/// <summary>分类为 Persistence 平面的 play route（save / load / saves）。</summary>
public sealed record PlayPersistenceRouteOperation(string Route) : PlayOperationDescriptor(Route);

/// <summary>分类为 Plan 的 play route（局内 action）；<paramref name="ActionOp"/> 为 <see cref="PlayActionOps"/> 子命令。</summary>
public sealed record PlayEngineRouteOperation(string? ActionOp = null) : PlayOperationDescriptor(PlayRoutes.Action);

/// <summary>Editor 通道；<paramref name="Kind"/> 对应具体 HTTP 终端。</summary>
public sealed record EditorOperationDescriptor(EditorOperationKind Kind) : OperationDescriptor(OperationChannel.Editor);

/// <summary>Platform 通道；mod 列表/变更/导入流水线。</summary>
public sealed record PlatformModOperationDescriptor(PlatformModOperationKind Kind)
    : OperationDescriptor(OperationChannel.Platform);

/// <summary>Static 通道；<paramref name="RequestPath"/> 为完整 HTTP 路径（如 <c>/content/…</c>、<c>/conquest-pages.json</c>）。</summary>
public sealed record StaticHttpAssetDescriptor(string RequestPath)
    : OperationDescriptor(OperationChannel.Static);

#endregion

#region Play action 子 op

/// <summary>
/// <see cref="PlayRoutes.Action"/> 下的子 op，与前端 <c>rpc('action', { op: … })</c> 一致。
/// 在注册表里统一为 Request play.action。
/// </summary>
public static class PlayActionOps
{
    public const string State = "state";               // 拉取完整局内视图（刷新 HUD）
    public const string Select = "select";             // 选中格子/单位，返回可达与菜单
    public const string Move = "move";                 // 单位移动（记录路径供 undo）
    public const string Attack = "attack";             // 攻击，返回战斗预览 JSON
    public const string Skill = "skill";               // 释放技能（单位或格子目标）
    public const string End = "end";                   // 结束当前方回合
    public const string Undo = "undo";                 // 撤销本回合移动
    public const string Deploy = "deploy";             // 从大本营部署单位到地图
    public const string Recruit = "recruit";           // 在格子上招募部队
    public const string Installation = "installation"; // 建造阵地/设施
    public const string Disband = "disband";           // 解散单位
    public const string Train = "train";               // 训练单位
    public const string Upgrade = "upgrade";           // 升级建筑
    public const string Transport = "transport";       // 购买运输船
    public const string Hire = "hire";                 // 酒馆招募将领到单位
    public const string UseItem = "use_item";          // 对单位使用道具
    public const string EventChoice = "event_choice";  // 剧情事件分支选择
    public const string Dialogue = "dialogue";         // 确认对话，推进剧本
    public const string Ai = "ai";                     // 执行单方 AI 一步
    public const string AiBatch = "ai_batch";          // 连续执行 AI 直到玩家回合或阻塞

    public static readonly string[] All =
    [
        State,
        Select,
        Move,
        Attack,
        Skill,
        End,
        Undo,
        Deploy,
        Recruit,
        Installation,
        Disband,
        Train,
        Upgrade,
        Transport,
        Hire,
        UseItem,
        EventChoice,
        Dialogue,
        Ai,
        AiBatch,
    ];
}

#endregion

#region 终端注册表

/// <summary>
/// <see cref="OperationCatalog"/> 中的一条记录。
/// <see cref="ParentId"/> 为 null 表示根下分类节点；叶子节点为可注册的终端能力。
/// </summary>
public sealed record OperationTerminalSpec(
    string Id,                           // 树节点唯一 id（如 play.meta.start）
    string? ParentId,                    // 父分类 id；null 表示通道根
    OperationChannel Channel,            // 所属通道
    string? RouteOrKind = null);         // Play 为 route 名；Editor/Platform 为 Kind 名

/// <summary>
/// 前端终端能力树（与 docs/arch/operations.md 一致）。
/// Backend 目标：用此表驱动路由注册，而非分散的多个 Endpoints 文件。
/// </summary>
public static class OperationCatalog
{
    public static IReadOnlyList<OperationTerminalSpec> All { get; } = Build();

    private static IReadOnlyList<OperationTerminalSpec> Build()
    {
        var list = new List<OperationTerminalSpec>();
        AddCategories(list);
        AddPlayMeta(list);
        AddPlayMenu(list);
        AddPlayEngine(list);
        AddPlayPersistence(list);
        AddEditor(list);
        AddPlatform(list);
        return list;
    }

    private static void AddCategories(List<OperationTerminalSpec> list)
    {
        list.Add(new("root.play", null, OperationChannel.Play));       // 游玩通道根
        list.Add(new("root.editor", null, OperationChannel.Editor));   // 创作通道根
        list.Add(new("root.platform", null, OperationChannel.Platform)); // mod 管理根
        list.Add(new("play.meta", "root.play", OperationChannel.Play)); // 选局/开局/离局
        list.Add(new("play.menu", "root.play", OperationChannel.Play));  // 大本营与商店 RPC
        list.Add(new("play.engine", "root.play", OperationChannel.Play)); // 地图 action/*
        list.Add(new("play.persistence", "root.play", OperationChannel.Play)); // 槽位
        list.Add(new("editor.workspace", "root.editor", OperationChannel.Editor)); // workspace 文件与校验
        list.Add(new("editor.campaign", "root.editor", OperationChannel.Editor));
        list.Add(new("editor.commander", "root.editor", OperationChannel.Editor));
        list.Add(new("editor.preview", "root.editor", OperationChannel.Editor));
    }

    private static void AddPlayMeta(List<OperationTerminalSpec> list)
    {
        Terminal(list, "play.meta.catalog", "play.meta", PlayRoutes.Catalog);                   // 选局目录
        Terminal(list, "play.meta.countries", "play.meta", PlayRoutes.Countries);               // 剧本国家
        Terminal(list, "play.meta.local-players", "play.meta", PlayRoutes.LocalPlayers);       // 本地多人
        Terminal(list, "play.meta.country-registry", "play.meta", PlayRoutes.CountryRegistry); // 国家表
        Terminal(list, "play.meta.start", "play.meta", PlayRoutes.Start);                       // 开局
        Terminal(list, "play.meta.leave", "play.meta", PlayRoutes.Leave);                       // 退局
    }

    private static void AddPlayMenu(List<OperationTerminalSpec> list)
    {
        Terminal(list, "play.menu.headquarters", "play.menu", PlayRoutes.Headquarters); // 大本营
        Terminal(list, "play.menu.deployment", "play.menu", PlayRoutes.Deployment);       // 部署
        Terminal(list, "play.menu.unit-info", "play.menu", PlayRoutes.UnitInfo);         // 单位信息
        Terminal(list, "play.menu.commander", "play.menu", PlayRoutes.Commander);       // 将领养成
        Terminal(list, "play.menu.commerce", "play.menu", PlayRoutes.Commerce);         // 商店/酒馆
        Terminal(list, "play.menu.items", "play.menu", PlayRoutes.Items);               // 道具
        Terminal(list, "play.menu.exchange", "play.menu", PlayRoutes.Exchange);         // 交易所
    }

    private static void AddPlayEngine(List<OperationTerminalSpec> list)
    {
        foreach (string op in PlayActionOps.All)
        {
            list.Add(new(
                "play.engine.action." + op,
                "play.engine",
                OperationChannel.Play,
                PlayRoutes.Action + "/" + op));
        }
    }

    private static void AddPlayPersistence(List<OperationTerminalSpec> list)
    {
        TerminalPersistence(list, "play.persistence.saves", PlayRoutes.Saves); // 列槽位
        TerminalPersistence(list, "play.persistence.save", PlayRoutes.Save);     // 存盘
        TerminalPersistence(list, "play.persistence.load", PlayRoutes.Load);     // 读盘
    }

    private static void AddEditor(List<OperationTerminalSpec> list)
    {
        Editor(list, "editor.workspace.list", EditorOperationKind.WorkspaceList);
        Editor(list, "editor.workspace.create", EditorOperationKind.WorkspaceCreate);
        Editor(list, "editor.workspace.delete", EditorOperationKind.WorkspaceDelete);
        Editor(list, "editor.workspace.validate", EditorOperationKind.WorkspaceValidate);
        Editor(list, "editor.workspace.export", EditorOperationKind.WorkspaceExport);
        Editor(list, "editor.workspace.campaigns", EditorOperationKind.WorkspaceCampaignList);
        Editor(list, "editor.workspace.file-read", EditorOperationKind.WorkspaceFileRead);
        Editor(list, "editor.workspace.file-write", EditorOperationKind.WorkspaceFileWrite);
        Editor(list, "editor.workspace.capabilities", EditorOperationKind.WorkspaceCapabilities);
        Editor(list, "editor.map.list", EditorOperationKind.MapList);
        Editor(list, "editor.map.describe", EditorOperationKind.MapDescribe);
        Editor(list, "editor.map.preview", EditorOperationKind.MapPreview);
        Editor(list, "editor.campaign.create", EditorOperationKind.CampaignCreate);
        Editor(list, "editor.campaign.get", EditorOperationKind.CampaignGet);
        Editor(list, "editor.campaign.put", EditorOperationKind.CampaignPut);
        Editor(list, "editor.campaign.edit", EditorOperationKind.CampaignEdit);
        Editor(list, "editor.campaign.roundtrip", EditorOperationKind.CampaignRoundtrip);
        Editor(list, "editor.commander.list", EditorOperationKind.CommanderList);
        Editor(list, "editor.commander.skills", EditorOperationKind.CommanderSkills);
        Editor(list, "editor.commander.get", EditorOperationKind.CommanderGet);
        Editor(list, "editor.commander.edit", EditorOperationKind.CommanderEdit);
        Editor(list, "editor.preview.enter", EditorOperationKind.PreviewEnter);
        Editor(list, "editor.preview.refresh", EditorOperationKind.PreviewRefresh);
        Editor(list, "editor.preview.exit", EditorOperationKind.PreviewExit);
        Editor(list, "editor.preview.status", EditorOperationKind.PreviewStatus);
        Editor(list, "editor.campaign.btl-to-json", EditorOperationKind.BtlToJson);
    }

    private static void AddPlatform(List<OperationTerminalSpec> list)
    {
        Platform(list, "platform.mods.list", PlatformModOperationKind.ListMods);           // 列 mod
        Platform(list, "platform.mods.mutate", PlatformModOperationKind.MutateMods);       // 激活/删/导入/丢弃
        Platform(list, "platform.mod-prepare", PlatformModOperationKind.PrepareImport);   // 上传暂存
        Platform(list, "platform.mod-import", PlatformModOperationKind.CommitImport);     // 提交安装
    }

    private static void Terminal(List<OperationTerminalSpec> list, string id, string parent, string route) =>
        list.Add(new(id, parent, OperationChannel.Play, route));

    private static void TerminalPersistence(List<OperationTerminalSpec> list, string id, string route) =>
        list.Add(new(id, "play.persistence", OperationChannel.Play, route));

    private static void Editor(List<OperationTerminalSpec> list, string id, EditorOperationKind kind)
    {
        string parent = id switch
        {
            _ when id.StartsWith("editor.workspace.", StringComparison.Ordinal) => "editor.workspace",
            _ when id.StartsWith("editor.campaign.", StringComparison.Ordinal) => "editor.campaign",
            _ when id.StartsWith("editor.commander.", StringComparison.Ordinal) => "editor.commander",
            _ when id.StartsWith("editor.preview.", StringComparison.Ordinal) => "editor.preview",
            _ => "editor.campaign",
        };

        list.Add(new(id, parent, OperationChannel.Editor, kind.ToString()));
    }

    private static void Platform(List<OperationTerminalSpec> list, string id, PlatformModOperationKind kind) =>
        list.Add(new(id, "root.platform", OperationChannel.Platform, kind.ToString()));
}

#endregion

#region Play route 描述符

/// <summary>
/// Backend 用 route 名组装 <see cref="PlayOperationDescriptor"/>。执行由 Service 的 PlanRegistry 按 <c>play.&lt;route&gt;</c> 查找。
/// </summary>
public static class PlayDescriptors
{
    private static readonly HashSet<string> PersistenceRoutes = new(StringComparer.Ordinal)
    {
        PlayRoutes.Save,   // 写入当前局到槽位文件
        PlayRoutes.Load,   // 从槽位恢复局内 Session
        PlayRoutes.Saves,  // 列出某模式下的槽位元数据
    };

    private static readonly HashSet<string> ContentRoutes = new(StringComparer.Ordinal)
    {
        PlayRoutes.Catalog,          // 剧本/章节/国家等选局目录
        PlayRoutes.CountryRegistry,  // 国家表 schema（扩展用）
        PlayRoutes.Countries,        // 某剧本可选国家列表
        PlayRoutes.LocalPlayers,     // 本地多人阵营分配
        PlayRoutes.Start,            // 开局：构建 PlaySession + RuleSet
        PlayRoutes.Leave,            // 关闭当前局，回到选局
        PlayRoutes.Headquarters,     // 大本营将领/资源总览
        PlayRoutes.Deployment,       // 部署界面可用单位
        PlayRoutes.UnitInfo,         // 单位详情面板数据
        PlayRoutes.Commander,        // 将领养成（技能/标签等，body.op 细分）
        PlayRoutes.Commerce,         // 城池商店/酒馆（body.op 细分）
        PlayRoutes.Items,            // 道具使用面板
        PlayRoutes.Exchange,         // 资源交易所
        PlayRoutes.Music,            // 背景音乐配置（get/set，按势力）
        PlayRoutes.Language,         // 语言设置（get/set）
        PlayRoutes.ExportApk,        // 导出 APK（占位，未实现）
    };

    /// <summary>Backend 从 play 的 route 名构造强类型 descriptor（可选 action 子 op）。</summary>
    public static PlayOperationDescriptor Describe(string route, string? actionOp = null)
    {
        if (string.Equals(route, PlayRoutes.Action, StringComparison.Ordinal))
        {
            return new PlayEngineRouteOperation(actionOp);
        }

        if (PersistenceRoutes.Contains(route))
        {
            return new PlayPersistenceRouteOperation(route);
        }

        if (ContentRoutes.Contains(route))
        {
            return new PlayContentRouteOperation(route);
        }

        throw new ArgumentException("Unknown play route: " + route, nameof(route));
    }
}

#endregion
