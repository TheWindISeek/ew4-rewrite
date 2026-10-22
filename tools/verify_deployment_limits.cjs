'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5341',folder=path.resolve('rewrite/reports/deployment-roster-20261022');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[]},check=(v,s)=>{assert.ok(v,s);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:844,height:390},hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const mods=body=>fetch(base+'/api/mods',{method:'POST',body:JSON.stringify(body)}).then(r=>r.json());
  try{
    await mods({op:'activate',key:null});
    await page.goto(base);await page.waitForFunction(()=>startupReady);
    const baseline=await page.evaluate(async()=>{
      await start('conquest1','fra');const unit=state.units.find(u=>u.owner===state.playerFaction&&!u.commanderId&&state.cells[u.cell].owner===u.owner);
      return rpc('deployment',{unit:unit.id,revision:state.revision});
    });
    const form=new FormData();form.set('file',new Blob([await fs.readFile('build/deployment-roster-20261022/deployment-limits.ew4mod')]),'deployment-limits.ew4mod');
    const uploaded=await fetch(base+'/api/mod-import',{method:'POST',body:form});check(uploaded.ok,'package upload succeeds');
    let status;
    for(let i=0;i<200;i++){status=await mods({});if(status.import.status!=='working')break;await new Promise(r=>setTimeout(r,100));}
    const installed=status.mods.find(m=>m.id==='deployment-limits');check(!!installed,'package imported in Web host');
    check((await mods({op:'activate',key:installed.key})).active===installed.key,'imported policy activates');
    await page.reload();await page.waitForFunction(()=>startupReady);
    await page.evaluate(async()=>{
      await menus.headquarters();if(!menus.data.roster.length)await menus.updateAccount({op:'recruit',commander:menus.data.offers[0][0]});
      await start('conquest1','fra');const unit=state.units.find(u=>u.owner===state.playerFaction&&!u.commanderId&&state.cells[u.cell].owner===u.owner);
      await menus.deployGeneral(unit.id,state.revision);
    });
    check(await page.evaluate(()=>menus.deploymentQuota.limit===13),'real mod quota reaches deployment menu');
    check(await page.locator('#hq-deployment-quota .original-text').getAttribute('aria-label')==='0 / 13','menu displays configured maximum');
    const commander=await page.locator('#hq-roster [data-commander]').first().getAttribute('data-commander');
    await page.locator('#hq-roster .commander-open').first().click();await page.locator('#hq-roster .deploy-card-confirm').first().click();
    await page.waitForFunction(id=>state.units.some(u=>u.commanderId===id),commander);
    check(await page.evaluate(id=>state.units.some(u=>u.commanderId===id),commander),'actual deployment succeeds under imported policy');
    await page.evaluate(async()=>{const unit=state.units.find(u=>u.owner===state.playerFaction&&!u.commanderId&&state.cells[u.cell].owner===u.owner);await menus.deployGeneral(unit.id,state.revision);});
    check(await page.locator('#hq-deployment-quota .original-text').getAttribute('aria-label')==='1 / 13','used count updates after real appointment');
    await page.waitForFunction(()=>[...document.querySelectorAll('#hq-roster img')].every(i=>i.complete&&i.naturalWidth));
    await page.screenshot({path:path.join(folder,'mod-limit.png')});
    await mods({op:'activate',key:null});await page.reload();await page.waitForFunction(()=>startupReady);
    const restored=await page.evaluate(async()=>{await start('conquest1','fra');const unit=state.units.find(u=>u.owner===state.playerFaction);return rpc('deployment',{unit:unit.id,revision:state.revision});});
    check(restored.limit===baseline.limit,'switching to builtin restores original maximum');
    check(report.errors.length===0&&report.missing.length===0,'no browser errors or missing assets');
  }finally{await browser.close();await fs.writeFile(path.join(folder,'limits-browser.json'),JSON.stringify(report,null,2));}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
