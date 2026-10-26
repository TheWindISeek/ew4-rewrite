# 扩展模型（冻结）

**加功能的方式只有一种：新增模块 + 往注册表登记 Step/Plan，不修改已有 Step 的内部。** 这是本项目“架构可承载更多功能而不乱动”的核心。

## 词汇

| 词 | 含义 |
|----|------|
| Request / route | 用户意图。Play 路由名 `play.<route>`，Editor 为 `editor.*` |
| Plan | 一个 Request 对应的**有序 Step 列表**，在 `Ew4.Service/Plans` 注册 |
| Step | 模块暴露的原子操作：声明读/写哪些 `PlanKey`，可测试，只读写自己声明的键 |
| PlanKey | 类型化键；是否进入响应由声明处的 `Expose` 决定 |
| 资源键 | 仲裁用（如 `session`、`workspace:{id}`）；同键串行 |

## 约束

1. 每个 HTTP/RPC 能力 = 一个已注册的 Plan。**不得**在 Backend 或 Service 里写旁路 `if route == ...`。
2. Step 之间只通过 `PlanContext` 的键传数据；越权读写直接抛错（框架已强制）。
3. **可选 Step**（`optional: true`）用于“附加能力”（音乐、音效提示等）：失败只写 `diagnostics`，不得影响主流程；其产物下游必须用 `OptionalReads` + `TryGet`。
4. **条件 Step** 用 `Step(step, optional, when)`；谓词只用该 Step 读权限内的键。
5. 注册表构建时校验（读前有写、`Single` 槽位唯一写入者、Id 唯一）；校验失败 = 启动失败，**不得**通过放宽校验来“让它跑起来”。
6. **失败语义不变**：必需 Step 异常 → 丢弃全部暴露值；`OperationException`/`RuleViolation` → 400；`PlanBusyException` → 409；Play 接口的**可预期**失败返回 HTTP 200 + `{ok:false,code,message}`（经 `PlayFailureException` + `OnFailure`）。
7. 响应契约：新能力优先用槽位（`view` / `effects` / `notices` / `diagnostics`）合成；**不改已迁移终端的 HTTP 形状**。
8. 音频：战斗音效由前端按时间轴播放，后端不参与；背景音乐由 `audio.pick-bgm`（可选 Step）选曲并写 `data.audio.bgm`。
9. 开局/读档的**提交点唯一**：`install-session`（替换会话状态）；计算类 Step 不改会话。

## 加一个功能的清单

1. 需要新数据？先看 `content-and-mods.md`：数据放 mod 资源，不放代码。
2. 写 Step（放在拥有该能力的模块，或 `Ew4.Service/Plans` 的薄封装）；声明 `StepSpec`。
3. 在对应 Plans 文件里用 `PlanBuilder` 登记（或追加一个可选 Step）。
4. 补测试（`Ew4.Test.Service` 等）；跑 `scripts/ew4.sh test`。
5. 前端：新字符串走 `ew4t(key, fallbackZh)`；新响应字段按槽位消费。
6. 若觉得需要改框架本身（PlanRunner、PlanContext、注册表语义）——这是改架构，先问维护者。

## 不允许

- 改已有 Step 的读/写声明来迁就新功能（应新增 Step）。
- 在 Step 里捕获并吞掉必需失败。
- 为“高频”绕过注册表。
- 把 mod 数据硬编码成常量（原版数值表属于 mod 内容）。
