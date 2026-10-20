'use strict';
const fs=require('node:fs/promises'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root='E:/Workplace/game/o4mod',folder=root+'/rewrite/reports/real-mod-ui-20261019',url='http://127.0.0.1:5185';
(async()=>{
 await fs.mkdir(folder,{recursive:true});
 const report={checks:0,errors:[],missing:[],screenshots:[],scope:'Real original APK installed by RealApkChecks, plus real 大前线 APK failure path. No passing claim for the other five Mods.'};
 const check=(v,m)=>{assert(v,m);report.checks++;};
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const page=await browser.newPage({viewport:{width:1280,height:720}});
 page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.url(),r.status()]);});
 const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);};
 try{
  await page.goto(url);await page.waitForFunction(()=>typeof catalog!=='undefined'&&!!catalog&&!!manifest,{},{timeout:90000});
  const before=await page.evaluate(()=>fetch('/api/mods').then(r=>r.json()));
  check(before.active&&before.mods.some(m=>m.active&&m.name==='欧陆战争4_1.6.6'),'real imported APK active');
  await page.locator('#mod-open').click();await page.locator('.mod-row').first().waitFor();await shot('actual-apk-manager');
  await page.locator('#mod-file').setInputFiles(root+'/大前线_1.6.6(1).apK');
  await page.waitForFunction(()=>!document.getElementById('mod-import').disabled);await page.locator('#mod-import').click();
  await page.waitForFunction(async()=>{const s=await fetch('/api/mods').then(r=>r.json());return s.import.status==='error';},{},{timeout:90000});
  await page.waitForFunction(()=>document.querySelector('#mod-progress').textContent.includes('commander'));
  const after=await page.evaluate(()=>fetch('/api/mods').then(r=>r.json()));
  check(after.active===before.active&&after.mods.length===before.mods.length,'failed real APK import preserves installed content and active selection');
  check(after.import.message.includes('984'),'actual XML failure visible');await shot('actual-mod-error');
  await page.locator('#mod-close').click();
  await page.evaluate(()=>start('campaign1_01'));
  await page.waitForFunction(()=>typeof state!=='undefined'&&state?.scenario==='campaign1_01'&&!busy,{},{timeout:60000});
  await page.waitForFunction(()=>[...document.images].filter(i=>i.src.includes('/content/')).every(i=>i.complete));
  const pixels=await page.evaluate(()=>{const c=document.querySelector('#map'),d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let opaque=0;const colors=new Set();for(let i=0;i<d.length;i+=4*101){if(d[i+3])opaque++;colors.add(d[i]+','+d[i+1]+','+d[i+2]);}return {opaque,colors:colors.size,width:c.width,height:c.height};});
  report.canvas=pixels;check(pixels.opaque>100&&pixels.colors>40,'real APK battlefield is nonblank');await shot('actual-apk-campaign-desktop');
  await page.setViewportSize({width:844,height:390});await page.waitForTimeout(250);await shot('actual-apk-campaign-mobile');
  check(report.errors.length===0,'no browser exceptions: '+report.errors.join(';'));check(report.missing.length===0,'no failed content requests');
 }finally{await fs.writeFile(folder+'/results.json',JSON.stringify(report,null,2));await browser.close();}
 console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
