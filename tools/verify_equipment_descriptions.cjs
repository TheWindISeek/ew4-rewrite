'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root = path.resolve(__dirname, '..');
const folder = path.join(root, 'rewrite/reports/equipment-fix-20261021');
const base = process.argv[2] || 'http://127.0.0.1:5214';
(async () => {
  const report = { checks: 0, errors: [], missing: [], screenshots: [] };
  const check = (value, label) => { assert.ok(value, label); report.checks++; };
  // Seed only this test server's isolated account with a genuine migrated fixture.
  for (const name of await fs.readdir(path.join(folder, 'saves'))) {
    const file = path.join(folder, 'saves', name, 'account.json');
    const account = JSON.parse(await fs.readFile(file, 'utf8'));
    if (!account.inventory['ew4.item.20']) continue;
    await fs.mkdir(path.join(folder, 'web-saves'), { recursive: true });
    await fs.writeFile(path.join(folder, 'web-saves/account.json'), JSON.stringify(account));
    break;
  }
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1136, height: 640 } });
  page.on('pageerror', error => report.errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) report.missing.push([response.status(), response.url()]); });
  const shot = async name => { await page.screenshot({ path: path.join(folder, name + '.png') }); report.screenshots.push(name + '.png'); };
  try {
    await page.goto(base);
    await page.waitForFunction(() => typeof menus !== 'undefined' && catalog && document.documentElement.dataset.fontsReady === 'true');
    await page.evaluate(() => rpc('start', { scenario: 'conquest1', country: 'fra' }));
    await page.locator('#headquarters').click();
    await page.waitForFunction(() => menus.data?.roster?.length === 1);
    await page.evaluate(() => menus.equipment(menus.data.roster[0].id));
    const rifleName = await page.evaluate(() => menus.data.items.find(i => i.id === 'ew4.item.20').name);
    await page.locator('[data-native-id="lbox_equipitem"]').getByRole('button', { name: rifleName, exact: true }).click();
    check((await page.locator('[data-native-id="text_desc"] .original-text').getAttribute('aria-label')).includes('攻击+8'), 'equipped rifle still displays original +8 description');
    await shot('equipment-desktop');
    await page.locator('[data-native-id="btn_equip"]').click();
    await page.waitForFunction(() => !menus.data.roster[0].items.includes('ew4.item.20'));
    check(await page.locator('[data-native-id="grid_items"]').getByRole('button', { name: rifleName, exact: true }).isVisible(), 'real unequip returns rifle to available inventory');
    await page.locator('[data-native-id="grid_items"]').getByRole('button', { name: rifleName, exact: true }).click();
    await page.locator('[data-native-id="btn_equip"]').click();
    await page.waitForFunction(() => menus.data.roster[0].items.includes('ew4.item.20'));
    check(true, 'real equip transaction restores rifle');
    await page.setViewportSize({ width: 844, height: 390 });
    await page.locator('[data-native-id="lbox_equipitem"]').getByRole('button', { name: rifleName, exact: true }).click();
    await shot('equipment-landscape');
    await page.locator('#close-detail').click();
    await page.evaluate(() => start('conquest1', 'fra'));
    report.deployed = await page.evaluate(async () => {
      for (const unit of state.units.filter(u => u.owner === state.playerFaction && !u.commanderId && state.cells[u.cell].owner === u.owner)) {
        await selectCell(unit.cell, true);
        if (!selection.deploy) continue;
        await menus.deployGeneral(unit.id, state.revision);
        const held = menus.data.roster[0];
        menus.commanderInfo(menus.definition(held), held);
        return { unit: unit.id, cell: unit.cell, commander: held.id };
      }
      throw new Error('No deployment site');
    });
    await page.locator('.commander-equipment .native-command.equip').click();
    await page.waitForFunction(id => state.units.some(u => u.commanderId === id), report.deployed.commander);
    check(true, 'equipped commander deploys through the real roster UI');
    await page.evaluate(async cell => { await selectCell(cell, true); view.x = point(cell).x; view.y = point(cell).y; requestRender(); }, report.deployed.cell);
    report.itemEntry = await page.evaluate(() => ({
      layout: !!manifest.layouts.form_useitem,
      nativeIcon: Object.values(manifest.atlases).some(atlas => !!atlas['button_items.png']),
      buttons: [...document.querySelectorAll('#actions button')].map(b => ({ title: b.title, label: b.getAttribute('aria-label') }))
    }));
    check(report.itemEntry.layout && report.itemEntry.nativeIcon, 'original use-item layout and chest asset are retained');
    check(report.itemEntry.buttons.some(b => /物品|道具/.test(b.title + ' ' + b.label)), 'Rewrite unit toolbar exposes the original chest action');
    await shot('unit-toolbar-landscape');
    await page.evaluate(() => rpc('save', { mode: 2 }));
    await page.reload();
    await page.waitForFunction(() => catalog && document.documentElement.dataset.fontsReady === 'true');
    await page.evaluate(() => menus.saveMenu(false, 2));
    await page.locator('.save-slot-confirm:not(:disabled)').first().click();
    await page.waitForFunction(id => state?.units.some(u => u.commanderId === id), report.deployed.commander);
    check(true, 'equipped deployment survives a real browser reload and continue');
    check(report.errors.length === 0, 'no browser exceptions');
    check(report.missing.length === 0, 'no failed asset requests');
  } catch (error) { report.failure = error.message; process.exitCode = 1; }
  finally {
    await fs.writeFile(path.join(folder, 'browser-verification.json'), JSON.stringify(report, null, 2));
    await browser.close();
  }
  console.log(JSON.stringify(report));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
