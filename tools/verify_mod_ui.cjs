'use strict';
const fs=require('node:fs/promises'),assert=require('node:assert/strict'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root='E:/Workplace/game/o4mod',folder=root+'/rewrite/reports/mod-ui-20261019';
(async()=>{
 await fs.mkdir(folder,{recursive:true});let archive=process.argv[2];
 if(!archive){const dirs=await fs.readdir(root+'/rewrite/reports/mod-import-20261019');const candidates=[];for(const dir of dirs){const file=root+'/rewrite/reports/mod-import-20261019/'+dir+'/test.ew4mod';try{candidates.push({file,time:(await fs.stat(file)).mtimeMs});}catch{}}archive=candidates.sort((a,b)=>b.time-a.time)[0]?.file;}
 assert(archive,'fixture package');
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const report={checks:0,errors:[],missing:[],screenshots:[]};const check=(v,m)=>{assert(v,m);report.checks++;};
 const page=await browser.newPage({viewport:{width:1280,height:720}});page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.url(),r.status()]);});
 const ready=()=>page.waitForFunction(()=>typeof catalog!=='undefined'&&!!catalog&&!!manifest,{},{timeout:30000});
 const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);};
 try{
  await page.request.get('http://127.0.0.1:5185/api/mods?q='+encodeURIComponent(JSON.stringify({op:'activate',key:null})));
  await page.goto('http://127.0.0.1:5185/');await ready();await page.locator('#mod-open').waitFor({state:'visible'});check(true,'main menu entry');await shot('main-menu');
  await page.locator('#main-settings').click();await page.locator('#settings').waitFor({state:'visible'});await page.waitForFunction(()=>!!document.querySelector('.city-language>span .original-text'));
  check(await page.locator('.city-language>span .original-text').getAttribute('data-typeface')==='font_text_2_cn_hd','city label stays in native bitmap typeface');
  check(await page.locator('.city-language-options button').count()===2,'language segmented controls');await page.locator('[data-language="zh"]').click();check(await page.locator('[data-language="zh"]').getAttribute('aria-pressed')==='true','language selection');await shot('settings-font');
  await page.locator('#close-settings').click();await page.locator('#mod-open').click();await page.locator('#mod-list .mod-row').first().waitFor();
  await page.locator('#mod-file').setInputFiles(archive);
  await page.waitForFunction(()=>!document.getElementById('mod-import').disabled);await page.locator('#mod-import').click();
  await page.waitForFunction(async()=>{const s=await fetch('/api/mods').then(r=>r.json());return s.import.status==='error'||s.mods.some(m=>m.id==='test-mod')&&s.import.status!=='working';},{},{timeout:120000});
  const importStatus=await page.evaluate(()=>fetch('/api/mods').then(r=>r.json()));assert.notEqual(importStatus.import.status,'error',importStatus.import.message);
  await page.waitForFunction(()=>document.querySelectorAll('#mod-list .mod-row').length>=2);check(true,'browser local upload and progress');await shot('imported-package');
  const mod=page.locator('.mod-row[aria-label="测试内容包"]');await mod.locator('button').first().click();await page.waitForEvent('load',{timeout:120000});await ready();
  check((await page.evaluate(()=>fetch('/api/mods').then(r=>r.json()))).active!=null,'activation persisted');
  await page.locator('[data-mode="2"]').click();await page.waitForFunction(()=>!conquestEntering);
  for(let i=0;i<3&&!await page.locator('[data-scenario="conquest7"]').count();i++){if(await page.locator('#conquest-next').isDisabled())break;await page.locator('#conquest-next').click();await page.waitForFunction(()=>!conquestEntering);}
  check(await page.locator('[data-scenario="conquest7"]').count()===1,'new scenario appears without manual frontend rebuild');await page.locator('[data-scenario="conquest7"]').click();await page.locator('#countries').waitFor({state:'visible'});check(await page.locator('#country-list button').count()>0,'new scenario country selector');await shot('custom-conquest');
  await page.locator('#cancel-country').click();await page.locator('#menu-back').click();await page.locator('#mod-open').click();
  for(const [w,h] of [[1280,720],[844,390],[390,844]]){await page.setViewportSize({width:w,height:h});await page.waitForTimeout(150);const b=await page.locator('.mod-dialog').boundingBox();check(b.x>=-1&&b.y>=-1&&b.x+b.width<=w+1&&b.y+b.height<=h+1,'manager fits '+w+'x'+h);await shot('manager-'+w+'x'+h);}
  await page.setViewportSize({width:1280,height:720});await page.locator('.mod-row[data-mod-key="builtin"]').locator('button').click();await page.waitForEvent('load',{timeout:120000});await ready();check(!(await page.evaluate(()=>fetch('/api/mods').then(r=>r.json()))).active,'return to builtin');
  check(report.errors.length===0,'no page errors: '+report.errors.join(';'));check(report.missing.length===0,'no missing resources');
 }finally{await fs.writeFile(folder+'/results.json',JSON.stringify(report,null,2));await browser.close();}
 console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
