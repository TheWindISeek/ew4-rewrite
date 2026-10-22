'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5248',folder=path.resolve('rewrite/reports/unit-display-20261021');
const profile=path.resolve('build/unit-display-20261021/profile');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]},check=(value,label)=>{assert.ok(value,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const shot=async name=>{await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');};
  const vectors=JSON.parse(await fs.readFile('reverse/reports/unit_display_vectors.json','utf8'));
  try{
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(()=>!settings.skipTurnFast&&!settings.followAi),'old settings default both switches off');
    await page.locator('#main-settings').click();
    for(const size of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(size);
      await page.locator('#follow-ai').scrollIntoViewIfNeeded();
      check(await page.locator('#follow-ai').evaluate(e=>{const r=e.getBoundingClientRect();return r.width>0&&r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight;}),'follow control fits '+size.width);
      check(await page.locator('#follow-ai').evaluate(e=>{const label=e.parentElement.querySelector('span'),a=label.getBoundingClientRect(),b=e.getBoundingClientRect();return a.right<b.left;}),'caption does not overlap checkbox '+size.width);
      await shot('settings-'+size.width);
    }
    await page.locator('#skip-turn-fast').check();await page.locator('#follow-ai').check();
    await page.reload();await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(()=>settings.skipTurnFast&&settings.followAi),'both settings persist across reload');
    const native=await page.evaluate(v=>{
      const flags=v.flags.filter(t=>Ew4Battlefield.flagAnimated({owner:t.owned?'player':'enemy',remainingMovement:t.movement,morale:t.morale,constructionTurns:t.construction,acted:t.acted,canAttack:t.target},{playerFaction:'player'})!==t.animated);
      const colors=v.colors.filter(t=>Ew4Battlefield.healthColor(t.health,t.maximum)!=='#'+t.rgb.map(c=>c.toString(16).padStart(2,'0')).join(''));
      return {flags:flags.slice(0,3),colors:colors.slice(0,3)};
    },vectors);
    check(!native.flags.length,'flags match all original ARM64 predicates: '+JSON.stringify(native.flags));
    check(!native.colors.length,'all original health tint submissions match: '+JSON.stringify(native.colors));
    report.nativeCases=vectors.checks;
    await page.setViewportSize({width:1280,height:720});
    await page.evaluate(()=>{settings.speed=5;settings.skipTurnFast=false;settings.followAi=false;mode=2;return start('conquest1','fra');});
    await page.evaluate(()=>rpc('save',{slot:6,mode:2}));
    await page.waitForTimeout(100);await shot('battlefield-desktop');
    for(const size of [{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(size);await page.waitForTimeout(120);
      check(await page.evaluate(()=>{const p=ctx.getImageData(0,0,canvas.width,canvas.height).data;let n=0;for(let i=4;i<p.length;i+=400)if(p[i]!==p[0]||p[i+1]!==p[1]||p[i+2]!==p[2])n++;return n>100;}),'battlefield is nonblank '+size.width);
      await shot('battlefield-'+size.width);
    }
    await page.setViewportSize({width:1280,height:720});
    report.turns=[];
    let fullSession;
    for(const fast of [true,false]){
      await page.evaluate(async fast=>{
        await installState(await rpc('load',{slot:6,mode:2}),true);
        settings.skipTurnFast=fast;settings.followAi=true;settings.speed=5;
        window.displayProbe={frames:0,moving:0,followed:0,cameraErrors:0,fastFrames:0,steps:0,batches:0,painted:[],cells:0,done:false};
        const frame=battlefield.frame.bind(battlefield),call=rpc;
        battlefield.frame=function(scene,time){
          if(skipAi){
            displayProbe.frames++;if(fastSkipping())displayProbe.fastFrames++;
            if(motions.size){displayProbe.moving++;if(followingAi()){
              displayProbe.followed++;
              const motion=motions.get(aiFocus.unit),u=state.units.find(u=>u.id===aiFocus.unit);
              const p=motion?Ew4Battlefield.pose(motion,state.width,time):point(u?.cell??aiFocus.cell);
              const expected=Ew4Camera.constrain({...view,x:p.x,y:p.y},{w:canvas.clientWidth,h:canvas.clientHeight},mapBounds);
              if(Math.abs(view.x-expected.x)>.01||Math.abs(view.y-expected.y)>.01)displayProbe.cameraErrors++;
            }}
          }
          return frame(scene,time);
        };
        rpc=async function(route,args){const result=await call(route,args);if(skipAi&&route==='action'){
          if(args.op==='ai_batch')displayProbe.batches++;
          if(args.op==='ai')displayProbe.steps++;
          const next=result.state||result;
          if(next.cellUpdates?.length||next.cells&&JSON.stringify(next.cells)!==JSON.stringify(state.cells))displayProbe.cells++;
        }return result;};
        const sample=()=>{
          const f=$('ai-flag');
          if(skipAi&&!$('ai-action').hidden&&f.complete&&f.naturalWidth&&displayProbe.painted.at(-1)?.faction!==state.activeFaction)
            displayProbe.painted.push({faction:state.activeFaction,source:f.getAttribute('src'),expected:flagSource(state.factions.find(f=>f.id===state.activeFaction))});
          if(!displayProbe.done)requestAnimationFrame(sample);
        };requestAnimationFrame(sample);
        window.displayTurn=(async()=>{try{await act('end');}finally{displayProbe.done=true;battlefield.frame=frame;rpc=call;}})();
      },fast);
      await page.waitForFunction(()=>aiRunning&&!displayProbe.done,null,{timeout:60000});
      check(await page.evaluate(()=>!skipAi),'setting alone does not activate skip');
      await page.locator('#skip-ai').click();
      await page.waitForFunction(()=>displayProbe.painted.length>=2,null,{timeout:120000});
      await shot(fast?'fast-progress':'basic-follow');
      await page.waitForFunction(()=>displayProbe.done,null,{timeout:300000});
      await page.evaluate(()=>displayTurn);
      const result=await page.evaluate(async()=>{
        await rpc('save',{slot:5,mode:2});
        const full=await rpc('action',{op:'state'});
        return {...displayProbe,active:state.activeFaction,player:state.playerFaction,round:state.round,phase:state.phase,
          released:!aiRunning&&!skipAi&&!aiFocus&&!followingAi(),indicatorHidden:$('ai-action').hidden,
          consistent:JSON.stringify(full.cells)===JSON.stringify(state.cells)&&JSON.stringify(full.units)===JSON.stringify(state.units)};
      });
      report.turns.push({fast,...result});
      check(result.released&&result.indicatorHidden,'camera/skip released at human turn');
      check(result.active===result.player&&result.round===2,'round returns to player');
      check(result.consistent,'final battlefield matches authoritative backend');
      check(result.painted.length>2&&result.painted.every(f=>f.source===f.expected),'current flags continue updating');
      if(fast){check(result.batches>0&&result.steps===0&&result.frames===0,'full skip uses batches with zero intermediate battlefield draws');}
      else{check(result.steps>0&&!result.batches&&result.moving>0&&result.cells>0,'basic skip shows real movement and territory changes');check(result.followed>0&&!result.cameraErrors,'camera follows moving AI positions within map bounds');}
      const saved=JSON.parse(await fs.readFile(path.join(profile,'saves/mode-2/slot-5.json'),'utf8'));
      if(fast)fullSession=saved.session;else check(JSON.stringify(fullSession)===JSON.stringify(saved.session),'basic and full skip produce byte-identical saved sessions and RNG');
    }
    check(!report.errors.length&&!report.missing.length,'no browser or asset errors');
    console.log(JSON.stringify({checks:report.checks,nativeCases:report.nativeCases,turns:report.turns.map(t=>({fast:t.fast,frames:t.frames,moving:t.moving,followed:t.followed,cameraErrors:t.cameraErrors,countries:t.painted.length})),errors:report.errors.length}));
  }finally{
    await fs.writeFile(path.join(folder,'browser.json'),JSON.stringify(report,null,2)+'\n');await browser.close();
  }
})().catch(e=>{console.error(e);process.exitCode=1;});
