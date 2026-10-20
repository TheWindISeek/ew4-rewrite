'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/sidebar_restoration_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  const report={checks:0,errors:[],missing:[],screenshots:[],chapters:[],scope:'Isolated 5185 profile; six original chapter backgrounds, centered bitmap lettering, shared sidebar entrance, selection, scrolling and reduced motion. No gameplay on 5184.'};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push([r.url(),r.status()]);});
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');};
  const sample=async(id,time)=>page.evaluate(async({id,time})=>{
    const list=document.getElementById(id),animations=list.getAnimations({subtree:true});
    for(const a of animations)a.pause();
    await Promise.all(animations.map(a=>a.ready));
    for(const a of animations)a.currentTime=time;
    await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
    const scale=parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale'));
    const bounds=list.getBoundingClientRect();
    return {count:animations.length,x:bounds.x/scale,rows:[...list.children].slice(0,6).map(b=>({
      x:b.getBoundingClientRect().x/scale,delay:b.getAnimations()[0]?.effect.getTiming().delay,
      selected:b.getAttribute('aria-pressed')==='true',translate:getComputedStyle(b).translate
    }))};
  },{id,time});
  const finish=async id=>page.evaluate(id=>{for(const a of document.getElementById(id).getAnimations({subtree:true}))a.finish();},id);
  try{
    await page.goto('http://127.0.0.1:5185/');
    await page.waitForFunction(()=>!!originalTextRenderer&&!!catalog);
    await page.getByRole('button',{name:'战役',exact:true}).click();
    await page.locator('.chapter-entry').first().click();
    await page.locator('.chapter-preview-confirm').click();
    await page.locator('#campaign-screen').waitFor();
    check(await page.locator('#campaign-battles').evaluate(e=>e.children.length>0),'chapter preview still opens its stage list');
    for(const [index,art] of ['france','coalition','holyroman','east','usa','uk'].entries()){
      await page.evaluate(index=>menus.openChapter(catalog.chapters[index]),index);
      const before=await sample('campaign-battles',0),middle=await sample('campaign-battles',100);
      if(!index){await shot('campaign-sliding');report.campaignAnimation={before,middle};}
      check(before.count===before.rows.length+1||before.count>7,art+' panel and rows animate together');
      check(before.x>middle.x&&before.rows[0].x>middle.rows[0].x,art+' moves left from the right edge');
      check(before.rows.every((r,i)=>r.delay===Math.min(i,5)*60),art+' rows have bounded progressive delays');
      check(middle.rows[0].translate!=='160px'&&middle.rows.at(-1).translate==='160px',art+' later rows remain outside while the first slides in');
      await finish('campaign-battles');
      const data=await page.locator('#campaign-battles').evaluate(list=>({
        background:getComputedStyle(list.firstElementChild).backgroundImage,
        entries:[...list.children].map(b=>{
          const rect=b.getBoundingClientRect(),c=b.querySelector('canvas'),r=c.getBoundingClientRect();
          const pixels=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
          let ink=0;for(let i=3;i<pixels.length;i+=4)if(pixels[i])ink++;
          return {label:b.getAttribute('aria-label'),centerError:Math.abs((r.left+r.right-rect.left-rect.right)/2),inside:r.left>=rect.left&&r.right<=rect.right,ink};
        }),
        offset:[...list.children].slice(0,2).map(b=>new DOMMatrix(getComputedStyle(b).transform).m41)
      }));
      check(data.background.endsWith('button_choosestage_'+art+'.png")'),art+' uses its own original HD artwork');
      check(data.entries.every(e=>e.centerError<1&&e.inside&&e.ink>0),art+' all stage names centered, contained and nonblank');
      check(data.offset[0]===0&&data.offset[1]===16,art+' selected stage retains the protruding offset');
      await page.locator('#campaign-battles .country-entry').nth(1).click();
      check(await page.locator('#campaign-battles').evaluate(l=>l.children[1].getAttribute('aria-pressed')==='true'&&l.getAnimations({subtree:true}).length===0),'selection changes without replaying the entrance');
      const scroll=await page.locator('#campaign-battles').evaluate(l=>{l.scrollTop=l.scrollHeight;return {y:l.scrollTop,max:l.scrollHeight-l.clientHeight,last:l.lastElementChild.getBoundingClientRect().bottom,bottom:l.getBoundingClientRect().bottom};});
      check(scroll.y>0&&scroll.y===scroll.max&&scroll.last<=scroll.bottom+1,art+' last stage remains reachable');
      await page.locator('#campaign-battles').evaluate(l=>l.scrollTop=0);
      await page.waitForTimeout(50);await shot('chapter-'+art);report.chapters.push({art,...data});
    }
    await page.evaluate(()=>menus.openChapter(catalog.chapters[0]));
    check((await sample('campaign-battles',0)).rows[0].translate==='160px','chapter reentry restarts entrance');
    await finish('campaign-battles');
    await page.evaluate(async()=>{
      await menus.openChapter(catalog.chapters[0]);await menus.openChapter(catalog.chapters[1]);
      const l=document.getElementById('campaign-battles');
      window.sidebarReentry={count:l.getAnimations({subtree:true}).length,rows:l.children.length};
    });
    check(await page.evaluate(()=>sidebarReentry.count===sidebarReentry.rows+1),'rapid reentry cancels previous panel animation');
    await page.waitForTimeout(700);
    check(await page.locator('#campaign-battles').evaluate(l=>l.getAnimations({subtree:true}).length===0&&getComputedStyle(l).translate==='none'&&[...l.children].every(b=>getComputedStyle(b).translate==='none')),'normal playback finishes without retained translation or animation');
    await page.evaluate(async()=>{showMain();await chooseScenario(catalog.scenarios.find(s=>s.id==='conquest1'));});
    const before=await sample('country-list',0),middle=await sample('country-list',100);
    check(before.count>1&&before.rows[0].x>middle.rows[0].x,'conquest countries share the same right-side entrance');
    report.conquestAnimation={before,middle};await shot('conquest-sliding');await finish('country-list');
    check(await page.locator('#country-list').evaluate(l=>getComputedStyle(l.firstElementChild).backgroundImage.includes('button_choosestage_conquest.png')),'conquest retains its original conquest artwork');
    await page.locator('#country-list .country-entry').nth(1).click();
    check(await page.locator('#country-list').evaluate(l=>new DOMMatrix(getComputedStyle(l.children[0]).transform).m41===20&&getComputedStyle(l.children[1]).transform==='none'),'conquest selection still protrudes');
    await shot('conquest-selected');
    await page.evaluate(()=>chooseScenario(catalog.scenarios.find(s=>s.id==='conquest1')));
    await page.waitForTimeout(700);
    check(await page.locator('#country-list').evaluate(l=>l.getAnimations({subtree:true}).length===0&&getComputedStyle(l).translate==='none'),'conquest entrance completes on the real clock');
    for(const [width,height] of [[1920,1080],[390,844],[844,390]]){
      await page.setViewportSize({width,height});
      await page.evaluate(()=>{document.getElementById('countries').hidden=true;return menus.openChapter(catalog.chapters[2]);});
      await finish('campaign-battles');
      check(await page.locator('#campaign-battles').evaluate(l=>{const r=l.getBoundingClientRect(),b=l.firstElementChild.getBoundingClientRect();return r.left>=0&&r.top>=0&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1&&b.height>0;}),'sidebar fits '+width+'x'+height);
      await shot('campaign-'+width+'x'+height);
    }
    await page.emulateMedia({reducedMotion:'reduce'});
    await page.evaluate(()=>menus.openChapter(catalog.chapters[1]));
    check(await page.locator('#campaign-battles').evaluate(l=>l.getAnimations({subtree:true}).length===0),'reduced motion skips chapter entrance');
    await page.evaluate(()=>{document.getElementById('campaign-screen').hidden=true;return chooseScenario(catalog.scenarios.find(s=>s.id==='conquest1'));});
    check(await page.locator('#country-list').evaluate(l=>l.getAnimations({subtree:true}).length===0),'reduced motion skips country entrance');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing resources');
    console.log(JSON.stringify({checks:report.checks,errors:report.errors,missing:report.missing,screenshots:report.screenshots.length}));
  }catch(error){report.failure=error.message;await shot('failure');throw error;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
})();
