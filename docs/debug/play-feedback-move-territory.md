# 移动占地与涂色（2026-10）

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 已修复 |
| 影响 | 局内移动后地图势力色；有 `TerritorialJurisdictions` 的战役 |
| 关联任务 | plan B2 子项；`TerritoryClaims` / `JurisdictionClaims` |

## 现象

- 部队移动后，路径旁一格未上色，或染到**已有其它单位**的格子。
- 走进**城池**落点，辖区色不更新。
- **要塞**附近（同区敌方要塞单位）路径邻格被 `JurisdictionClaims` 回滚，几乎无法涂色。
- 邻格有单位时**落点**有时也不上色。

## 原因

| # | 说明 |
|---|------|
| A | `TerritoryClaims.Claim` 只涂空地/农田，**城池/港口**落点未 `occupySite` 占领。 |
| B | `JurisdictionClaims` 用 `CanClaim` 回滚 `TerritoryClaims` 结果；同区**敌方要塞**格在 `Ring` 内即否决邻格。 |
| C | `unitBlocked` 回滚时误伤**移动终点**（终点格曾在 `blockedCells` 中）。 |
| D | 其它单位所在格应永不涂色，但终点必须从 blocked 集合排除。 |

## 修复

| 改动 | 文件 | 说明 |
|------|------|------|
| 路径/落点 `occupySite` 占城、终点邻圈 | `TerritoryClaims.cs` | `Claim(..., occupySite)`；终点 `Claim(end,true)` + 终点六邻 |
| `movePaint` 保留行军涂色 | `TerritorialJurisdictions.cs` | 路径及邻格内、`TerritoryClaims` 已改为己方且非 `unitBlocked` 的格不再被 `CanClaim` 抹掉 |
| 要塞仅阻挡非踩格扩张 | `TerritorialJurisdictions.cs` | `!occupiedSite` 时才做同区要塞 `Ring` 检查 |
| blocked 不含 destination | `WorldActions.cs` | 邻格有兵不涂；落点仍占领 |
| 移动后刷新士气 | `WorldActions.cs` | `WorldDeaths.RefreshMorale` |

## 验证

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh test -c Release --filter FullyQualifiedName~TerritoryClaims
```

- [x] 邻格有敌军/友军单位的格子不染色
- [x] 走进城池后落点及辖区转移正常
- [x] 要塞旁行军路径与邻格可上色（要塞所在格仍有单位则不涂）
- [x] 邻格有单位时落点仍可上色

## 相关代码

- `src/Ew4.Platform.Engine/Meta/TerritoryClaims.cs`
- `src/Ew4.Platform.Engine/Meta/TerritorialJurisdictions.cs`
- `src/Ew4.Platform.Engine/World/WorldActions.cs`
- `tests/Ew4.Test.Platform.Engine/TerritoryClaimsTests.cs`
