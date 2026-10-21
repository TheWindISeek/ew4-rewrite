'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5211';
const folder=path.resolve(__dirname,'../rewrite/reports/shop-refresh-20261021');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1136,height:640}});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  const check=(value,label)=>{assert(value,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name);};
  const stock=()=>page.evaluate(()=>rpc('commerce',{cell:-1}));
  const ready=()=>page.waitForFunction(()=>typeof menus!=='undefined'&&catalog&&document.documentElement.dataset.fontsReady==='true',null,{timeout:60000});
  const requestArgs=request=>JSON.parse(new URL(request.url()).searchParams.get('q')||request.postData()||'{}');
  const refreshResponse=()=>page.waitForResponse(r=>new URL(r.url()).pathname==='/api/commerce'&&requestArgs(r.request()).op==='refresh');
  try{
    await page.goto(base);await ready();
    await page.locator('#headquarters').click();await page.locator('#hq-shop').click();
    const before=await stock();
    const refresh=page.getByRole('button',{name:'刷新商品',exact:true});
    await refresh.waitFor();
    check(await refresh.getAttribute('title')==='刷新商品','refresh icon has a tooltip');
    check(await refresh.locator('img').evaluate(e=>e.complete&&e.naturalWidth>0),'original refresh icon loads');
    const response=refreshResponse();await refresh.click();const result=(await (await response).json()).data;
    await page.waitForFunction(()=>document.querySelector('.shop-refresh')&&!document.querySelector('.shop-refresh').disabled);
    check(result.offers.map(o=>o.itemId).join()!==before.offers.map(o=>o.itemId).join(),'click changes offered types');
    check(result.medals===before.medals&&JSON.stringify(result.items)===JSON.stringify(before.items),'refresh preserves medals and inventory');
    check(result.offers.length===7,'original headquarters slot count is preserved');
    await page.locator('[data-native-id="grid_seller"] .native-item:not(:disabled):not(.unavailable-item)').first().click();
    await page.locator('[data-native-id="btn_buy"]').waitFor({state:'visible'});
    const selectedResponse=refreshResponse();await refresh.click();await selectedResponse;
    await page.waitForFunction(()=>document.querySelector('[data-native-id="btn_buy"]').hidden);
    check(await page.locator('.native-item[aria-pressed="true"]').count()===0,'refresh clears the prior purchase selection');
    await page.waitForTimeout(250);
    for(const [name,size] of [['desktop',{width:1136,height:640}],['mobile-landscape',{width:844,height:390}],['mobile-portrait',{width:390,height:844}]]){
      await page.setViewportSize(size);await page.waitForTimeout(120);
      check(await refresh.evaluate(e=>{
        const r=e.getBoundingClientRect(),close=document.getElementById('close-detail').getBoundingClientRect(),title=document.getElementById('detail-title').getBoundingClientRect();
        return r.width>0&&r.height>0&&r.left>=0&&r.right<=innerWidth+1&&r.top>=0&&r.bottom<=innerHeight+1&&r.left>=close.right&&title.left<r.left&&document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)?.closest('button')===e;
      }),'refresh control fits and can receive input: '+name);
      await shot(name);
    }
    const saved=await stock();await page.locator('#close-detail').click();await page.locator('#hq-shop').click();
    check(JSON.stringify((await stock()).offers)===JSON.stringify(saved.offers),'closing and reopening preserves refreshed stock');
    await page.reload();await ready();await page.locator('#headquarters').click();await page.locator('#hq-shop').click();
    check(JSON.stringify((await stock()).offers)===JSON.stringify(saved.offers),'browser reload preserves stock');
    await page.setViewportSize({width:1136,height:640});
    let requests=0;
    await page.route('**/api/commerce?**',async route=>{
      if(requestArgs(route.request()).op==='refresh'){requests++;await new Promise(resolve=>setTimeout(resolve,350));}
      await route.continue();
    });
    const delayed=refreshResponse();await refresh.evaluate(e=>{e.click();e.click();});
    check(await refresh.isDisabled(),'refresh disables while request is pending');
    await page.locator('#close-detail').click();await delayed;await page.waitForTimeout(500);
    check(requests===1,'repeated clicks send only one request');
    check(await page.locator('#detail-modal').isHidden(),'closing during refresh stays closed after response');
    await page.unroute('**/api/commerce?**');
    const current=await stock();
    await page.evaluate(snapshot=>menus.shop(0,snapshot),current);
    check(await page.locator('.shop-refresh').count()===0,'map shop layout has no refresh control');
    await shot('map-layout-without-refresh');
    await page.locator('#close-detail').click();
    await page.evaluate(()=>start('conquest1','fra'));
    const world=await page.evaluate(()=>({round:state.round,active:state.activeFaction,outcome:state.outcome,units:state.units,cells:state.cells,factions:state.factions}));
    const stale=await page.evaluate(()=>rpc('commerce',{cell:-1}));
    await page.evaluate(()=>menus.shop());
    const inGameResponse=refreshResponse();await refresh.click();await inGameResponse;
    await page.waitForTimeout(250);await page.locator('#close-detail').click();
    await page.waitForTimeout(250);
    const after=await page.evaluate(()=>({round:state.round,active:state.activeFaction,outcome:state.outcome,units:state.units,cells:state.cells,factions:state.factions}));
    check(JSON.stringify(world)===JSON.stringify(after),'refresh during a paused scenario changes no world or victory state');
    const rejected=await (await page.request.post(base+'/api/commerce',{data:{op:'buy',cell:-1,revision:stale.revision,offer:stale.offers[0].id}})).json();
    check(!rejected.ok&&rejected.code==='account.revision','pre-refresh purchase command is rejected');
    check(report.errors.length===0&&report.missing.length===0,'no browser errors or missing assets');
  }catch(e){report.failure=e.message;process.exitCode=1;}
  finally{await fs.writeFile(path.join(folder,'browser.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})();
