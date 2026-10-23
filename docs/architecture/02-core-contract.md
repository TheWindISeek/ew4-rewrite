# 02 Core 对外契约

状态:草案

`src/Ew4.Platform.Engine` 由旧 `rewrite/Ew4.Platform.Engine` **原样复制**而来(`netstandard2.1`、无第三方依赖)。内部算法一行不改。本文定义的是 **Core 对外应该暴露什么**,以及哪些东西不应该暴露。

## 1. 现状的问题

- Core 暴露了几十个公开静态类(`WorldActions`、`WorldBattle`、`WorldTurns`、`WorldImprovements`、`WorldSupportActions`...),每个都要调用方传入对应的规则记录。
- 调用方(旧 `ClientGame.Action`)用一个长 `switch` 决定"移动传 `rules.Movement`,攻击传 `rules.Battle`..."。**宿主需要知道 Core 的内部划分。**
- 没有版本声明:调用方无法在运行前询问"这个引擎支持什么"。
- 存档只记录数据指纹,没有记录引擎行为版本。

## 2. 目标:三样东西

```text
1. 输入模型   RuleSet、WorldState、PlayerAccount        (数据,只增不改)
2. 操作门面   IEngine                                    (行为,窄接口)
3. 能力声明   EngineInfo                                 (元数据)
```

### 2.1 输入模型

- `WorldState`、`PlayerAccount`、`GameSession`:沿用现有类型。
- **`RuleSet`(新增):** 把旧代码里分散传递的规则记录汇总成一个对象。

```csharp
// 草案
public sealed record RuleSet(
    WorldTurnRules Turns,
    WorldMoveRules Movement,
    WorldBattleRules Battle,
    WorldAiRules Ai,
    IReadOnlyDictionary<string, WorldProductionRules> Production,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, RecruitmentOffer>> Recruitment,
    WorldCommanderRules Commanders,
    RosterDeploymentRules Roster,
    TrainingRules Training,
    BuildingUpgradeRules Upgrades,
    IReadOnlyDictionary<string, InstallationBuildRules> Installations,
    TransportPurchaseRules Transport,
    TavernRules Tavern,
    SessionTradingRules Trading,
    IReadOnlySet<string> Compat);   // 兼容开关,见 06 文档
```

主体字段来自旧 `LegacyScenarioRules`(位于 `Ew4.Legacy/LegacyScenarioHost.cs`),类型位置从 `Legacy` 搬到 `Core`,并加上 `Compat`。
旧记录里的 `Dialogues`、`MissingEventReferences`、`HighestRatingRound` 属于剧本内容或展示数据,不进入 `RuleSet`,由 Content 层的剧本模型承载(实现时需逐项确认归属)。

**演进规则:**

1. 只增不改:新增字段必须有默认值,且默认值等于旧行为。
2. 不允许删除或改变已有字段的含义。需要改动时见 [06 版本与兼容](06-versioning-compat.md)。

### 2.2 操作门面 `IEngine`

```csharp
// 草案
public interface IEngine
{
    EngineInfo Info { get; }

    /// 用已构建好的初始世界开一局。校验失败抛 RuleViolation(带错误码)。
    GameSession NewGame(WorldState initial, RuleSet rules, PlayerAccount account, NewGameOptions options);

    /// 执行一条命令,返回新的会话和事件。命令非法抛 RuleViolation。
    EngineResult Apply(GameSession session, RuleSet rules, EngineCommand command);

    // 只读查询,不改变状态。
    ReachableMap Reachable(WorldState world, RuleSet rules, string unitId);
    AttackPreview PreviewAttack(WorldState world, RuleSet rules, string attackerId, string targetId);
    AiStepPlan PlanAiStep(WorldState world, RuleSet rules);
}

public sealed record EngineResult(GameSession Session, IReadOnlyList<WorldNotice> Notices);
```

`EngineCommand` 是一个封闭的命令层次(`Move`、`Attack`、`EndTurn`、`Recruit`、`Train`、`UpgradeBuilding`、`BuyTransport`、`BuildInstallation`、`UseSkill`、`Disband`、`UseItem`、`Deploy`、`AcknowledgeDialogue`、`ChooseEvent`...),对应旧 `ClientGame.Action` 里的各个 `Op`。

**要点:**

- **所有命令都带 `ExpectedRevision`。** 状态已变化时引擎拒绝,这条规则现在由宿主检查(`client.stale`),应下沉到 Core。
- **引擎不持有状态。** `Apply` 是纯函数:输入旧会话,返回新会话。持有"当前一局"是 Session 层的事。
- **规则选择在 Core 内部完成。** 宿主只传 `RuleSet`,不挑子规则。

**实现方式:**

- 第一版 `Engine : IEngine` 只做转调:`switch (command)` → 调用现有的 `WorldActions.Move(world, revision, ...)` 等静态函数。**不改内部算法。**
- 门面稳定后,把原先的公开静态类逐步改成 `internal`,只让门面可见。
- 现有的 `[InternalsVisibleTo("Ew4.Verify")]` 在新测试项目建立时改为 `Ew4.Platform.Engine.Tests`。

### 2.3 能力声明 `EngineInfo`

```csharp
// 草案
public sealed record EngineInfo(
    string EngineVersion,                              // 如 "0.3.0"
    int BehaviorVersion,                               // 规则行为版本,行为改动时 +1
    IReadOnlyDictionary<string, int> ContentFormats,   // 每类内容支持的最高 schema,如 { "scenario": 1, "rules": 1 }
    IReadOnlySet<string> Capabilities,                 // 如 "rule-api-2"、"lua"
    IReadOnlySet<string> KnownCompatFlags);            // 引擎认识的兼容开关
```

用途:

- Pack 的 `requires` 与它对照,不兼容时在**加载前**失败,并说明缺少什么。
- 存档记录 `EngineVersion` 和 `BehaviorVersion`,读档时对照。

## 3. Core 不做什么

| 不做 | 谁来做 |
|---|---|
| 读写文件、网络、数据库 | Content / Session / Host |
| 知道 Pack、Mod、`pack.json` | Content |
| 知道 BTL/XML/JSON 格式 | Content(Importers/Exporters/Schema) |
| 知道"原版 / legacy" 这类历史概念 | 只在 Importers 里出现 |
| 渲染、动画帧调度 | 前端。Core 只提供动画时序数据 |
| 声明式规则扩展 | Content 层 `ExtensionLoader` 加载 `rewrite/extension.json`；**无 Lua**（D8） |

Core 依赖脚本运行时的方式不变:Core 定义接口 `IRuleScriptRuntime`,Content 层提供实现,通过 `RuleEventBus` 注入。**Core 因此不依赖 MoonSharp。**

## 4. 迁移影响

- `src/Ew4.Platform.Engine` 中 `AssemblyInfo.cs` 暂保留旧 `InternalsVisibleTo`,待测试项目建立后更新。
- 引入 `RuleSet` 需要把 `LegacyScenarioRules` 的类型移入 Core,这是第一个对 Core 的**增量修改**,属于"新增类型",不改既有算法。
- 门面的验收:现有 `Ew4.Verify` 的世界/战斗/AI 检查通过 `IEngine` 重新跑一遍,结果不变。
