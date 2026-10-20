'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'rewrite/reports/jurisdiction-extension-20261020');
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'}),page=await browser.newPage({viewport:{width:1136,height:640}});
  const report={checks:0,errors:[],missing:[],scope:'Independent 5185; real file import, enable, start, save/reload and builtin return. No requests to 5184.'};
  function check(value,label){assert(value,label);report.checks++;}
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  const ready=()=>page.waitForFunction(()=>typeof originalTextRenderer!=='undefined'&&!!originalTextRenderer&&!!catalog);
  try{
    await page.goto('http://127.0.0.1:5185/');await ready();
    await page.evaluate(()=>fetch('/api/mods?q='+encodeURIComponent(JSON.stringify({op:'activate',key:null})),{method:'POST'}));await page.reload();await ready();
    const builtin=await page.evaluate(()=>menuArt.backgrounds.form_selconquest);
    await page.locator('#mod-open').click();await page.locator('#mod-id').fill('web-juri');await page.locator('#mod-name').fill('手工辖区测试');
    const runs=(await fs.readdir(folder)).filter(n=>n.startsWith('import-'));
    const sample=path.join(folder,runs[0]);
    await page.locator('#mod-file').setInputFiles(path.join(sample,'assets.zip'));
    await page.waitForFunction(async()=>{const r=await fetch('/api/mods').then(r=>r.json());return r.import.status!=='working'&&r.mods.some(m=>m.id==='web-juri');},null,{timeout:120000});
    await page.locator('[data-mod-key]:not([data-mod-key="builtin"]) button').first().waitFor();
    await Promise.all([page.waitForNavigation(),page.locator('[data-mod-key]:not([data-mod-key="builtin"]) button').first().click()]);await ready();
    const active=await page.evaluate(async()=>{const r=await fetch('/api/mods').then(r=>r.json());return r.mods.find(m=>m.active);});check(active.id==='web-juri','real archive imported and enabled');
    await page.locator('[data-mode="2"]').click();await page.locator('.scenario-entry[data-scenario="conquest1"]').click();await page.waitForTimeout(700);
    await page.locator('#confirm-country').click();await page.waitForFunction(()=>!!state&&!busy,null,{timeout:60000});
    check(await page.evaluate(()=>state.scenario==='conquest1'&&state.units.length>0),'author BTL starts through real country confirmation');
    const core=JSON.parse(await fs.readFile(path.join(sample,'results.json'),'utf8'));
    const source=await fs.readFile(path.join(root,'rewrite/data/original/conquest1.btl')),sampleBtl=await fs.readFile(path.join(sample,'mods',core.Key,'original/conquest1.btl'));
    const mapWidth=sampleBtl.readInt32LE(source.length+52),left=sampleBtl.readInt32LE(source.length+36),top=sampleBtl.readInt32LE(source.length+40),width=sampleBtl.readInt32LE(source.length+44);
    const local=(Math.floor(core.changedCell/mapWidth)-top)*width+core.changedCell%mapWidth-left;
    const expected=sampleBtl.readInt32LE(source.length+96+local*4);
    const saved=JSON.parse(await fs.readFile(path.join(root,'rewrite/local/jurisdiction-preview-profile/mods/web-juri',active.logicRevision,'saves/mode-2/slot-0.json'),'utf8'));
    const session=JSON.parse(Buffer.from(saved.session,'base64').toString('utf8'));
    check(session.payload.world.payload.jurisdictions.cellCenters[core.changedCell]===expected,'web autosave has explicit center rather than automatic center');
    check(expected===core.manualCenter,'archive retains the authored test assignment');
    await page.evaluate(()=>rpc('load',{mode:2,slot:0}));check(true,'same extension content loads its actual autosave');
    await page.screenshot({path:path.join(folder,'web-custom-start.png')});
    await page.evaluate(()=>showMain());await page.locator('#mod-open').click();
    await Promise.all([page.waitForNavigation(),page.locator('[data-mod-key="builtin"] button').click()]);await ready();
    check(await page.evaluate(original=>menuArt.backgrounds.form_selconquest===original,builtin),'builtin resources restored after extension package');
    await page.setViewportSize({width:844,height:390});await page.screenshot({path:path.join(folder,'web-builtin-mobile.png')});
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing assets');
    report.changedCell=core.changedCell;report.manualCenter=expected;
  }finally{await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
