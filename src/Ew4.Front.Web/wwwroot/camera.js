'use strict';
(function (root) {
  function constrain(view, viewport, bounds) {
    const width = viewport.w, height = viewport.h;
    if (!(width > 0 && height > 0 && bounds.width > 0 && bounds.height > 0)) return view;
    const minimum = Math.max(width / bounds.width, height / bounds.height);
    const maximum = Math.max(2.5, minimum);
    const zoom = Math.min(maximum, Math.max(minimum, Number.isFinite(view.zoom) ? view.zoom : minimum));
    const halfWidth = width / (2 * zoom), halfHeight = height / (2 * zoom);
    const left = bounds.left || 0, top = bounds.top || 0;
    const x = Number.isFinite(view.x) ? view.x : left + bounds.width / 2;
    const y = Number.isFinite(view.y) ? view.y : top + bounds.height / 2;
    return {
      x: Math.max(left + halfWidth, Math.min(left + bounds.width - halfWidth, x)),
      y: Math.max(top + halfHeight, Math.min(top + bounds.height - halfHeight, y)), zoom
    };
  }
  root.Ew4Camera = Object.freeze({constrain});
})(globalThis);
