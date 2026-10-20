'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const dependencies=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=dependencies('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/frontend_refinement_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});page.setDefaultTimeout(5000);
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'Independent 5185 backend and automation-profile; shared typography, popup geometry, tutorial/campaign flow and movement. No device or original-pixel acceptance.'};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push([r.url(),r.status()]);});
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  const shot=async name=>{await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);};
  try{
    await page.goto('http://127.0.0.1:5185/');await page.waitForFunction(()=>typeof manifest!=='undefined'&&manifest);
    await page.getByRole('button',{name:'战役',exact:true}).click();
    report.chapter=await page.evaluate(()=>{
      document.querySelector('.chapter-entry[aria-label="反法同盟"]').click();
      const popup=document.querySelector('.chapter-preview'),a=popup.getAnimations()[0];a.pause();a.currentTime=70;
      return {scale:new DOMMatrix(getComputedStyle(popup).transform).a,origin:getComputedStyle(popup).transformOrigin,opacity:getComputedStyle(popup).opacity,flags:popup.querySelectorAll('.chapter-preview-flags>img').length};
    });
    check(report.chapter.scale>0&&report.chapter.scale<1&&report.chapter.opacity==='1','chapter preview expands outward without fading');
    check(report.chapter.flags===4&&await page.locator('#campaign-screen').isHidden(),'chapter preview contains coalition flags before entering chapter');await shot('chapter-expanding');
    await page.evaluate(()=>{const a=document.querySelector('.chapter-preview').getAnimations()[0];a.finish();});await shot('chapter-coalition');
    await page.locator('.chapter-entry[aria-label="美国的崛起"]').click();await page.waitForTimeout(240);
    check(await page.locator('.chapter-preview').count()===1&&await page.locator('.chapter-preview-flags>img').count()===1,'switching chapter replaces the preview');await shot('chapter-america');
    check(await page.locator('.chapter-preview').evaluate(e=>{const flag=e.querySelector('.chapter-preview-flags>img').getBoundingClientRect(),r=e.getBoundingClientRect(),a=document.querySelector('.chapter-entry[aria-label="美国的崛起"]'),scale=a.getBoundingClientRect().width/a.offsetWidth,p=getComputedStyle(e,'::after');return Math.abs((flag.left+flag.right-r.left-r.right)/2)<1&&Math.abs(r.left+(1+parseFloat(p.left)+15.5)*scale-(a.getBoundingClientRect().left+25*scale))<1;}),'flag is centered and pointer aligns to original marker tip');
    await page.locator('.chapter-preview-confirm').click();await page.locator('#campaign-screen').waitFor();check(true,'preview confirmation opens chapter battles');
    await page.evaluate(()=>showMain());
    await page.locator('#headquarters').click();
    await page.waitForFunction(()=>menus.data&&!document.querySelector('#hq-screen').hidden);
    if(!await page.locator('#hq-roster .commander-open').count()){
      await page.locator('#hq-princess').click();await page.locator('[data-native-id="btn_go_2"]').click();await page.waitForFunction(()=>menus.data.princesses[1].claimed);await page.locator('#close-detail').click();
    }
    await page.locator('#hq-roster .commander-open').first().waitFor();
    await shot('headquarters-shortcuts');
    check(await page.locator('.promote-commander>img').first().evaluate(e=>Math.abs(e.width/e.height-40/45)<.04),'promotion badge preserves original hexagon aspect ratio');
    await page.locator('#hq-roster .promote-commander').first().click();
    check(await page.locator('.native-layout').getAttribute('data-layout')==='form_generalupgrade','top-left roster shortcut opens promotion directly');
    await page.locator('#close-detail').click();
    check(await page.locator('#detail-modal').isHidden(),'direct promotion returns to roster');
    await page.locator('#hq-roster .commander-open').first().click({position:{x:60,y:120}});
    check(await page.locator('.commander-detail').isVisible(),'portrait body opens commander information');
    check(await page.locator('.commander-detail-card .promote-commander').count()===1&&await page.locator('.commander-ranks .promote-commander').count()===0,'promotion belongs to portrait top left');
    check(await page.locator('.commander-info').first().evaluate(e=>e.offsetWidth>=20)&&await page.locator('.skill-info').first().evaluate(e=>e.offsetWidth>=18),'portrait and skill information controls are enlarged');
    await page.locator('.skill-info').first().hover();
    check(await page.locator('.skill-tooltip').isVisible(),'skill description opens on hover');
    await page.locator('.skill-info').first().click();await page.mouse.move(640,680);
    check(await page.locator('.skill-tooltip').evaluate(e=>{const s=getComputedStyle(e,'::after');return s.backgroundImage.includes('box_skilldetail_arrow.png')&&s.transform==='none';}),'skill tooltip uses the original horizontal arrow');
    check(await page.locator('.skill-tooltip').isVisible(),'clicked skill description remains open');await page.waitForTimeout(100);await shot('skill-description');
    await page.keyboard.press('Escape');check(await page.locator('.skill-tooltip').count()===0,'Escape dismisses skill description');
    await page.locator('.skill-info').first().click();await page.locator('.commander-abilities').click({position:{x:80,y:40}});
    check(await page.locator('.skill-tooltip').count()===0,'outside click dismisses skill description');
    await page.locator('#close-detail').click();await page.locator('#hq-roster .commander-open').first().click({position:{x:80,y:195}});
    check(await page.locator('.commander-detail').isVisible(),'name plate also opens commander information');
    await page.locator('#close-detail').click();await page.locator('#hq-recruit').click();await page.locator('#hq-roster .commander-open').first().click();
    check(await page.locator('#hq-buy-buttons').evaluate(e=>e.getAnimations().length>0),'academy cost controls animate when selecting a general');await page.locator('#hq-back').click();await page.locator('#hq-back').click();
    await page.evaluate(()=>start('conquest2','usa'));
    await page.evaluate(async()=>{
      await document.fonts.ready;
      await Promise.all(['font_city_labels_hd'].map(name=>{const img=originalTextRenderer.sheet(name);return img.complete?Promise.resolve():new Promise(resolve=>img.addEventListener('load',resolve,{once:true}));}));
    });
    report.city=await page.evaluate(async()=>{
      const measure=language=>{
        settings.cityLanguage=language;
        const c=battlefield.api.cityLabel('LEXINGTON').firstElementChild,d=c.getContext('2d'),pixels=d.getImageData(0,0,c.width,c.height).data;
        let top=c.height,bottom=0,white=0,dark=0,ink=0;
        for(let i=0;i<pixels.length;i+=4)if(pixels[i+3]>128){ink++;if(pixels[i]>=240&&pixels[i+1]>=240&&pixels[i+2]>=240)white++;if(pixels[i]<64&&pixels[i+1]<64&&pixels[i+2]<64)dark++;const y=Math.floor(i/4/c.width);top=Math.min(top,y);bottom=Math.max(bottom,y);}
        return {height:bottom-top+1,white,dark,ink};
      };
      const en=measure('en'),zh=measure('zh');settings.cityLanguage='en';battlefield.cityLabels.clear();
      const cell=state.cells.findIndex(c=>/lexington/i.test(c.name||''));view.x=point(cell).x;view.y=point(cell).y;return {en,zh,cell};
    });
    check(report.city.en.white>0&&report.city.en.dark>0&&report.city.en.white<report.city.en.ink*.8,'English city atlas retains white ink and dark outline');
    check(report.city.zh.white>0&&report.city.zh.dark>0,'Chinese city atlas has white ink and black outline');
    check(report.city.zh.height>=report.city.en.height*.9&&report.city.zh.height<=report.city.en.height*1.35,'Chinese city visible height matches English');
    await page.waitForTimeout(100);await shot('city-english');
    await page.evaluate(()=>{settings.cityLanguage='zh';battlefield.cityLabels.clear();});await page.waitForTimeout(100);await shot('city-chinese');
    await page.evaluate(()=>menus.shop());await page.waitForTimeout(100);await shot('shop');
    check(await page.locator('.dialog').filter({has:page.locator('[data-layout="form_shop"]')}).evaluate(e=>e.getAnimations().length>0),'headquarters shop uses shared popup animation');
    await page.waitForTimeout(140);await page.locator('[data-native-id="grid_seller"] .native-item:not(:disabled)').first().click();
    check(await page.locator('[data-native-id="btn_buy"]').evaluate(e=>e.getAnimations().length>0),'shop purchase control animates from its selected item');await page.waitForTimeout(230);
    check(await page.locator('[data-native-id="btn_buy"]').evaluate(e=>{const r=e.getBoundingClientRect(),s=e.firstElementChild.getBoundingClientRect(),i=document.querySelector('.native-item[aria-pressed=true]').getBoundingClientRect();return Math.abs((r.left+r.right-s.left-s.right)/2)<1&&Math.abs((r.top+r.bottom-s.top-s.bottom)/2)<1&&Math.abs(r.left-i.left)<8&&Math.abs(r.top-i.bottom)<1;}),'purchase button meets selected item and centers its caption');await shot('shop-purchase-aligned');
    check(await page.evaluate(()=>{const l=document.querySelector('.native-layout').nativeLayout;return l.get('group_seller/text_name').nativeAttributes.color==='235,230,225,255'&&l.get('text_desc').nativeAttributes.color==='235,230,225,255';}),'shop dark panels use light lettering');
    await page.evaluate(()=>menus.commerceStars(document.querySelector('.native-layout').nativeLayout,3));
    check(await page.locator('[data-native-id="lbox_stars"] .commerce-star-empty').count()===2,'inactive stars remain dark and fully shaped');await shot('commerce-stars');
    await page.locator('#close-detail').click();await page.evaluate(()=>start('conquest1','rus'));
    await page.evaluate(()=>menus.exchange(1799));
    for(const size of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(size);await page.waitForTimeout(80);
      const quotes=await page.locator('.exchange-quote').evaluateAll(es=>es.map(e=>{
        const b=e.parentElement.getBoundingClientRect(),r=e.getBoundingClientRect();
        const children=[...e.children].map(n=>n.getBoundingClientRect());
        return {fits:children.every(n=>n.left>=b.left-1&&n.right<=b.right+1),centered:children.every(n=>Math.abs((n.top+n.bottom-r.top-r.bottom)/2)<1),count:children.length};
      }));
      check(quotes.length===8&&quotes.every(q=>q.fits&&q.centered&&q.count===5),'all eight quotes fit and share vertical center at '+size.width);
      check(await page.locator('.native-layout').evaluate(e=>{const d=e.parentElement.getBoundingClientRect();return [...e.querySelectorAll('[data-native-id^="group_"]')].every(n=>{const r=n.getBoundingClientRect();return r.left>=d.left-1&&r.right<=d.right+1;});}),'market gray panels stay within window at '+size.width);
      await shot('exchange-'+size.width);
    }
    await page.locator('#close-detail').click();await page.setViewportSize({width:1280,height:720});
    await page.evaluate(()=>start('conquest1','fra'));
    await page.evaluate(async()=>{
      await selectCell(2158,true);const cell=selection.range.find(i=>i!==2158&&!state.units.some(u=>u.cell===i));
      await act('move',{unit:selected.id,cell});await selectCell(2158,true);openRecruitment(2158);
    });
    await page.locator('.recruit-cards>button').first().click();
    report.formation=await page.evaluate(()=>{
      const b=document.querySelector('.recruit-cards>button'),old=b.querySelector('.formation-outgoing'),a=old?.getAnimations()[0];
      if(!a)return null;a.pause();a.currentTime=120;const transform=new DOMMatrix(getComputedStyle(old).transform);
      return {y:transform.m42,alpha:Number(getComputedStyle(old).opacity),newY:new DOMMatrix(getComputedStyle(b.firstElementChild).transform).m42,costs:old.querySelectorAll('.cost-strip').length};
    });
    check(report.formation&&report.formation.y<0&&report.formation.alpha>0&&report.formation.alpha<1&&report.formation.newY===0&&report.formation.costs===0,'only old portrait rises and fades; new card and costs remain fixed');await shot('formation-midpoint');
    await page.evaluate(()=>{const b=document.querySelector('.recruit-cards>button');b.click();b.click();b.click();});await page.waitForTimeout(300);
    await page.waitForFunction(()=>!document.querySelector('.formation-outgoing'));
    check(true,'rapid formation changes leave no ghosts');await shot('formation-settled');
    await page.locator('#close-detail').click();
    await page.evaluate(async()=>{
      const u=state.units.find(u=>u.owner===state.playerFaction&&u.category==='cavalry'&&u.canMove);await selectCell(u.cell,true);
      const destinations=selection.range.filter(i=>i!==u.cell&&!state.units.some(u=>u.cell===i));
      const origin=point(u.cell),cell=destinations.sort((a,b)=>Math.hypot(point(b).x-origin.x,point(b).y-origin.y)-Math.hypot(point(a).x-origin.x,point(a).y-origin.y))[0];
      view.x=origin.x;view.y=origin.y;window.refinementMove=act('move',{unit:u.id,cell});
    });
    await page.waitForFunction(()=>motions.size>0);await page.waitForTimeout(160);
    report.movement=await page.evaluate(()=>({step:[...motions.values()][0]?.stepMs,expected:255/Math.sqrt(settings.speed),dust:effects.instances.filter(i=>i.layer==='ground').map(i=>({scale:i.scale,particles:i.particles.length}))}));
    check(Math.abs(report.movement.step-report.movement.expected)<.01,'movement duration uses the calibrated speed');
    check(report.movement.dust.length>0&&report.movement.dust.every(i=>i.scale===.5&&i.particles<=7),'moving dust uses map scale and sparse emission');await shot('cavalry-movement');
    await page.evaluate(()=>window.refinementMove);await page.waitForTimeout(1100);
    check(await page.evaluate(()=>!effects.instances.some(i=>i.layer==='ground')),'dust expires after movement');
    await page.evaluate(async()=>{
      const enemy=state.units.find(u=>Ew4Battlefield.selectionHostile(state,u.cell));await selectCell(enemy.cell,true);view.x=point(enemy.cell).x;view.y=point(enemy.cell).y;
    });await page.waitForTimeout(100);
    check(await page.evaluate(()=>{const c=battlefield.redChoose?.texture;if(!c)return false;const d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let red=0;for(let i=0;i<d.length;i+=4)if(d[i+3]>128&&d[i]>80&&d[i+1]===0&&d[i+2]===0)red++;return red>20;}),'enemy selection texture retains visible red corners');await shot('enemy-selection');
    await page.evaluate(()=>{window.refinementLoading=start('tutorials1',null);});await page.locator('#loading-screen').waitFor();
    check(await page.locator('#loading-tip .original-text').getAttribute('aria-label')!==''&&await page.locator('#loading-word .original-text').getAttribute('aria-label')==='LOADING...','scenario loading displays a source tip and loading label');await shot('scenario-loading');await page.evaluate(()=>window.refinementLoading);
    for(const id of ['tutorials1','tutorials2']){
      if(id==='tutorials2')await page.evaluate(()=>start('tutorials2',null));
      for(const size of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
        await page.setViewportSize(size);await page.waitForTimeout(80);
        check(await page.locator('#tutorial-talk').evaluate(e=>{
          const p=e.querySelector('#tutorial-portrait').getBoundingClientRect(),t=e.querySelector('#tutorial-text').getBoundingClientRect(),s=e.querySelector('#tutorial-speaker').getBoundingClientRect(),b=e.getBoundingClientRect();
          return !e.hidden&&t.left>=p.right&&s.bottom<=t.top+1&&t.right<=b.right&&t.bottom<=innerHeight+1&&b.left>=0&&b.right<=innerWidth+1&&getComputedStyle(e,'::before').borderImageSource.includes('board_dialog_ex.png')&&!!e.querySelector('#tutorial-text canvas');
        }),id+' dialogue text and portrait fit at '+size.width);await shot(id+'-'+size.width);
      }
      await page.evaluate(()=>tutorial.notify({type:'touch'}));
      check(await page.evaluate(expected=>tutorial.wait?.name===expected&&!document.querySelector('#tutorial-talk').hidden,id==='tutorials1'?'wait touch':'wait area'),'tutorial touch advances to the next source wait');
    }
    await page.evaluate(async()=>{let failed=false;try{await withLoading(()=>Promise.reject(Error('loading-cleanup-probe')));}catch{failed=true;}window.refinementLoadingFailed=failed;});
    check(await page.evaluate(()=>refinementLoadingFailed&&document.querySelector('#loading-screen').hidden),'failed startup removes loading overlay');
    await page.setViewportSize({width:1280,height:720});await page.evaluate(()=>start('tutorials1',null));
    report.shopWorldBefore=await page.evaluate(()=>({scenario:state.scenario,map:state.map,units:state.units.length,active:state.cells.filter(c=>c.active).length,view:{...view}}));
    await page.evaluate(()=>menus.shop(state.cells.findIndex(c=>c.facility==='shop'&&c.owner===state.playerFaction)));await page.locator('#close-detail').click();await page.waitForTimeout(300);
    check(await page.evaluate(before=>state.scenario===before.scenario&&state.map===before.map&&state.units.length===before.units&&state.cells.filter(c=>c.active).length===before.active&&Math.abs(view.x-before.view.x)<.01&&Math.abs(view.y-before.view.y)<.01,report.shopWorldBefore),'closing in-game shop preserves scenario, objects and camera');await shot('tutorial-after-shop');
    await page.evaluate(async()=>{await rpc('start',{scenario:'conquest2',country:'usa'});await menus.api.refresh();});
    check(await page.evaluate(()=>{const b=Ew4Battlefield.cameraBounds(state,manifest.maps[state.map]);return state.scenario==='conquest2'&&JSON.stringify(b)===JSON.stringify(mapBounds)&&state.units.some(u=>Math.abs(point(u.cell).x-view.x)<500&&Math.abs(point(u.cell).y-view.y)<500);}),'replacing a backend map resets bounds and recenters the scene');
    check(report.errors.length===0&&report.missing.length===0,'no script or resource failures');
  }catch(error){report.failure=error.message;throw error;}
  finally{await fs.writeFile(folder+'/browser-verification.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
