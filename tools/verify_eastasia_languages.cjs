'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const {chromium}=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs')('playwright');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'maps/eastasia_rewrite');
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}});
  const report={checks:0,pageErrors:[],missing:[],combinations:[],screenshots:[]};
  const check=(value,message)=>{assert.ok(value,message);report.checks++;};
  page.on('pageerror',e=>report.pageErrors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  async function ready(){await page.waitForFunction(()=>typeof manifest!=='undefined'&&manifest&&catalog&&originalTextRenderer);}
  async function settle(){await page.waitForLoadState('networkidle');await page.waitForFunction(()=>[...images.values()].every(i=>i.complete&&i.naturalWidth));await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));}
  async function shot(name){await settle();await page.screenshot({path:path.join(folder,name+'.png')});report.screenshots.push(name+'.png');}
  async function language(id,value){if(await page.locator('#'+id).getAttribute('data-language')!==value)await page.locator('#'+id).click();}
  try{
    await page.goto('http://127.0.0.1:5190/');await ready();
    await page.evaluate(()=>localStorage.setItem('ew4.settings',JSON.stringify({...settings,cityLanguage:'zh',geographicLanguage:undefined})));
    await page.reload();await ready();
    check(await page.evaluate(()=>settings.cityLanguage==='zh'&&settings.geographicLanguage==='en'),'Existing city-language preference migrates independently');
    await page.evaluate(async()=>await installState(await rpc('start',{scenario:'conquest7',country:'fra'}),true));
    await settle();
    report.glyphs=await page.evaluate(()=>{
      const labels=manifest.maps[state.map].labels,result=[];
      for(const language of ['en','zh']){
        const r=Object.create(Ew4Battlefield.Renderer.prototype);
        Object.assign(r,{api:{...battlefield.api,geographicLanguage:language},map:{labels},mapLabels:new Map(),visible:()=>true,draw:document.createElement('canvas').getContext('2d')});r.mapText();
        for(const label of labels){
          const cached=r.mapLabels.get(label.libName+':1:'+label.opacity+':'+language),c=cached.canvas,p=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
          let ink=0,edgeInk=0;for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(p[(y*c.width+x)*4+3]){ink++;if(!x||!y||x===c.width-1||y===c.height-1)edgeInk++;}
          if(!ink||edgeInk)throw Error(label.text+' '+language+' blank/clipped: '+ink+'/'+edgeInk);
          result.push({text:label.text,textZh:label.textZh,language,ink,width:c.width,height:c.height});
        }
      }return result;
    });
    check(report.glyphs.length===142,'Both languages render every geographic name without clipping');
    for(const city of ['en','zh'])for(const geo of ['en','zh']){
      await page.evaluate(()=>openSettings());await language('city-language',city);await language('geographic-language',geo);await page.locator('#confirm-settings').click();await settle();
      const data=await page.evaluate(()=>{
        const cities=state.cells.flatMap((c,i)=>c.name?[{cell:i,name:c.name,text:battlefield.api.cityLabel(c.name,i).getAttribute('aria-label')}]:[]);
        return {city:settings.cityLanguage,geography:settings.geographicLanguage,cities,key:battlefield.staticKey,stored:JSON.parse(localStorage.getItem('ew4.settings'))};
      });
      check(data.city===city&&data.geography===geo,'Independent language buttons');
      check(data.stored.cityLanguage===city&&data.stored.geographicLanguage===geo,'Both language choices persist');
      check(data.cities.length===278&&data.cities.every(c=>city==='en'?/^[\x20-\x7e]+$/.test(c.text):c.text===c.name),'All cities display the chosen language');
      report.combinations.push({city,geography:geo,cityCount:data.cities.length,key:data.key});
      await page.evaluate(()=>{const l=manifest.maps[state.map].labels.find(l=>l.text==='QINLING MOUNTAINS');view={x:l.tx,y:l.ty,zoom:.9};clampView();});
      await shot('game-languages-city-'+city+'-geography-'+geo);
    }
    report.overrides=await page.evaluate(()=>{
      const entries=Object.entries(manifest.maps[state.map].cityNames),[cell]=entries.find(([,c])=>c.zh==='北京');
      const override=battlefield.api.cityLabel('北平',Number(cell)).getAttribute('aria-label');
      const sameName=entries.filter(([,c])=>c.zh==='海州').map(([,c])=>c.en);
      return{override,sameName};
    });
    check(report.overrides.override==='北平','Scenario names take priority over base translations');
    check(new Set(report.overrides.sameName).size===2,'Same-name cities use distinct cell translations');
    await page.evaluate(()=>openSettings());await language('city-language','zh');await language('geographic-language','en');
    await page.reload();await ready();
    check(await page.evaluate(()=>settings.cityLanguage==='zh'&&settings.geographicLanguage==='en'),'Mixed choices survive reload');
    await page.evaluate(async()=>await installState(await rpc('action',{op:'state'}),true));
    report.mobile=[];
    for(const [w,h] of [[844,390],[390,844]]){
      await page.setViewportSize({width:w,height:h});await page.evaluate(()=>openSettings());await shot('game-language-settings-'+w+'x'+h);
      const layout=await page.evaluate(()=>{
        const ids=['city-language','geographic-language','confirm-settings'],r=ids.map(id=>{const b=document.getElementById(id).getBoundingClientRect();return{id,left:b.left,top:b.top,right:b.right,bottom:b.bottom};});
        return{width:innerWidth,height:innerHeight,buttons:r,within:r.every(b=>b.left>=0&&b.top>=0&&b.right<=innerWidth&&b.bottom<=innerHeight),separate:r.every((a,i)=>r.slice(i+1).every(b=>a.right<=b.left||b.right<=a.left||a.bottom<=b.top||b.bottom<=a.top))};
      });check(layout.within&&layout.separate,'Language controls fit and remain separate on mobile');report.mobile.push(layout);
      await language('geographic-language','zh');await page.locator('#confirm-settings').click();
      await page.evaluate(()=>{const l=manifest.maps[state.map].labels.find(l=>l.text==='TAIWAN STRAIT');view={x:l.tx,y:l.ty,zoom:.85};clampView();});await shot('game-language-map-zh-'+w+'x'+h);
      check(await page.evaluate(()=>{const p=ctx.getImageData(0,0,canvas.width,canvas.height).data,s=new Set();for(let i=0;i<p.length;i+=104)s.add(p[i]+','+p[i+1]+','+p[i+2]);return s.size>20;}),'Mobile map is nonblank');
    }
    await page.setViewportSize({width:1280,height:720});await page.evaluate(()=>openSettings());await shot('game-language-settings-desktop');await page.locator('#confirm-settings').click();
    await page.evaluate(()=>{const l=manifest.maps[state.map].labels.find(l=>l.text==='TAIWAN STRAIT');view={x:l.tx,y:l.ty,zoom:1.05};clampView();});await shot('game-language-taiwan-zh');
    report.layerPriority=await page.evaluate(()=>{
      const order=[],r=Object.create(Ew4Battlefield.Renderer.prototype);
      for(const name of ['ground','mapTiles','mapText','borders','ownership','objectives','flags','units','labels','highlights'])r[name]=()=>order.push(name);
      r.facilities=farms=>order.push('facilities:'+farms);r.terrain=sea=>order.push('terrain:'+sea);r.draw=document.createElement('canvas').getContext('2d');r.api={imageRevision:0};r.frame({state:{factions:[]},map:{}},0);return order;
    });
    check(['facilities:false','units','labels'].every(name=>report.layerPriority.indexOf('mapText')<report.layerPriority.indexOf(name)),'Buildings, units and city names render above geography');
    report.typography=[];
    for(const [w,h] of [[1280,720],[405,829]]){
      await page.setViewportSize({width:w,height:h});await page.locator('#menu-button').click();
      await shot('game-pause-font-'+w+'x'+h);
      const typography=await page.evaluate(()=>{
        const city=originalTextRenderer.element('城市名称','font_city_labels_hd');
        return{width:innerWidth,height:innerHeight,family:originalTextRenderer.fontFamily,cityFamily:city.dataset.typeface,
          geographicFamily:Ew4OriginalUI.fontFamily,buttons:[...document.querySelectorAll('.pause-dialog>button')].map(b=>{
            const label=b.firstElementChild,c=label.firstElementChild,p=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
            let ink=0,white=0;for(let i=0;i<p.length;i+=4)if(p[i+3]){ink++;if(p[i]>=250&&p[i+1]>=250&&p[i+2]>=250)white++;}
            const r=b.getBoundingClientRect(),t=label.getBoundingClientRect();
            return{id:b.id,family:label.dataset.typeface,color:label.dataset.color,ink,white,fits:t.left>=r.left&&t.right<=r.right&&t.top>=r.top&&t.bottom<=r.bottom};
          })};
      });
      check(typography.family===typography.cityFamily&&typography.family===typography.geographicFamily&&typography.buttons.every(b=>b.family===typography.family),'City, geography and menu share one serif font stack');
      check(typography.buttons.length===4&&typography.buttons.every(b=>b.white>0&&b.white===b.ink&&b.fits),'All pause buttons have white text and fit');
      report.typography.push(typography);await page.locator('#resume').click();
    }
    const runtime=JSON.parse(await fs.readFile(path.join(folder,'runtime-verification.json'),'utf8'));report.activeKey=runtime.activeKey;
    check(!report.pageErrors.length&&!report.missing.length,'No page errors or missing resources');
  }finally{await fs.writeFile(path.join(folder,'languages-browser-verification.json'),JSON.stringify(report,null,2)+'\n');await browser.close();}
  console.log(JSON.stringify({checks:report.checks,glyphs:report.glyphs.length,combinations:report.combinations.length,pageErrors:report.pageErrors.length,missing:report.missing.length,screenshots:report.screenshots.length}));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
