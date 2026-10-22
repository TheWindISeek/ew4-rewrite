'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5250',folder=path.resolve('rewrite/reports/unit-visuals-20261021');
(async()=>{
  const report={checks:0,errors:[],missing:[],screenshots:[]},check=(value,label)=>{assert.ok(value,label);report.checks++;};
  const fixtures=JSON.parse(await fs.readFile(path.join(folder,'cases.json'),'utf8'));
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const shot=async name=>{await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');};
  try{
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    await page.evaluate(()=>start('conquest1','fra'));
    report.initial=await page.evaluate(()=>{
      const bad=[];let count=0,hre=0;
      for(const u of state.units){const f=state.factions.find(f=>f.id===u.owner),p=visualProfile(u),b=manifest.profiles[u.profile];
        const key=[`${b.kind} ${f.visualVariant??f.unitVariant} ${b.grade+1}`,`${b.kind} ${b.grade+1}`,b.kind].find(k=>effects.data.motions[k]);
        if(p.visualMotion!==key)bad.push(u.id);count++;if((f.visualVariant??f.unitVariant)==='hre')hre++;
      }
      const f=state.factions.find(f=>(f.visualVariant??f.unitVariant)==='hre'),u=state.units.find(u=>u.owner===f.id),p=point(u.cell);
      view.x=p.x;view.y=p.y;view.zoom=2;clampView();requestRender();return {count,hre,bad};
    });
    check(report.initial.hre>0&&report.initial.bad.length===0,'all actual conquest units resolve original country visuals');
    await page.waitForTimeout(200);await shot('hre-desktop');
    const cases=fixtures.cases.filter(c=>c.variant==='hre'||c.slot==='line_infantry.0'||c.variant==='others'&&c.slot.endsWith('.0'));
    report.poses=await page.evaluate(async cases=>{
      const captures=[];
      for(const c of cases){
        const faction={id:'audit.'+c.variant,unitVariant:c.variant},profile=effects.profile(c.profile,faction),lib=await loadLibrary(profile.library);
        const original=state.units[0],unit={...original,id:'visual.audit',profile:c.profile,owner:faction.id,health:100,maxHealth:100,constructionTurns:0,burning:0,veterancy:0,commanderImage:null};
        const p=point(unit.cell),surface=document.createElement('canvas');surface.width=400;surface.height=400;const d=surface.getContext('2d');
        const renderer=new Ew4Battlefield.Renderer(battlefield.api),names=[],paint=renderer.unitAnimation;
        renderer.unitAnimation=function(lib,item,tick){names.push(item?.name);return paint.call(this,lib,item,tick);};
        Object.assign(renderer,{draw:d,state:{...state,units:[unit]},time:0,orderedUnits:[unit],visibleUnits:[unit],motions:new Map(),facings:new Map(),arrivals:new Map(),combatMotions:new Map(),factions:new Map([[faction.id,faction]]),visibleRect:{left:p.x-200,top:p.y-200,width:400,height:400}});
        for(const action of ['ready','move','attack'])for(const facing of [-1,1]){
          const native=effects.motion(profile,action,facing),expected=native?.name||profile.motions[action]||profile.motions.ready;
          renderer.facings.set(unit.id,facing);renderer.motions.clear();renderer.combatMotions.clear();renderer.time=0;
          if(action==='move')renderer.motions.set(unit.id,{path:[unit.cell,unit.cell+facing],started:0,stepMs:300});
          if(action==='attack'&&native)renderer.combatMotions.set(unit.id,{started:0,segments:[{name:native.name,start:0,end:1000,speed:1,directional:native.dir==='left'||native.dir==='right'}]});
          d.setTransform(1,0,0,1,200-p.x,200-p.y);d.clearRect(p.x-200,p.y-200,400,400);renderer.units();
          const selected=names.at(-1),cached=[...renderer.unitFrames.entries()].find(([key,v])=>key.includes(':'+selected+':')&&v.lib===lib&&v.canvas&&v.pixels>0)?.[1];
          const pixels=cached?.canvas.getContext('2d').getImageData(0,0,cached.canvas.width,cached.canvas.height).data;
          let opaque=0;if(pixels)for(let i=3;i<pixels.length;i+=4)if(pixels[i]>0)opaque++;
          captures.push({variant:c.variant,slot:c.slot,action,facing,selected,expected,opaque});
        }
      }
      return captures;
    },cases);
    for(const pose of report.poses)check(pose.selected===pose.expected&&pose.opaque>20,'nonblank original pose '+JSON.stringify(pose));
    for(const viewport of [{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(viewport);await page.waitForTimeout(150);await shot('hre-'+viewport.width);
      check(await page.evaluate(()=>{const f=state.factions.find(f=>(f.visualVariant??f.unitVariant)==='hre');return state.units.filter(u=>u.owner===f.id).every(u=>visualProfile(u).visualMotion.includes(' hre ')||u.category==='fort'||u.category==='naval');}),'HRE mapping survives viewport resize');
    }
    await page.setViewportSize({width:1280,height:720});
    await page.evaluate(async()=>{
      const gallery=document.createElement('canvas');gallery.id='visual-audit-gallery';gallery.width=1120;gallery.height=640;
      gallery.style.cssText='position:fixed;inset:0;z-index:999999;background:white';document.body.append(gallery);
      const d=gallery.getContext('2d');d.fillStyle='#edf0f1';d.fillRect(0,0,1120,640);d.font='16px sans-serif';
      const variants=Object.keys(effects.visuals.config.variants);
      for(const [index,variant] of variants.entries()){
        const id='ew4.unit.others.line_infantry.0',profile=effects.profile(id,{id:'gallery.'+variant,unitVariant:variant}),lib=await loadLibrary(profile.library);
        const motion=effects.motion(profile,'ready',1),item=lib.byName.get(motion?.name||profile.motions.ready),x=index%7*160+80,y=Math.floor(index/7)*160+110;
        d.fillStyle='#263438';d.fillText(variant,x-24,y-78);d.save();d.translate(x+profile.x,y+profile.y);d.scale(.5*profile.scale,.5);animation(lib,item,0,0,d);d.restore();
      }
    });
    await page.locator('#visual-audit-gallery').screenshot({path:path.join(folder,'country-models.png')});report.screenshots.push('country-models.png');
    check(report.errors.length===0,'zero browser errors');check(report.missing.length===0,'zero missing resources');
    await fs.writeFile(path.join(folder,'browser-tests.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify({checks:report.checks,poses:report.poses.length,initial:report.initial,screenshots:report.screenshots.length,errors:report.errors,missing:report.missing}));
  }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
