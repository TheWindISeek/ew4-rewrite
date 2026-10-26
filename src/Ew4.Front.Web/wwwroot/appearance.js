'use strict';
(function(root) {
  function extensionUrl(flagImage) {
    return '/content/extensions/' + flagImage.slice('rewrite/'.length).split('/').map(encodeURIComponent).join('/');
  }
  function flagUrl(faction) {
    if (!faction) return null;
    if (faction.flagImage) return extensionUrl(faction.flagImage);
    const flag = faction.flag || faction.country || faction.id;
    if (!flag) return null;
    return '/content/ui/image_flag_hd/' + encodeURIComponent(flag) + '1.png';
  }
  // 颜色由后端（Ew4.Platform.Content.FactionAppearanceResolve）解析并随势力下发；此处仅为未知势力的占位色。
  const UNKNOWN_COLOR = 197 | (157 << 8) | (28 << 16) | (255 << 24);
  function factionColor(faction) {
    const color = faction?.color;
    if (color) {
      const alpha = (color >>> 24) & 255;
      if (alpha && (color & 0xffffff)) return color;
    }
    return UNKNOWN_COLOR;
  }
  root.Ew4Appearance = { flagUrl, factionColor };
})(globalThis);
