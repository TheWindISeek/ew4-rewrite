'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'rewrite/reports/scenario-factions-20261021');
const url=process.argv[2]||'http://127.0.0.1:5207';
(async()=>{
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(pass,label)=>{assert(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1136,height:640}});
  page.on('pageerror',error=>report.errors.push(error.message));
  page.on('response',response=>{if(response.status()>=400)report.missing.push([response.status(),response.url()]);});
  const api=async(route,body={})=>{
    const result=await (await page.request.post(url+'/api/'+route,{data:body})).json();
    assert(result.ok,route+': '+JSON.stringify(result));return result.data;
  };
  const shot=async name=>{await page.screenshot({path:path.join(folder,name)});report.screenshots.push(name);};
  const ready=()=>page.waitForFunction(()=>typeof originalTextRenderer!=='undefined'&&originalTextRenderer&&catalog&&document.documentElement.dataset.fontsReady==='true',null,{timeout:60000});
  try{
    const upload=await page.request.post(url+'/api/mod-import',{multipart:{id:'scenario-factions-test',name:'Scenario factions test',file:{name:'scenario-factions.ew4mod',mimeType:'application/zip',buffer:await fs.readFile(path.join(folder,'fixture.ew4mod'))}}});
    assert(upload.ok(),await upload.text());
    let installed;
    for(let i=0;i<180;i++){
      const status=await (await page.request.get(url+'/api/mods')).json();
      assert(status.import.status!=='error',status.import.message);
      if(status.import.status==='complete'){installed=status;break;}
      await page.waitForTimeout(500);
    }
    assert(installed,'fixture import timed out');
    const activation=await (await page.request.post(url+'/api/mods',{data:{op:'activate',key:installed.import.key}})).json();
    check(activation.active===installed.import.key,'real package imported and activated');
    await page.goto(url);await ready();
    await page.evaluate(async()=>{await start('conquest1','fra');});
    await page.locator('#scenario-event').waitFor({state:'visible'});
    const initial=await page.evaluate(()=>({count:state.factions.length,flag:document.getElementById('player-flag').src}));
    check(!await page.evaluate(()=>state.factions.some(f=>f.id==='rebel')),'pending popup does not create country');
    await shot('pending-desktop.png');
    await page.locator('#scenario-event-choices [data-choice="no"]').click();
    await page.waitForFunction(()=>!state.scenarioPopup&&!busy);
    check(await page.evaluate(()=>!state.factions.some(f=>f.id==='rebel')),'decline branch creates no country');
    await page.evaluate(async()=>{await start('conquest1','fra');});
    await page.locator('#scenario-event').waitFor({state:'visible'});
    await api('save',{mode:2,slot:1});
    await page.evaluate(async()=>{await load(1,2);});
    check(await page.evaluate(()=>state.scenarioPopup?.id==='uprising'&&!state.factions.some(f=>f.id==='rebel')),'pending choice survives reload before creation');
    await page.evaluate(()=>{const original=Ew4Battlefield.Renderer.prototype.flag;window.customFlagDraws=0;
      Ew4Battlefield.Renderer.prototype.flag=function(...args){if(args[3])window.customFlagDraws++;return original.apply(this,args);};});
    await page.locator('#scenario-event-choices [data-choice="yes"]').click();
    await page.waitForFunction(()=>!busy&&state.factions.some(f=>f.id==='rebel'));
    check(await page.evaluate(count=>state.factions.length===count+1,initial.count),'accept branch creates exactly one country');
    const rebel=await page.evaluate(()=>state.factions.find(f=>f.id==='rebel'));
    check(rebel.name==='独立共和国'&&rebel.color===-2142406109,'chained rename and RGBA color projected');
    check(rebel.flagImage==='rewrite/images/rebel.png','custom package flag projected');
    await page.waitForFunction(()=>{const flag=document.getElementById('player-flag');return flag.complete&&flag.naturalWidth>0&&flag.src.includes('/extensions/images/rebel.png');});
    check(await page.locator('#player-flag').getAttribute('title')==='新法兰西','existing country name and custom HUD flag updated');
    check(await page.evaluate(old=>document.getElementById('player-flag').src!==old,initial.flag),'HUD flag replaced after event');
    const fixture=JSON.parse(await fs.readFile(path.join(folder,'core.json'),'utf8'));
    check(await page.evaluate(({army,cell})=>state.units.find(u=>u.id===army).owner==='rebel'&&state.cells[cell].owner==='rebel',fixture),'army and city transfer reflected in UI');
    await page.waitForFunction(()=>image('extensions/images/rebel.png').complete&&image('extensions/images/rebel.png').naturalWidth>0);
    check(await page.evaluate(()=>window.customFlagDraws>0),'visible battlefield flags use custom PNG rendering');
    check(await page.evaluate(()=>{const canvas=document.getElementById('map'),pixels=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;let colors=0;for(let i=0;i<pixels.length;i+=80)if(pixels[i]+pixels[i+1]+pixels[i+2]>30)colors++;return colors>100;}),'battlefield canvas renders nonblank pixels');
    await shot('created-desktop.png');
    await api('save',{mode:2,slot:1});
    await page.evaluate(async()=>{await load(1,2);});
    check(await page.evaluate(()=>state.factions.filter(f=>f.id==='rebel').length===1&&state.factions.find(f=>f.id==='rebel').name==='独立共和国'),'created country and identity survive actual client load');
    for(const [width,height,name] of [[844,390,'created-mobile-landscape.png'],[390,844,'created-mobile-portrait.png']]){
      await page.setViewportSize({width,height});
      await page.waitForFunction(()=>Math.abs(parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale'))-Math.min(innerWidth/568,innerHeight/320))<.001);
      const box=await page.locator('.battle-resources').boundingBox();
      check(box&&box.width>0&&box.x>=0&&box.y>=0&&box.x+box.width<=width+1&&box.y+box.height<=height+1,name+' resource HUD fits viewport');
      await shot(name);
    }
    check(report.errors.length===0,'no browser runtime errors');
    check(report.missing.length===0,'no missing assets');
  }catch(error){report.failure=error.message;await shot('failure.png');throw error;}finally{
    await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2));
    await browser.close();
  }
  console.log(JSON.stringify(report));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
