# 菜单与城市名称

`conquest-pages.json` 的 `pages` 为页面列表，每页 `slots` 恰好六项。每项可为 `null` 灰框占位，或 `{ "scenario": "conquest7" }`。可加 `art` 指向 client 内图片相对路径，`labels` 指向原菜单图集的标签。新增征服导入时会自动追加菜单页；作者可用本文件安排页面和图片。

`city-names.json` 将原城市英文名称映射为中文，切换设置中的城市名语言生效。只改文字不改变游戏地块归属、坐标或事件。

覆盖包中的路径为 `frontend/conquest-pages.json` 与 `frontend/city-names.json`。这两个JSON可覆盖；游戏程序脚本不接受Mod覆盖。

## 征服页面完整示例

```json
{
  "pages": [
    {
      "id": "example-page",
      "slots": [
        { "scenario": "conquest1", "art": "images/tex_conquest_1793@2x.png", "labels": ["europe", "1798"] },
        null, null, null, null, null
      ]
    }
  ]
}
```

| 字段 | 含义/约束 |
| --- | --- |
| `pages` | 当前验证1～200页 |
| `id` | 稳定页面ID，建议不同页使用不同名称 |
| `slots` | 每页恰好六项；null灰框不可进入，不用删项减少卡片数 |
| `scenario` | 无扩展名，例如conquest7；须已在`mod.json.scenarios`且BTL可启动。只写这里不会新建剧本 |
| `art` | client内相对路径，不带client/data/content前缀；可省略 |
| `labels` | 原菜单图集的贴图标签名，不是任意文字；可省略 |

省略art/labels会使用简化卡片。原资源新增征服导入会每六个追加一页，不会自动填已有灰位；定点排版须维护完整配置。上例会替换全部页面，保留原六项时从现有完整文件复制修改。

前端`app.js`的`scenarioList()`建卡，`updateConquestPagination()`显示页码，`turnConquestPage()`处理翻页/动画锁，`chooseScenario()`进入选国。导入器`LegacyModImporter.Convert()`追加页，`ResourceValidation.Check()`检查槽数/图片依赖。

## 城市译名完整对象示例

```json
{
  "London": "伦敦",
  "Paris": "巴黎"
}
```

键精确匹配`def_areaname.xml`的name，不是地点数字ID。配置是整文件替换，只有这两项就只保留两项译名，其他地点回退原名；保留其他译名需复制完整文件。改地点原名时需同步键，不能改变地块坐标/归属。

城市语言由`app.js`的`syncCityLanguage()/persistSettings()`维护，战场由`battlefield.js`绘制。缺字及字体颜色见[字库](../client/fonts/README.md)。

## 生效与校验

JSON用UTF-8、字段大小写一致、无注释/尾逗号。覆盖包pack、import、export后在游戏导入启用，会重载界面；不要手改已安装的哈希目录。两个配置对应URL`/conquest-pages.json`和`/city-names.json`，不在/content/frontend下。

当前前端两JSON不参与逻辑指纹；只改它们通常沿用同Mod存档，新增BTL则改变逻辑指纹。源码路径见[函数索引](../../docs/SOURCE_INDEX.md)。
