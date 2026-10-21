'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const runtime=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=runtime('playwright'),{PNG}=runtime('pngjs');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'rewrite/reports/eastasia-lettering-20261021'),base='http://127.0.0.1:5194';
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const labels=JSON.parse(await fs.readFile(path.join(root,'maps/eastasia_rewrite/geographic_labels.json'),'utf8'));
  const mask=PNG.sync.read(await fs.readFile(path.join(root,'maps/eastasia_rewrite/land_mask.png')));
  for(const label of labels.filter(l=>['SEA OF OKHOTSK','EAST CHINA SEA'].includes(l.text))){
    for(let i=0;i<=24;i++)for(const h of [-label.size/2,0,label.size/2]){
      const t=(i/24-.5)*(label.letteringLength-label.size),a=label.angle*Math.PI/180;
      const x=Math.round(label.tx+t*Math.cos(a)-h*Math.sin(a)+32),y=Math.round(label.ty+t*Math.sin(a)+h*Math.cos(a)+54);
      assert.ok(mask.data[(y*mask.width+x)*4]<128,label.text+' crosses visible land');
    }
  }
  const form=new FormData();form.append('file',new Blob([await fs.readFile(path.join(root,'rewrite/content/eastasia-test-conquest.ew4mod'))]),'eastasia-test-conquest.ew4mod');
  form.append('id','eastasia-lettering-check');form.append('name','East Asia lettering check');
  const initial=await fetch(base+'/api/mod-import',{method:'POST',body:form}).then(r=>r.json());assert.ok(!initial.error,JSON.stringify(initial));
  let status;
  for(let i=0;i<120;i++){
    status=await fetch(base+'/api/mods').then(r=>r.json());
    if(status.import.status!=='working')break;
    await new Promise(r=>setTimeout(r,250));
  }
  assert.ok(status.mods.length,JSON.stringify(status));
  const activated=await fetch(base+'/api/mods?q='+encodeURIComponent(JSON.stringify({op:'activate',key:status.mods[0].key}))).then(r=>r.json());assert.ok(!activated.error,JSON.stringify(activated));
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}}),report={errors:[],missing:[],screenshots:[]};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto(base);await page.waitForFunction(()=>typeof manifest!=='undefined'&&manifest&&originalTextRenderer);
    await page.evaluate(async()=>await installState(await rpc('start',{scenario:'conquest7',country:'fra'}),true));
    await page.waitForLoadState('networkidle');
    report.glyphs=await page.evaluate(()=>{
      const labels=manifest.maps[state.map].labels,result=[];
      for(const language of ['en','zh']){
        const r=Object.create(Ew4Battlefield.Renderer.prototype);
        Object.assign(r,{api:{...battlefield.api,geographicLanguage:language},map:{labels},mapLabels:new Map(),visible:()=>true,draw:document.createElement('canvas').getContext('2d')});r.mapText();
        for(const label of labels){
          const cached=r.mapLabels.get(label.libName+':1:'+label.opacity+':'+language),c=cached.canvas,p=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
          let ink=0,edge=0;for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(p[(y*c.width+x)*4+3]){ink++;if(!x||!y||x===c.width-1||y===c.height-1)edge++;}
          if(!ink||edge)throw Error(label.text+' '+language+' blank/clipped');
          const font=c.getContext('2d').font;
          if(language==='zh'&&!font.includes(label.sea?'EW4 Kai':'EW4 Serif'))throw Error('Wrong bundled font: '+font);
          result.push({text:label.text,language,ink,width:c.width,height:c.height,font});
        }
      }return result;
    });
    assert.equal(report.glyphs.length,144);
    for(const language of ['en','zh']){
      for(const [name,text,zoom] of [['tibet','TIBETAN PLATEAU',.6],['mongolia','MONGOLIAN PLATEAU',.6],['china','QINLING MOUNTAINS',.75],['sea','EAST CHINA SEA',.6]]){
        await page.evaluate(({text,zoom,language})=>{settings.geographicLanguage=language;const l=manifest.maps[state.map].labels.find(l=>l.text===text);view={x:l.tx,y:l.ty,zoom};clampView();battlefield.staticKey=null;},{text,zoom,language});
        await page.waitForTimeout(180);
        const file=name+'-'+language+'.png';await page.screenshot({path:path.join(folder,file)});report.screenshots.push(file);
      }
    }
    for(const [width,height] of [[844,390],[390,844]]){
      await page.setViewportSize({width,height});
      await page.evaluate(()=>{const l=manifest.maps[state.map].labels.find(l=>l.text==='TIBETAN PLATEAU');view={x:l.tx,y:l.ty,zoom:Math.min(innerWidth/2200,innerHeight/1400)};clampView();});
      await page.waitForTimeout(180);
      const file='mobile-'+width+'x'+height+'.png';await page.screenshot({path:path.join(folder,file)});report.screenshots.push(file);
    }
    report.layerPriority=await page.evaluate(()=>{const order=[],r=Object.create(Ew4Battlefield.Renderer.prototype);for(const name of ['ground','mapTiles','mapText','borders','ownership','objectives','flags','units','labels','highlights'])r[name]=()=>order.push(name);r.facilities=b=>order.push('facilities:'+b);r.terrain=b=>order.push('terrain:'+b);r.draw=document.createElement('canvas').getContext('2d');r.api={imageRevision:0};r.frame({state:{factions:[]},map:{}},0);return order;});
    assert.ok(['facilities:false','units','labels'].every(n=>report.layerPriority.indexOf('mapText')<report.layerPriority.indexOf(n)));
    assert.deepEqual(report.errors,[]);assert.deepEqual(report.missing,[]);
  }finally{await fs.writeFile(path.join(folder,'results.json'),JSON.stringify(report,null,2)+'\n');await browser.close();}
  console.log(JSON.stringify({glyphs:report.glyphs.length,screenshots:report.screenshots.length,errors:report.errors,missing:report.missing}));
})().catch(e=>{console.error(e);process.exitCode=1;});
