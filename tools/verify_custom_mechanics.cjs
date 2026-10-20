'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const dependencies=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=dependencies('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/custom-mechanics';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.setDefaultTimeout(7000);
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'Independent backend on 5185 only'};
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push({url:r.url(),status:r.status()});});
  async function shot(name){await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');}
  async function begin(id,country){
    await page.evaluate(async({id,country})=>{await start(id,country);while(state.dialogue)await act('dialogue');},{id,country});
  }
  async function selectOwn(){
    return page.evaluate(async()=>{const u=state.units.find(u=>u.owner===state.playerFaction);await selectCell(u.cell,true);view.x=point(u.cell).x;view.y=point(u.cell).y;return {unit:u,refund:selection.disband};});
  }
  const snapshot=()=>page.evaluate(()=>({revision:state.revision,units:state.units.map(u=>u.id),resources:state.factions.find(f=>f.id===state.playerFaction).resources}));
  try{
    await page.goto('http://127.0.0.1:5185/');
    await page.waitForFunction(()=>!!originalTextRenderer&&!!catalog);
    await begin('conquest1','fra');
    const chosen=await selectOwn();report.quote=chosen;
    check(chosen.refund&&['money','industry','food'].every(k=>Number.isSafeInteger(chosen.refund[k])),'initial conquest unit has an exact resource refund');
    check(await page.locator('#actions [aria-label="解散部队"]').count()===1,'bottom bar contains one disband action');
    check(await page.locator('#actions [aria-label="解散部队"] img').evaluate(e=>e.complete&&e.naturalWidth>0&&e.src.endsWith('/disband.png')),'disband uses the round original-style X icon');
    const before=await snapshot();
    for(const size of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(size);
      await page.locator('#actions [aria-label="解散部队"]').click();
      await page.waitForTimeout(260);
      check(await page.locator('.disband-refund').getAttribute('aria-label')===`返还金钱 ${chosen.refund.money}，工业 ${chosen.refund.industry}，粮食 ${chosen.refund.food}`,'confirmation quotes exact resources at '+size.width);
      check(await page.locator('#detail-modal>.dialog').evaluate(e=>{
        const r=e.getBoundingClientRect();return r.left>=0&&r.top>=0&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1
          &&[...e.querySelectorAll('.disband-confirmation .original-text')].every(n=>{const q=n.getBoundingClientRect();return q.left>=r.left-1&&q.right<=r.right+1&&q.top>=r.top&&q.bottom<=r.bottom;});
      }),'confirmation text fits its window at '+size.width);
      check(await page.locator('#map').evaluate(c=>{
        const d=c.getContext('2d').getImageData(0,0,c.width,c.height).data,colors=new Set();
        for(let i=0;i<d.length;i+=4*397)colors.add(d[i]+','+d[i+1]+','+d[i+2]);return colors.size>20;
      }),'battlefield canvas is nonblank at '+size.width);
      await shot('disband-confirm-'+size.width);
      await page.locator('#close-detail').click();
      check(JSON.stringify(await snapshot())===JSON.stringify(before),'cancel leaves army and resources unchanged at '+size.width);
    }
    await page.setViewportSize({width:1280,height:720});
    await page.locator('#actions [aria-label="解散部队"]').click();
    await page.locator('#detail-confirm').click();
    await page.waitForFunction(id=>!state.units.some(u=>u.id===id),chosen.unit.id);
    await page.locator('#detail-modal').waitFor({state:'hidden'});
    const after=await snapshot();
    check(after.units.length===before.units.length-1,'confirm removes exactly one initial unit');
    for(const resource of ['money','industry','food'])check(after.resources[resource]-before.resources[resource]===chosen.refund[resource],'confirmation credits quoted '+resource);
    check(await page.locator('#actions [aria-label="解散部队"]').isHidden(),'confirmation closes and hides the old unit action');
    check(await page.evaluate(id=>state.undoUnit!==id,chosen.unit.id),'disbanded unit cannot be restored by move undo');
    await shot('disband-completed');
    await page.evaluate(()=>load(0,2));
    check(JSON.stringify(await snapshot())===JSON.stringify(after),'autosave reload preserves the credited resources and removed unit');
    await page.evaluate(async()=>{const u=state.units.find(u=>u.owner!==state.playerFaction);await selectCell(u.cell,true);});
    check(await page.locator('#actions [aria-label="解散部队"]').count()===0,'enemy unit has no disband action');
    await page.evaluate(async()=>{const u=state.units.find(u=>u.owner===state.playerFaction&&u.commanderId);if(!u)throw Error('Expected an initial commanded unit');await selectCell(u.cell,true);});
    check(await page.locator('#actions [aria-label="解散部队"]').count()===0&&await page.evaluate(()=>selection.disband===null),'commanded unit has no disband action or refund quote');
    check(await page.evaluate(()=>state.cells.filter(c=>c.active&&!c.sea).every(c=>Number.isInteger(c.jurisdictionCenter))),'every conquest land cell has a persistent district center');
    for(const id of ['campaign1_01','tutorials1']){
      await begin(id,null);await selectOwn();
      check(await page.locator('#actions [aria-label="解散部队"]').count()===0,'disband is absent in '+id);
      check(await page.evaluate(tutorial=>state.cells.some(c=>c.jurisdictionCenter!==null)!==tutorial,id.startsWith('tutorial')),'district mode boundary in '+id);
      await shot('mode-'+id);
    }
    check(report.errors.length===0&&report.missing.length===0,'no JavaScript errors or missing assets');
  }catch(error){report.failure=error.message;throw error;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify({checks:report.checks,errors:report.errors.length,missing:report.missing.length,screenshots:report.screenshots.length}));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
