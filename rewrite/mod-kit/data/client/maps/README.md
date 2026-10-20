# 地形、底图和剧本：三个独立层

| 层 | 原格式输入 | rewrite输出/作用 |
| --- | --- | --- |
| 地形网格 | def_map.xml的file，如europe.bin | original内YSAE网格，影响移动和地形解析 |
| 画面底图 | tile指向map1.bin，HD为map1_hd.bin；map/map1_*@2x.pvr | 本目录europe/*.png及manifest.maps.europe.tiles |
| 剧本 | conquest1.btl等 | 初始国家/部队/城市/事件/范围；辖区优先读取该BTL的JURI，无扩展才自动划分，见[制作指南](../../../docs/porting/BTL_JURISDICTIONS.md) |

改PNG只能换画面，不能让海变陆；改地形需同步画面；改BTL不能自动生成新地图。

## 原格式地图配套

以europe为例：`def_map.xml`记录id1、name=europe、file=europe.bin、tile=map1.bin、textpos=maptextpos_europe.xml。HD输入使用map1_hd.bin及map/map1_1@2x.pvr等图集；不要混用SD坐标和HD图。

转换器`ConvertMaps()`按62逻辑像素步长读取块索引，按512逻辑像素输出PNG块，HD pixelRatio为2。PVR每图16槽、RGBA4444格式；当前不是通用任意PVR解码器。地形头中宽/高在8/12，记录12字节，`LegacyTerrainMap.Read()`最终校验；不要把块索引当YSAE编辑。

只改某个索引/PVR时需提供重建所需整套原索引与被引用图集；基础运行目录只保存转换结果，不保证保留全部原PVR供回退。

## 已转换覆盖包

`manifest.maps.europe`有width/height/pixelRatio/tiles/overlays/labels。每块记录x/y/width/height/file，例如`maps/europe/0_0.png`。坐标/宽高是逻辑单位，PNG像素受pixelRatio影响；边缘块不一定满512。换同尺寸块可只覆盖PNG，重新分块要同步完整manifest。

overlays为地形叠层，含cell/layer/seaForeground/image/x/y；labels来自原地图文本位置。归属线及普通地块染色由运行状态绘制，不存在“给底图某区域填色即可永久改变国家”机制。

新增地图还需def_map登记及BTL合法MapId；`ClientContent`构造函数会加载登记的全部地图，不是只检查当前关卡。未使用但损坏的地图也可能导致导入失败。
