'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/terrain_hud_20261019';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage();
  const report={checks:0,errors:[],missing:[],cases:[],screenshots:[],scope:'5185 only; real conquest terrain selections and long-number/three-row controlled display fixtures'};
  const check=(v,label)=>{assert.ok(v,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto('http://127.0.0.1:5185/');await page.waitForFunction(()=>!!originalTextRenderer&&!!catalog);
    await page.evaluate(()=>start('conquest1','fra'));
    report.fixtures=await page.evaluate(()=>{
      window.hudBase=state;
      const rows=state.cells.map((c,cell)=>({cell,active:c.active,rows:Ew4Battlefield.terrainRows(c)})).filter(r=>r.active);
      const find=test=>rows.find(r=>test(r.rows))?.cell;
      return {income:find(r=>r.some(v=>!v.defense)&&!r.some(v=>v.defense)),mixed:find(r=>r.some(v=>!v.defense)&&r.some(v=>v.defense)),
        defense:find(r=>r.length>0&&r.every(v=>v.defense)),empty:find(r=>!r.length)};
    });
    for(const size of [{width:1280,height:720},{width:844,height:390},{width:390,height:844},{width:1920,height:1080}]){
      await page.setViewportSize(size);
      await page.evaluate(()=>{state=hudBase;});
      for(const [kind,cell] of Object.entries(report.fixtures)){
        if(cell===undefined)continue;
        await page.evaluate(async cell=>{await selectCell(cell,true);view.x=point(cell).x;view.y=point(cell).y;},cell);
        await page.waitForTimeout(50);
        const result=await page.evaluate(()=>{
          const box=e=>{const r=e.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom,width:r.width,height:r.height,cy:(r.top+r.bottom)/2};};
          const panel=$('terrain-info'),p=box(panel),bar=box(document.querySelector('.battle-resources'));
          const rows=[...panel.querySelectorAll('.terrain-row')].map(e=>({row:box(e),icon:box(e.querySelector('img')),text:box(e.querySelector('.original-text')),value:e.querySelector('.original-text').getAttribute('aria-label')}));
          const top=[...$('resources').children].map(e=>({row:box(e),icon:box(e.querySelector('img')),text:box(e.querySelector('.original-text'))}));
          return {hidden:panel.hidden,p,bar,rows,top};
        });
        const label=kind+'/'+size.width;
        check(result.top.every(r=>r.text.left>=r.icon.right+1&&Math.abs(r.text.cy-r.icon.cy)<1),label+' total resource icon left, number right, centered');
        check(result.top.every((r,i)=>!i||r.row.left>=result.top[i-1].row.right-1),label+' total resource items do not overlap');
        if(kind==='empty')check(result.hidden,label+' empty terrain panel hidden');
        else{
          check(result.rows.length>0&&!result.hidden,label+' selected terrain rows visible');
          check(result.p.top>=result.bar.bottom,label+' terrain panel below total resources');
          check(result.rows.every(r=>r.text.left>=r.icon.right+1&&Math.abs(r.text.cy-r.icon.cy)<1),label+' terrain icon left, number right, centered');
          check(result.rows.every(r=>r.text.right<=result.p.right+1&&r.row.bottom<=result.p.bottom+1),label+' all rows contained by background');
          check(result.rows.every((r,i)=>result.rows.every((s,j)=>i===j||r.row.right<=s.row.left+1||r.row.left>=s.row.right-1||r.row.bottom<=s.row.top+1||r.row.top>=s.row.bottom-1)),label+' no row overlap');
          if(['mixed','defense'].includes(kind)){
            const name=label.replace('/','-')+'.png';await page.screenshot({path:folder+'/'+name});report.screenshots.push(name);
          }
        }
        report.cases.push({label,...result});
      }
      await page.evaluate(async cell=>{
        state={...state,cells:state.cells.map((c,i)=>i===cell?{...c,tileInfo:{income:{money:99999,industry:9999,food:999},defense:0,penalties:{infantry:100,cavalry:85,artillery:50}}}:c)};
        await selectCell(cell,true);view.x=point(cell).x;view.y=point(cell).y;
      },report.fixtures.mixed??report.fixtures.income);
      const stress=await page.evaluate(()=>{
        const p=$('terrain-info').getBoundingClientRect(),bar=document.querySelector('.battle-resources').getBoundingClientRect();
        return {below:p.top>=bar.bottom,rows:[...document.querySelectorAll('.terrain-row')].map(e=>{const a=e.querySelector('img').getBoundingClientRect(),b=e.querySelector('canvas').getBoundingClientRect();return {separate:b.left>a.right,centered:Math.abs((a.top+a.bottom-b.top-b.bottom)/2)<1,contained:b.right<=p.right&&b.bottom<=p.bottom};})};
      });
      check(stress.rows.length===6&&stress.rows.every(r=>r.separate&&r.centered&&r.contained)&&stress.below,'long values and three rows fit at '+size.width);
      const name='three-rows-'+size.width+'.png';await page.screenshot({path:folder+'/'+name});report.screenshots.push(name);
      await page.evaluate(async cell=>{
        state={...state,cells:state.cells.map((c,i)=>i===cell?{...c,tileInfo:{income:{},defense:0,penalties:{infantry:20,cavalry:70,artillery:50}}}:c)};
        await selectCell(cell,true);
      },report.fixtures.mixed??report.fixtures.income);
      check(await page.evaluate(()=>{
        const columns=document.querySelectorAll('#terrain-info>.terrain-column'),panel=$('terrain-info').getBoundingClientRect();
        return columns.length===1&&columns[0].childElementCount===3&&[...columns[0].children].every(e=>{
          const i=e.querySelector('img').getBoundingClientRect(),t=e.querySelector('canvas').getBoundingClientRect();
          return t.left>i.right&&t.right<=panel.right&&t.bottom<=panel.bottom&&i.left<panel.left+panel.width/4;
        });
      }),'defense-only rows start at left and stay inside panel at '+size.width);
      const defense='defense-only-'+size.width+'.png';await page.screenshot({path:folder+'/'+defense});report.screenshots.push(defense);
      await page.evaluate(()=>{
        state={...state,factions:state.factions.map(f=>f.id===state.playerFaction?{...f,resources:{money:999999999,industry:123456789,food:987654321}}:f)};
        refreshHud();
      });
      check(await page.evaluate(()=>{
        const entries=[...$('resources').children],bar=document.querySelector('.battle-resources').getBoundingClientRect();
        return bar.right<innerWidth-30&&entries.every((e,i)=>{
          const icon=e.querySelector('img').getBoundingClientRect(),text=e.querySelector('canvas').getBoundingClientRect();
          return text.left>icon.right&&Math.abs((icon.top+icon.bottom-text.top-text.bottom)/2)<1&&
            (!i||icon.left>=entries[i-1].getBoundingClientRect().right);
        });
      }),'nine-digit total resources remain horizontal and separated at '+size.width);
    }
    check(report.errors.length===0&&report.missing.length===0,'no script errors or missing assets');
  }catch(e){report.failure=e.message;throw e;}
  finally{await fs.writeFile(folder+'/browser.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify({checks:report.checks,fixtures:report.fixtures,errors:report.errors,missing:report.missing,screenshots:report.screenshots}));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
