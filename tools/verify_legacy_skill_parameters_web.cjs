'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root = path.resolve(__dirname, '..');
const folder = path.join(root, 'rewrite/reports/legacy-skill-parameters-20261021');
const url = 'http://127.0.0.1:5228';

(async () => {
  const fixture = JSON.parse(await fs.readFile(path.join(folder, 'import-checks.json'), 'utf8'));
  const result = { checks: 0, errors: [], missing: [], screenshots: [], url };
  const check = (value, label) => { assert(value, label); result.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', error => result.errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) result.missing.push([response.status(), response.url()]); });
  const ready = () => page.waitForFunction(() => typeof originalTextRenderer !== 'undefined' && originalTextRenderer && catalog);
  const api = async (route, data) => {
    const response = await page.request.post(url + '/api/' + route, { data });
    check(response.ok(), route + ' HTTP');
    const body = await response.json();
    check(body.ok, route + ': ' + (body.message || 'ok'));
    return body.data;
  };
  try {
    await page.goto(url); await ready();
    const builtin = await api('start', { scenario: 'conquest1', country: 'fra' });
    const response = await page.request.post(url + '/api/mod-import', { multipart: {
      id: 'legacy-skill-parameters', name: 'Original skill parameter check',
      file: { name: 'legacy-skill-parameters.ew4mod', mimeType: 'application/zip', buffer: await fs.readFile(fixture.archive) }
    } });
    check(response.ok(), 'package upload');
    let status;
    for (let attempt = 0; attempt < 120; attempt++) {
      status = await (await page.request.get(url + '/api/mods')).json();
      if (status.import.status === 'error') throw new Error(status.import.message);
      if (status.import.status === 'complete') break;
      await page.waitForTimeout(500);
    }
    check(status.import.status === 'complete', 'package imported');
    const activated = await page.request.post(url + '/api/mods', { data: { op: 'activate', key: status.import.key } });
    check(activated.ok(), 'activate parameter package');
    await page.reload(); await ready();
    await page.evaluate(async () => { await start('conquest1', 'fra'); });
    await page.waitForFunction(() => state && !busy);
    const configured = await page.evaluate(() => state);
    const engineers = configured.units.filter(unit => unit.category === 'artillery' && unit.skills.some(skill => skill.id === 'ew4.skill.6'));
    check(engineers.length > 0, 'scenario contains artillery with engineering');
    check(engineers.every(unit => unit.remainingMovement === builtin.units.find(old => old.id === unit.id).remainingMovement + 2), 'JSON movement bonus 4 replaces native 2 through live HTTP');
    check(configured.units.length === builtin.units.length, 'numeric package preserves scenario units');
    await api('save', { mode: 2, slot: 1 });
    const loaded = await api('load', { mode: 2, slot: 1 });
    check(engineers.every(unit => loaded.units.find(saved => saved.id === unit.id).remainingMovement === unit.remainingMovement), 'configured movement survives actual client save/load');
    for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
      await page.setViewportSize(viewport);
      await page.waitForFunction(() => !busy);
      const name = `skill-parameters-${viewport.width}x${viewport.height}.png`;
      await page.screenshot({ path: path.join(folder, name) }); result.screenshots.push(name);
      check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'viewport has no horizontal page overflow');
    }
    check(result.errors.length === 0 && result.missing.length === 0, 'no browser exceptions or missing resources');
  } finally {
    await browser.close();
    await fs.writeFile(path.join(folder, 'browser-checks.json'), JSON.stringify(result, null, 2) + '\n');
  }
  console.log(JSON.stringify(result));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
