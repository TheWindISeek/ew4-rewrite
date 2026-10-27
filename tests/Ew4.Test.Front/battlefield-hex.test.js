import {describe, expect, it} from 'vitest';
import {loadWwwrootScript} from './load-wwwroot-script.js';

loadWwwrootScript('js/map-surface.js');
loadWwwrootScript('js/appearance.js');
const {Ew4Battlefield} = loadWwwrootScript('js/battlefield.js');

describe('Ew4Battlefield hex helpers', () => {
  const width = 8;

  it('point places odd rows staggered', () => {
    expect(Ew4Battlefield.point(0, width)).toEqual({x: 0, y: -18});
    expect(Ew4Battlefield.point(width, width)).toEqual({x: 32, y: 36});
  });

  it('neighbor returns -1 off map', () => {
    expect(Ew4Battlefield.neighbor(0, 3, width, 4)).toBe(-1);
  });

  it('neighbor east from (0,0)', () => {
    expect(Ew4Battlefield.neighbor(0, 0, width, 4)).toBe(1);
  });

  it('moraleSprite maps effective morale to atlas frames', () => {
    expect(Ew4Battlefield.moraleSprite(1)).toBe('morale_up.png');
    expect(Ew4Battlefield.moraleSprite(0)).toBeNull();
    expect(Ew4Battlefield.moraleSprite(-1)).toBe('morale_down1.png');
    expect(Ew4Battlefield.moraleSprite(-2)).toBe('morale_down2.png');
    expect(Ew4Battlefield.moraleSprite(-3)).toBe('morale_down3.png');
  });
});
