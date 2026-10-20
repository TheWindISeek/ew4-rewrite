'use strict';
const fs = require('node:fs/promises');
const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root = 'E:/Workplace/game/o4mod';
const folder = root + '/rewrite/reports/mod-resolution-ui-20261019';
const url = 'http://127.0.0.1:5185';

(async () => {
  const results = JSON.parse(await fs.readFile(process.argv[2], 'utf8'));
  const packages = results.filter(p => p.success);
  assert(packages.some(p => p.apk.includes('皇凯')) && packages.some(p => p.apk.includes('一战MOD')), 'Both formerly failing Mods must pass headless checks first');
  await fs.mkdir(folder, { recursive: true });
  const report = { checks: 0, errors: [], missing: [], scenes: [], screenshots: [], scope: 'Real imported APK assets, desktop/mobile battlefield render on isolated 5185; no Android or full playthrough' };
  const check = (value, message) => { assert(value, message); report.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', error => report.errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) report.missing.push([response.url(), response.status()]); });
  try {
    await page.goto(url);
    for (const pkg of packages) {
      const status = await page.evaluate(key => fetch('/api/mods', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ op: 'activate', key }) }).then(r => r.json()), pkg.key);
      check(status.active === pkg.key, 'Activate actual imported package: ' + pkg.apk);
      await page.reload();
      await page.waitForFunction(() => typeof catalog !== 'undefined' && !!catalog && !!manifest, {}, { timeout: 90000 });
      const scenes = pkg.apk.includes('大鏖战') ? ['campaign1_01', 'conquest1'] : pkg.apk.includes('皇凯') ? ['campaign3_11', 'conquest4']
        : pkg.apk.includes('一战MOD') ? ['campaign2_01', 'campaign5_02', 'conquest1'] : ['campaign1_01'];
      for (const scene of scenes) {
        await page.setViewportSize({ width: 1280, height: 720 });
        await page.evaluate(async scenario => {
          const countries = scenario.startsWith('conquest') ? await rpc('countries', { scenario }) : null;
          await start(scenario, countries?.[0]?.id);
        }, scene);
        await page.waitForFunction(id => typeof state !== 'undefined' && state?.scenario === id && !busy, scene, { timeout: 60000 });
        await page.waitForFunction(() => [...images.values()].every(i => i.complete), {}, { timeout: 60000 });
        await page.waitForTimeout(250);
        if(pkg.apk.includes('大鏖战'))check(await page.evaluate(()=>{
          const profile=Object.entries(manifest.profiles).find(([,p])=>Number(p.stats.strength)===-999)?.[0];
          return !!profile&&abilityGrid(profile).textContent.includes('一次性');
        }),'Single-use missile displays its weapon state');
        const pixels = await page.evaluate(() => {
          const canvas = document.querySelector('#map');
          const data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
          let opaque = 0; const colors = new Set();
          for (let i = 0; i < data.length; i += 404) { if (data[i + 3]) opaque++; colors.add(data[i] + ',' + data[i + 1] + ',' + data[i + 2]); }
          return { opaque, colors: colors.size, width: canvas.width, height: canvas.height };
        });
        check(pixels.opaque > 100 && pixels.colors > 40, pkg.apk + ' ' + scene + ' nonblank canvas');
        report.scenes.push({ apk: pkg.apk, scene, pixels });
        const name = pkg.key.slice(0, 8) + '-' + scene;
        await page.screenshot({ path: folder + '/' + name + '-desktop.png' }); report.screenshots.push(name + '-desktop');
        await page.setViewportSize({ width: 844, height: 390 }); await page.waitForTimeout(250);
        await page.screenshot({ path: folder + '/' + name + '-mobile.png' }); report.screenshots.push(name + '-mobile');
      }
    }
    check(report.errors.length === 0, 'Browser exceptions: ' + report.errors.join(';'));
    check(report.missing.length === 0, 'Missing resources: ' + JSON.stringify(report.missing));
  } finally {
    await fs.writeFile(folder + '/results.json', JSON.stringify(report, null, 2));
    await browser.close();
  }
  console.log(JSON.stringify(report));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
