# 东亚地图 V2

东亚海陆、主要河流和概括化山地、丘陵、森林、沙漠已生成，复用原版地形精灵。
地图槽位、79×68地块、普通及高清PVR文件格式保持原结构，未修改原生库。
地形分布来自 `../terrain_regions.geojson` 的人工概括，不代表实测海拔或历史植被。
静态核验462项、0错误，河流边标记互反。资源包包含209个文件。

`eastasia_terrain_preview.png` 是地形总览；`assets/` 是可重建地图资源。
`probe_assets/` 另含第一个征服的非历史测试剧本与19个城市名称，
沿用原版国家槽位和兵种，不属于最终六个民国剧本。
已通过 `tools/integrate_eastasia.py --probe` 汇入游戏工作目录。

`build/ew4-eastasia-map-v2.apk` 在MuMu Android12上进入过第一个1798征服，
截图位于 `build/tests/eastasia_v2/`。一加Ace 3真机同入口新局加载闪退，
因此尚不能交付为真机可玩地图；排查见 `build/tests/phone_crash/README.md`。
地图边缘、完整地块对齐、河流实际效果、征服选择画面及六个正式剧本仍待完善。

生成：`tools/build_eastasia.py`，核验：`tools/verify_eastasia.py`，默认输出本目录。
V1目录作为早期原型保留。
