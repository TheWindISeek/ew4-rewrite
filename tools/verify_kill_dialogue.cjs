'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5245';
const folder=path.resolve(__dirname,'../rewrite/reports/kill-dialogue-20261021');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(pass,label)=>{assert.ok(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    await page.evaluate(async()=>{
      await start('conquest1','fra');
      window.dialogueFixture=await rpc('action',{op:'state'});
      window.dialogueRpc=rpc;window.dialogueCalls=[];
      rpc=async(...args)=>{dialogueCalls.push(args);return dialogueRpc(...args);};
      window.injectKillDialogue=async(count=1,extra={})=>{
        const next=structuredClone(dialogueFixture),speaker=next.units.find(u=>u.commanderId&&u.commanderImage);
        if(!speaker)throw Error('Missing real commander fixture');
        next.revision=state.revision+1;next.movements=[];next.combat=null;next.recruited=[];
        next.notices=Array.from({length:count},()=>({kind:'battle.kill_dialogue',subject:speaker.commanderId}));
        Object.assign(next,extra);await installState(next);return speaker;
      };
    });
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});
      for(let index=0;index<6;index++){
        const result=await page.evaluate(async index=>{
          const random=Math.random;Math.random=()=> (index+.1)/6;
          let speaker;try{speaker=await injectKillDialogue();}finally{Math.random=random;}
          return {text:state.dialogue,expected:manifest.strings['desc_destroy enemy '+(index+1)],portrait:state.dialoguePresentation.portrait,expectedPortrait:speaker.commanderImage+'.png',revision:state.revision};
        },index);
        check(result.text===result.expected,'original random line '+(index+1)+' '+name);
        check(result.portrait===result.expectedPortrait,'killing commander portrait '+name);
        check(await page.locator('#dialogue').isVisible(),'original panel visible '+name);
        await page.waitForFunction(()=>{const img=$('dialogue-portrait');return img.complete&&img.naturalWidth>0;});
        if(index===0){
          await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');
          await page.locator('#menu-button').tap();await page.touchscreen.tap(width/2,2);
          check(await page.evaluate(()=>state.localBattleDialogue&&battleDialogues.length===1),'pause preserves local dialogue '+name);
          await page.locator('#pause-settings').tap();await page.locator('#close-settings').tap();await page.locator('#resume').tap();
        }
        const calls=await page.evaluate(()=>dialogueCalls.length);
        await page.touchscreen.tap(width/2,height/2);await page.waitForFunction(()=>!busy&&!state.localBattleDialogue);
        check(await page.evaluate(({calls,revision})=>dialogueCalls.length===calls&&state.revision===revision&&!state.dialogue,{calls,revision:result.revision}),'local acknowledgement preserves server revision and sends no RPC '+name);
      }
    }
    check(await page.evaluate(async()=>{
      await injectKillDialogue(2);const revision=state.revision;
      await act('end');if(state.revision!==revision||battleDialogues.length!==2)return false;
      await act('dialogue');if(!state.localBattleDialogue||battleDialogues.length!==1)return false;
      await act('dialogue');return !state.dialogue&&battleDialogues.length===0;
    }),'multiple notifications queue and block gameplay until acknowledged');
    check(await page.evaluate(async()=>{
      await injectKillDialogue();
      const next=structuredClone(dialogueFixture);next.revision=state.revision;next.notices=[{kind:'battle.kill_dialogue',subject:next.units.find(u=>u.commanderId&&u.commanderImage).commanderId}];
      await installState(next);await act('dialogue');await installState(next);
      return !state.dialogue&&battleDialogues.length===0;
    }),'same revision refresh neither duplicates nor replays a notification');
    check(await page.evaluate(async()=>{
      await injectKillDialogue(1,{dialogue:'scenario fixture',dialoguePresentation:null});
      if(state.localBattleDialogue||state.dialogue!=='scenario fixture'||battleDialogues.length!==1)return false;
      const next=structuredClone(dialogueFixture);next.revision=state.revision+1;next.notices=[];
      await installState(next);return state.localBattleDialogue&&battleDialogues.length===1;
    }),'scenario dialogue takes priority and queued shout follows it');
    await page.evaluate(()=>act('dialogue'));
    check(await page.evaluate(async()=>{
      const before=dialogueCalls.length;
      await injectKillDialogue();busy=true;await $('dialogue').onclick();busy=false;
      const blocked=state.localBattleDialogue&&dialogueCalls.length===before;
      const next=structuredClone(dialogueFixture);await installState(next,true);
      return blocked&&!state.localBattleDialogue&&battleDialogues.length===0;
    }),'busy guard and fresh session reset preserve acknowledgement boundaries');
    check(await page.evaluate(async()=>{
      aiRunning=true;skipAi=true;try{await injectKillDialogue();return !state.dialogue&&battleDialogues.length===0;}finally{aiRunning=false;skipAi=false;}
    }),'skipping AI does not accumulate presentation dialogues');
    check(await page.evaluate(async()=>{
      const next=structuredClone(dialogueFixture);next.revision=state.revision+1;next.activeFaction=next.factions.find(f=>!f.human).id;
      const speaker=next.units.find(u=>u.commanderId&&u.commanderImage);
      next.notices=[{kind:'battle.kill_dialogue',subject:speaker.commanderId}];
      await installState(next);if(aiCanContinue(state)||!state.localBattleDialogue)return false;
      const original=rpc;let actions=0;
      rpc=async(route,args)=>{if(route==='action'&&args.op==='ai'){actions++;return {...dialogueFixture,revision:state.revision+1,notices:[],movements:[],combat:null};}throw Error('Unexpected RPC');};
      try{await act('dialogue');return actions===1&&!state.dialogue&&state.activeFaction===state.playerFaction;}finally{rpc=original;}
    }),'AI pauses for the shout and resumes after local acknowledgement');
    check(await page.evaluate(async()=>{
      await injectKillDialogue();state.revision=dialogueFixture.revision;await showMain();
      return !state&&battleDialogues.length===0&&$('play').hidden;
    }),'returning to main menu clears local dialogue');
    check(await page.evaluate(async()=>{
      await start('campaign1_01');const revision=state.revision,before=dialogueCalls.length;
      if(!state.dialogue||state.localBattleDialogue)return false;
      await act('dialogue');
      return state.revision===revision+1&&dialogueCalls.slice(before).some(([route,args])=>route==='action'&&args.op==='dialogue');
    }),'real campaign dialogue still acknowledges through the backend');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing assets');
  }finally{await fs.writeFile(path.join(folder,'web.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
