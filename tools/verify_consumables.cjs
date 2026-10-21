'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder = path.resolve(__dirname, '../rewrite/reports/consumables-20261021');
const base = process.argv[2] || 'http://127.0.0.1:5220';
(async () => {
  const report = { checks: 0, errors: [], missing: [], screenshots: [] };
  const check = (value, label) => { assert.ok(value, label); report.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1136, height: 640 } });
  page.on('pageerror', error => report.errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) report.missing.push([response.status(), response.url()]); });
  const shot = async name => { await page.screenshot({ path: path.join(folder, name + '.png') }); report.screenshots.push(name + '.png'); };
  try {
    const fixture = JSON.parse(await fs.readFile(path.join(folder, 'backend.json'), 'utf8'));
    report.target = fixture.target;
    await page.goto(base);
    await page.waitForFunction(() => catalog && document.documentElement.dataset.fontsReady === 'true');
    await page.evaluate(() => menus.saveMenu(false, 2));
    await page.locator('.save-slot-confirm:not(:disabled)').first().click();
    await page.waitForFunction(() => state?.scenario === 'conquest1');
    await page.evaluate(async cell => { await selectCell(cell, true); view.x=point(cell).x;view.y=point(cell).y;requestRender(); }, fixture.target.cell);
    const chest = page.getByRole('button', { name: '使用物品', exact: true });
    check(await chest.isVisible(), 'original chest is visible for a unit without a commander');
    check((await chest.locator('img').getAttribute('src')).includes('button_items.png'), 'chest uses original asset');
    await chest.click();
    await page.locator('[data-layout="form_useitem"] .native-item').last().waitFor();
    check(await page.locator('[data-layout="form_useitem"] .native-item').count() === 6, 'all six consumable slots rendered');
    check((await page.locator('#detail-confirm').getAttribute('aria-label')).includes('美酒'), 'first usable item auto selected');
    await shot('items-desktop');
    await page.setViewportSize({ width: 844, height: 390 });
    await shot('items-landscape');
    await page.setViewportSize({ width: 390, height: 844 });
    await shot('items-portrait');
    for (const element of ['.native-dialog', '[data-native-id="group_desc"]', '#detail-confirm']) {
      const b = await page.locator(element).boundingBox();
      check(b && b.x >= -1 && b.y >= -1 && b.x+b.width <= 391 && b.y+b.height <= 845, 'mobile bounds ' + element);
    }
    await page.getByRole('button', { name: '使用 美酒', exact: true }).click();
    await page.waitForFunction(id => state.units.find(u=>u.id===id).morale === 0, fixture.target.id);
    check(true, 'wine restores negative morale through real UI');
    const use = async (id, name) => {
      await page.getByRole('button', { name: '使用物品', exact: true }).click();
      const item = page.locator('[data-layout="form_useitem"]').getByRole('button', { name: new RegExp('^'+name+' × ') });
      await item.scrollIntoViewIfNeeded();await item.click();
      await page.getByRole('button', { name: '使用 '+name, exact: true }).click();
      await page.waitForFunction(() => document.getElementById('detail-modal').hidden);
      const menu=await page.evaluate(unit=>rpc('items',{unit}),fixture.target.id);
      check(menu.items.find(i=>i.id===id).quantity===2, name+' consumes exactly one copy');
    };
    await use('ew4.item.12', '烈酒');
    check(await page.evaluate(id=>state.units.find(u=>u.id===id).morale===1,fixture.target.id), 'spirit raises actual morale');
    await use('ew4.item.13', '医药包');
    check(await page.evaluate(id=>state.units.find(u=>u.id===id).health===66,fixture.target.id), 'medical kit heals actual unit by 65');
    await use('ew4.item.53', '金星奖章');
    check(await page.evaluate(id=>state.units.find(u=>u.id===id).veterancy==='1',fixture.target.id), 'star advances actual unit level');
    await page.getByRole('button', { name: '使用物品', exact: true }).click();
    await page.getByRole('button', { name: /^烈酒 × / }).click();
    check(await page.locator('#detail-confirm').isDisabled(), 'spirit disabled at high morale');
    await page.locator('#close-detail').click();
    const before=await page.evaluate(id=>({unit:state.units.find(u=>u.id===id),revision:state.revision}),fixture.target.id);
    await page.reload();
    await page.waitForFunction(() => catalog && document.documentElement.dataset.fontsReady === 'true');
    await page.evaluate(() => menus.saveMenu(false,2));
    await page.locator('.save-slot-confirm:not(:disabled)').first().click();
    await page.waitForFunction(() => state?.scenario==='conquest1');
    check(await page.evaluate(expected=>{
      const u=state.units.find(u=>u.id===expected.unit.id);
      return u.health===expected.unit.health&&u.veterancy===expected.unit.veterancy&&u.morale===1;
    },before), 'effect persists across browser reload and real save menu');
    const menu=await page.evaluate(unit=>rpc('items',{unit}),fixture.target.id);
    check(['ew4.item.11','ew4.item.12','ew4.item.13','ew4.item.53'].every(id=>menu.items.find(i=>i.id===id).quantity===2), 'consumed inventory persists');
    check(report.errors.length===0, 'no browser exceptions');check(report.missing.length===0, 'no missing assets');
  } catch(error) { report.failure=error.message;process.exitCode=1; }
  finally { await fs.writeFile(path.join(folder,'browser.json'),JSON.stringify(report,null,2));await browser.close(); }
  console.log(JSON.stringify(report));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
