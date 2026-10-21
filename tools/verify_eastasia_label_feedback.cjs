'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),{createRequire}=require('node:module');
const runtime=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=runtime('playwright'),{PNG}=runtime('pngjs');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'rewrite/reports/eastasia-lettering-feedback-20261021');
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}}),report={errors:[],missing:[],glyphs:[],screenshots:[]};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push(r.url());});
  try{
    await page.goto('http://127.0.0.1:5194/');
    await page.waitForFunction(()=>typeof manifest!=='undefined'&&manifest&&originalTextRenderer);
    // Read the existing battle without starting a scene or submitting a game action.
    await page.evaluate(async()=>await installState(await rpc('action',{op:'state'}),true));
    await page.waitForLoadState('networkidle');
    report.glyphs=await page.evaluate(()=>{
      const labels=manifest.maps.eastasia.labels,result=[];
      for(const language of ['en','zh']){
        const r=Object.create(Ew4Battlefield.Renderer.prototype);
        Object.assign(r,{api:{...battlefield.api,geographicLanguage:language},map:{labels},mapLabels:new Map(),visible:()=>true,draw:document.createElement('canvas').getContext('2d')});r.mapText();
        for(const label of labels){
          const cached=r.mapLabels.get(label.libName+':1:'+label.opacity+':'+language),c=cached.canvas,p=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
          let ink=0,edge=0;for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(p[(y*c.width+x)*4+3]){ink++;if(!x||!y||x===c.width-1||y===c.height-1)edge++;}
          if(!ink||edge)throw Error(label.text+' '+language+' blank/clipped');
          result.push({text:label.text,language,ink,width:c.width,height:c.height});
        }
      }return result;
    });
    for(const [name,text,language,zoom] of [
      ['japan-of','SEA OF JAPAN','en',.75],['okhotsk-of','SEA OF OKHOTSK','en',.65],
      ['south-china','SOUTH CHINA SEA','zh',.65],['south-china-en','SOUTH CHINA SEA','en',.65],
      ['qilian','QILIAN MOUNTAINS','zh',.9],['stanovoy','STANOVOY RANGE','zh',.7],
      ['east-china','EAST CHINA SEA','zh',.65],['seto','SETO INLAND SEA','zh',1.5],['seto-en','SETO INLAND SEA','en',1.5],
      ['taebaek','TAEBAEK MOUNTAINS','zh',1.4]]){
      await page.evaluate(({text,language,zoom})=>{settings.geographicLanguage=language;const l=manifest.maps.eastasia.labels.find(l=>l.text===text);view={x:l.tx,y:l.ty,zoom};clampView();battlefield.staticKey=null;},{text,language,zoom});
      await page.waitForTimeout(200);const file=name+'.png';await page.screenshot({path:path.join(folder,file)});report.screenshots.push(file);
    }
    const seaInk=await page.evaluate(()=>{
      const out=[];
      for(const text of ['SOUTH CHINA SEA','EAST CHINA SEA','SETO INLAND SEA'])for(const language of ['zh','en']){
        const label=manifest.maps.eastasia.labels.find(l=>l.text===text),r=Object.create(Ew4Battlefield.Renderer.prototype);
        Object.assign(r,{api:{...battlefield.api,geographicLanguage:language},map:{labels:[label]},mapLabels:new Map(),visible:()=>true,draw:document.createElement('canvas').getContext('2d')});r.mapText();
        const cache=[...r.mapLabels.values()][0];out.push({label,language,left:cache.left,top:cache.top,png:cache.canvas.toDataURL()});
      }return out;
    });
    const mask=PNG.sync.read(await fs.readFile(path.join(root,'maps/eastasia_rewrite/land_mask.png')));
    report.water=[];
    for(const text of ['SOUTH CHINA SEA','EAST CHINA SEA','SETO INLAND SEA']){
      const entries=seaInk.filter(i=>i.label.text===text),points=[];
      for(const entry of entries){const p=PNG.sync.read(Buffer.from(entry.png.split(',')[1],'base64'));
        for(let y=0;y<p.height;y+=2)for(let x=0;x<p.width;x+=2)if(p.data[(y*p.width+x)*4+3]>24)points.push([entry.left+x,entry.top+y]);
      }
      const label=entries[0].label,options=[];
      const step=text==='SETO INLAND SEA'?1:25,limit=text==='SETO INLAND SEA'?100:350;
      for(let dx=-limit;dx<=limit;dx+=step)for(let dy=-limit;dy<=limit;dy+=step){
        let land=0;
        for(const [x,y] of points){const xx=Math.round(label.tx+dx+x+32),yy=Math.round(label.ty+dy+y+54);
          if(xx<0||yy<0||xx>=mask.width||yy>=mask.height||mask.data[(yy*mask.width+xx)*4]>127)land++;
        }
        options.push({dx,dy,land});
      }
      options.sort((a,b)=>a.land-b.land||Math.hypot(a.dx,a.dy)-Math.hypot(b.dx,b.dy));
      const current=options.find(o=>!o.dx&&!o.dy);
      assert.equal(current.land,0,text+' lettering overlaps land');
      report.water.push({text,points:points.length,current,best:options.slice(0,3)});
    }
    report.camera=[];
    for(const [width,height] of [[1280,720],[390,844],[844,390]]){
      await page.setViewportSize({width,height});
      for(const [name,x,y] of [['top-left',-1e6,-1e6],['bottom-right',1e6,1e6],['top-right',1e6,-1e6],['bottom-left',-1e6,1e6]]){
        const result=await page.evaluate(({x,y})=>{
          view={x,y,zoom:.7};clampView();
          const b=manifest.maps.eastasia.cameraBounds,rect={left:view.x-innerWidth/(2*view.zoom),top:view.y-innerHeight/(2*view.zoom),right:view.x+innerWidth/(2*view.zoom),bottom:view.y+innerHeight/(2*view.zoom)};
          return{bounds:b,rect,inside:rect.left>=b.left-1e-6&&rect.top>=b.top-1e-6&&rect.right<=b.left+b.width+1e-6&&rect.bottom<=b.top+b.height+1e-6};
        },{x,y});
        assert.ok(result.inside,JSON.stringify(result));report.camera.push({width,height,corner:name,...result});
        await page.waitForTimeout(120);const file='edge-'+width+'x'+height+'-'+name+'.png';await page.screenshot({path:path.join(folder,file)});report.screenshots.push(file);
      }
    }
    assert.equal(report.glyphs.length,144);assert.deepEqual(report.errors,[]);assert.deepEqual(report.missing,[]);
  }finally{await fs.writeFile(path.join(folder,'results.json'),JSON.stringify(report,null,2)+'\n');await browser.close();}
  console.log(JSON.stringify({glyphs:report.glyphs.length,screenshots:report.screenshots.length,errors:report.errors,missing:report.missing,water:report.water}));
})().catch(e=>{console.error(e);process.exitCode=1;});
