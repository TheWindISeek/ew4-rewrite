'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/combat_sync_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'5185 only; controlled original artillery/fort presentations, real effect emission and canvas pixels; authoritative naval backend regression is separate'};
  const check=(v,label)=>{assert.ok(v,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto('http://127.0.0.1:5185/');
    await page.waitForFunction(()=>!!catalog&&!!effects);
    await page.evaluate(()=>start('conquest1','fra'));
    await page.evaluate(async()=>{
      await Promise.all(['army_artillery','army_fort'].map(loadLibrary));
      window.syncBase=state;
      window.syncSetup=(artillery,fort,reverse,speed)=>{
        effects.clear();damageNumbers.clear();deaths.clear();combatMotions.clear();settings.speed=speed;
        const a=syncBase.units.find(u=>u.owner===syncBase.playerFaction&&u.category==='artillery');
        const b=syncBase.units.find(u=>u.category==='fort'&&u.owner!==a.owner);
        const center=point(a.cell);
        const cell=syncBase.cells.findIndex((c,i)=>c.active&&!c.sea&&Math.abs(point(i).y-center.y)<1&&point(i).x===center.x+64);
        if(cell<0)throw Error('adjacent land fixture unavailable');
        const units=[{...a,id:'sync-artillery',profile:'ew4.unit.fra.'+artillery+'.0'},
          {...b,id:'sync-fort',cell,profile:'ew4.unit.gbr.'+fort+'.0'}];
        state={...syncBase,units};selected=null;selection=null;
        view.x=(center.x+point(cell).x)/2;view.y=center.y;view.zoom=1.5;
        return {attacker:units[reverse?1:0].id,defender:units[reverse?0:1].id,
          damage:21,counterDamage:13,critical:false,counterCritical:false,counterattack:true};
      };
    });
    report.matrix=[];
    for(const artillery of ['light_artillery','heavy_artillery','siege_artillery','rocket']){
      for(const fort of ['small_fortress','fortress','large_fortress','coastal_fort']){
        for(const reverse of [false,true])for(const speed of [1,3,5]){
          const result=await page.evaluate(async args=>{
            const combat=syncSetup(...args);const pending=playCombat(combat);
            const motions=[...combatMotions.values()],instances=[...effects.instances];
            const first=instances.map(i=>i.started+Math.min(...i.events.map(e=>Number(e.at)||0))*1000/i.speed);
            const hits=motions.map(m=>m.started+m.delay+m.segments[0].end);
            const before=new Map(),emitted=[];const emit=effects.emit;
            effects.emit=function(instance,event){emitted.push({instance,event});return emit.call(this,instance,event);};
            const draw=document.createElement('canvas').getContext('2d');
            effects.frame(draw,Math.min(...first)-.1);
            for(const i of instances)before.set(i,i.done.size);
            effects.frame(draw,Math.max(...first)+.1);
            const fired=instances.map(i=>emitted.some(e=>e.instance===i));
            effects.emit=emit;
            const ticks=motions.map(m=>(Math.max(...first)-m.started-m.delay)*m.segments[0].speed/1000*24);
            const result={first,hits,fired,ticks,noEarlyEmission:[...before.values()].every(n=>n===0),
              waits:presentationWaiters.size,sharedClock:motions[0].started===motions[1].started};
            aiRunning=true;skipAi=true;finishPresentation();await pending;aiRunning=false;skipAi=false;
            result.cleaned=presentationWaiters.size===0&&combatMotions.size===0;
            return result;
          },[artillery,fort,reverse,speed]);
          const label=[artillery,fort,reverse?'fort initiates':'artillery initiates',speed].join('/');
          check(result.first.length===2&&Math.abs(result.first[0]-result.first[1])<.001,label+' simultaneous first shot');
          check(Math.abs(result.hits[0]-result.hits[1])<.001,label+' simultaneous impact');
          check(result.noEarlyEmission&&result.fired.every(Boolean),label+' actual emit waits for both shots');
          check(result.ticks.every(t=>t>=-1e-8)&&result.sharedClock,label+' no negative frames or independent clocks');
          check(result.waits===2&&result.cleaned,label+' cancellation clears both waits');
          report.matrix.push({label,...result});
        }
      }
    }
    report.allUnits=await page.evaluate(async()=>{
      await Promise.all([...new Set(Object.values(manifest.profiles).map(p=>p.library))].map(loadLibrary));
      const entries=Object.entries(manifest.profiles),canonical=[...new Map(entries.map(e=>[e[1].kind+':'+e[1].grade,e])).values()];
      const a=syncBase.units.find(u=>u.category==='artillery'),b=syncBase.units.find(u=>u.category==='fort');
      const cells=[a.cell,a.cell+1],failures=[];let cases=0;
      const categories={warship:'naval',fortress:'fort'};
      async function verify(left,right,speed,reverse,retaliate=true){
        effects.clear();damageNumbers.clear();deaths.clear();settings.speed=speed;
        state={...syncBase,units:[left,right].map(([profile,p],i)=>({...((i===0)?a:b),id:'all-'+i,profile,
          category:categories[p.stats.type]||p.stats.type,cell:cells[reverse?1-i:i]}))};
        const pending=playCombat({attacker:'all-0',defender:'all-1',damage:21,counterDamage:retaliate?13:0,counterattack:retaliate});
        const m=[...combatMotions.values()],instances=[...effects.instances];
        const first=instances.map(i=>i.started+Math.min(...i.events.map(e=>Number(e.at)||0))*1000/i.speed);
        const last=instances.map(i=>i.started+Math.max(...i.events.map(e=>Number(e.at)||0))*1000/i.speed);
        const hits=m.map(m=>m.started+m.delay+m.segments[0].end);
        const valid=m.length===(retaliate?2:1)&&instances.length===m.length&&
          Math.max(...first)-Math.min(...first)<1e-6&&Math.max(...hits)-Math.min(...hits)<1e-6&&
          Math.min(...hits)>=Math.max(...last)+199/Math.sqrt(speed)&&
          m.every(m=>m.delay>=0&&m.segments.every(s=>s.start>=0&&s.end>s.start&&Number.isFinite(s.speed)));
        aiRunning=true;skipAi=true;finishPresentation?.();await pending;aiRunning=false;skipAi=false;
        cases++;
        if((!valid||presentationWaiters.size||combatMotions.size)&&failures.length<10)failures.push({left:left[0],right:right[0],speed,reverse,retaliate,first,last,hits,motions:m.length,instances:instances.length});
      }
      for(const left of canonical)for(const right of canonical)for(const speed of [1,2,3,4,5])for(const reverse of [false,true])await verify(left,right,speed,reverse);
      const references=[entries.find(([,p])=>p.kind==='Light Artillery'),entries.find(([,p])=>p.kind==='Fortress')];
      for(const profile of entries)for(const reference of references)for(const speed of [1,3,5])for(const reverse of [false,true])await verify(profile,reference,speed,reverse);
      for(const profile of entries)for(const reverse of [false,true])await verify(profile,references[0],1,reverse,false);
      effects.clear();
      return {cases,profiles:entries.length,kinds:new Set(entries.map(([,p])=>p.kind)).size,grades:canonical.length,failures};
    });
    check(report.allUnits.failures.length===0,'all original kinds, grades, country variants, directions, speed settings and no-retaliation presentations share the salvo and impact clock: '+JSON.stringify(report.allUnits.failures));
    await page.evaluate(()=>{
      const combat=syncSetup('light_artillery','fortress',false,1);
      window.syncCombat=playCombat(combat);window.syncStarted=performance.now();
    });
    await page.waitForTimeout(500);await shot('artillery-fort-windup');
    await page.waitForFunction(()=>effects.instances.length===2&&effects.instances.every(i=>i.done.size>0),{},{timeout:5000});
    const pixels=await page.evaluate(()=>{
      const p=point(state.units[0].cell),q=point(state.units[1].cell),c=document.createElement('canvas');c.width=320;c.height=180;
      const d=c.getContext('2d');d.translate(110-p.x,100-p.y);effects.frame(d,performance.now()+60);
      const count=(x,y)=>{const data=d.getImageData(x-45,y-45,90,90).data;let n=0;for(let i=3;i<data.length;i+=4)if(data[i]>8)n++;return n;};
      return [count(110,100),count(110+q.x-p.x,100+q.y-p.y)];
    });
    check(pixels.every(n=>n>20),'both muzzle effects contain visible pixels in the same frame');report.muzzlePixels=pixels;
    await shot('artillery-fort-simultaneous-fire');
    await page.waitForFunction(()=>damageNumbers.instances.length===2);
    check(await page.evaluate(()=>{const a=damageNumbers.instances;return a[0].amount===21&&a[1].amount===13&&Math.abs(a[0].started-a[1].started)<30;}),'both authoritative damage values appear together');
    await shot('artillery-fort-simultaneous-hits');await page.evaluate(()=>syncCombat);
    await page.evaluate(()=>{const combat=syncSetup('heavy_artillery','large_fortress',true,1);window.syncCombat=playCombat(combat);});
    await page.waitForFunction(()=>effects.instances.length===2&&effects.instances.every(i=>i.done.size>0));
    await shot('fort-artillery-simultaneous-fire');await page.evaluate(()=>syncCombat);
    await page.setViewportSize({width:844,height:390});
    await page.evaluate(()=>{const combat=syncSetup('siege_artillery','coastal_fort',false,1);window.syncCombat=playCombat(combat);});
    await page.waitForFunction(()=>effects.instances.length===2&&effects.instances.every(i=>i.done.size>0));
    await shot('mobile-simultaneous-fire');await page.evaluate(()=>syncCombat);
    await page.setViewportSize({width:1280,height:720});
    for(const [left,right] of [['line_infantry','light_artillery'],['heavy_cavalry','fortress'],['machine_gun','light_infantry'],['battleship','coastal_fort']]){
      await page.evaluate(([left,right])=>{
        const combat=syncSetup('light_artillery','fortress',false,1);
        for(const [i,kind] of [left,right].entries()){
          const profile=Object.entries(manifest.profiles).find(([key,p])=>key.startsWith('ew4.unit.fra.'+kind+'.'));
          state.units[i]={...state.units[i],profile:profile[0],category:({warship:'naval',fortress:'fort'})[profile[1].stats.type]||profile[1].stats.type};
        }
        window.syncCombat=playCombat(combat);
      },[left,right]);
      await page.waitForFunction(()=>effects.instances.length===2&&effects.instances.every(i=>i.done.size>0));
      check(await page.evaluate(()=>effects.instances.every(i=>i.done.size>0)),'visible mixed-family first salvo '+left+'/'+right);
      await shot(left+'-'+right+'-simultaneous');await page.evaluate(()=>syncCombat);
    }
    check(report.errors.length===0&&report.missing.length===0,'no script errors or missing assets');
  }catch(e){report.failure=e.message;throw e;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify({checks:report.checks,cases:report.matrix.length,allUnits:report.allUnits,errors:report.errors,missing:report.missing,screenshots:report.screenshots,muzzlePixels:report.muzzlePixels}));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
