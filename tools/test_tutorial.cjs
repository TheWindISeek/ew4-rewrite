'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const {Runner}=require('../rewrite/Ew4.Web/wwwroot/tutorial.js');
const manifest=JSON.parse(fs.readFileSync('rewrite/data/client/manifest.json','utf8'));
(async()=>{
  let checks=0;
  for(const [id,source] of Object.entries(manifest.tutorials)){
    const effects=[];const runner=new Runner(source,c=>effects.push(c));await runner.begin();
    assert.equal(runner.wait.name,'wait touch');checks++;
    assert.equal(effects.find(c=>c.name==='show text').id,id==='tutorials1'?'1':'18');checks++;
    const checkpoint=runner.checkpoint();
    assert.equal(await runner.notify({type:'area',cell:0}),false);checks++;
    assert.deepEqual(runner.checkpoint(),checkpoint);checks++;
    const restored=new Runner(source,()=>{});await restored.begin(checkpoint);assert.deepEqual(restored.checkpoint(),checkpoint);checks++;
    let count=0;
    while(!runner.finished){assert.ok(count++<source.commands.length,'finite script');const wait=runner.wait;assert.ok(wait);
      const event=wait.name==='wait touch'?{type:'touch'}:wait.name==='wait action'?{type:'action'}:wait.name==='wait area'?{type:'area',cell:Number(wait.id)}:{type:'ui',id:wait.string,row:wait.row===undefined?-1:Number(wait.row)};
      if(wait.name==='wait ui'&&wait.row!==undefined){assert.equal(await runner.notify({...event,row:event.row+1}),false);checks++;}
      assert.equal(await runner.notify(event),true);checks++;
    }
    assert.equal(effects.at(-1).name,'exit');checks++;
    assert.equal(effects.filter(c=>c.name==='show text').length,id==='tutorials1'?17:16);checks++;
    const completed=new Runner(source,()=>{throw Error('Finished tutorial replayed');});await completed.begin(runner.checkpoint());
    assert.equal(completed.finished,true);checks++;assert.equal(completed.wait,null);checks++;
  }
  const report={checks,errors:0,scope:'Both source tutorial scripts, exact wait-area/UI-row matching, save/restore cursor, 33 text commands and exit. Does not prove every underlying game UI target exists.'};
  fs.writeFileSync('rewrite/reports/tutorial_script.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
