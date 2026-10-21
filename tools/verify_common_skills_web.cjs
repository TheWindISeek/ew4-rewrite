'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root=path.resolve(__dirname,'..'),fixtureFolder=path.join(root,'rewrite/reports/common-skills-20261021');
const folder=process.env.COMMON_SKILL_REPORT||fixtureFolder;
const url=process.env.COMMON_SKILL_URL||'http://127.0.0.1:5206';
assert(/^http:\/\/127\.0\.0\.1:(5206|5210)$/.test(url),'Use an isolated common skills preview on 5206 or release test on 5210');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const fixture=JSON.parse(await fs.readFile(path.join(fixtureFolder,'latest-import.json'),'utf8'));
  const result={checks:0,errors:[],missing:[],screenshots:[],url};
  const check=(value,label)=>{assert(value,label);result.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1136,height:640}});
  page.on('pageerror',error=>result.errors.push(error.message));
  page.on('response',response=>{if(response.status()>=400)result.missing.push([response.status(),response.url()]);});
  const ready=()=>page.waitForFunction(()=>typeof originalTextRenderer!=='undefined'&&originalTextRenderer&&catalog);
  const shot=async name=>{await page.screenshot({path:path.join(folder,name)});result.screenshots.push(name);};
  const fresh=async()=>{await page.evaluate(async cell=>{closeDetail();await start('conquest1','fra');await selectCell(cell,true);},fixture.cell);await page.waitForFunction(()=>state&&!busy);};
  const open=async index=>{await page.locator('.active-skill-button').nth(index).click();await page.locator('.skill-targets').waitFor();};
  const confirm=async()=>{await page.locator('#detail-confirm').click();await page.waitForFunction(()=>!busy&&document.getElementById('detail-modal').hidden);};
  const fits=async()=>page.locator('#detail-modal .dialog').evaluate(e=>{const r=e.getBoundingClientRect();return r.x>=-1&&r.y>=-1&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1;});
  try{
    await page.goto(url);await ready();
    const response=await page.request.post(url+'/api/mod-import',{multipart:{id:'common-skills-test',name:'Common skills API 2',
      file:{name:'common-skills.ew4mod',mimeType:'application/zip',buffer:await fs.readFile(fixture.archive)}}});
    check(response.ok(),'offline package upload');
    let status;
    for(let i=0;i<240;i++){
      status=await (await page.request.get(url+'/api/mods')).json();
      if(status.import.status==='error')throw new Error(status.import.message);
      if(status.import.status==='complete')break;
      await page.waitForTimeout(500);
    }
    check(status.import.status==='complete','offline package import');
    await page.request.post(url+'/api/mods',{data:{op:'activate',key:status.import.key}});
    await page.reload();await ready();await fresh();
    check(await page.locator('.active-skill-button').count()===6,'six real active skill controls');
    await open(0);
    check(await page.locator('.skill-targets>button').count()>0,'cell target options rendered');
    check(await fits(),'desktop skill dialog fits');await shot('skill-cell-desktop.png');
    const before=await page.evaluate(()=>({count:state.units.length,funds:state.factions.find(f=>f.id===state.playerFaction).resources.money,
      destination:selection.skills[0].cells[0]}));
    await confirm();
    check(await page.evaluate(before=>state.units.length===before.count+1&&state.recruited.some(id=>state.units.find(u=>u.id===id).cell===before.destination),before),'summon via confirmation button');
    check(await page.evaluate(before=>before.funds-state.factions.find(f=>f.id===state.playerFaction).resources.money===5,before),'summon cost paid once');

    await fresh();await open(1);
    const destination=await page.evaluate(()=>selection.skills[1].cells[0]);
    await page.locator('.skill-targets>button').first().click();await confirm();
    check(await page.evaluate(({id,cell})=>state.units.find(u=>u.id===id).cell===cell,{id:fixture.unitId,cell:destination}),'teleport selected cell through UI');
    await shot('skill-teleport-result.png');
    await fresh();await open(2);
    check(await page.locator('.skill-targets>button').count()>1,'area skill accepts center cells');
    await shot('skill-area-desktop.png');await page.locator('#close-detail').click();

    await open(3);await confirm();
    check(await page.evaluate(()=>selected.statuses.length===1),'status returned after rally');
    await page.evaluate(async()=>{await rpc('save',{mode:2,slot:1});await installState(await rpc('load',{mode:2,slot:1}));});
    await page.evaluate(cell=>selectCell(cell,true),fixture.cell);
    check(await page.evaluate(()=>selected.statuses[0].id==='example.vigor'),'status reload in browser');
    for(const viewport of [{width:1136,height:640},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(viewport);await page.evaluate(()=>openUnitInfo(selected));
      check(await fits(),'unit status dialog fits '+viewport.width);
      check(await page.locator('.unit-skill-row').count()===7,'status description accessible '+viewport.width);
      await page.locator('.unit-skill-row').last().scrollIntoViewIfNeeded();
      await shot('skill-status-'+viewport.width+'.png');await page.locator('#close-detail').click();
      await open(0);check(await fits(),'cell picker fits '+viewport.width);
      check(await page.locator('.skill-targets>button').evaluateAll(nodes=>nodes.every(node=>{const r=node.getBoundingClientRect();return r.width>0&&r.height>0;})),'cell buttons stable '+viewport.width);
      await shot('skill-picker-'+viewport.width+'.png');await page.locator('#close-detail').click();
    }
    check(await page.evaluate(()=>{const c=document.getElementById('map'),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let pixels=0;for(let i=0;i<data.length;i+=400)if(data[i]+data[i+1]+data[i+2]>20&&data[i+3]>0)pixels++;return pixels>100;}),'battle canvas contains rendered pixels');
    check(result.errors.length===0,'no browser errors');check(result.missing.length===0,'no missing assets');
    await page.setViewportSize({width:1136,height:640});
  }finally{
    await fs.writeFile(path.join(folder,'web-results.json'),JSON.stringify(result,null,2));await browser.close();
  }
  console.log(JSON.stringify(result));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
