'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createRequire } = require('node:module');
const { chromium } = createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder = path.resolve(__dirname, '../rewrite/reports/fort-construction-20261021');
const runtime = path.resolve(__dirname, '../build/fort-construction-20261021/profile');
const base = process.argv[2] || 'http://127.0.0.1:5241';
(async () => {
  await fs.mkdir(runtime, { recursive: true });
  const fixture = JSON.parse(await fs.readFile(path.join(folder, 'backend.json'), 'utf8'));
  const report = { checks: 0, errors: [], missing: [], stages: [], screenshots: [] };
  const check = (pass, label) => { assert.ok(pass, label); report.checks++; };
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('response', r => { if (r.status() >= 400) report.missing.push(r.url()); });
  const shot = async name => { await page.screenshot({ path: path.join(folder, name + '.png') }); report.screenshots.push(name + '.png'); };
  const seed = async stage => {
    await fs.copyFile(path.join(folder, stage.name + '.json'), path.join(runtime, 'slot-4.json'));
    await page.evaluate(async stage => {
      await installState(await rpc('load', { slot: 4 }), true);
      await selectCell(stage.cell, true);
      const p = point(stage.cell); view.x = p.x; view.y = p.y; view.zoom = 2; clampView(); requestRender();
    }, stage);
  };
  try {
    await page.goto(base); await page.waitForFunction(() => startupReady);
    for (const stage of fixture.stages) {
      await seed(stage);
      const result = await page.evaluate(stage => {
        const unit = state.units.find(u => u.id === stage.id), profile = manifest.profiles[unit.profile];
        const lib = libraries.get(profile.library), p = point(unit.cell);
        const canvas = document.createElement('canvas'); canvas.width = 400; canvas.height = 400;
        const d = canvas.getContext('2d');
        const renderer = new Ew4Battlefield.Renderer(battlefield.api), names = [];
        const paint = renderer.unitAnimation;
        renderer.unitAnimation = function(lib, item, tick) { names.push(item?.name); return paint.call(this, lib, item, tick); };
        Object.assign(renderer, {draw:d,state,time:0,orderedUnits:[unit],visibleUnits:[unit],motions:new Map(),
          facings:new Map(),arrivals:new Map(),combatMotions:new Map(),factions:new Map(state.factions.map(f => [f.id,f])),
          visibleRect:{left:p.x-200,top:p.y-200,width:400,height:400}});
        const captures = [];
        for (const facing of [-1,1]) {
          renderer.facings.set(unit.id,facing);
          d.setTransform(1,0,0,1,200-p.x,200-p.y); d.clearRect(p.x-200,p.y-200,400,400); renderer.units();
          const name = names.at(-1), item = lib.byName.get(name), cached = [...renderer.unitFrames.values()].find(c => c.canvas && c.lib === lib && c.pixels > 0);
          const pixels = cached?.canvas.getContext('2d').getImageData(0,0,cached.canvas.width,cached.canvas.height).data;
          let opaque = 0; if (pixels) for(let i=3;i<pixels.length;i+=4) if(pixels[i]>0)opaque++;
          captures.push({facing,name,expected:effects.motion(unit.profile,stage.remaining>0?'build':'ready',facing)?.name,
            opaque,bounds:Ew4Battlefield.animationBounds(lib,item)});
        }
        // Reuse the same renderer cache across the completion boundary.
        const current = renderer.visibleUnits[0]; renderer.visibleUnits = [{...current,constructionTurns:0}];
        renderer.units(); const completed = names.at(-1);
        renderer.visibleUnits = [{...current,constructionTurns:1}]; renderer.units(); const buildingAgain = names.at(-1);
        renderer.visibleUnits = [{...current,constructionTurns:undefined}]; renderer.units(); const legacy = names.at(-1);
        return {remaining:unit.constructionTurns,canAttack:unit.canAttack,canMove:unit.canMove,captures,completed,buildingAgain,legacy};
      }, stage);
      check(result.remaining === stage.remaining, 'authoritative remaining turns ' + stage.name);
      check(result.captures.every(c => c.name === c.expected && c.opaque > 100 && c.bounds), 'correct nonblank original sprites in both directions ' + stage.name);
      if (stage.remaining > 0) check(!result.canMove && !result.canAttack && result.captures.every(c => c.name === 'Building'), 'unfinished fort uses scaffolding without action badge ' + stage.name);
      check(result.completed !== 'Building' && result.buildingAgain === 'Building' && result.legacy === result.completed, 'cache and missing-field fallback switch correctly ' + stage.name);
      report.stages.push({name:stage.name,...result});
      if (stage.remaining === 0 || stage.remaining === fixture.stages.find(s => s.profile === stage.profile).remaining) {
        await page.waitForTimeout(150); await shot(stage.name);
      }
    }
    const unfinished = fixture.stages.find(s => s.remaining > 0);
    for (const size of [{width:844,height:390},{width:390,height:844}]) {
      await page.setViewportSize(size); await seed(unfinished); await page.waitForTimeout(150); await shot('construction-' + size.width);
      check(await page.evaluate(() => {
        const c = [...battlefield.unitFrames.values()].find(c => c.lib.byName.get('Building') && c.pixels > 0);
        const p = point(state.units.find(u => u.id === 'construction.fort').cell);
        const x = (p.x-view.x)*view.zoom+viewport.w/2, y = (p.y-view.y)*view.zoom+viewport.h/2;
        return !!c && x > 0 && x < viewport.w && y > 0 && y < viewport.h;
      }), 'construction is framed on mobile ' + size.width);
    }
    check(report.errors.length === 0 && report.missing.length === 0, 'no script errors or missing assets');
    console.log(JSON.stringify({checks:report.checks,errors:report.errors.length,missing:report.missing.length,stages:report.stages.length,screenshots:report.screenshots.length}));
  } finally {
    await fs.writeFile(path.join(folder,'browser.json'),JSON.stringify(report,null,2)); await browser.close();
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
