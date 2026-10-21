'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const deps=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=deps('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/victory-mod-20261021';
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  page.setDefaultTimeout(30000);
  const report={checks:0,errors:[],missing:[],screenshots:[]};
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto(process.env.VICTORY_MOD_URL||'http://127.0.0.1:5213/');
    await page.waitForFunction(()=>startupReady);
    await page.evaluate(()=>load(1,0));
    await page.waitForSelector('[data-layout="form_mod_victory"]:visible');
    const receipt=await page.evaluate(()=>({id:state.battleResult.id,medals:state.medals}));
    check(await page.evaluate(()=>state.battleResult.presentation.bindings.exit==='leave'),'packaged screen selected by backend');
    check(await page.locator('[data-native-id="next"]').isVisible()&&await page.locator('[data-native-id="retry"]').isHidden(),'victory command visibility');
    for(const [name,width,height] of [['desktop',1280,720],['landscape',844,390],['portrait',390,844]]){
      await page.setViewportSize({width,height});await page.waitForTimeout(300);
      const geometry=await page.evaluate(()=>{
        const layout=document.querySelector('.victory-result'),dialog=layout.parentElement.getBoundingClientRect();
        const bounds=el=>{const r=el.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right,bottom:r.bottom};};
        return {dialog:bounds(layout.parentElement),buttons:[...layout.querySelectorAll('button:not([hidden])')].map(bounds),
          slots:layout.querySelector('[data-native-id="officers"]').children.length,
          stars:layout.querySelector('[data-native-id="rating"]').children.length,
          broken:[...layout.querySelectorAll('img')].filter(i=>!i.complete||!i.naturalWidth).length,
          generalRow:bounds(layout.querySelector('.result-generals')),
          generalCards:[...layout.querySelectorAll('.result-general')].map(bounds),
          pixels:[...layout.querySelectorAll('canvas')].reduce((n,c)=>{const d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;for(let i=3;i<d.length;i+=4)if(d[i])n++;return n;},0),
          overlaps:[...layout.querySelectorAll('[data-native-id$="value"]')].some(el=>{const r=el.getBoundingClientRect();return r.x<dialog.x||r.right>dialog.right;})};
      });
      const inside=r=>r.x>=0&&r.y>=0&&r.right<=width&&r.bottom<=height;
      if(!inside(geometry.dialog))console.error(JSON.stringify({name,width,height,geometry}));
      check(inside(geometry.dialog),name+' custom dialog fits');
      check(geometry.buttons.every(inside),name+' commands fit');
      check(geometry.slots>=6&&geometry.stars===5,name+' lists rendered');
      const cards=geometry.generalCards,row=geometry.generalRow;
      check(Math.abs(cards[0].x-row.x)<1&&Math.abs(cards.at(-1).right-row.right)<1,name+' generals span row evenly');
      check(cards.every((r,i)=>Math.abs((r.bottom-r.y)/(r.right-r.x)-98/78)<.02&&(!i||r.x>cards[i-1].right)),name+' original card ratio and separate slots');
      check(geometry.broken===0&&geometry.pixels>100,name+' images and text visible');
      check(!geometry.overlaps,name+' numeric values fit');
      await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');
    }
    await page.evaluate(()=>{closeDetail();refreshHud();refreshHud();});
    check(await page.locator('#detail-modal').isHidden(),'dismiss survives repeated refresh');
    check(await page.evaluate(()=>state.medals)===receipt.medals,'screen refresh does not award');
    await page.evaluate(()=>load(2,0));
    await page.waitForSelector('[data-native-id="retry"]:visible');
    check(await page.locator('[data-native-id="next"]').isHidden(),'defeat hides continue');
    await page.screenshot({path:folder+'/defeat.png'});report.screenshots.push('defeat.png');
    await page.locator('[data-native-id="retry"]').click();
    await page.waitForFunction(()=>!busy&&state.battleResult===null&&state.scenario==='conquest1');
    check(await page.evaluate(()=>state.startCountry==='fra'&&state.victoryRules.win.round===4),'custom restart resets country and initial stage');
    await page.evaluate(()=>load(1,0));await page.waitForSelector('[data-native-id="next"]:visible');
    await page.locator('[data-native-id="next"]').click();
    check(await page.locator('#main-menu').isVisible(),'continue from final conquest returns to menu');
    await page.evaluate(()=>start('campaign1_01',null));
    await page.waitForFunction(()=>!busy);
    check(await page.evaluate(()=>state.battleResult===null),'other scenario still starts normally');
    await page.evaluate(()=>load(1,0));await page.waitForSelector('[data-native-id="leave"]:visible');
    await page.locator('[data-native-id="leave"]').click();
    check(await page.locator('#main-menu').isVisible(),'custom exit command works');
    await page.setViewportSize({width:1280,height:720});
    await page.evaluate(async()=>{
      $('play').hidden=false;$('menu').hidden=true;
      const definitions=(await rpc('headquarters')).definitions.slice(0,5);
      globalThis.resultCardFixture={...state.battleResult,generals:definitions.map((d,i)=>({
        id:d.id,name:d.name,image:d.image,rank:i+1,title:i+1,withdrawn:false
      }))};
      victoryUi.reset();victoryUi.open(resultCardFixture);
      await Promise.all([...document.querySelectorAll('.victory-result img')].map(i=>i.decode()));
    });
    await page.waitForTimeout(300);
    check(await page.locator('.result-general>img:first-child').count()===6,'five portraits and one empty slot');
    await page.screenshot({path:folder+'/generals.png'});report.screenshots.push('generals.png');
    const originalResult=JSON.parse(await fs.readFile('E:/Workplace/game/o4mod/rewrite/reports/victory_settlement_20261021/completed-state.json','utf8')).battleResult;
    await page.evaluate(result=>{victoryUi.reset();victoryUi.open({...result,generals:resultCardFixture.generals,presentation:null});},originalResult);
    await page.waitForSelector('[data-layout="form_victory"]:visible');await page.waitForTimeout(300);
    await page.evaluate(()=>Ew4OriginalUI.prepareText('0123456789',['EW4 Serif','EW4 Unicode']));
    check(await page.evaluate(()=>['text_star','text_star2'].every(id=>{
      const el=document.querySelector('[data-native-id="'+id+'"]'),text=el.querySelector('.original-text');
      const r=el.getBoundingClientRect(),p=el.parentElement.getBoundingClientRect();
      return text.dataset.color==='#ffffff'&&text.dataset.typeface.startsWith('"EW4 Serif"')&&r.right<=p.right&&r.left>=p.left;
    })),'white serif threshold digits stay inside rating board');
    check(await page.evaluate(()=>['text_roundval','text_award_val','text_gain_val'].every(id=>
      document.querySelector('[data-native-id="'+id+'"] .original-text').dataset.typeface.startsWith('"EW4 Serif"'))),'result numbers use serif face');
    check(await page.evaluate(()=>{const row=document.querySelector('.result-generals'),cards=[...row.children],r=row.getBoundingClientRect();
      return Math.abs(cards[0].getBoundingClientRect().left-r.left)<1&&Math.abs(cards.at(-1).getBoundingClientRect().right-r.right)<1;
    }),'original layout has aligned six-slot row');
    await page.screenshot({path:folder+'/original-generals.png'});report.screenshots.push('original-generals.png');
    for(const [name,width,height] of [['original-landscape',844,390],['original-portrait',390,844]]){
      await page.setViewportSize({width,height});await page.waitForTimeout(250);
      check(await page.evaluate(()=>['text_star','text_star2'].every(id=>{
        const el=document.querySelector('[data-native-id="'+id+'"]'),r=el.getBoundingClientRect(),p=el.parentElement.getBoundingClientRect();
        return r.right<=p.right+.1&&r.right<=innerWidth&&r.left>=0&&el.firstChild.dataset.color==='#ffffff';
      })),name+' white threshold digits fit');
      await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name+'.png');
    }
    await page.setViewportSize({width:1280,height:720});
    await page.evaluate(result=>{victoryUi.reset();victoryUi.open({...result,generals:[],presentation:null,minimumRatingRound:999,highestRatingRound:1000});},originalResult);
    check(await page.evaluate(()=>['text_star','text_star2'].every(id=>{
      const el=document.querySelector('[data-native-id="'+id+'"]'),r=el.getBoundingClientRect(),p=el.parentElement.getBoundingClientRect(),t=el.firstChild.getBoundingClientRect();
      return r.right<=p.right&&t.right<=r.right+.1&&t.width>0;
    })),'long threshold values fit without overflow');
    await page.evaluate(()=>{victoryUi.reset();victoryUi.open({...resultCardFixture,presentation:null,
      generals:[...resultCardFixture.generals,...resultCardFixture.generals]});});
    check(await page.evaluate(()=>{const row=document.querySelector('.result-generals');row.scrollLeft=row.scrollWidth;
      return row.children.length===10&&row.scrollLeft>0&&row.clientHeight>=row.firstChild.offsetHeight;
    }),'extra generals scroll without clipping card height');
    await page.evaluate(()=>{victoryUi.reset();showMain();});
    check(report.errors.length===0&&report.missing.length===0,'no browser errors or missing resources');
    await fs.writeFile(folder+'/browser-checks.json',JSON.stringify(report,null,2));
    console.log(JSON.stringify(report));
  }finally{await browser.close();}
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
