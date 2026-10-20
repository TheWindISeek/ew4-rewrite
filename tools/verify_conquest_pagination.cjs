'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/conquest_pagination_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'5185 isolated menu-only tests; configured conquest pages, original six scenarios, empty slots, native arrow assets, animation, paging guards and four viewports. No gameplay or save writes on 5184.'};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push([r.url(),r.status()]);});
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');};
  const settled=()=>page.waitForFunction(()=>!conquestEntering);
  const open=async()=>{await page.locator('[data-mode="2"]').click();await settled();};
  const sample=async time=>page.evaluate(async time=>{
    const rows=[...document.querySelector('#scenarios').children],animations=rows.flatMap(r=>r.getAnimations());
    for(const a of animations)a.pause();await Promise.all(animations.map(a=>a.ready));
    for(const a of animations)a.currentTime=time;
    await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
    return rows.map(r=>({x:new DOMMatrix(getComputedStyle(r).transform).m41,delay:r.getAnimations()[0]?.effect.getTiming().delay}));
  },time);
  const finish=()=>page.evaluate(()=>{for(const a of document.querySelector('#scenarios').getAnimations({subtree:true}))a.finish();});
  try{
    await page.goto('http://127.0.0.1:5185/');await page.waitForFunction(()=>!!catalog&&!!conquestPages);
    await open();
    check(await page.locator('#scenarios button').count()===6,'original page has all six playable conquests');
    check((await page.locator('#scenarios button').evaluateAll(rows=>rows.map(r=>r.dataset.scenario))).join(',')==='conquest1,conquest2,conquest3,conquest4,conquest5,conquest6','original scenario identity and order preserved');
    check(await page.locator('#conquest-page-number').getAttribute('aria-label')==='第1页，共2页','initial page indicator');
    check(await page.locator('#conquest-prev').isDisabled()&&await page.locator('#conquest-next').isEnabled(),'first-page bounds');
    const originals=await page.locator('#scenarios button').evaluateAll(rows=>rows.map(r=>({art:r.querySelector('.scenario-art').getAttribute('src'),labels:[...r.querySelectorAll('.conquest-caption img')].map(i=>i.getAttribute('src')),ready:[...r.querySelectorAll('img')].every(i=>i.complete&&i.naturalWidth>0)})));
    check(originals.every(r=>r.ready&&r.labels.length===2),'all original art and labels render');report.originals=originals;await shot('original-page');
    await page.evaluate(()=>document.getElementById('conquest-next').click());
    const beginning=await sample(0),middle=await sample(160);
    check(beginning.every((r,i)=>r.x===(i%2?568:-568)),'new-page frames enter from opposite sides');
    check(middle.every((r,i)=>Math.abs(r.x)<568&&Math.sign(r.x)===(i%2?1:-1)),'frames move toward the middle');
    check(middle.every((r,i)=>r.delay===Math.floor(i/2)*60),'pairs retain original staggered entrance');
    check(await page.locator('#conquest-prev').isDisabled()&&await page.locator('#conquest-next').isDisabled(),'paging locked during entry');
    await page.evaluate(()=>{turnConquestPage(-1);turnConquestPage(1);});
    check(await page.evaluate(()=>conquestPage===1),'rapid repeated commands cannot overlap pages');
    await shot('reserved-page-entering');report.animation={beginning,middle};await finish();await settled();
    check(await page.locator('#scenarios .scenario-placeholder').count()===6&&await page.locator('#scenarios button').count()===0,'second page has six noninteractive empty frames');
    check(await page.locator('#scenarios').evaluate(l=>[...l.children].every(r=>r.textContent===''&&!r.querySelector('img')&&getComputedStyle(r).backgroundColor==='rgb(104, 104, 102)')),'placeholders are empty gray frames');
    check(await page.locator('#conquest-page-number').getAttribute('aria-label')==='第2页，共2页','second page indicator');
    check(await page.locator('#conquest-prev').isEnabled()&&await page.locator('#conquest-next').isDisabled(),'last-page bounds');
    await shot('reserved-page');
    await page.locator('#menu-back').click();await open();
    check(await page.evaluate(()=>conquestPage===1),'menu reentry retains the chosen page');
    await page.locator('#conquest-prev').click();await settled();
    await page.locator('#scenarios button').first().click();await page.locator('#countries').waitFor();
    check(await page.evaluate(()=>chosenScenario==='conquest1'&&countryChoices.length>0),'original conquest still opens the correct country selector');
    await page.locator('#cancel-country').click();check(await page.locator('#conquest-pagination').isVisible(),'country-selector back returns to paging');
    await page.locator('#menu-back').click();await page.locator('[data-mode="1"]').click();
    check(await page.locator('#conquest-pagination').isHidden()&&await page.locator('.chapter-entry').count()===6,'campaign chapter menu remains unchanged');
    await page.locator('.chapter-entry').first().click();await page.locator('.chapter-preview-confirm').click();await page.locator('#campaign-screen').waitFor();
    check(await page.locator('#campaign-battles .country-entry').count()>0,'campaign chapter flow still works');
    await page.evaluate(()=>showMain());await open();
    for(const [width,height] of [[1920,1080],[1280,720],[844,390],[390,844]]){
      await page.setViewportSize({width,height});
      for(let number=0;number<2;number++){
        if(await page.evaluate(()=>conquestPage)!==number){await page.locator(number?'#conquest-next':'#conquest-prev').click();await settled();}
        const layout=await page.evaluate(()=>{
          const grid=document.getElementById('scenarios').getBoundingClientRect(),pager=document.getElementById('conquest-pagination').getBoundingClientRect();
          return {gap:pager.top-grid.bottom,inside:pager.right<=innerWidth+1&&pager.bottom<=innerHeight+1&&pager.left>=0&&pager.top>=0,
            boxes:[...document.querySelector('#scenarios').children].map(r=>{const b=r.getBoundingClientRect();return b.left>=0&&b.right<=innerWidth+1&&b.bottom<=pager.top;})};
        });
        check(layout.inside&&layout.gap>0&&layout.boxes.every(Boolean),'cards and page controls fit without overlap '+width+'x'+height+' page '+(number+1));
        await shot('page-'+(number+1)+'-'+width+'x'+height);
      }
    }
    await page.emulateMedia({reducedMotion:'reduce'});await page.locator('#conquest-prev').click();await settled();
    check(await page.locator('#scenarios').evaluate(l=>l.getAnimations({subtree:true}).length===0),'reduced motion skips entry animations');
    await page.evaluate(()=>{conquestPages.pages.push({id:'test-third',slots:[null]});scenarioList();turnConquestPage(1);turnConquestPage(1);});await settled();
    check(await page.evaluate(()=>conquestPage===2)&&await page.locator('#scenarios .scenario-placeholder').count()===6,'configuration can add a third page and pads empty slots');
    check(await page.locator('#conquest-page-number').getAttribute('aria-label')==='第3页，共3页','page count follows configuration');
    await page.evaluate(()=>{conquestPages.pages[2].slots=[{scenario:'conquest6'}];scenarioList();});await settled();
    check(await page.locator('#scenarios button[data-scenario="conquest6"]').count()===1&&await page.locator('#scenarios .scenario-placeholder').count()===5,'configured content replaces one slot without code edits');
    check(await page.locator('#scenarios .scenario-fallback canvas').evaluate(c=>c.getContext('2d').getImageData(0,0,c.width,c.height).data.some((v,i)=>i%4===3&&v>0)),'entry without dedicated art has a nonblank original-font caption');
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing resources');
    console.log(JSON.stringify({checks:report.checks,errors:report.errors,missing:report.missing,screenshots:report.screenshots.length}));
  }catch(error){report.failure=error.message;await shot('failure');throw error;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
})();
