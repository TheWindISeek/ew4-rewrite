import {describe, expect, it} from 'vitest';
import {loadWwwrootScript} from './load-wwwroot-script.js';

const {Ew4Camera} = loadWwwrootScript('js/camera.js');

describe('Ew4Camera', () => {
  it('constrain keeps view inside bounds', () => {
    const view = {x: 0, y: 0, zoom: 2};
    const bounds = {left: 10, top: 20, width: 100, height: 80};
    const viewport = {w: 200, h: 150};
    const next = Ew4Camera.constrain(view, viewport, bounds);
    expect(next.x).toBeGreaterThanOrEqual(bounds.left);
    expect(next.y).toBeGreaterThanOrEqual(bounds.top);
    expect(next.zoom).toBe(2);
  });
});
