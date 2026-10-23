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
  function defaultPackedColor(id) {
    let hash = 2166136261;
    for (let i = 0; i < id.length; i++) hash = Math.imul(hash ^ id.charCodeAt(i), 16777619);
    const r = hash & 255, g = (hash >>> 8) & 255, b = (hash >>> 16) & 255;
    return r | (g << 8) | (b << 16) | (255 << 24);
  }
  function factionColor(faction) {
    const color = faction?.color;
    if (color) {
      const alpha = (color >>> 24) & 255;
      if (alpha && (color & 0xffffff)) return color;
    }
    return defaultPackedColor(faction?.id || '');
  }
  root.Ew4Appearance = { flagUrl, factionColor, defaultPackedColor };
})(globalThis);
