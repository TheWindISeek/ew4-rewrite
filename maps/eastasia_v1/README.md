# 东亚地图资源原型

本目录单独保存东亚地图成果，不修改项目根目录中的本体、参考 Mod 和工具。

## 查看成果

- `eastasia_preview.png`：整体预览，不含国家和城市标记。
- `eastasia_base.png`：普通分辨率底图，4993 × 3583。
- `eastasia_base_hd.png`：高清底图，9986 × 7166。
- `eastasia_places_check.png`：城市位置核对图。标注仅供核对，不是游戏内城市部署。
- `eastasia_grid_check.png`：六角格及海陆中心核对图。
- `eastasia_land_mask.png`：陆地遮罩，后续地形编辑的基础。
- `eastasia_shores_rivers.png`：透明陆地上的海岸、海洋和河流层。

范围以中国为中心，涵盖日本、朝鲜半岛、台湾、海南、蒙古及周边地区。
使用兰伯特正形圆锥投影，以统一比例尺布局。此阶段不画政治边界。
海色和岸线参考原版，陆地纹理复用用户提供的地图素材。

## 游戏资源

`eastasia_map_resources.zip` 中保留 `assets/` 相对路径，对应展开的 `assets/` 目录：

- `europe.bin`：79 × 68 地块；初步配置海陆及主要河流边缘标记。
- `map1.bin`、`map1_hd.bin`：普通及高清贴图索引。
- `map/map1_*.pvr`、`map/map1_*@2x.pvr`：原格式 RGBA4444 图集，各保留 102 个文件名。
- `maptextpos_europe.xml`：清空原来的欧洲地名位置。
- `def_map.xml`：本体原文件，维持地图槽位与尺寸配置。

这些资源替换欧洲地图槽位。地图文件名保留原名，以兼容已有读取方式。
压缩包是地图资源包，不是可安装 APK，也不是已经完成的征服剧本。
未修改 `.so`，未配置将领、势力、城市、山林、沙漠或军队。
不能将旧欧洲征服直接视为适配新地图的可玩剧本。

## 已验证与待验证

生成时将写出的 PVR 和索引重新读入、拼接，检查是否与量化后的图像一致。
同时检查图集容量、地块文件长度、关键地点范围及原始 APK 哈希。
结果保存在 `validation.json`，资源哈希保存在 `manifest.json`。
独立核验脚本 `tools/verify_eastasia.py` 检查压缩包、文件格式、河流标记互反及地理参考点，
本轮 460 项检查通过，记录在 `verification.json`。

尚未在 Android 游戏中测试。贴图索引的外圈偏移及六角格中心坐标依据本体结构推断，
必须在后续独立测试包中确认显示位置、地块选择和移动路径，再作为正式可玩地图使用。
河流边缘字段的最终游戏行为也需实测。

地理数据来自 Natural Earth 的公开物理地理数据，来源与哈希见 `sources.json`。
已排除标为 Reservoir 的湖泊，仍不是逐年复原的历史地理资料；
例如黄河历史改道和湖岸变化，需要在制作具体年代剧本时复核。

## 重建

生成器：`tools/build_eastasia.py`，以项目根目录为基准定位输入和输出。
使用 Python 3、NumPy、Pillow、Shapely 2 和 PyShp。
额外依赖位于 `C:\Users\WIN11\.codex\tools\ew4-inspect-deps`。
运行生成器会重建本目录的生成物，始终保留根目录原始材料。
已缓存的地理资料位于 `maps/source_cache`，重建优先使用缓存。
