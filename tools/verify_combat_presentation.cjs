'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5223';
const folder=path.resolve(__dirname,'../rewrite/reports/combat-presentation-20261021');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const check=(v,label)=>{assert(v,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');};
  try{
    const fonts=JSON.parse(await fs.readFile(path.resolve(__dirname,'../rewrite/data/client/fight-font.json'),'utf8'));
    for(const font of ['font_fight_num','font_fight_num_hd']){
      const raw=await fs.readFile(path.resolve(__dirname,'../apk_work/original/assets/'+font+'.fnt'));
      check(raw.readInt32LE(4)===fonts[font].height,'original line height '+font);
      for(let p=8;p<raw.length;p+=12){
        assert.deepEqual(fonts[font].glyphs[raw.readUInt16LE(p)],[raw.readUInt16LE(p+2),raw.readUInt16LE(p+4),raw[p+6],raw[p+7],raw.readInt8(p+8),raw.readInt8(p+9),raw.readUInt16LE(p+10)]);report.checks++;
      }
      check((await fs.readFile(path.resolve(__dirname,'../apk_work/original/assets/'+font+'.png'))).equals(await fs.readFile(path.resolve(__dirname,'../rewrite/data/client/images/'+font+'.png'))),'original atlas bytes '+font);
    }
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    await page.evaluate(()=>start('conquest1','fra'));
    report.undo=await page.evaluate(async()=>{
      settings.speed=5;
      for(const unit of state.units.filter(u=>u.owner===state.playerFaction)){
        await selectCell(unit.cell,true);
        const cell=selection.range.find(c=>c!==unit.cell&&!state.units.some(u=>u.cell===c));
        if(cell==null)continue;
        const before=state,origin=unit.cell;
        await act('move',{unit:unit.id,cell});
        const facing=facings.get(unit.id),set=motions.set.bind(motions),records=[];
        motions.set=(id,m)=>{records.push(id);return set(id,m);};
        let response;const send=rpc;
        rpc=async(...args)=>{const result=await send(...args);if(args[1]?.op==='undo')response=result;return result;};
        const started=performance.now();
        try{await $('undo-move').onclick();}finally{motions.set=set;rpc=send;}
        return {origin,returned:state.units.find(u=>u.id===unit.id).cell,facing,afterFacing:facings.get(unit.id),
          animations:records,ms:performance.now()-started,instant:response.movements[0].instant,
          unitsRestored:JSON.stringify(before.units)===JSON.stringify(state.units)};
      }
      throw Error('No movable fixture');
    });
    check(report.undo.returned===report.undo.origin&&report.undo.unitsRestored,'real undo restores authoritative state');
    check(report.undo.instant&&report.undo.animations.length===0,'real undo creates no movement animation');
    check(report.undo.facing===report.undo.afterFacing,'undo preserves displayed facing');await shot('instant-undo');
    await page.evaluate(()=>start('campaign1_02b',null));
    await page.evaluate(async()=>{
      settings.speed=1;await selectCell(2544,true);
      const attacker=selected.id,target=state.units.find(u=>u.cell===selection.targets[0]);
      view.x=point(target.cell).x;view.y=point(target.cell).y;
      await selectCell(target.cell,true);window.testRedSelection=Ew4Battlefield.selectionHostile(state,selection.cell);
      window.testAttack=act('attack',{unit:attacker,target:target.id});
    });
    await page.waitForFunction(()=>combatMotions.size>0);
    check(await page.evaluate(()=>testRedSelection&&!selection&&$('selection').hidden),'red enemy selection clears at attack start');
    await shot('attack-start-no-selection');
    await page.waitForFunction(()=>damageNumbers.instances.length>0);
    check(await page.evaluate(()=>damageNumbers.sheet.src.endsWith('font_fight_num_hd.png')),'damage renderer uses original combat atlas');
    await shot('original-damage-digits');await page.evaluate(()=>testAttack);
    const native=JSON.parse(await fs.readFile(path.resolve(__dirname,'../reverse/reports/frontend_service_contracts.json'),'utf8'));
    check(await page.evaluate(vectors=>vectors.every(v=>Ew4Effects.impact(v.weapon,v.category,v.damage,v.sea,v.facing)===v.name),native.impacts),'all native impact mappings match');
    report.effects=await page.evaluate(async()=>{
      const results=[];effects.clear();damageNumbers.clear();
      const c=document.createElement('canvas');c.width=240;c.height=220;const d=c.getContext('2d');
      for(const weapon of ['cold','gun','cannon']){
        effects.clear();const name=Ew4Effects.impact(weapon,'infantry',20,false,1);
        effects.timeline(name,120,110);const i=effects.instances[0];
        d.clearRect(0,0,c.width,c.height);effects.frame(d,i.started+70);
        const pixels=d.getImageData(0,0,c.width,c.height).data;
        let count=0,hash=0;for(let p=0;p<pixels.length;p++){hash=(Math.imul(hash,31)+pixels[p])>>>0;if(p%4===3&&pixels[p]>8)count++;}
        results.push({weapon,name,count,hash,particles:i.particles.length});
      }
      effects.clear();return results;
    });
    check(report.effects.every(e=>e.count>20&&e.particles>0),'knife, bullet and explosion effects all draw real pixels');
    check(new Set(report.effects.map(e=>e.hash)).size===3,'three weapon impacts have distinct rendered imagery');
    await page.evaluate(()=>{
      window.fixtureBase=state;
      window.showImpact=async weapon=>{
        effects.clear();damageNumbers.clear();
        const unit=state.units[0],p=point(unit.cell);view.x=p.x;view.y=p.y;view.zoom=2;
        const instance=effects.timeline(Ew4Effects.impact(weapon,'infantry',30,false,1),p.x,p.y);
        requestRender();
      };
    });
    for(const [width,height] of [[1280,720],[844,390],[390,844]]){
      await page.setViewportSize({width,height});
      for(const weapon of ['cold','gun','cannon']){
        await page.evaluate(w=>showImpact(w),weapon);await page.waitForTimeout(65);await shot(weapon+'-'+width);
      }
    }
    report.death=await page.evaluate(()=>{
      effects.clear();damageNumbers.clear();settings.speed=1;
      const unit=state.units.find(u=>!state.cells[u.cell].sea),p=point(unit.cell);
      const c=document.createElement('canvas');c.width=400;c.height=400;const d=c.getContext('2d');
      const renderer=new Ew4Battlefield.Renderer(battlefield.api),records=[],draw=renderer.unitAnimation;
      renderer.unitAnimation=function(...args){records.push({y:this.draw.getTransform().f,alpha:this.draw.globalAlpha});return draw.apply(this,args);};
      const death=new Map([[unit.id,{started:0,duration:500}]]);
      for(const time of [0,100,200,300,400,500]){
        d.setTransform(1,0,0,1,200-p.x,200-p.y);
        const scene={draw:d,state:{...state,units:[unit]},map:manifest.maps[state.map],visibleRect:{left:p.x-200,top:p.y-200,width:400,height:400},zoom:1,
          showGrid:false,selection:null,selected:null,selectedAt:0,motions:new Map(),facings,arrivals:new Map(),deaths:death,combatMotions:new Map()};
        renderer.frame(scene,time);
      }
      return records;
    });
    check(report.death.length===5&&new Set(report.death.map(d=>d.y)).size===1,'dying body stays at its original position and vanishes at 500ms');
    const captured=JSON.parse(await fs.readFile(path.resolve(__dirname,'../reverse/reports/combat_presentation.json'),'utf8'));
    check(report.death.every((d,i)=>Math.abs(d.alpha-captured.deaths[i].alpha)<1e-5),'death alpha agrees with executed original ARM64 samples');
    report.splash=await page.evaluate(async()=>{
      const original=state,units=original.units.slice(0,3),profile=Object.entries(manifest.profiles).find(([,p])=>p.kind==='Heavy Artillery')[0];
      await loadLibrary(manifest.profiles[profile].library);
      state={...original,units:units.map((u,i)=>({...u,profile:i===0?profile:u.profile}))};
      effects.clear();damageNumbers.clear();settings.speed=5;
      const names=[],timeline=effects.timeline;effects.timeline=function(name,...args){names.push(name);return timeline.call(this,name,...args);};
      try{await playCombat({attacker:units[0].id,defender:units[1].id,damage:25,counterattack:false,splashTarget:units[2].id,splashDamage:15});}
      finally{effects.timeline=timeline;state=original;}
      return names;
    });
    check(report.splash.filter(n=>/strike/.test(n)).length===2,'primary and splash victim both receive impact effects');
    check(await page.evaluate(()=>{
      damageNumbers.clear();damageNumbers.add(12,true,0,0);
      const canvas=damageNumbers.instances[0].canvas,data=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;
      let red=0,black=0;for(let i=0;i<data.length;i+=4){if(data[i+3]>180&&data[i]>200&&data[i+1]<100)red++;if(data[i+3]>40&&data[i]<25&&data[i+1]<25)black++;}
      return red>20&&black>20;
    }),'critical tint retains original dark outline and visible red digits');
    await page.route('**/manifest.json',async route=>{
      const response=await route.fetch(),old=await response.json();delete old.fonts.font_fight_num;delete old.fonts.font_fight_num_hd;
      await route.fulfill({response,json:old});
    });
    await page.reload();await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(()=>damageNumbers.font.height===48&&damageNumbers.sheet.src.endsWith('font_fight_num_hd.png')),'previously imported manifests use separate original HD bitmap metrics');
    check(report.errors.length===0&&report.missing.length===0,'no script errors or missing assets');
  }catch(error){report.failure=error.message;throw error;}
  finally{await fs.writeFile(path.join(folder,'browser.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
