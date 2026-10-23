# 04 会话、前端接口与宿主

状态:已实现（路由与 `Ew4.Service` 对齐；下文接口块仍为设计备忘）

## 1. Session(运行时数据)

**职责:** 持有"当前这一局"和"局外账户",负责存读档。它是 Core 的使用者,不是 Core 的一部分。

```csharp
// 草案
public interface ISessionService
{
    SessionView Catalog();                         // 可玩的剧本、章节、解锁状态(来自 ResolvedContent)
    SessionView Start(StartRequest request);       // 开局:Builder → IEngine.NewGame
    SessionView Apply(EngineCommand command);      // 转交 IEngine.Apply,并自动存档
    SessionView Leave(long expectedRevision);
    SaveResult Save(SaveSlotId slot);
    SessionView Load(SaveSlotId slot);             // 校验失败则拒绝,返回原因
    IReadOnlyList<SaveSlotInfo> Slots(int mode);
}

public interface IAccountService                   // 局外:名册、勋章、物品库存
{
    AccountView Get();
    AccountView Apply(AccountCommand command);     // 招募、培养、装备、交易
}
```

- 一个 `SessionService` 实例绑定**一个** `ResolvedContent`。切换 Pack 时,Host 丢弃旧实例、创建新实例。
- 每次命令串行执行(加锁),保证回合归属和 `ExpectedRevision` 检查不被绕过。
- **局内(Session)与局外(Account)分开存储**:账户存 `account.json`,局存 `slot-*.json`。这对应 PRD 的 FR-S01。
- 旧 `ClientGame` 里"总部、商店、酒馆、名册"属于局外功能。它们先作为 `IAccountService`,与局内服务**分成两个接口、可以仍在同一个项目里**。是否独立成项目,等实现时再根据体量决定(开放问题,见 §7)。

### 1.1 存档

存档文件头(草案):

```json
{
  "format": "ew4.session",
  "saveSchema": 3,
  "engineVersion": "0.3.0",
  "behaviorVersion": 1,
  "packId": "my-mod",
  "fingerprint": "ab12...",
  "compat": ["counter-siege-original"],
  "sha256": "...",
  "payload": { }
}
```

读档规则:

| 情况 | 处理 |
|---|---|
| `packId` 或 `fingerprint` 与当前激活内容不同 | **拒绝**,提示"此存档属于另一内容/版本" |
| `saveSchema` 不是当前版本 | **拒绝**(现阶段不做存档迁移),提示原因 |
| `behaviorVersion` 与引擎不同 | 若存档里的 `compat` 开关能还原旧行为,按该开关继续;否则**拒绝** |
| 摘要校验失败 | **拒绝**,提示存档损坏;保留 `.bak` |

写入沿用旧实现的做法:写临时文件 → flush → 原子替换并保留上一版备份。

**目录布局:**

```text
saves/
  {packId}/
    {fingerprint-short}/
      account.json
      slot-*.json
      session.json          自动存档
```

- base 与 Mod 使用同一规则(`packId = "base"`),不再有"原版放根、Mod 放子目录"的特例。
- 删除 Pack 时**默认保留**对应存档目录(PRD FR-M04)。

## 2. 游玩 API（`Ew4.Service`）

**职责:** 只和前端页面打交道。把 HTTP/WebView 请求翻译成 Session 命令,把状态翻译成前端需要的视图模型。**不加载内容、不管理 Pack。**（实现：`Ew4.Service`；路由名见 `Ew4.Interface`。）

**路由:沿用旧路由,前端 `wwwroot` 暂不改动。**

| 路由 | 说明 |
|---|---|
| `/api/catalog`、`/api/country-registry`、`/api/countries` | 剧本、国家目录 |
| `/api/start`、`/api/leave`、`/api/action` | 开局、离开、局内命令 |
| `/api/save`、`/api/saves`、`/api/load` | 存读档 |
| `/api/headquarters`、`/api/deployment`、`/api/commander`、`/api/unit-info` | 总部、派遣、将领、部队信息 |
| `/api/commerce`、`/api/items`、`/api/exchange` | 商店、物品、资源兑换 |
| `/api/local-players` | 本地多人 |

