# 动作库、特效绑定及音画时间轴

## 兵种如何找到动作

`manifest.profiles["ew4.unit.fra.militia.0"]`实际包含`library:"army_militia"`、`x:-34`、`y:-47`、`scale:1`和motions的ready/attack/reload/finish。动作名必须精确匹配`animations/army_militia.json`的items[].name。

库JSON包含fps/items/elements/atlas；item的duration为帧数，layers包含key关键帧，indices引用element/子item。配套atlas.textures指向`images/`，sprites保存图集坐标。修改名称需同步profile和所有引用，不靠文件名自动推断。

原格式输入由`def_motion.xml`指定兵种res、scale、x/y及动作名，BILE v3的`army_militia.bin`等和对应XML转换为本目录JSON。HD库优先选择`*_hd.bin`及相应资源。`LegacyModImporter.ConvertProfiles()/ConvertAnimations()`负责；`ResourceValidation.CheckAnimation()`验证帧率、引用、循环及32层嵌套限制。

## effects.json具体节点

| 节点 | 作用 |
| --- | --- |
| bindings | profile ID到动作定义名，如Militia fra 1 |
| motions | 原def_motion记录及ready/attack/reload/finish的effect引用 |
| timelines | 效果名到事件数组，事件含type/name/at/x/y/rot/sound；at是秒 |
| definitions | 粒子effect定义，含life及parameters、timetrack/lifetrack等 |
| sounds | 允许播放的音效文件名列表 |

`Effects.motion()`找方向动作，`timeline()`排程，`emit()`生成粒子，`sound()`播放声音，`trail()`控制移动灰尘数量。原XML粒子文件需要通过导入器生成，不能直接放一个effect_foo.xml到client并期望读取。

## 攻击同步与伤害

`app.playCombat()`先排程双方动作，结合`firstEvent()/lastEvent()`同步首发及命中；不要只给某兵种手加延迟，会再次造成先反击后攻击。前端只表现后端CombatResult；`Ew4Combat.Calculate()`判伤害和致死禁反击。改动画不能改战果。

招募下落、死亡和伤害浮字部分由前端状态驱动，不全是BILE item：`app.beginDeath()/playMovements()`、`DamageNumbers.add()/frame()`也是相关入口。调整fps可能影响动作时长，需核对开火、命中、声音、死亡和跳过演出。
