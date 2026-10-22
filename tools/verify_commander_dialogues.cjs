'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5247';
const archive=path.resolve(process.argv[3]||'build/conditional-dialogues-20261021/commander-dialogues.zip');
const folder=path.resolve(__dirname,'../rewrite/reports/conditional-dialogues-20261021');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(pass,label)=>{assert.ok(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    const upload=await page.request.post(base+'/api/mod-import',{multipart:{id:'commander-dialogues',name:'CommanderDialogues',file:{name:'commander-dialogues.zip',mimeType:'application/zip',buffer:await fs.readFile(archive)}}});
    check(upload.ok(),'example ZIP uploaded');
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    let key;
    for(let attempt=0;attempt<300;attempt++){
      const response=await (await page.request.get(base+'/api/mods')).json();
      if(response.import.status==='error')throw Error(response.import.message);
      if(response.import.status==='complete'&&response.mods.some(m=>m.key===response.import.key)){key=response.import.key;break;}
      await page.waitForTimeout(200);
    }
    check(!!key,'packaged extension imported');
    const activation=await page.request.post(base+'/api/mods',{data:{op:'activate',key}});
    check((await activation.json()).active===key,'packaged extension activated');
    await page.reload();await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(()=>catalog.commanderDialogues['ew4.commander.1'].kill.length===2),'catalog contains actual imported JSON');
    await page.evaluate(async()=>{
      await start('conquest1','fra');
      window.commanderDialogueFixture=await rpc('action',{op:'state'});
      window.injectCommanderShout=async(id,random=0)=>{
        const next=structuredClone(commanderDialogueFixture),unit=next.units.find(u=>u.commanderDefinitionId===id);
        if(!unit)throw Error('Missing real commander '+id);
        next.revision=state.revision+1;next.movements=[];next.combat=null;next.recruited=[];
        next.notices=[{kind:'battle.kill_dialogue',subject:unit.commanderId}];
        const original=Math.random;Math.random=()=>random;
        try{await installState(next);}finally{Math.random=original;}
        return state.dialogue;
      };
    });
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});
      for(let index=0;index<2;index++){
        check(await page.evaluate(async index=>{
          const text=await injectCommanderShout('ew4.commander.20104',index*.999);
          return text===catalog.commanderDialogues['ew4.commander.20104'].kill[index]&&state.dialoguePresentation.portrait==='Napoleon.png';
        },index),'dedicated text and correct portrait '+name+' '+index);
        check(await page.locator('#dialogue').isVisible(),'existing panel displays dedicated text '+name);
        await page.waitForFunction(()=>$('dialogue-portrait').complete&&$('dialogue-portrait').naturalWidth>0);
        if(index===0){await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');}
        const before=await page.evaluate(()=>state.revision);
        await page.touchscreen.tap(width/2,height/2);
        check(await page.evaluate(revision=>!state.dialogue&&state.revision===revision,before),'dedicated dialogue acknowledges locally '+name);
      }
    }
    check(await page.evaluate(async()=>{
      const unit=commanderDialogueFixture.units.find(u=>u.commanderDefinitionId&&!catalog.commanderDialogues[u.commanderDefinitionId]);
      const text=await injectCommanderShout(unit.commanderDefinitionId);
      return text===manifest.strings['desc_destroy enemy 1'];
    }),'unconfigured commander falls back to generic text');
    await page.evaluate(()=>act('dialogue'));
    check(await page.evaluate(()=>{
      const rule=catalog.commanderDialogues['ew4.commander.20104'].killRules[0];
      const speaker=commanderDialogueFixture.units.find(u=>u.commanderDefinitionId==='ew4.commander.20104');
      const victim=commanderDialogueFixture.units.find(u=>u.commanderDefinitionId==='ew4.commander.9501');
      if(!killDialogueMatches(rule,commanderDialogueFixture,speaker,victim))return false;
      const test=(when,next=commanderDialogueFixture,killer=speaker,target=victim)=>killDialogueMatches({...rule,when:{...rule.when,...when}},next,killer,target);
      return !test({minRound:2})&&!test({maxRound:0})&&!test({scenarios:['other']})
        &&!test({victimCommanders:['ew4.commander.6401']})&&!test({victimCategories:['fort']})
        &&!test({victimHasCommander:false})
        &&test({killerHealthPercentAtMost:50},commanderDialogueFixture,{...speaker,health:50,maxHealth:100})
        &&!test({killerHealthPercentAtMost:50},commanderDialogueFixture,{...speaker,health:51,maxHealth:100});
    }),'target, scenario, round, category, commander presence and health conditions combine with AND');
    await page.evaluate(()=>{
      window.injectConditionalShout=async(victimId,{rules,random=()=>0,generic=false,deadSpeaker=false}={})=>{
        const initial=structuredClone(commanderDialogueFixture);await installState(initial,true);
        const next=structuredClone(initial),speaker=initial.units.find(u=>u.commanderDefinitionId==='ew4.commander.20104');
        const victim=initial.units.find(u=>u.commanderDefinitionId===victimId);
        if(!victim)throw Error('Missing real victim '+victimId);
        const entry=catalog.commanderDialogues[speaker.commanderDefinitionId],saved=entry.killRules,original=Math.random;
        if(rules)entry.killRules=rules;
        next.revision=initial.revision+1;next.units=next.units.filter(u=>u.id!==victim.id&&(!deadSpeaker||u.id!==speaker.id));
        next.movements=[];next.combat=null;next.recruited=[];
        next.notices=[{kind:'battle.kill_context',subject:speaker.id,target:victim.id}];
        if(generic)next.notices.push({kind:'battle.kill_dialogue',subject:speaker.commanderId,target:victim.id});
        Math.random=random;
        try{await installState(next);return {text:state.dialogue,count:battleDialogues.length};}
        finally{entry.killRules=saved;Math.random=original;}
      };
    });
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});
      check(await page.evaluate(async()=>{
        const result=await injectConditionalShout('ew4.commander.9501',{generic:true,random:()=>{throw Error('Fixed rule must not sample randomness');}});
        return result.text===catalog.commanderDialogues['ew4.commander.20104'].killRules[0].lines[0]&&result.count===1;
      }),'fixed target line fires without RNG or duplicate generic shout '+name);
      await page.waitForFunction(()=>$('dialogue-portrait').complete&&$('dialogue-portrait').naturalWidth>0);
      await page.screenshot({path:path.join(folder,'conditional-'+name+'.png')});report.screenshots.push('conditional-'+name+'.png');
      await page.touchscreen.tap(width/2,height/2);
      check(await page.evaluate(()=>!state.dialogue&&battleDialogues.length===0),'conditional line acknowledges locally '+name);
    }
    check(await page.evaluate(async()=>{
      const rule=catalog.commanderDialogues['ew4.commander.20104'].killRules[1];
      const hit=await injectConditionalShout('ew4.commander.6401',{random:()=>.1});
      const miss=await injectConditionalShout('ew4.commander.6401',{random:()=>.9});
      const fallback=await injectConditionalShout('ew4.commander.6401',{generic:true,random:()=>.9});
      return hit.text===rule.lines[0]&&!miss.text&&fallback.text==='胜利属于我们的军队！';
    }),'rule probability success/failure and ordinary shout fallback');
    check(await page.evaluate(async()=>{
      const original=catalog.commanderDialogues['ew4.commander.20104'].killRules[1];
      const rules=[{...original,chance:100}];
      const result=await injectConditionalShout('ew4.commander.6401',{rules,random:()=>.99});
      return result.text===original.lines[1];
    }),'random selection reaches the second configured line');
    check(await page.evaluate(async()=>{
      const rule=catalog.commanderDialogues['ew4.commander.20104'].killRules[0];
      const rules=[{...rule,chance:0},{...rule,id:'second',lines:['second rule']},{...rule,id:'third',lines:['third rule']}];
      const result=await injectConditionalShout('ew4.commander.9501',{rules});
      return result.text==='second rule'&&result.count===1;
    }),'first eligible rule wins and zero chance is disabled');
    check(await page.evaluate(async()=>{
      const rule=catalog.commanderDialogues['ew4.commander.20104'].killRules[0];
      const result=await injectConditionalShout('ew4.commander.9501',{rules:[{...rule,when:{...rule.when,killerHealthPercentAtMost:0}}],deadSpeaker:true});
      return result.text===rule.lines[0];
    }),'posthumous kill uses zero remaining health and original portrait');
    await page.evaluate(()=>act('dialogue'));
    check(await page.evaluate(async()=>{
      const initial=structuredClone(commanderDialogueFixture);await installState(initial,true);
      const speaker=initial.units.find(u=>u.commanderDefinitionId==='ew4.commander.20104');
      const first=initial.units.find(u=>u.commanderDefinitionId&&!catalog.commanderDialogues[u.commanderDefinitionId]
        &&!['ew4.commander.9501','ew4.commander.6401'].includes(u.commanderDefinitionId));
      const second=initial.units.find(u=>u.commanderDefinitionId==='ew4.commander.9501');
      const next={...initial,revision:initial.revision+1,movements:[],combat:null,recruited:[],
        units:initial.units.filter(u=>u.id!==first.id&&u.id!==second.id),
        notices:[{kind:'battle.kill_context',subject:speaker.id,target:first.id},
          {kind:'battle.kill_dialogue',subject:speaker.commanderId,target:first.id},
          {kind:'battle.kill_context',subject:speaker.id,target:second.id}]};
      const random=Math.random;Math.random=()=>0;
      try{await installState(next);}finally{Math.random=random;}
      return battleDialogues.length===2&&battleDialogues[0].text===catalog.commanderDialogues[speaker.commanderDefinitionId].kill[0]
        &&battleDialogues[1].text===catalog.commanderDialogues[speaker.commanderDefinitionId].killRules[0].lines[0];
    }),'multiple kills retain notice order with ordinary and conditional lines');
    await page.evaluate(async()=>{await act('dialogue');await act('dialogue');});
    check(await page.evaluate(async()=>{
      const entry=catalog.commanderDialogues['ew4.commander.20104'],original=entry.kill;entry.kill=[];
      try{return await injectCommanderShout('ew4.commander.20104')===manifest.strings['desc_destroy enemy 1'];}finally{entry.kill=original;}
    }),'empty dedicated list falls back to generic text');
    await page.evaluate(()=>act('dialogue'));
    check(await page.evaluate(async()=>{
      const unit=commanderDialogueFixture.units.find(u=>u.commanderDefinitionId&&!catalog.commanderDialogues[u.commanderDefinitionId]),original=unit.commanderImage;
      unit.commanderImage='Napoleon';
      try{return await injectCommanderShout(unit.commanderDefinitionId)===manifest.strings['desc_destroy enemy 1'];}finally{unit.commanderImage=original;}
    }),'shared portrait does not match a different definition');
    await page.evaluate(()=>act('dialogue'));
    const builtin=await page.request.post(base+'/api/mods',{data:{op:'activate',key:null}});
    check((await builtin.json()).active===null,'builtin content restored');
    await page.reload();await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(()=>Object.keys(catalog.commanderDialogues).length===0),'switching to builtin does not leak dedicated lines');
    check(await page.evaluate(async()=>{
      await start('conquest1','fra');
      const speaker=state.units.find(u=>u.commanderDefinitionId),victim=state.units.find(u=>u.commanderDefinitionId&&u.id!==speaker.id);
      await installState({...state,revision:state.revision+1,notices:[{kind:'battle.kill_context',subject:speaker.id,target:victim.id}]});
      return !state.dialogue&&battleDialogues.length===0;
    }),'unconfigured builtin kill context never creates a new shout');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing assets');
  }finally{await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
