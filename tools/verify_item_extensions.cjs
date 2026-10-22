'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root=path.resolve(__dirname,'..'),base=process.argv[2]||'http://127.0.0.1:5251';
const folder=path.join(root,'rewrite/reports/item-extensions-20261021');
const saves=path.resolve(process.argv[3]||path.join(root,'build/item-extensions-20261021/browser-saves'));
(async()=>{
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(pass,label)=>{assert.ok(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    const bytes=await fs.readFile(path.join(root,'build/item-extensions-20261021/item-effects.ew4mod'));
    check((await page.request.post(base+'/api/mod-import',{multipart:{file:{name:'item-effects.ew4mod',mimeType:'application/zip',buffer:bytes}}})).ok(),'author archive uploaded');
    let info;
    for(let n=0;n<300;n++){
      const response=await (await page.request.get(base+'/api/mods')).json();
      if(response.import.status==='error')throw Error(response.import.message);
      if(response.import.status==='complete'){info=response.mods.find(m=>m.key===response.import.key);if(info)break;}
      await page.waitForTimeout(200);
    }
    check(!!info,'archive imported');
    check((await (await page.request.post(base+'/api/mods',{data:{op:'activate',key:info.key}})).json()).active===info.key,'archive activated');
    const fixtureDir=path.join(saves,'mods',info.id,info.logicRevision);
    await fs.mkdir(fixtureDir,{recursive:true});
    await fs.copyFile(path.join(folder,'fixture-slot.json'),path.join(fixtureDir,'session.json'));
    await fs.copyFile(path.join(folder,'fixture-account.json'),path.join(fixtureDir,'account.json'));
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    const fixture=JSON.parse(await fs.readFile(path.join(folder,'backend.json'),'utf8'));
    fixture.target={id:fixture.target.id||fixture.target.Id,cell:fixture.target.cell??fixture.target.Cell};
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});
      await page.evaluate(async()=>{await load(0,0);});
      const available=await page.evaluate(async({id})=>{
        await selectCell(state.units.find(u=>u.id===id).cell);
        return selection.skills.map(s=>s.skill.instanceId);
      },fixture.target);
      check(available.includes('item.8.repair'),name+' equipment active skill offered');
      check(await page.locator('[title*="野战补给"]').count()>0,name+' active skill button rendered');
      await page.evaluate(async({id})=>{await menus.useItems(id);},fixture.target);
      check(await page.locator('#detail-confirm').isEnabled(),name+' custom medicine usable in original item form');
      const screenshot=path.join(folder,name+'-items.png');await page.screenshot({path:screenshot});report.screenshots.push(screenshot);
      await page.locator('#detail-confirm').click();
      await page.waitForFunction(id=>state.units.find(u=>u.id===id).health===22,fixture.target.id);
      check(await page.evaluate(id=>state.units.find(u=>u.id===id).statuses.some(s=>s.id==='example.item.vigor'),fixture.target.id),name+' actual medicine adds status');
      check(await page.evaluate(async(id)=>(await rpc('items',{unit:id})).items.find(i=>i.id==='ew4.item.13').quantity===2,fixture.target.id),name+' exactly one item consumed');
      await page.evaluate(()=>rpc('save',{mode:2,slot:1}));await page.evaluate(()=>load(1,2));
      check(await page.evaluate(id=>state.units.find(u=>u.id===id).health===22,fixture.target.id),name+' real save reload preserves health');
      await page.evaluate(async({id})=>{await selectCell(state.units.find(u=>u.id===id).cell);},fixture.target);
      await page.locator('.active-skill-button[title="野战补给"]').click();
      check(await page.locator('#detail-confirm').isEnabled(),name+' equipment active confirmation available');
      await page.locator('#detail-confirm').click();
      await page.waitForFunction(id=>!busy&&state.units.find(u=>u.id===id).health===42,fixture.target.id);
      check(await page.evaluate(id=>state.units.find(u=>u.id===id).health===42,fixture.target.id),name+' real equipment button applies healing');
      await page.evaluate(async({id})=>{await selectCell(state.units.find(u=>u.id===id).cell);},fixture.target);
      check(await page.locator('.active-skill-button[title^="野战补给"]').isDisabled(),name+' equipment cooldown disables button');
      await fs.copyFile(path.join(folder,'fixture-slot.json'),path.join(fixtureDir,'session.json'));
      await fs.copyFile(path.join(folder,'fixture-account.json'),path.join(fixtureDir,'account.json'));
      await page.request.post(base+'/api/mods',{data:{op:'activate',key:info.key}});
      await page.reload();await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    }
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing assets');
  }finally{await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