这些路由的**请求和响应格式在迁移期保持不变**。`Ew4.Service` 在背后调用 `ISessionService` / 账户相关服务,前端无感。

**展示数据归这里,不归 Content:** 章节(`CampaignChapter`)、剧本分类 Mode(战役/征服/多人/教程)、剧本说明、征服页面槽位等"给 UI 看的东西"。内容文件仍由 Content 读取,但把它们组织成视图模型是 Service 层的事。

## 3. Host(宿主)

**职责:** 进程、路由、文件路径、安全策略、激活 Pack。不含业务规则。

### 3.1 Host 的公共部分

```csharp
// 草案
public interface IHost
{
    PackStatus Status();                         // 已安装包、当前激活、导入进度
    void Activate(string? packId);               // null 表示 base
    ImportToken Prepare(Stream package, string fileName);   // 读取并识别身份,不落盘
    void Import(ImportToken token, string id, string name);
    void Remove(string packId);
}
```

Web 和 Android 共用这一部分实现;只有"怎么收到请求"和"文件放哪里"不同。

### 3.2 切换 Pack(沿用旧版"指针 + 热切换")

```text
安装   把包复制到 packs/{id}/,校验通过才落盘
启用   ① 校验 → ② 解析出新的 ResolvedContent → ③ 新建 SessionService(丢弃旧的)
       → ④ 原子写入 active.json(指针)→ ⑤ 通知前端刷新
启动   读 active.json → 加载;失败则回退到 base,并向用户显示原因
删除   删除包目录;若是当前激活的则先切回 base;存档目录保留
```

- **不需要退出游戏。** Android 与 Web 行为一致。
- 启用时先构造候选内容,成功后才替换指针;失败时当前状态不变。

### 3.3 Web 宿主

- 单进程 ASP.NET Core(Kestrel),监听本机。
- 保留旧策略:`/api/` 拒绝跨站请求(`Sec-Fetch-Site` 与 `Origin` 检查)、API 响应 `no-store`、上传大小上限、资源路径白名单。
- 路径白名单**由 ContentKind 注册表生成**,不再在 `Program.cs` 里手写目录名。

### 3.4 Android 宿主

- `MainActivity` 持有同一个 `IHost`,WebView 通过拦截 `ew4.local` 的请求调用 PlayApi。
- Pack 与存档放应用私有目录(`FilesDir/packs`、`FilesDir/saves`)。
- 与 Web 的差异仅限于:文件选择器桥、返回键处理、屏幕/安全区布局。这些保持在 `Ew4.Platform.Runtime.Android` 里,不进入共用部分。

## 4. 请求处理的分工(一次 `/api/action` 的路径)

```text
前端 → Host(路由/安全检查)
     → Ew4.Service(解析 JSON → EngineCommand)
     → SessionService(加锁;检查回合/Revision)
     → IEngine.Apply(session, ruleSet, command)         ← Core
     → SessionService(提交新状态;自动存档)
     → Ew4.Service(状态 → 视图 JSON)
     → 前端
```

## 5. 错误处理

- Core 抛 `RuleViolation(code, message)`。Service 转成 `{ ok:false, code, message }`,格式与旧版一致。
- 内容或存档不兼容使用专门的错误码前缀(`content.*`、`save.*`),便于前端给出明确提示。

## 6. 与 PRD 的对应

| PRD | 本文位置 |
|---|---|
| FR-S01 账户与局分离 | §1 |
| FR-S02 读档校验 | §1.1 |
| FR-M02 启用 Mod、切回原版 | §3.2 |
| FR-M03 存档隔离 | §1.1 目录布局 |
| FR-M04 删除保留存档 | §1.1 |
| FR-A01 Client 职责单一 | §2 |

## 7. 开放问题

1. 局外功能(总部/商店/酒馆/名册)是否独立成 `Ew4.Account` 项目?当前倾向先与 Session 同项目、分接口。
2. 战役进度(`CampaignProgress`、解锁)归 Account 还是 Session?
3. 本地多人(`ClientLocalMultiplayer`)作为 Session 的一种开局选项,还是独立功能?
