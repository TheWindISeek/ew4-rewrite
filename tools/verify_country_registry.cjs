'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const base=process.argv[2]||'http://127.0.0.1:5251',folder=path.resolve('rewrite/reports/unit-visuals-20261021');
(async()=>{
  const fixture=JSON.parse(await fs.readFile(path.join(folder,'country-tests.json'),'utf8'));
  const report={checks:0,errors:[],missing:[],screenshots:[]},check=(pass,label)=>{assert.ok(pass,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  const shot=async name=>{await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');};
  try{
    const upload=await page.request.post(base+'/api/mod-import',{multipart:{file:{name:'countries.ew4mod',mimeType:'application/zip',buffer:await fs.readFile(fixture.archive)}}});
    check(upload.ok(),'actual country package uploaded');
    let key;
    for(let attempt=0;attempt<300;attempt++){
      const result=await(await page.request.get(base+'/api/mods')).json();
      if(result.import.status==='error')throw Error(result.import.message);
      if(result.import.status==='complete'){key=result.import.key;break;}
      await page.waitForTimeout(100);
    }
    check(!!key,'actual country package imported');
    check((await(await page.request.post(base+'/api/mods',{data:{op:'activate',key}})).json()).active===key,'country package activated');
    await page.goto(base);await page.waitForFunction(()=>startupReady,null,{timeout:60000});
    check(await page.evaluate(id=>catalog.countries[id].visualVariant==='hre'&&catalog.countries[id].unitVariant==='fra',fixture.fullId),'global definition independent of long country ID');
    for(const viewport of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(viewport);
      await page.evaluate(async()=>{mode=2;$('menu').hidden=false;$('scenario-menu').hidden=false;await chooseScenario(catalog.scenarios.find(s=>s.id==='conquest1'));});
      const row=page.getByRole('button',{name:'\u72ec\u7acb\u914d\u7f6e\u5171\u548c\u56fd',exact:true});
      await row.waitFor();check(await row.count()===1,'configured name shown once at '+viewport.width);
      await row.locator('img').evaluate(img=>img.decode());
      check(await row.locator('img').evaluate(img=>img.src.includes('/extensions/images/country-check.png')&&img.naturalWidth>0),'custom flag renders in selection '+viewport.width);
      await row.click();
      check(await page.evaluate(id=>chosenCountry===id,fixture.fullId),'selection preserves full Unicode ID '+viewport.width);
      await shot('country-selection-'+viewport.width);
      await page.evaluate(id=>start('conquest1',id),fixture.fullId);
      await page.waitForFunction(()=>{
        const surface=$('map'),data=surface.getContext('2d').getImageData(0,0,surface.width,surface.height).data,colors=new Set();
        for(let i=0;i<data.length;i+=128)colors.add(data[i]+','+data[i+1]+','+data[i+2]);return colors.size>8;
      },null,{timeout:15000});
      const result=await page.evaluate(id=>{
        const faction=state.factions.find(f=>f.human),units=state.units.filter(u=>u.owner===faction.id);
        const canvas=$('map'),pixels=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;
        const colors=new Set();for(let i=0;i<pixels.length;i+=128)colors.add(pixels[i]+','+pixels[i+1]+','+pixels[i+2]);
        return {correct:faction.country===id&&faction.visualVariant==='hre'&&faction.flagImage==='rewrite/images/country-check.png',
          models:units.every(u=>visualProfile(u).visualMotion===[''+manifest.profiles[u.profile].kind+' hre '+(manifest.profiles[u.profile].grade+1),manifest.profiles[u.profile].kind+' '+(manifest.profiles[u.profile].grade+1),manifest.profiles[u.profile].kind].find(k=>effects.data.motions[k])),colors:colors.size,order:state.factions.map(f=>f.id)};
      },fixture.fullId);
      check(result.correct&&result.models,'CNTR country and HRE models render '+viewport.width);check(result.colors>8,'nonblank map '+viewport.width);
      report.order=result.order;await shot('country-battle-'+viewport.width);
      await page.evaluate(async()=>{await rpc('save',{mode:2,slot:1});await load(1,2);});
      check(await page.evaluate(id=>state.startCountry===id&&state.factions.find(f=>f.human).country===id,fixture.fullId),'save load preserves arbitrary ID '+viewport.width);
      await page.evaluate(()=>showMain());
    }
    check(report.errors.length===0,'zero browser errors');check(report.missing.length===0,'zero missing resources');
    await fs.writeFile(path.join(folder,'country-browser-tests.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify({checks:report.checks,screenshots:report.screenshots.length,errors:report.errors,missing:report.missing}));
  }finally{
    await page.request.post(base+'/api/mods',{data:{op:'activate',key:null}});
    await browser.close();
  }
})().catch(e=>{console.error(e.message);process.exitCode=1;});
