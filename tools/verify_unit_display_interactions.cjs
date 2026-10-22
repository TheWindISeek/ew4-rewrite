'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder=path.resolve('rewrite/reports/unit-display-20261021'),base=process.argv[2]||'http://127.0.0.1:5248';
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}}),report={checks:0,errors:[],cases:[]};
  page.on('pageerror',e=>report.errors.push(e.message));
  const check=(v,label)=>{assert.ok(v,label);report.checks++;};
  try{
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    await page.evaluate(()=>start('conquest1','fra'));
    const scene=await page.evaluate(async()=>{
      window.displayOriginal=state;
      const unit=state.units.find(u=>u.owner!==state.playerFaction&&u.category==='infantry');
      const cells=Array.from({length:state.cells.length},(_,i)=>i).filter(i=>state.cells[i].active&&!state.cells[i].sea);
      const cell=cells.find(i=>{const p=point(i);return p.x>1800&&p.y>900;});
      window.displayActor={...unit,cell};
      state={...state,activeFaction:unit.owner,units:state.units.map(u=>u.id===unit.id?displayActor:u),movements:[],recruited:[],combat:null,notices:[]};
      aiRunning=true;busy=true;settings.followAi=true;settings.skipTurnFast=false;skipAi=false;
      const target=Ew4Battlefield.neighbor(cell,0,state.width,state.height);
      window.displayNext={...state,revision:state.revision+1,units:state.units.map(u=>u.id===unit.id?{...u,cell:target}:u),movements:[{unit:unit.id,path:[cell,target]}]};
      window.normalMove=installState(displayNext);
      return {unit:unit.id,cell,target};
    });
    await page.waitForFunction(()=>motions.size>0);
    check(await page.evaluate(()=>!!followingAi()),'ordinary AI movement locks follow camera');
    await page.evaluate(()=>normalMove);
    const held=await page.evaluate(()=>({...view}));
    await page.mouse.move(650,350);await page.mouse.down();await page.mouse.move(800,450,{steps:5});await page.mouse.up();
    await page.mouse.wheel(0,-200);await page.waitForTimeout(80);
    check(await page.evaluate(v=>Math.abs(view.x-v.x)<.01&&Math.abs(view.y-v.y)<.01&&view.zoom===v.zoom,held),'drag and wheel cannot override locked camera');
    await page.evaluate(()=>{settings.followAi=false;aiFocus=null;});
    const free=await page.evaluate(()=>({...view}));
    await page.mouse.wheel(0,-200);await page.waitForTimeout(80);
    check(await page.evaluate(v=>view.zoom!==v.zoom,free),'turning follow off releases user zoom during AI turn');
    await page.evaluate(async()=>{
      settings.followAi=true;aiFocus={unit:displayActor.id,cell:displayActor.cell};
      await installState({...state,activeFaction:state.playerFaction,movements:[]});
    });
    check(await page.evaluate(()=>!aiFocus&&!followingAi()),'installing a human turn releases camera before AI loop finishes');
    for(const fast of [false,true]){
      const result=await page.evaluate(fast=>{
        state={...displayOriginal,activeFaction:displayActor.owner};aiRunning=true;skipAi=false;settings.skipTurnFast=fast;settings.followAi=true;
        motions.set('sample',{path:[displayActor.cell,displayActor.cell+1],started:performance.now(),stepMs:200});
        combatMotions.set('sample',{});arrivals.set('sample',performance.now());deaths.set('sample',{});
        aiFocus={unit:displayActor.id,cell:displayActor.cell};
        let finished=false;finishPresentation=()=>{finished=true;finishPresentation=null;};
        $('skip-ai').onclick();
        return {fast:fastSkipping(),skip:skipAi,finished,remaining:motions.size+combatMotions.size+arrivals.size+deaths.size,focus:!!aiFocus};
      },fast);
      check(result.skip&&result.fast===fast&&result.finished&&!result.remaining,'skip interrupts active presentations for mode '+fast);
      check(result.focus===!fast,'only full skip suppresses camera focus '+fast);
    }
    await page.evaluate(async()=>{aiRunning=false;skipAi=false;busy=false;aiFocus=null;await installState(displayOriginal,true);});
    report.colors=await page.evaluate(()=>{
      const u=state.units.find(u=>u.owner===state.playerFaction),p=point(u.cell);
      view.x=p.x;view.y=p.y;view.zoom=2.5;clampView();
      window.colorUnit=u.id;
      return [.1,.25,.5,.75,1].map(r=>({ratio:r,color:Ew4Battlefield.healthColor(Math.floor(u.maxHealth*r),u.maxHealth)}));
    });
    for(const test of report.colors){
      await page.evaluate(r=>{state={...state,units:state.units.map(u=>u.id===colorUnit?{...u,health:Math.floor(u.maxHealth*r)}:u)};requestRender();},test.ratio);
      await page.waitForTimeout(100);
      await page.screenshot({path:path.join(folder,'health-'+test.ratio+'.png')});
      check(await page.evaluate(color=>{
        const original=ctx.stroke,seen=[];ctx.stroke=function(...args){seen.push(this.strokeStyle);return original.apply(this,args);};
        try{renderDirty=true;render(performance.now());}finally{ctx.stroke=original;}
        return seen.includes(color);
      },test.color),'actual battlefield uses health tint '+test.ratio);
    }
    await page.evaluate(()=>installState(displayOriginal,true));
    check(!report.errors.length,'no browser errors');report.cases.push(scene);
    console.log(JSON.stringify({checks:report.checks,errors:report.errors.length,colors:report.colors}));
  }finally{await fs.writeFile(path.join(folder,'interactions.json'),JSON.stringify(report,null,2)+'\n');await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
