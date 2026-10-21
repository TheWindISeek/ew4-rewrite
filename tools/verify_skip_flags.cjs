'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder = path.resolve(__dirname, '../rewrite/reports/skip-flags-20261021');
const base = process.argv[2] || 'http://127.0.0.1:5221';
(async () => {
  await fs.mkdir(folder, { recursive: true });
  const report = { checks: 0, errors: [], missing: [], screenshots: [] };
  const check = (pass, label) => { assert.ok(pass, label); report.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('response', r => { if (r.status() >= 400) report.missing.push(r.url()); });
  const shot = async name => { await page.screenshot({ path: path.join(folder, name + '.png') }); report.screenshots.push(name + '.png'); };
  try {
    await page.goto(base); await page.waitForFunction(() => startupReady);
    await page.evaluate(() => start('conquest1', 'fra'));
    await page.evaluate(() => {
      window.flagProbe = { batches: [], painted: [], frames: 0, done: false };
      const originalRpc = rpc, originalFrame = battlefield.frame;
      rpc = async function (route, args) {
        if (route === 'action' && args.op === 'ai_batch') {
          const active = state.factions.find(f => f.id === state.activeFaction), flag = $('ai-flag');
          flagProbe.batches.push({ faction: active.id, name: active.name, expected: flagSource(active), source: flag.getAttribute('src'), visible: !$('ai-action').hidden, title: flag.title });
        }
        return originalRpc(route, args);
      };
      battlefield.frame = function (...args) { if (skipAi) flagProbe.frames++; return originalFrame.apply(this, args); };
      const sample = () => {
        if (skipAi && !$('ai-action').hidden) {
          const flag = $('ai-flag');
          if (flag.complete && flag.naturalWidth && flagProbe.painted.at(-1)?.source !== flag.getAttribute('src'))
            flagProbe.painted.push({ faction: state.activeFaction, source: flag.getAttribute('src') });
        }
        if (!flagProbe.done) requestAnimationFrame(sample);
      };
      requestAnimationFrame(sample);
      window.flagTurn = (async () => {
        try { await act('end'); }
        finally { flagProbe.done = true; rpc = originalRpc; battlefield.frame = originalFrame; }
      })();
    });
    await page.waitForFunction(() => aiRunning && !flagProbe.done);
    await page.locator('#skip-ai').click();
    await page.waitForFunction(() => flagProbe.painted.length >= 2, null, { timeout: 90000 });
    await shot('desktop-progress');
    for (const size of [{ width: 844, height: 390 }, { width: 390, height: 844 }]) {
      await page.setViewportSize(size);
      await page.waitForFunction(() => {
        const el = $('ai-action'), flag = $('ai-flag'), r = el.getBoundingClientRect();
        return !el.hidden && r.width > 0 && r.height > 0 && r.x >= 0 && r.y >= 0 && r.right <= innerWidth + 1 && r.bottom <= innerHeight + 1 && flag.complete && flag.naturalWidth > 0;
      });
      const visible = await page.locator('#ai-action').evaluate(el => {
        const r = el.getBoundingClientRect(), flag = document.getElementById('ai-flag');
        return !el.hidden && r.width > 0 && r.height > 0 && r.x >= 0 && r.y >= 0 && r.right <= innerWidth + 1 && r.bottom <= innerHeight + 1 && flag.complete && flag.naturalWidth > 0;
      });
      check(visible, 'progress flag fits ' + size.width + 'x' + size.height);
      await shot('mobile-' + size.width);
    }
    await page.evaluate(() => flagTurn);
    report.progress = await page.evaluate(() => ({ ...flagProbe, active: state.activeFaction, player: state.playerFaction, phase: state.phase, dialogue: state.dialogue, hidden: $('ai-action').hidden, busy, aiRunning, skipAi }));
    const progress = report.progress;
    check(progress.batches.length > 1, 'real accelerated AI turn exercised');
    check(progress.batches.every(b => b.source === b.expected && b.title === b.name && b.visible), 'every batch shows the current faction flag and name');
    const expected = [...new Set(progress.batches.map(b => b.faction))];
    check(expected.length > 2, 'multiple AI factions executed');
    check(expected.every(id => progress.painted.some(p => p.faction === id)), 'each acting faction flag reaches a browser animation frame');
    check(progress.frames === 0, 'skip retains zero intermediate battlefield draws');
    check(progress.active === progress.player && progress.hidden && !progress.busy && !progress.aiRunning && !progress.skipAi, 'player turn restores controls and hides AI indicator');
    check(await page.evaluate(async () => {
      const full = await rpc('action', { op: 'state' });
      return full.revision === state.revision && JSON.stringify(full.cells) === JSON.stringify(state.cells);
    }), 'final delta reconstruction matches backend');
    check(report.errors.length === 0 && report.missing.length === 0, 'no browser or resource errors');
    console.log(JSON.stringify({ checks: report.checks, factions: expected, painted: progress.painted.length, skippedFrames: progress.frames, errors: report.errors.length, missing: report.missing.length }));
  } finally {
    await fs.writeFile(path.join(folder, 'browser.json'), JSON.stringify(report, null, 2));
    await browser.close();
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
