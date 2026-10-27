import {describe, expect, it} from 'vitest';
import {loadWwwrootScript} from './load-wwwroot-script.js';

const {Ew4SaveFormat} = loadWwwrootScript('js/save-format.js');

describe('Ew4SaveFormat', () => {
  it('formatSavedAt uses local calendar parts', () => {
    const text = Ew4SaveFormat.formatSavedAt('2024-03-05T14:07:00.000Z');
    expect(text).toMatch(/2024\/03\/05/);
    expect(text).toContain('\n');
  });

  it('panelStyle offsets title band', () => {
    expect(Ew4SaveFormat.panelStyle({x: 10, y: 50, w: 112, h: 80})).toEqual({
      left: '10px',
      top: '23px',
      width: '112px',
      height: '80px',
    });
  });

  it('slotConfirmDisabled respects autosave and corrupt slots', () => {
    expect(Ew4SaveFormat.slotConfirmDisabled(true, {slot: 0, exists: true})).toBe(true);
    expect(Ew4SaveFormat.slotConfirmDisabled(true, {slot: 1, exists: false})).toBe(false);
    expect(Ew4SaveFormat.slotConfirmDisabled(false, {slot: 1, exists: false, corrupt: false})).toBe(true);
    expect(Ew4SaveFormat.slotConfirmDisabled(false, {slot: 1, exists: true, corrupt: true})).toBe(true);
    expect(Ew4SaveFormat.slotConfirmDisabled(false, {slot: 1, exists: true, corrupt: false})).toBe(false);
  });
});
