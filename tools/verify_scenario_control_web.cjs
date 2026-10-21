'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder=path.resolve(__dirname,'../rewrite/reports/scenario-control-20261021'),url=process.argv[2]||'http://127.0.0.1:5212';
(async()=>{
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(pass,label)=>{assert(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1136,height:640}});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  const api=async(route,body={})=>{const result=await(await page.request.post(url+'/api/'+route,{data:body})).json();assert(result.ok,route+JSON.stringify(result));return result.data;};
  const shot=async name=>{await page.screenshot({path:path.join(folder,name)});report.screenshots.push(name);};
  const choose=async id=>{
    await page.locator('#scenario-event-choices [data-choice="'+id+'"]').click();
    if(id==='fight')await page.locator('#skip-ai').click();
    await page.waitForFunction(()=>!busy,null,{timeout:120000});
  };
  try{
    const uploaded=await page.request.post(url+'/api/mod-import',{multipart:{id:'scenario-control-test',name:'ScenarioControl',file:{name:'scenario-control.zip',mimeType:'application/zip',buffer:await fs.readFile(path.join(folder,'fixture.zip'))}}});
    assert(uploaded.ok(),await uploaded.text());
    let installed;
    for(let i=0;i<180;i++){const status=await(await page.request.get(url+'/api/mods')).json();assert(status.import.status!=='error',status.import.message);if(status.import.status==='complete'){installed=status;break;}await page.waitForTimeout(500);}
    assert(installed,'import timeout');
    const activation=await(await page.request.post(url+'/api/mods',{data:{op:'activate',key:installed.import.key}})).json();
    check(activation.active===installed.import.key,'real package import and activation');
    await page.goto(url);
    await page.waitForFunction(()=>catalog&&document.documentElement.dataset.fontsReady==='true',null,{timeout:60000});
    await page.evaluate(async()=>{await start('conquest1','fra');});
    await page.locator('#scenario-event').waitFor({state:'visible'});
    const origin=await page.evaluate(()=>state.playerFaction);
    await shot('choice-desktop.png');
    await choose('stay');
    check(await page.evaluate(id=>state.playerFaction===id&&state.factions.some(f=>f.id==='free'&&!f.human),origin),'decline role while rebellion still exists');
    await page.evaluate(async()=>{await start('conquest1','fra');});
    await choose('join');
    check(await page.evaluate(()=>state.playerFaction==='free'&&state.factions.filter(f=>f.human).length===1),'single player changes controlled country');
    check(await page.evaluate(id=>!state.factions.find(f=>f.id===id).human,origin),'former country becomes AI');
    check(await page.evaluate(()=>state.startCountry==='fra'&&state.victoryRules.roundLimit===60),'start identity and replacement victory rules projected');
    check(await page.locator('#player-flag').getAttribute('title')==='地方共和国','HUD reflects new controlled country');
    await api('save',{mode:2,slot:1});
    await page.evaluate(async()=>{await load(1,2);});
    check(await page.evaluate(()=>state.playerFaction==='free'&&state.scenarioPopup.id==='epilogue'),'role and pending branch restore together');
    for(const [width,height,name]of[[1136,640,'role-desktop.png'],[844,390,'role-landscape.png'],[390,844,'role-portrait.png']]){
      await page.setViewportSize({width,height});
      await page.waitForTimeout(250);
      const box=await page.locator('#scenario-event').boundingBox();
      check(box&&box.width>0&&box.x>=0&&box.y>=0&&box.x+box.width<=width+1&&box.y+box.height<=height+1,'role popup fits '+name);
      await shot(name);
    }
    await page.setViewportSize({width:1136,height:640});
    await choose('fight');
    check(await page.evaluate(()=>!state.scenarioPopup&&state.outcome==='Continue'&&state.playerFaction==='free'&&state.activeFaction==='free'),'AI hands control to rebellion through normal turn order');
    check(await page.evaluate(()=>{const c=document.getElementById('map'),p=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let n=0;for(let i=0;i<p.length;i+=80)if(p[i]+p[i+1]+p[i+2]>30)n++;return n>100;}),'battlefield canvas nonblank after role change');
    await page.evaluate(async()=>{await load(1,2);});
    await choose('finish');
    check(await page.evaluate(()=>state.outcome==='Victory'&&state.ending==='republic.independence'&&state.battleResult?.outcome==='Victory'),'ending branch reaches final settlement');
    await page.locator('.native-layout[data-layout="form_victory"]').waitFor({state:'visible',timeout:20000});
    await shot('ending-desktop.png');
    const result=await page.evaluate(()=>({id:state.battleResult.id,medals:state.medals}));
    await api('save',{mode:2,slot:2});
    await page.evaluate(async()=>{await load(2,2);});
    check(await page.evaluate(prior=>state.playerFaction==='free'&&state.ending==='republic.independence'&&state.battleResult.id===prior.id&&state.medals===prior.medals,result),'finished save preserves role ending and one-time result');
    check(report.errors.length===0&&report.missing.length===0,'no browser errors or missing resources');
    await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2)+'\n');
    console.log(JSON.stringify(report));
  }catch(e){await shot('failure.png').catch(()=>{});throw e;}finally{await browser.close();}
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
