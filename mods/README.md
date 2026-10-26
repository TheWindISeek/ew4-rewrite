# 怎么改 Mod 内容

内容写在 **`mods/<你的名字>/`**，玩之前同步到 **`install/web/mods/`**（`scripts/ew4.sh web` / `sync-mods` 会自动从仓库 `mods/` 拷贝；APK 渠道用 **`install/apk/mods/`**）。

**切换已安装的 Mod（已实现）**：浏览器打开游戏后，点左下角 **Mod** → 在列表中选择要用的包 → 确认激活（会校验 `parent`、引擎版本等，成功后刷新页面）。当前激活包记录在 **`install/web/user/active.json`**。也可在同一面板导入 zip / `.ew4mod` 到 `install/web/mods/<id>/`。

每个包根上要有 **`mod.json`**（包名、`parent` 等）。**`id` 必须和文件夹名一样**。

---

## 两种包

**1. 完整包**（例如 `base`）  
自己带全套表和资源。一般用 `Ew4.Tool import-base` 生成，不要手搓。

**2. 差异包（最常见）**  
`mod.json` 里写 `"parent": "base"`，目录里**只放你要改的文件**，其余用原版。  
激活时：你的文件盖住 `base` 里同名路径。

```text
mods/my-mod/
  mod.json
  assets/tables/def_commander.xml    ← 只这一份，就只改将领
```

差异包 `mod.json` 可直接复制仓库里的示例（见下）再改 `id` / `name`。

有 `mod.json` 时旁边的旧 `pack.json` 会被忽略，不用管。

---

## 我想改……该动哪个文件？

运行时读的是「逻辑路径」（如 `original/def_commander.xml`）。**在包里落盘时**，按下面放即可（差异包只放改动的那个文件）。

| 想改什么 | 包里放这里（推荐） | 说明 |
|----------|-------------------|------|
| **将领**（属性、技能、头像用的表） | `assets/tables/def_commander.xml` | 从 `base` 拷出整表再改，或只覆盖你动过的条目 |
| **国家 / 势力**（选关名、地图色、国旗代码） | `assets/client/countries.json` | 改 `name`、`color`（`#RRGGBB`）、`flag` 或 `flagImage` |
| **自定义国旗图** | `rewrite/.../*.png` + 在 `countries.json` 里写 `flagImage`: `"rewrite/..."` | 浏览器走 `/content/extensions/…` |
| **背景音乐** | `assets/client/music.json` + `assets/client/music/*.ogg` | `default` 是默认曲目，`countries` 里按国家 id 指定专属曲目；进入战役/征服的局后循环播放（`.ogg/.mp3/.m4a/.wav`，路径相对 `client/`）；玩家可在本机按国家覆盖（`play.music` 接口，存于存档根 `music.json`）。默认曲目是公有领域的《马赛曲》（Wikimedia Commons） |
| **兵种数值 / 兵种表** | `assets/tables/def_army.xml` 等 | 和原版一样叫 `def_*.xml` 的表，多在 `assets/tables/` |
| **物品、建筑、科技…** | `assets/tables/def_item.xml` 等 | 同上，改哪个表就覆盖哪个文件 |
| **某一关剧本** | `scenarios/campaign1_01.json` | 嵌入 JSON；`name` 会显示在选关列表（覆盖 battlelist 标题） |
| **整包 APK 差异** | 用工具生成 | `Ew4.Tool import-apk-overlay …`，再在 `mods/<id>/` 里微调 |

只改 XML、不改 `countries.json` / `client/manifest.json` 时，**表变了但 UI 可能还是旧样子**（旗、兵种卡、涂色不对）。改国家外观时，记得一起动 **`countries.json`**。

---

## 带 `parent` 时怎么理解？

例子：只加强法国将领。

```text
mods/fr-commanders/
  mod.json                 parent: base
  assets/tables/def_commander.xml
```

不复制 `base` 里其它几千个文件。进游戏激活 `fr-commanders` 后，引擎用 **base + 你这一份 XML** 合并读。

再举一个：换几个国家的旗和颜色。

```text
mods/pretty-flags/
  mod.json
  assets/client/countries.json
  rewrite/flags/custom_fra.png
```

`countries.json` 里对应国家条目加上 `"flagImage": "rewrite/flags/custom_fra.png"`。

---

## 做完之后

```bash
cp -a mods/my-mod install/web/mods/my-mod    # 或 scripts/ew4.sh sync-mods
scripts/ew4.sh web                         # 浏览器里激活 Mod
```

从零做 `base`：`Ew4.Tool import-base`。  
旧 BTL 索引桩转 schema 3 场景 JSON：`Ew4.Tool scenario to-v3 --mod mods/<id>`。

## 仓库里的示例 Mod（进 Git，与 `base` 同级）

需本机有 **`mods/base`** 或 **`install/web/mods/base`**。`sync-mods` 后在游戏里激活。

| 目录 | 内容 |
|------|------|
| `mods/ex-commander-skills/` | 只覆盖 `assets/tables/def_commander.xml`（拿破仑 `skill2=99`） |
| `mods/ex-campaign-tweak/` | 只覆盖 `scenarios/campaign1_01.json`（改战役第一关列表名） |
| `mods/ex-conquest-tweak/` | 只覆盖 `scenarios/conquest1.json`（改征服第一关列表名） |

单测：`ExampleModsTests`（有 base 时会合并解析剧本 / 将领表）。

更底层的目录规则见 [docs/arch/module-responsibilities.md](../docs/arch/module-responsibilities.md)。
