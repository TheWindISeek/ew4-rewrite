'use strict';
const fs=require('node:fs/promises'),assert=require('node:assert/strict'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const folder='E:/Workplace/game/o4mod/rewrite/reports/menu-compat-20261019'+(process.argv[2]?'/v2':''),url='http://127.0.0.1:5185';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const report={checks:0,errors:[],missing:[],screenshots:[],scope:'Desktop/mobile menu entry, real APK roundtrip and text pixel checks; isolated 5185'},check=(value,label)=>{assert(value,label);report.checks++;};
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'}),page=await browser.newPage({viewport:{width:1280,height:720}});
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  async function ready(){await page.waitForFunction(()=>typeof menuArt!=='undefined'&&!!menuArt?.strategic&&!!originalTextRenderer,{}, {timeout:90000});}
  async function shot(name){await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);}
  async function snapshot(){return page.evaluate(()=>({main:menuArt.main,campaign:menuArt.backgrounds.form_selcampaign,conquest:menuArt.backgrounds.form_selconquest,script:[...document.scripts].find(s=>s.src.includes('app.js')).src,css:getComputedStyle(document.querySelector('.main-entry')).backgroundImage}));}
  async function menus(name){
    await page.setViewportSize({width:1280,height:720});await shot(name+'-main');
    for(const mode of [1,2]){
      await page.locator('[data-mode="'+mode+'"]').click();await page.waitForTimeout(650);await shot(name+'-'+mode);
      check(await page.evaluate(mode=>getComputedStyle(document.getElementById('scenario-menu')).backgroundImage.includes(menuArt.backgrounds[mode===1?'form_selcampaign':'form_selconquest']),mode),name+' layout background '+mode);
      if(mode===1){await page.locator('.chapter-entry').first().click();await page.locator('.chapter-preview-confirm').click();await page.waitForTimeout(700);await shot(name+'-chapter');await page.locator('#campaign-back').click();}
      else {await page.locator('.scenario-entry[data-scenario]').first().click();await page.waitForTimeout(700);await shot(name+'-country');await page.locator('#cancel-country').click();}
      await page.locator('#menu-back').click();
    }
    await page.setViewportSize({width:844,height:390});await shot(name+'-mobile-main');
  }
  try{
    await page.goto(url);await page.evaluate(()=>fetch('/api/mods?q='+encodeURIComponent(JSON.stringify({op:'activate',key:null})),{method:'POST'}));await page.reload();await ready();const builtin=await snapshot();await menus('builtin-before');
    const imported=await page.evaluate(async key=>{
      const res=await fetch('/api/mods').then(r=>r.json());return key?res.mods.find(m=>m.key===key):res.mods[0];
    },process.argv[2]||null);check(!!imported,'Actual APK imported on isolated server');
    await page.setViewportSize({width:1280,height:720});await page.locator('#mod-open').click();
    await Promise.all([page.waitForNavigation({waitUntil:'load'}),page.locator('[data-mod-key="'+imported.key+'"] button').click()]);await ready();
    const mod=await snapshot();check(mod.main!==builtin.main&&mod.campaign!==builtin.campaign,'Real Mod textures differ');check(mod.css.includes(imported.key)&&!builtin.css.includes(imported.key),'CSS URLs isolated by resource revision');
    await menus('mod');
    const text=await page.evaluate(()=>{
      const renderer=originalTextRenderer,other=new Ew4OriginalUI.TextRenderer({fonts:{font_city_labels_hd:{height:99,preserveColor:false,glyphs:{}}}});other.measure=renderer.measure;
      const a=renderer.element('伦敦 LONDON','font_city_labels_hd','#ffffff'),b=other.element('伦敦 LONDON','font_city_labels_hd','#ffffff');
      document.body.append(a);const canvas=a.firstChild,p=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;let white=0,black=0;
      for(let i=0;i<p.length;i+=4)if(p[i+3]>160){if(p[i]>240&&p[i+1]>240&&p[i+2]>240)white++;if(p[i]<30&&p[i+1]<30&&p[i+2]<30)black++;}
      const equal=canvas.toDataURL()===b.firstChild.toDataURL();a.remove();
      return {equal,white,black,en:Ew4MenuArt.cityName('伦敦','en',cityNames),zh:Ew4MenuArt.cityName('LONDON','zh',cityNames),custom:Ew4MenuArt.cityName('自定义城','en',cityNames)};
    });check(text.equal,'Imported bitmap style and metrics cannot alter text');check(text.white>50&&text.black>30,'Chinese and English city ink has independent white fill/black edge');check(text.en==='LONDON'&&text.zh==='伦敦'&&text.custom==='自定义城','Bidirectional names and custom name preserved');report.text=text;
    await page.locator('#main-settings').click();
    for(const [language,label] of [['zh','中文'],['en','English']]){
      await page.locator('.city-language-options button[data-language="'+language+'"]').click();
      check(await page.evaluate(value=>settings.cityLanguage===value,language),'Actual settings control switches '+label);
    }
    await page.evaluate(()=>document.getElementById('settings').hidden=true);
    await page.evaluate(async()=>{const countries=await rpc('countries',{scenario:'conquest1'});await start('conquest1',countries[0].id);});
    await page.waitForFunction(()=>!!state&&!busy,{}, {timeout:60000});
    for(const language of ['zh','en']){
      await page.evaluate(language=>{document.getElementById('city-language').value=language;document.getElementById('city-language').dispatchEvent(new Event('change'));},language);
      await page.waitForTimeout(300);await shot('mod-cities-'+language);
    }
    await page.evaluate(()=>showMain());
    await page.setViewportSize({width:1280,height:720});await page.locator('#mod-open').click();await Promise.all([page.waitForNavigation({waitUntil:'load'}),page.locator('[data-mod-key="builtin"] button').click()]);await ready();
    const restored=await snapshot();check(restored.main===builtin.main&&restored.campaign===builtin.campaign&&restored.conquest===builtin.conquest,'Builtin backgrounds exactly restored in same browser');check(restored.css===builtin.css,'Builtin CSS namespace restored');
    await menus('builtin-after');
    check(report.errors.length===0,'No browser exceptions: '+report.errors.join(';'));check(report.missing.length===0,'No missing resources: '+JSON.stringify(report.missing));
  }finally{await fs.writeFile(folder+'/results.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
