'use strict';
(function (root) {
  function pad2(n) {
    return String(n).padStart(2, '0');
  }
  function formatSavedAt(savedAt) {
    const d = new Date(savedAt);
    return d.getFullYear() + '/' + pad2(d.getMonth() + 1) + '/' + pad2(d.getDate()) + '\n' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
  }
  function panelStyle(attrs, titleBandOffset = 27) {
    return {
      left: attrs.x + 'px',
      top: (Number(attrs.y) - titleBandOffset) + 'px',
      width: attrs.w + 'px',
      height: attrs.h + 'px',
    };
  }
  function slotConfirmDisabled(saving, slot) {
    return saving ? slot.slot === 0 : !slot.exists || slot.corrupt;
  }
  root.Ew4SaveFormat = Object.freeze({formatSavedAt, panelStyle, slotConfirmDisabled});
})(globalThis);
