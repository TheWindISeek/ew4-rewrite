'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base = process.argv[2] || 'http://127.0.0.1:5224';
const folder = path.resolve(__dirname, '../rewrite/reports/commander-exit-20261021');
(async () => {
  await fs.mkdir(folder, { recursive: true });
  const report = { checks: 0, errors: [], missing: [], screenshots: [] };
  const check = (value, label) => { assert.ok(value, label); report.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', error => report.errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) report.missing.push(response.url()); });
  try {
    await page.goto(base);
    await page.waitForFunction(() => startupReady);
    const held = await page.evaluate(async () => {
      let data = await rpc('headquarters');
      for (const commander of data.offers[0].slice(0, 2))
        data = await rpc('commander', { op: 'recruit', revision: data.revision, commander });
      return data.roster[0];
    });
    await page.evaluate(async id => {
      await start('conquest1', 'fra');
      const unit = state.units.find(u => u.owner === state.playerFaction && !u.commanderId);
      await act('deploy', { unit: unit.id, commander: id });
      await rpc('save', { slot: 1, mode: 2 });
      await menus.headquarters();
    }, held.id);
    check(await page.locator('#hq-roster .commander-card[data-locked=true]').count() === 1, 'active battle retains one deployment lock');
    check(await page.locator('#hq-roster .commander-card[data-locked=true]').evaluate(el => getComputedStyle(el).filter) === 'brightness(0.65)', 'active battle uses intentional dim state');
    await page.screenshot({ path: path.join(folder, 'active-battle-lock.png') });
    report.screenshots.push('active-battle-lock.png');
    // Restore the battle surface so the actual pause-menu exit is exercised.
    await page.evaluate(() => { menus.closeRoster(); document.getElementById('menu').hidden = true; });
    await page.locator('#menu-button').click();
    await page.locator('#return-menu').click();
    await page.waitForFunction(() => !state && !document.getElementById('main-menu').hidden && !busy);
    check(await page.locator('#pause').isHidden(), 'exit dismisses pause panel');
    check(await page.evaluate(() => !tutorial && !selected && !selection), 'exit clears frontend battle state');
    await page.locator('#headquarters').click();
    await page.waitForSelector('#hq-screen:visible');
    for (const [name, width, height] of [['desktop', 1280, 720], ['landscape', 844, 390], ['portrait', 390, 844]]) {
      await page.setViewportSize({ width, height });
      await page.waitForFunction(() => [...document.querySelectorAll('#hq-roster img')].every(img => img.complete && img.naturalWidth));
      const cards = page.locator('#hq-roster .commander-card:not(.empty-commander)');
      check(await cards.count() === 2, name + ' owned roster preserved');
      check(await page.locator('#hq-roster [data-locked=true]').count() === 0, name + ' no lingering deployment locks');
      check(await cards.evaluateAll(els => els.every(el => getComputedStyle(el).filter === 'none')), name + ' portraits have full brightness');
      check(await page.locator('#hq-roster .promote-commander').count() === 2, name + ' promotion controls restored');
      await page.screenshot({ path: path.join(folder, name + '-released.png') });
      report.screenshots.push(name + '-released.png');
    }
    await page.locator('#hq-back').click();
    for (const slot of [0, 1]) {
      await page.evaluate(slot => load(slot, 2), slot);
      check(await page.evaluate(id => state.units.some(u => u.commanderId === id), held.id), 'load preserves deployed commander, slot ' + slot);
      const hq = await page.evaluate(() => rpc('headquarters'));
      check(hq.roster.find(c => c.id === held.id).locked, 'load restores in-battle lock, slot ' + slot);
      await page.locator('#menu-button').click();
      await page.locator('#return-menu').click();
      await page.waitForFunction(() => !state && !busy);
      check((await page.evaluate(() => rpc('headquarters'))).roster.every(c => !c.locked), 'repeated exit releases lock, slot ' + slot);
    }
    await page.evaluate(() => start('conquest1', 'fra'));
    await page.route('**/api/leave?*', route => route.fulfill({ json: { ok: false, code: 'client.failure', message: 'test exit failure' } }));
    await page.locator('#menu-button').click();
    await page.locator('#return-menu').click();
    await page.waitForFunction(() => !busy && document.getElementById('notice').textContent === 'test exit failure');
    check(await page.evaluate(() => !!state && !document.getElementById('play').hidden), 'failed exit keeps active battle available');
    await page.unroute('**/api/leave?*');
    await page.locator('#return-menu').click();
    await page.waitForFunction(() => !state && !busy);
    check(await page.locator('#main-menu').isVisible(), 'failed exit can be retried');
    check(report.errors.length === 0, 'no browser exceptions');
    check(report.missing.length === 0, 'no missing assets');
  } catch (error) { report.failure = error.message; process.exitCode = 1; }
  finally { await fs.writeFile(path.join(folder, 'browser.json'), JSON.stringify(report, null, 2)); await browser.close(); }
  console.log(JSON.stringify(report));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
