# 旧东亚地图复用核查

核查日期：2026-10-19。用户明确：后端已经重写，旧 APK 真机结果不作为 rewrite 验收证据。

## 结论

可继续利用，建议以 `eastasia_v5/` 的地理资料和源图为基础，按当前 rewrite 的坐标与资源接口重新导入。尚未接入 rewrite，不能称为重写版可玩地图。

- V1 至 V5 均保留；V4 是旧 APK 曾运行的版本，曾反馈岸线、上海落点和西安字体问题。
- 旧短锚点称 V5 未按 62 像素步距修正，但当前生成器和 V5 实际资源已使用普通 62/高清124 步距及防渗色边框。以本次资源重读结果为准。
- 本次运行 `tools/verify_eastasia.py --output maps/eastasia_v5`：505 项检查，0 错误，河流互反错误0；结果在 `eastasia_v5/verification.json`。只验证保存资源的结构及地理一致性，不代表 rewrite 运行验收。

## 可复用材料

- `eastasia_v5/eastasia_base.png`、`eastasia_base_hd.png`：4993×3583 / 9986×7166 底图。
- 同目录海陆蒙版、河岸图、外圈参考画布和地形预览。
- `place_coordinates.json`：19处城市经纬度及投影像素，作为定位参考；不是正式城市表或数量上限。
- `maps/source_cache/` 与 `eastasia_v5/sources.json`：Natural Earth 公有领域海陆、河流、湖泊数据及来源哈希。
- `maps/terrain_regions.geojson`：手工概化的山脉、森林等区域。可作玩法起点，不能当作实测高程、历史植被或历史水系。
- `tools/build_eastasia.py`、`verify_eastasia.py`：保留投影和生成逻辑；运行使用支持已有 cp312 依赖的 bundled Python，系统 Python 3.10 无法载入现有 Shapely。

## rewrite 接入前需要处理

1. 统一格心坐标。旧生成器使用 `x=64*col+32*(row%2), y=54*row`；当前 `rewrite/Ew4.Web/wwwroot/battlefield.js` 的 `point()` 使用 `y=54*row-18`。旧地图海陆采样、河流连接和诊断城市定位必须按当前显示及选格体系重新核对，不能只复制 BIN。
2. 确定地图规模。旧网格79×68，共5372格，覆盖宽度约6600公里，每格约80公里。正式规模需结合重要城市、行军回合和部队密度决定，不由旧格式限定。
3. 转换运行资源。当前前端读取 `rewrite/data/client/manifest.json` 中的地图与PNG分块，当前 `ClientContent` 仍从 `def_map.xml` 读取旧地图并按整数编号索引。核心已有 `WorldMapDefinition` / `WorldMapCatalog` 和 JSON `MapDocument`，但不代表新地图已贯通菜单、场景宿主与渲染。
4. 重新配置中文地名、城市落格、设施和剧本。旧19城仅为原国家槽位的诊断样例；旧 `integrate_eastasia.py` 会写入 `apk_work/game`，不能直接当成 rewrite 集成工具。
5. 在隔离预览中核对岸线、边缘、上海及岛屿落点、选择格、移动和存档；后续按用户要求构建 rewrite 包。旧 APK 和手机运行记录仅作历史参考。

## 当前边界

本轮仅检索、看图、复核和记录。未替换 rewrite 的欧洲/美洲地图，未操作用户正在使用的5184对局，未构建 APK，未修改另一会话负责的兼容实现。
