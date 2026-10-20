'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/objective_markers_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});page.setDefaultTimeout(12000);
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'5185 isolated profile, original circle pixels/timeline, flag anchor/frames and persistent hidden-stage menu unlock'};
  const check=(v,label)=>{assert.ok(v,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto('http://127.0.0.1:5185/');await page.waitForFunction(()=>!!originalTextRenderer&&!!catalog);
    await page.evaluate(()=>start('campaign2_03',null));
    report.nelson=await page.evaluate(()=>{const u=state.units.find(u=>u.commanderDefinition==='Nelson'||u.id==='campaign2_03.unit.31');view.x=point(u.cell).x;view.y=point(u.cell).y;return {id:u.id,cell:u.cell,marker:u.objectiveMarker,objective:u.objective,owner:u.owner};});
    check(report.nelson.objective&&report.nelson.marker===1,'Nelson key unit receives the backend red objective');
    report.composite=await page.evaluate(()=>{
      const renderLayer=enabled=>{const c=document.createElement('canvas');c.width=canvas.width;c.height=canvas.height;const draw=c.getContext('2d');draw.setTransform(ctx.getTransform());const r=new Ew4Battlefield.Renderer(battlefield.api);if(!enabled)r.objectives=()=>{};r.frame({...battlefield,draw},200);return draw.getImageData(0,0,c.width,c.height).data;};
      const a=renderLayer(false),b=renderLayer(true);let changed=0;for(let i=0;i<a.length;i+=4)if(b[i]>a[i]+5&&b[i+1]<a[i+1])changed++;return {visibleRedPixels:changed};
    });
    check(report.composite.visibleRedPixels>100,'objective ring remains visible in the complete scene');
    await page.waitForTimeout(200);await shot('nelson-red-objective');
    await page.evaluate(()=>{window.markerPixels=(cell,tick)=>{
      const c=document.createElement('canvas');c.width=c.height=256;const draw=c.getContext('2d'),p=point(cell),r=new Ew4Battlefield.Renderer(battlefield.api);
      Object.assign(r,{draw,state,time:tick,motions:new Map(),visibleRect:{left:p.x-80,top:p.y-80,width:160,height:160}});
      draw.translate(128-p.x,128-p.y);r.objectives();const pixels=draw.getImageData(0,0,256,256).data;
      let red=0,yellow=0,nonblank=0;for(let i=0;i<pixels.length;i+=4){if(pixels[i+3]>10)nonblank++;if(pixels[i]>80&&pixels[i+1]<30&&pixels[i+2]<30&&pixels[i+3]>10)red++;if(pixels[i]>80&&pixels[i+1]>80&&pixels[i+2]<30&&pixels[i+3]>10)yellow++;}
      return {red,yellow,nonblank,png:c.toDataURL()};
    };});
    const red=await page.evaluate(cell=>window.markerPixels(cell,200),report.nelson.cell);
    check(red.red>100&&red.nonblank>100,'red circle contains visible original animation pixels');
    const redNext=await page.evaluate(cell=>window.markerPixels(cell,450),report.nelson.cell);
    check(red.png!==redNext.png,'red objective follows the original changing timeline');
    await fs.writeFile(folder+'/red-circle-layer.png',Buffer.from(red.png.split(',')[1],'base64'));
    await page.evaluate(()=>start('campaign1_01',null));
    const redSite=await page.evaluate(()=>{const cell=state.cells.findIndex(c=>c.objective);view.x=point(cell).x;view.y=point(cell).y;return {cell,marker:state.cells[cell].objectiveMarker};});
    check(redSite.marker===1,'backend red site objective exposed');await page.waitForTimeout(100);await shot('city-red-objective');
    await page.evaluate(()=>start('campaign1_04',null));
    const yellow=await page.evaluate(()=>{const cell=state.cells.findIndex(c=>c.objectiveMarker===2),unit=state.units.find(u=>u.objectiveMarker===2);const target=cell>=0?cell:unit.cell;view.x=point(target).x;view.y=point(target).y;return {cell:target,marker:cell>=0?state.cells[cell].objectiveMarker:unit.objectiveMarker};});
    check(yellow.marker===2,'real hidden-stage target has marker 2');
    const pixels=await page.evaluate(cell=>window.markerPixels(cell,200),yellow.cell);
    check(pixels.yellow>100,'yellow circle has actual yellow pixels');await fs.writeFile(folder+'/yellow-circle-layer.png',Buffer.from(pixels.png.split(',')[1],'base64'));
    await page.waitForTimeout(100);await shot('yellow-hidden-objective');
    const baseline=await page.evaluate(()=>catalog.unlockedCampaigns.filter(id=>id.startsWith('campaign1_')).length);
    await page.evaluate(()=>menus.openChapter(catalog.chapters[0]));
    check(await page.locator('#campaign-battles .country-entry').count()===16+baseline,'chapter lists normal stages and only unlocked hidden stages');await shot('hidden-stage-locked');
    const denied=await page.evaluate(async()=>{try{await rpc('start',{scenario:'campaign1_05'});return false;}catch(e){return e.message.includes('黄圈');}});
    check(baseline>0||denied,'hidden stage cannot be started by bypassing the menu');
    await page.evaluate(()=>load(6,1));
    await page.evaluate(()=>menus.openChapter(catalog.chapters[0]));
    check(await page.locator('#campaign-battles .country-entry').count()===17,'winning yellow-objective fixture adds exactly its hidden stage');
    check(await page.evaluate(()=>catalog.unlockedCampaigns.includes('campaign1_05')&&!catalog.unlockedCampaigns.includes('campaign1_18')),'only corresponding hidden stage unlocked');await shot('hidden-stage-unlocked');
    await page.evaluate(()=>start('conquest1','fra'));
    report.port=await page.evaluate(()=>{const tur=state.factions.find(f=>f.flag==='tur'),ports=state.cells.map((c,i)=>({...c,cell:i})).filter(c=>c.buildingKind==='port'&&c.owner===tur?.id);const port=ports.find(c=>!state.units.some(u=>u.cell===c.cell))||ports[0];view.x=point(port.cell).x;view.y=point(port.cell).y;return {cell:port.cell,owner:port.owner,center:port.jurisdictionCenter,flag:tur.flag};});
    check(report.port.center===null,'port stays independent of city districts');await page.waitForTimeout(150);await shot('port-native-anchor');
    report.flags=await page.evaluate(()=>{
      const draws=[],c=document.createElement('canvas');c.width=c.height=256;const draw=c.getContext('2d');
      const r=new Ew4Battlefield.Renderer({...battlefield.api,sprite:(atlas,name,x,y,scale,d)=>{draws.push({name,x,y});sprite(atlas,name,x,y,scale,d);}});r.draw=draw;r.time=0;r.flag('tur',{x:100,y:100});const first=c.toDataURL();draw.clearRect(0,0,256,256);r.time=100;r.flag('tur',{x:100,y:100});const second=c.toDataURL();return {draws,different:first!==second,first,second};
    });
    check(report.flags.draws.every(d=>d.x===73&&d.y===55),'actual flag and pole share original -27/-45 anchor');
    check(report.flags.draws.some(d=>d.name==='tur1.png')&&report.flags.draws.some(d=>d.name==='tur2.png'),'flag changes frames every 100ms');
    check(report.flags.different,'flag frame changes produce different visible pixels');
    await fs.writeFile(folder+'/flag-frame1.png',Buffer.from(report.flags.first.split(',')[1],'base64'));await fs.writeFile(folder+'/flag-frame2.png',Buffer.from(report.flags.second.split(',')[1],'base64'));
    delete report.flags.first;delete report.flags.second;
    for(const size of [{width:844,height:390},{width:390,height:844}]){await page.setViewportSize(size);await page.waitForTimeout(100);await shot('port-'+size.width);check(await page.locator('#map').evaluate(c=>c.width>0&&c.height>0),'map canvas sized at '+size.width);}
    check(report.errors.length===0&&report.missing.length===0,'no browser errors or missing assets');
  }catch(e){report.failure=e.message;throw e;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
