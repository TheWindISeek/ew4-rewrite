# 第二阶段基线（S0）

在开始 S1（删 APK/旧栈）**之前**，由 Agent 或维护者填写本文件。之后每完成一步 S1–S7，对比此基线跑 G1–G5。

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-23 |
| Git 提交 | `36fc30b` |
| 分支 | `refactor/pack-architecture` |

## G1 build

```bash
scripts/ew4.sh build -c Release
```

- 结果: 通过
- 警告数: 0（Docker 内 restore 时有一次 workload 提示，非编译警告）

## G2 test

```bash
scripts/ew4.sh test -c Release
```

- 通过/失败/跳过: **9 / 0 / 0**（Content 2，Editor 5，Session 2）

## G3 validate smoke

```bash
scripts/ew4.sh validate smoke
```

- 内容包路径: `mods/base`（CLI 校验）；运行时 `install/mods/base`（`sync-mods` / `build` / `web`）
- 结果: 通过（`errors=0`，`warnings=6` 为多人剧本无包内 BTL，与既有行为一致）

## G4 check btl

```bash
scripts/ew4.sh check btl
```

- 结果: 通过（`failures=0`，`btl=95`）

## G5 web 烟测

- 结果: **未自动执行**（需人工：开局、一回合、存读档、切包）；后续 S1+ 每步完成后补测并在此更新
