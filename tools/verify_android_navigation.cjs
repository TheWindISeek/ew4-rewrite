'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process'),{createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {ws:WebSocket}=deps('playwright-core/lib/utilsBundle');
const adb='C:/Users/WIN11/.codex/tools/android-sdk/platform-tools/adb.exe',serial=process.argv[2]||'127.0.0.1:16416';
const output=path.resolve(process.argv[3]||'build/android-layout-20261020');
const shell=(...args)=>cp.execFileSync(adb,['-s',serial,'shell',...args],{encoding:'utf8'});
const pause=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 const pages=await fetch('http://127.0.0.1:9223/json').then(r=>r.json());
 const ws=new WebSocket(pages.find(p=>p.type==='page').webSocketDebuggerUrl);
 await new Promise((yes,no)=>{ws.once('open',yes);ws.once('error',no);});
 let id=0;const pending=new Map(),report={checks:[],errors:[],scope:'Android diagnostic build, real system Back key, isolated test emulator'};
 ws.on('message',data=>{const r=JSON.parse(data);if(!r.id)return;const p=pending.get(r.id);if(!p)return;pending.delete(r.id);clearTimeout(p.timer);r.error?p.no(Error(JSON.stringify(r.error))):p.yes(r.result);});
 const send=(method,params={})=>new Promise((yes,no)=>{const key=++id,timer=setTimeout(()=>{pending.delete(key);no(Error(method+' timed out'));},45000);pending.set(key,{yes,no,timer});ws.send(JSON.stringify({id:key,method,params}));});
 const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
 async function check(expression,label){for(let n=0;n<80;n++){if(await evaluate(expression)){report.checks.push(label);return;}await pause(150);}throw Error(label);}
 async function back(expression,label){shell('input','keyevent','4');await check(expression,label);}
 async function shot(name){shell('screencap','-p','/sdcard/ew4-navigation.png');cp.execFileSync(adb,['-s',serial,'pull','/sdcard/ew4-navigation.png',path.join(output,name)],{stdio:'pipe'});}
 try{
  await check('!!catalog&&!!originalTextRenderer','main menu ready');
  const viewport=await evaluate('({width:innerWidth,height:innerHeight,ratio:devicePixelRatio})');
  const dimensions=shell('wm','size').match(/Physical size: (\d+)x(\d+)/);
  assert(dimensions,'physical display size');
  const landscape=viewport.width>viewport.height,physical=[Number(dimensions[1]),Number(dimensions[2])];
  assert(Math.abs(viewport.height*viewport.ratio-(landscape?Math.min(...physical):Math.max(...physical)))<3,'WebView uses full display height');report.checks.push('fullscreen viewport');
  await shot('diagnostic-menu.png');
  await evaluate('document.querySelector("#main-settings").click()');
  await back('document.querySelector("#settings").hidden','settings closes');
  await evaluate('document.querySelector("#mod-open").click()');
  await check('document.querySelectorAll(".mod-row").length>0','Mod list ready');
  await evaluate('document.querySelector("#mod-delete-confirm").showModal()');
  await back('!document.querySelector("#mod-delete-confirm").open&&!document.querySelector("#mod-manager").hidden','confirmation cancels without closing manager');
  await back('document.querySelector("#mod-manager").hidden','Mod manager closes');
  await evaluate('document.querySelector("[data-mode=\\"2\\"]").click()');
  await check('!!document.querySelector(".scenario-entry[data-scenario=\\"conquest1\\"]")','conquest list ready');
  await evaluate('document.querySelector(".scenario-entry[data-scenario=\\"conquest1\\"]").click()');
  await check('!document.querySelector("#countries").hidden','country selection opens');
  await back('document.querySelector("#countries").hidden&&!document.querySelector("#scenario-menu").hidden','country selection returns to conquest list');
  await back('!document.querySelector("#main-menu").hidden','conquest list returns to main');
  await evaluate('document.querySelector("[data-mode=\\"1\\"]").click()');
  await evaluate('(async()=>{await menus.openChapter(catalog.chapters[0]);return true})()');
  await back('document.querySelector("#campaign-screen").hidden&&!document.querySelector("#scenario-menu").hidden','campaign returns to chapters');
  await back('!document.querySelector("#main-menu").hidden','chapters return to main');
  await evaluate('(async()=>{await menus.headquarters();document.querySelector("#hq-recruit").click();return true})()');
  await back('!menus.recruiting&&!document.querySelector("#hq-screen").hidden','recruiting returns to roster');
  await back('!document.querySelector("#main-menu").hidden','headquarters returns to main');
  await evaluate('menus.tutorialMenu()');
  await back('document.querySelector("#detail-modal").hidden','tutorial menu closes');
  await evaluate('(async()=>{await start("conquest1","fra");return true})()');
  await check('state?.scenario==="conquest1"&&state.units.length===252&&!busy','conquest loads');
  await back('!document.querySelector("#pause").hidden','battle Back opens pause');
  await shot('diagnostic-pause.png');
  await evaluate('document.querySelector("#pause-settings").click()');
  await back('document.querySelector("#settings").hidden&&!document.querySelector("#pause").hidden','settings returns to pause');
  await back('document.querySelector("#pause").hidden&&!document.querySelector("#play").hidden','pause Back resumes battle');
  await evaluate('menus.saveMenu(true,2)');
  await check('!document.querySelector("#detail-modal").hidden','save dialog opens');
  await back('document.querySelector("#detail-modal").hidden','save dialog closes without saving');
  await shot('diagnostic-battle.png');
  const pixels=await evaluate('(()=>{const p=ctx.getImageData(0,0,canvas.width,canvas.height).data;let n=0;for(let i=0;i<p.length;i+=1600)if(p[i]+p[i+1]+p[i+2]>40)n++;return n})()');
  assert(pixels>500,'map pixels render');report.checks.push('map canvas renders');
  await evaluate('showMain()');shell('input','keyevent','4');await pause(400);
  shell('uiautomator','dump','/sdcard/ew4-navigation.xml');
  assert(shell('cat','/sdcard/ew4-navigation.xml').includes('退出游戏？'),'root requires exit confirmation');report.checks.push('main Back asks before exit');
  shell('input','keyevent','4');await check('!document.querySelector("#main-menu").hidden','cancel exit preserves app');
  await evaluate('document.querySelector("#mod-open").click();document.querySelector("#mod-select").click()');await pause(500);
  assert(shell('dumpsys','activity','activities').includes('com.android.documentsui'),'native file chooser opens');report.checks.push('native file chooser');
  shell('input','keyevent','4');await check('!document.querySelector("#mod-manager").hidden','file chooser cancel returns to manager');
  await back('document.querySelector("#mod-manager").hidden','manager closes after chooser');
 }catch(e){report.errors.push(e.message);throw e;}
 finally{fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'navigation.json'),JSON.stringify(report,null,2));ws.close();console.log(JSON.stringify({checks:report.checks.length,errors:report.errors}));}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
