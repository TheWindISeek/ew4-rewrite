'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),{createRequire}=require('node:module');
const dependencies=createRequire('C:/Users/WIN11/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_probe.cjs');
const {chromium}=dependencies('playwright'),{PNG}=dependencies('pngjs');
const base=process.env.EW4_TEST_URL||'http://127.0.0.1:5193';
const folder='E:/Workplace/game/o4mod/rewrite/reports/rank-gauge-alignment-20261021';
function paintedPixels(buffer,track){
  const image=PNG.sync.read(buffer),points=[];
  for(let y=0;y<image.height;y++)for(let x=0;x<image.width;x++){
    const i=(y*image.width+x)*4,[r,g,b]=image.data.subarray(i,i+3);
    if(track==='rank'?g>150&&r<80&&b<120:b>150&&r<80&&g>90)points.push([x,y]);
  }
  return {image,points};
}
function trackOverlap(buffer,background,track){
  const {points}=paintedPixels(buffer,track),image=PNG.sync.read(background);
  let covered=0;
  for(const [x,y] of points){
    let onTrack=false;
    for(let dy=-1;dy<=1&&!onTrack;dy++)for(let dx=-1;dx<=1;dx++){
      const px=x+dx,py=y+dy;if(px<0||py<0||px>=image.width||py>=image.height)continue;
      const i=(py*image.width+px)*4,[r,g,b]=image.data.subarray(i,i+3);
      if(r>=50&&r<=85&&Math.abs(r-g)<3&&Math.abs(r-b)<3){onTrack=true;break;}
    }
    if(onTrack)covered++;
  }
  return {pixels:points.length,coverage:points.length?covered/points.length:1};
}
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:720}}),report={checks:0,errors:[],missing:[],alignment:[],screenshots:[]};
  const check=(value,label)=>{assert.ok(value,label);report.checks++;};
  page.on('pageerror',e=>report.errors.push(e.message));page.on('response',r=>{if(r.status()>=400)report.missing.push([r.status(),r.url()]);});
  try{
    await page.goto(base);await page.waitForFunction(()=>typeof menus!=='undefined'&&!!menus&&document.documentElement.dataset.fontsReady==='true');
    await page.locator('#headquarters').click();await page.waitForFunction(()=>menus.data?.progression);
    await page.evaluate(()=>{
      const panel=document.createElement('div');panel.id='gauge-geometry';panel.style.cssText='position:fixed;left:20px;top:60px;display:grid;grid-template-columns:repeat(5,100px);row-gap:.5px;z-index:99999';
      for(const track of ['rank','title'])for(const ratio of [0,.25,.5,.75,1]){
        const slot=document.createElement('div');slot.dataset.probe=track+'-'+ratio;slot.style.cssText='position:relative;width:100px;height:100px;background:#e9e6e0';
        const badge=menus.progressionBadge(track,{level:0,experience:menus.data.progression[track][0]*ratio});badge.style.cssText='position:absolute;left:35.75px;top:35.75px;transform:scale(3)';slot.append(badge);panel.append(slot);
      }
      document.body.append(panel);
    });
    await page.waitForFunction(()=>[...document.querySelectorAll('#gauge-geometry .rank-gauge')].every(badge=>badge.dataset.ready==='true'));
    for(const track of ['rank','title']){
      const background=await page.locator('[data-probe="'+track+'-0"]').screenshot();let previous=0;
      for(const ratio of [.25,.5,.75,1]){
        const result=trackOverlap(await page.locator('[data-probe="'+track+'-'+ratio+'"]').screenshot(),background,track);
        report.alignment.push({track,ratio,...result});
        check(result.pixels>previous,'painted arc grows at '+track+' '+ratio);previous=result.pixels;
        check(result.coverage>.95,'colored arc stays on original gray track at '+track+' '+ratio+': '+result.coverage);
      }
    }
    for(const scale of [3.17,2.25,1.33,.686]){
      await page.locator('#gauge-geometry .rank-gauge').evaluateAll((nodes,scale)=>nodes.forEach(node=>node.style.transform='scale('+scale+')'),scale);
      for(const track of ['rank','title']){
        const background=await page.locator('[data-probe="'+track+'-0"]').screenshot();
        const result=trackOverlap(await page.locator('[data-probe="'+track+'-1"]').screenshot(),background,track);
        report.alignment.push({track,scale,...result});check(result.pixels>0&&result.coverage>.95,'composited layers stay aligned at '+track+' '+scale);
      }
    }
    await page.locator('#gauge-geometry .rank-gauge').evaluateAll(nodes=>nodes.forEach(node=>node.style.transform='scale(3)'));
    await page.locator('#gauge-geometry').screenshot({path:folder+'/aligned-progress-samples.png'});report.screenshots.push('aligned-progress-samples');
    await page.evaluate(()=>{document.getElementById('gauge-geometry').remove();const held=menus.data.roster[0];menus.commanderInfo(menus.definition(held),held);});
    for(const viewport of [{width:1280,height:720},{width:844,height:390},{width:390,height:844}]){
      await page.setViewportSize(viewport);
      await page.waitForFunction(()=>[...document.querySelectorAll('.commander-ranks .rank-gauge')].every(badge=>badge.dataset.ready==='true'));
      check(await page.locator('.commander-ranks .rank-gauge').evaluateAll(nodes=>nodes.length===2&&nodes.every(n=>n.children.length===1&&n.firstChild.tagName==='CANVAS')),'each badge scales as one image at '+viewport.width);
      const name='commander-'+viewport.width;await page.screenshot({path:folder+'/'+name+'.png'});report.screenshots.push(name);
    }
    check(report.errors.length===0,'no browser exceptions');check(report.missing.length===0,'no missing resources');
  }catch(error){report.failure=error.message;throw error;}
  finally{await fs.writeFile(folder+'/results.json',JSON.stringify(report,null,2));await browser.close();}
  console.log(JSON.stringify(report));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
