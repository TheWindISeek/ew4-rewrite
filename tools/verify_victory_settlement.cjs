'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/victory_settlement_20261021';
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.setDefaultTimeout(20000);
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(v,label)=>{assert.ok(v,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto('http://127.0.0.1:5197/');
    await page.waitForFunction(()=>!!originalTextRenderer&&startupReady);
    await page.evaluate(()=>load(1,0));
    await page.waitForSelector('[data-layout="form_victory"]');
    check(await page.locator('[data-native-id="lbox_stars"]').getAttribute('aria-label')==='5星','actual five-star result');
    const receipt=await page.evaluate(()=>({id:state.battleResult.id,medals:state.medals}));
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});await page.waitForTimeout(350);
      const geometry=await page.evaluate(()=>{
        const dialog=document.querySelector('.dialog.native-dialog').getBoundingClientRect();
        const layout=document.querySelector('[data-layout="form_victory"]');
        const buttons=[...layout.querySelectorAll('button:not([hidden])')].map(el=>{const r=el.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};});
        return {dialog:{left:dialog.left,right:dialog.right,top:dialog.top,bottom:dialog.bottom},buttons,
          stars:layout.querySelector('[data-native-id="lbox_stars"]').children.length,
          generals:layout.querySelector('[data-native-id="lbox_general"]').children.length,
          broken:[...layout.querySelectorAll('img')].filter(i=>!i.complete||!i.naturalWidth).map(i=>i.src),
          textPixels:[...layout.querySelectorAll('canvas')].reduce((count,c)=>{const d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;for(let i=3;i<d.length;i+=4)if(d[i])count++;return count;},0)};
      });
      check(geometry.dialog.left>=0&&geometry.dialog.right<=width&&geometry.dialog.top>=0&&geometry.dialog.bottom<=height,name+' dialog stays inside viewport');
      if(name==='portrait')check(geometry.dialog.right-geometry.dialog.left>=width*.85,'portrait result uses readable width');
      check(geometry.buttons.every(r=>r.left>=0&&r.right<=width&&r.top>=0&&r.bottom<=height),name+' buttons inside viewport');
      check(geometry.stars===5&&geometry.generals>=6,name+' stars and general slots');
      check(geometry.broken.length===0,name+' original artwork loaded');
      check(geometry.textPixels>100,name+' offline text has visible pixels');
      check(await page.evaluate(()=>{const d=ctx.getImageData(0,0,canvas.width,canvas.height).data,colors=new Set();for(let i=0;i<d.length;i+=400)colors.add(d[i]+','+d[i+1]+','+d[i+2]);return colors.size>20;}),name+' battlefield background is nonblank');
      await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');
    }
    await page.evaluate(()=>{closeDetail();refreshHud();refreshHud();});
    check(await page.locator('#detail-modal').isHidden(),'closing and repeated refresh do not reopen');
    check(await page.evaluate(()=>state.medals)===receipt.medals,'presentation never pays');
    await page.evaluate(()=>start('campaign1_01',null));
    check(await page.locator('#detail-modal').isHidden(),'new game clears result window');
    await page.evaluate(()=>load(4,0));
    await page.waitForSelector('[data-native-id="btn_restart"]:visible');
    check(await page.locator('[data-native-id="btn_restart"]').isVisible(),'defeat offers restart');
    check(await page.locator('[data-native-id="btn_continue"]').isHidden(),'defeat hides continue');
    await page.screenshot({path:folder+'/defeat.png'});report.screenshots.push('defeat.png');
    await page.locator('[data-native-id="btn_restart"]').click();
    await page.waitForFunction(()=>!busy&&state.phase!=='Finished'&&document.getElementById('detail-modal').hidden);
    check(await page.evaluate(()=>state.battleResult===null),'campaign restart works with scenario-selected country');
    await page.evaluate(()=>load(1,0));
    await page.waitForSelector('[data-native-id="btn_continue"]:visible');
    await page.locator('[data-native-id="btn_continue"]').click();
    await page.waitForFunction(()=>!busy&&state.scenario==='campaign1_02');
    check(await page.evaluate(()=>state.battleResult===null),'continue enters the next campaign');
    await page.evaluate(()=>load(1,0));
    await page.waitForSelector('[data-native-id="btn_exit"]:visible');
    await page.locator('[data-native-id="btn_exit"]').click();
    check(await page.locator('#main-menu').isVisible(),'exit returns to menu');
    await page.evaluate(()=>load(6,0));
    await page.waitForSelector('[data-layout="form_victory"]:visible');
    check(await page.evaluate(()=>state.battleResult.generals.length===1&&document.querySelector('.result-general>img').naturalWidth>0),'deployed general portrait renders');
    await page.waitForTimeout(250);
    await page.screenshot({path:folder+'/generals.png'});report.screenshots.push('generals.png');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing assets');
    await fs.writeFile(folder+'/browser-checks.json',JSON.stringify(report,null,2));
    console.log(JSON.stringify(report));
  }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
