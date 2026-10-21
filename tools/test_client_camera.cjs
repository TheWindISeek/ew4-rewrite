'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
require('../rewrite/Ew4.Web/wwwroot/camera.js');
require('../rewrite/Ew4.Web/wwwroot/battlefield.js');
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '../rewrite/data/client/manifest.json'), 'utf8'));
let cases = 0;
const activeState={width:8,height:6,cells:Array.from({length:48},(_,i)=>({active:i%8>0&&i%8<7})),cameraBounds:{left:12,top:34,width:400,height:280}};
const activeBounds=Ew4Battlefield.cameraBounds(activeState,{width:512,height:324});
assert.deepEqual(activeBounds,activeState.cameraBounds,'authoritative scenario rectangle, not active-row intersection');
assert.deepEqual(Ew4Battlefield.cameraBounds(activeState,{width:512,height:324,cameraBounds:{left:20,top:40,width:300,height:200}}),
  {left:20,top:40,width:300,height:200},'scenario camera stays inside map artwork');
const eastasiaBounds=Ew4Battlefield.cameraBounds({cameraBounds:{left:-52,top:-74,width:10184,height:7402}},manifest.maps.eastasia);
assert.ok(eastasiaBounds.left>=-31&&eastasiaBounds.top>=-53&&eastasiaBounds.left+eastasiaBounds.width<=10111&&eastasiaBounds.top+eastasiaBounds.height<=7307);
for (const bounds of [...Object.values(manifest.maps), {width:80, height:60}, {left:125,top:70,width:2800,height:1800}, activeBounds, eastasiaBounds]) {
  for (const [w,h] of [[1920,1080],[844,390],[390,844],[3440,1440],[568,320]]) {
    for (const zoom of [.01,.22,.95,2.5,100]) {
      for (const [x,y] of [[-100000,-100000],[100000,100000],[-100000,100000],[100000,-100000],[bounds.width/2,bounds.height/2]]) {
        const view = Ew4Camera.constrain({x,y,zoom}, {w,h}, bounds);
        const epsilon = 1e-8;
        assert.ok(view.x - w / (2*view.zoom) >= (bounds.left||0)-epsilon, 'left viewport edge');
        assert.ok(view.y - h / (2*view.zoom) >= (bounds.top||0)-epsilon, 'top viewport edge');
        assert.ok(view.x + w / (2*view.zoom) <= (bounds.left||0)+bounds.width+epsilon, 'right viewport edge');
        assert.ok(view.y + h / (2*view.zoom) <= (bounds.top||0)+bounds.height+epsilon, 'bottom viewport edge');
        assert.deepEqual(Ew4Camera.constrain(view,{w,h},bounds),view, 'stable at boundary');
        cases++;
      }
    }
  }
}
const motion={path:[17,18,19],started:100,stepMs:200};
const start=Ew4Battlefield.pose(motion,8,100),mid=Ew4Battlefield.pose(motion,8,200),end=Ew4Battlefield.pose(motion,8,500);
assert.equal(start.x,Ew4Battlefield.point(17,8).x);assert.equal(mid.x,start.x+32);assert.equal(end.x,Ew4Battlefield.point(19,8).x);assert.equal(end.moving,false);
const badgeState={playerFaction:'a',activeFaction:'a',phase:'Active'};
for(const unit of [{owner:'a',canMove:true,canAttack:false},{owner:'a',canMove:false,canAttack:true}]){
  assert.equal(Ew4Battlefield.badgeAlpha(unit,badgeState,0),1);assert.equal(Ew4Battlefield.badgeAlpha(unit,badgeState,600),.25);
}
for(const unit of [{owner:'a',canMove:false,canAttack:false},{owner:'enemy',canMove:true,canAttack:true}]){
  assert.equal(Ew4Battlefield.badgeAlpha(unit,badgeState,600),1);
}
assert.equal(Ew4Battlefield.badgeAlpha({owner:'a',canMove:true},{...badgeState,activeFaction:'b'},600),1);
const report = {cases,errors:0,scope:'Authoritative scenario bounds, portrait/landscape/resize viewport constraints, zoom extremes, stable clamping, path interpolation. Native UI and Android acceptance are separate.'};
fs.writeFileSync(path.join(__dirname,'../rewrite/reports/client_camera.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report));
