# 音效与绑定

此目录为短音效，如枪声、炮声等。覆盖路径`client/sounds/<原文件名>`；保留编码兼容性和文件名，不只改扩展名。

播放由`effects.json.sounds`白名单及timelines事件的sound字段共同决定。新增文件还需写入列表、绑定时间轴；只复制wav不会自动播放。原资源导入将wav/ogg转存到sounds，mp3转存到music。

执行入口`effects.js`的`Effects.sound()`，音量取当前设置。浏览器可能需要首次用户操作才允许声音；核对开火/命中时序，不因静音就修改伤害或AI逻辑。
