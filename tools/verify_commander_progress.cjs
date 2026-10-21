'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.env.EW4_TEST_URL||'http://127.0.0.1:5185';
const folder='E:/Workplace/game/o4mod/rewrite/reports/commander-progress-20261021';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}}),report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  const shot=async name=>{await page.waitForFunction(()=>[...document.querySelectorAll('#detail-modal .rank-gauge')].every(badge=>badge.dataset.ready==='true'));await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);};
  const gauges=()=>page.locator('#detail-modal .rank-gauge').evaluateAll(nodes=>nodes.map(n=>({track:n.dataset.track,level:+n.dataset.level,xp:+n.dataset.experience,max:n.getAttribute('aria-valuemax'),now:n.getAttribute('aria-valuenow'),text:n.getAttribute('aria-valuetext'),sweep:+n.dataset.sweep})));
  try{
    await page.goto(base);await page.waitForFunction(()=>typeof menus!=='undefined'&&!!menus&&document.documentElement.dataset.fontsReady==='true');
    await shot('main-wenkai');await page.locator('#headquarters').click();
    await page.waitForFunction(()=>menus.data?.progression);
    check(await page.evaluate(()=>{const tracks=menus.data.progression;try{delete menus.data.progression;const badge=menus.progressionBadge('rank',{level:0,experience:100});return !badge.hasAttribute('aria-valuenow')&&badge.getAttribute('aria-valuetext')==='100'&&badge.dataset.sweep==='0';}finally{menus.data.progression=tracks;}}),'older running backend remains usable until restarted with threshold API');
    await page.evaluate(async()=>{for(const p of menus.data.princesses.slice(0,3))if(!p.claimed)await menus.updateAccount({op:'claim',offer:p.id});});
    const id=await page.evaluate(()=>menus.data.roster.find(c=>c.fullPrice>0).id);
    await page.evaluate(id=>menus.reopenCommander(id),id);
    check((await gauges()).length===2,'both commander experience tracks displayed');
    await shot('commander-desktop');
    for(const viewport of [{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(viewport);
      check(await page.locator('.commander-ranks .rank-gauge').evaluateAll(nodes=>nodes.every(n=>{const r=n.getBoundingClientRect(),p=n.parentElement.getBoundingClientRect();return r.left>=p.left&&r.right<=p.right&&r.top>=p.top&&r.bottom<=p.bottom;})),'badges contained at '+viewport.width);
      await shot('commander-'+viewport.width);
    }
    await page.setViewportSize({width:1280,height:720});
    await page.evaluate(id=>menus.equipment(id),id);check((await gauges()).length===2,'equipment uses same progress badges');await shot('equipment');
    await page.evaluate(id=>menus.promotion(id),id);check((await gauges()).length===4,'promotion shows current and next stages');await shot('promotion');
    const donors=await page.evaluate(id=>menus.data.roster.filter(c=>c.id!==id).map(c=>c.id),id);
    for(const donor of donors.slice(0,2)){
      await page.evaluate(async({id,donor})=>{await menus.regroup(id,donor);},{id,donor});
      const expected=await page.evaluate(async({id,donor})=>(await menus.api.rpc('commander',{op:'preview_inherit',revision:menus.data.revision,commander:id,target:donor})).learning,{id,donor});
      check((await gauges()).every(g=>g.level===expected[g.track].level&&g.xp===expected[g.track].experience),'selected donor preview shows actual level and residual XP');
      check(await page.evaluate(expected=>['text_life','text_apply'].every((key,i)=>document.querySelector('[data-native-id="'+key+'"] .original-text').getAttribute('aria-label')===String((i?menus.data.titleHealing:menus.data.rankHealth)[expected[i?'title':'rank'].level])),expected),'preview health and healing match resulting stages');
    }
    await shot('regroup-preview');
    const preview=await gauges();await page.getByRole('button',{name:'整编',exact:true}).click();await page.locator('#detail-confirm').click();
    await page.waitForFunction(()=>!!document.querySelector('.commander-detail .rank-gauge'));
    check(JSON.stringify(await gauges())===JSON.stringify(preview),'committed progress equals selected donor preview');await shot('regroup-committed');
    const persisted=await gauges();await page.reload();await page.waitForFunction(()=>typeof menus!=='undefined'&&!!menus&&document.documentElement.dataset.fontsReady==='true');
    await page.locator('#headquarters').click();await page.waitForFunction(()=>menus.data?.progression);await page.evaluate(id=>menus.reopenCommander(id),id);
    check(JSON.stringify(await gauges())===JSON.stringify(persisted),'residual experience survives reload');
    await page.evaluate(()=>{const body=document.querySelector('.commander-ranks');document.querySelector('.commander-detail').replaceChildren(body);body.replaceChildren();for(const track of ['rank','title'])for(const ratio of [0,.25,.5,.75,1]){const row=document.createElement('div');row.append(menus.progressionBadge(track,{level:0,experience:menus.data.progression[track][0]*ratio}));body.append(row);}body.style.cssText='left:2px;top:3px;width:435px;display:grid;grid-template-columns:repeat(5,1fr)';});
    const samples=await gauges();check(samples.every((g,i)=>Math.abs(parseFloat(g.sweep)-(i%5)*45)<.01),'zero through full stage render correct half-circle sweep');
    const images=[];for(const badge of await page.locator('.rank-gauge').all())images.push((await badge.screenshot()).toString('base64'));
    check(new Set(images).size===10,'all progress samples paint distinct nonblank images');
    await shot('gauge-samples');
    await page.evaluate(id=>menus.promotion(id),id);await page.getByRole('button',{name:'军衔爵位升至满级',exact:true}).click();
    await page.waitForFunction(()=>[...document.querySelectorAll('.rank-gauge')].every(n=>n.getAttribute('aria-valuetext')==='MAX'));
    check((await gauges()).every(g=>g.text==='MAX'&&g.sweep===180),'final stages show full bars without division by zero');await shot('max-level');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing resources');
  }catch(e){report.failure=e.message;throw e;}
  finally{await fs.writeFile(folder+'/results.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
