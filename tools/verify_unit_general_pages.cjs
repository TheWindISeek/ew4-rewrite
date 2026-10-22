'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5278',folder=path.resolve('rewrite/reports/unit-general-20261022');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  const report={checks:0,errors:[],missing:[],screenshots:[]},check=(v,s)=>{assert.ok(v,s);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const shot=async name=>{await page.waitForFunction(()=>[...document.querySelectorAll('#detail-modal:not([hidden]) img,#hq-screen:not([hidden]) img')].every(i=>i.complete&&i.naturalWidth));await page.evaluate(async()=>{await Promise.all(document.getAnimations().map(a=>a.finished.catch(()=>{})));});await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name);};
  try{
    await page.goto(base);await page.waitForFunction(()=>startupReady);
    const held=await page.evaluate(async()=>{await menus.headquarters();if(!menus.data.roster.length)await menus.updateAccount({op:'recruit',commander:menus.data.offers[0][0]});const held=menus.data.roster[0];await menus.updateAccount({op:'tags',taggedCommanders:[held.id]});return menus.data.roster[0];});
    await page.evaluate(()=>start('conquest1','fra'));
    const units=await page.evaluate(()=>({plain:state.units.find(u=>!u.commanderId).id,friendly:state.units.find(u=>u.commanderId&&u.owner===state.playerFaction).id,enemy:state.units.find(u=>u.commanderId&&u.owner!==state.playerFaction).id}));
    for(const [kind,id] of Object.entries(units)){
      await page.evaluate(id=>openUnitInfo(state.units.find(u=>u.id===id)),id);
      check(await page.locator('.unit-detail').getAttribute('data-layout')==='form_unitinfo',kind+' uses original layout');
      check(await page.locator('.unit-effect-region').count()===1,kind+' reserves skill/effect band');
      check(await page.locator('[data-native-id=group_info]').isVisible()===(kind!=='plain'),kind+' commander visibility');
      check(await page.evaluate(()=>document.querySelector('.unit-detail .cost-strip').textContent!=='0 0'),kind+' no invented recruitment zero price');
      if(kind!=='plain'){
        const expected=await page.evaluate(async id=>(await rpc('unit-info',{unit:id,revision:state.revision})).commander,id);
        await page.locator('[data-native-id=btn_info]').click();
        check(await page.locator('.commander-detail').isVisible(),kind+' opens commander detail');
        check(await page.locator('.commander-ranks .rank-gauge').evaluateAll((els,expected)=>els.every(e=>+e.dataset.level===expected.learning[e.dataset.track].level&&+e.dataset.experience===expected.learning[e.dataset.track].experience),expected),kind+' actual battle experience');
        check(await page.locator('.commander-equipment .regroup').count()===0,kind+' battle detail has original item-only control');
        await page.getByRole('button',{name:'物品',exact:true}).click();
        check(await page.locator('.native-layout').getAttribute('data-layout')==='form_deployitem',kind+' equipment is viewable');
        await page.locator('#close-detail').click();await page.locator('#close-detail').click();
        await page.waitForSelector('[data-native-id=btn_info]:visible');
        check(await page.locator('[data-native-id=btn_info]').isVisible(),kind+' return restores same unit');
      }
      await shot(kind+'-unit');await page.locator('#close-detail').click();
    }
    // Read-only queries, including stale/invalid requests, must not change a battle.
    const readOnly=await page.evaluate(async id=>{
      const before=await rpc('action',{op:'state',revision:state.revision});
      const result=await rpc('unit-info',{unit:id,revision:state.revision});
      const stale=await fetch('/api/unit-info',{method:'POST',body:JSON.stringify({unit:id,revision:state.revision+1})}).then(r=>r.json());
      const absent=await fetch('/api/unit-info',{method:'POST',body:JSON.stringify({unit:'absent',revision:state.revision})}).then(r=>r.json());
      return {equal:JSON.stringify(before)===JSON.stringify(await rpc('action',{op:'state',revision:state.revision})),cost:result.cost,stale:stale.code,absent:absent.code};
    },units.enemy);
    check(readOnly.equal,'detail queries never mutate battle');check(readOnly.stale==='client.stale','stale query rejected');check(readOnly.absent==='client.unit','absent unit rejected');check(readOnly.cost?.money>0,'real recruitment cost supplied');
    // Leave HQ scrolled before entering the deployment roster with the saved tag.
    await page.evaluate(async()=>{await menus.headquarters();menus.rosterRating=2;menus.drawRoster();document.getElementById('hq-roster').scrollLeft=500;});
    const target=await page.evaluate(()=>state.units.find(u=>u.owner===state.playerFaction&&!u.commanderId&&state.cells[u.cell].owner===state.playerFaction).id);
    await page.evaluate(async id=>menus.deployGeneral(id,state.revision),target);
    check(await page.locator('#hq-filters').isHidden(),'deployment has no HQ filters');
    check(await page.locator('#hq-tags,#hq-princess,#hq-shop').count()===0,'deployment has no HQ extra controls');
    check(await page.locator('#hq-roster [data-commander]').count()>=1,'HQ filter cannot hide deployment candidates');
    check(await page.locator('#hq-roster').evaluate(e=>e.scrollLeft===0),'deployment opens at the pinned first column');
    check(await page.locator('#hq-roster [data-commander="'+held.id+'"] .commander-tag').count()===1,'deployment displays persisted HQ tags');
    await page.locator('#hq-roster .commander-info').first().click();check(await page.locator('.commander-detail').isVisible(),'deployment information button works');await page.locator('#close-detail').click();
    const actualRoster=await page.evaluate(()=>menus.data.roster);
    // Use distinct definitions so the visual fixture covers all three star groups.
    await page.evaluate(()=>{
      const base=menus.data.roster[0],section=d=>Math.max(0,Math.min(2,Math.floor((d.rating-1)/3)));
      const definitions=[0,1,2].flatMap(group=>menus.data.definitions.filter(d=>section(d)===group).slice(0,6));
      menus.data.roster=definitions.map((d,i)=>({...base,id:'fixture.'+i,definitionId:d.id,locked:i===4,tag:i===3||i===16?'置顶':''}));
      menus.rosterSelected=null;menus.drawRoster();
    });
    const fixtureRoster=await page.evaluate(()=>menus.data.roster);
    check(await page.locator('#hq-roster [data-commander]').count()===18,'deployment keeps more than twelve commanders');
    check(await page.locator('#hq-roster [data-commander]').evaluateAll(cards=>cards.slice(0,2).every(c=>c.querySelector('.commander-tag'))&&cards.slice(2).every(c=>!c.querySelector('.commander-tag'))),'tagged commanders precede every untagged star group');
    check(await page.locator('#hq-roster [data-commander="fixture.4"] .deploy-card-confirm').isDisabled(),'unavailable commanders retain disabled deployment');
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});
      await page.evaluate(roster=>{menus.data.roster=roster;menus.rosterSelected=null;document.getElementById('hq-roster').scrollLeft=0;menus.drawRoster();},fixtureRoster);
      await shot(name+'-deployment');
      check(await page.locator('#hq-roster').evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1;}),name+' roster fits');
      const geometry=await page.locator('#hq-roster').evaluate(e=>{
        const r=e.getBoundingClientRect(),cards=[...e.children].map(c=>c.getBoundingClientRect());
        return {left:r.left,top:r.top,scale:r.width/e.offsetWidth,twoRows:new Set(cards.map(c=>Math.round(c.top))).size===2,
          contained:cards.every(c=>c.top>=r.top-1&&c.bottom<=r.bottom+1),
          alternating:cards.every((c,i)=>Math.abs(c.left-cards[i-i%2].left)<1&&(i%2?c.top>cards[i-1].top:!i||c.left>cards[i-2].left))};
      });
      check(geometry.twoRows&&geometry.contained&&geometry.alternating,name+' two full rows fill each column top then bottom');
      const x=geometry.left+250*geometry.scale,y=geometry.top+45*geometry.scale;
      await page.mouse.move(x,y);await page.mouse.down();await page.mouse.move(x-180*geometry.scale,y,{steps:12});await page.mouse.up();
      check(await page.locator('#hq-roster').evaluate(e=>e.scrollLeft>150),name+' mouse drag scrolls left');
      check(await page.evaluate(()=>menus.rosterSelected===null&&!document.getElementById('hq-screen').hidden&&document.getElementById('detail-modal').hidden),name+' drag does not select, inspect or deploy');
      await page.mouse.move(x-180*geometry.scale,y);await page.mouse.down();await page.mouse.move(x,y,{steps:12});await page.mouse.up();
      check(await page.locator('#hq-roster').evaluate(e=>e.scrollLeft<2),name+' mouse drag scrolls right');
      const cdp=await page.context().newCDPSession(page);
      for(const direction of [-1,1]){
        const start=direction<0?x:x-140*geometry.scale;
        await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:start,y}]});
        for(let step=1;step<=10;step++)await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:start+direction*step*14*geometry.scale,y}]});
        await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
        check(await page.locator('#hq-roster').evaluate((e,d)=>d<0?e.scrollLeft>100:e.scrollLeft<2,direction),name+' touch swipe '+(direction<0?'left':'right'));
      }
      await cdp.detach();
      check(await page.evaluate(()=>menus.rosterSelected===null&&document.getElementById('detail-modal').hidden),name+' touch swipe does not select or inspect');
      await page.locator('#hq-roster').evaluate(e=>{e.scrollLeft=e.scrollWidth;});
      check(await page.locator('#hq-roster').evaluate(e=>{const r=e.getBoundingClientRect(),last=e.lastElementChild.getBoundingClientRect();return last.left>=r.left-1&&last.right<=r.right+1;}),name+' last commander is fully reachable');
      await shot(name+'-deployment-last');await page.locator('#hq-roster').evaluate(e=>{e.scrollLeft=0;});
      const selected=page.locator('#hq-roster [data-commander]').first();await selected.locator('.commander-open').click();
      check(await selected.evaluate(e=>{const card=e.getBoundingClientRect(),button=e.querySelector('.deploy-card-confirm').getBoundingClientRect();return Math.abs((card.left+card.right-button.left-button.right)/2)<.5&&button.left>=card.left&&button.right<=card.right&&button.bottom<=card.bottom+.5;}),name+' deploy button is centered within commander slot');
      await shot(name+'-deployment-selected');await page.evaluate(()=>{menus.rosterSelected=null;menus.drawRoster();});
      await page.evaluate(id=>openUnitInfo(state.units.find(u=>u.id===id)),units.enemy);await shot(name+'-detail');
      check(await page.locator('#detail-modal>.dialog').evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=-1&&r.top>=-1&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1;}),name+' unit detail fits');
      check(await page.evaluate(()=>{const rect=id=>document.querySelector('[data-native-id='+id+']').getBoundingClientRect(),boxes=['group_unit','group_ability','group_info'].map(rect);return boxes.every(r=>Math.abs(r.top-boxes[0].top)<1&&Math.abs(r.bottom-boxes[0].bottom)<1)&&Math.abs(boxes[0].width-boxes[2].width)<1;}),name+' upper three panels align and side cards have equal widths');
      report.clipping=await page.locator('.info-abilities img,.info-abilities canvas').evaluateAll(nodes=>nodes.flatMap(n=>{const r=n.getBoundingClientRect(),cell=n.closest('.ability-grid>span').getBoundingClientRect(),panel=n.closest('[data-native-id=group_ability]').getBoundingClientRect();return r.left>=cell.left-.5&&r.right<=cell.right+.5&&r.top>=cell.top-.5&&r.bottom<=cell.bottom+.5&&r.bottom<=panel.bottom+.5?[]:[{text:n.parentElement.getAttribute('aria-label'),source:n.getAttribute('src'),node:r.toJSON(),cell:cell.toJSON(),panel:panel.toJSON()}];}));
      check(report.clipping.length===0,name+' all attribute icons and digits fit inside cells and panel');
      check(await page.locator('[data-native-id=group_info] img').evaluateAll(nodes=>nodes.every(n=>{const r=n.getBoundingClientRect(),panel=n.closest('[data-native-id=group_info]').getBoundingClientRect();return r.left>=panel.left-.5&&r.right<=panel.right+.5&&r.top>=panel.top-.5&&r.bottom<=panel.bottom+.5;})),name+' portrait and badges do not cross panel bottom');
      await page.locator('[data-native-id=btn_info]').click();await shot(name+'-commander');await page.locator('#close-detail').click();await page.waitForSelector('[data-native-id=btn_info]:visible');await page.locator('#close-detail').click();
    }
    await page.evaluate(roster=>{menus.data.roster=roster;document.getElementById('hq-roster').scrollLeft=0;menus.drawRoster();},actualRoster);
    const candidate=page.locator('#hq-roster [data-commander="'+held.id+'"]');
    await candidate.locator('.commander-open').click();
    await candidate.locator('.deploy-card-confirm').click();
    await page.waitForFunction(id=>state.units.some(u=>u.commanderId===id),held.id);
    check(await page.locator('#hq-screen').isHidden()&&await page.locator('#play').isVisible(),'actual appointment returns to battle');
    const deployed=await page.evaluate(async id=>(await rpc('unit-info',{unit:id,revision:state.revision})).commander,target);
    check(deployed.id===held.id&&deployed.definitionId===held.definitionId,'owned deployment queries correct commander instance');
    // A long extension list scrolls inside its reserved area and returns from descriptions.
    await page.evaluate(id=>{const u=state.units.find(u=>u.id===id);u.statuses=Array.from({length:14},(_,i)=>({id:'test.'+i,name:'效果 '+i,description:'效果说明 '+i}));return openUnitInfo(u);},target);
    check(await page.locator('.unit-skill-row').count()>=14,'extension labels preserved');
    check(await page.locator('.unit-skill-list').evaluate(e=>e.scrollHeight>e.clientHeight),'long effect list scrolls');
    await page.locator('.unit-skill-row').first().click();await page.locator('#close-detail').click();await page.waitForSelector('.unit-detail');check(await page.locator('.unit-skill-row').count()>=14,'description returns to same list');await shot('extension-tags');
    check(report.errors.length===0,'no browser errors');check(report.missing.length===0,'no missing sprites');
  }catch(e){report.failure=e.message;await page.screenshot({path:path.join(folder,'failure.png')});throw e;}finally{await fs.writeFile(path.join(folder,'results.json'),JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
