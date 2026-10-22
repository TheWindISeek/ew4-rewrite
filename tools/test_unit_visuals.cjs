'use strict';
const fs=require('node:fs'),assert=require('node:assert/strict');
require('../rewrite/Ew4.Web/wwwroot/effects.js');
const read=path=>JSON.parse(fs.readFileSync(path,'utf8'));
const manifest=read('rewrite/data/client/manifest.json'),effects=read('rewrite/data/client/effects.json'),config=read('rewrite/data/client/unit-visuals.json');
const fixtures=read('rewrite/reports/unit-visuals-20261021/cases.json');
const pristine=JSON.stringify(manifest),engine=new Ew4Effects.Effects({manifest},effects,config);
let checks=0;
for(const test of fixtures.cases){
  const profile=engine.profile(test.profile,{id:'fixture.'+test.variant,unitVariant:test.variant});
  const expected=effects.motions[test.expected];
  assert.equal(profile.visualMotion,test.expected,JSON.stringify(test));
  assert.equal(profile.library,expected.res);
  assert.equal(profile.x,Number(expected.x||0));assert.equal(profile.y,Number(expected.y||0));
  assert.equal(profile.scale,Number(expected.scale??1));
  assert.strictEqual(profile.stats,manifest.profiles[test.profile].stats);checks+=6;
  for(const action of ['ready','move','attack','reload','finish','build'])for(const facing of [-1,1]){
    const side=facing<0?'left':'right';
    const motion=expected.motions.find(m=>m.type===action&&Number(m.index||0)===0&&(m.dir===side||!m.dir||m.dir==='all'));
    assert.deepEqual(engine.motion(profile,action,facing),motion);checks++;
  }
}
const id='ew4.unit.others.militia.0',base=manifest.profiles[id];
assert.equal(engine.profile(id,{id:'hre.test',unitVariant:'hre',flag:'fra'}).visualMotion,'Militia hre 1');checks++;
assert.equal(engine.profile(id,{id:'new',unitVariant:'unknown'}).visualMotion,'Militia 1');checks++;
assert.equal(engine.profile(id,{id:'__proto__',unitVariant:'others'}).visualMotion,'Militia 1');checks++;
assert.equal(engine.profile(id,{id:'toString',visualVariant:'constructor'}).visualMotion,'Militia 1');checks++;
assert.equal(engine.profile(id).visualMotion,effects.bindings[id]);checks++;
const custom={schema:1,factions:{'new.republic':'my.skin'},variants:{'my.skin':{'militia.0':'my.militia'}},defaults:{},profiles:{},definitions:{'my.militia':{...effects.motions['Militia hre 1'],res:'my_army',x:12,y:-8,scale:1.25}}};
const configured=new Ew4Effects.Effects({manifest},effects,custom),faction={id:'new.republic',unitVariant:'pru',flag:'rus'};
const unit=configured.profile(id,faction);
assert.equal(unit.visualMotion,'my.militia');assert.equal(unit.library,'my_army');assert.equal(unit.x,12);assert.equal(unit.scale,1.25);checks+=4;
assert.equal(configured.profile('ew4.unit.others.line_infantry.0',faction).visualMotion,'Line Infantry 1');checks++;
const exact=structuredClone(custom);exact.profiles[id]='Militia fra 1';
assert.equal(new Ew4Effects.Effects({manifest},effects,exact).profile(id,faction).visualMotion,'Militia fra 1');checks++;
const broken=structuredClone(custom);broken.variants['my.skin']['militia.0']='typo';
assert.throws(()=>new Ew4Effects.Effects({manifest},effects,broken).profile(id,faction),/Unknown unit visual/);checks++;
assert.throws(()=>new Ew4Effects.UnitVisuals(manifest,effects,{schema:2}),/schema/);checks++;
assert.equal(JSON.stringify(manifest),pristine);assert.strictEqual(base.stats,unit.stats);checks+=2;
const report={checks,cases:fixtures.cases.length,errors:0};
fs.writeFileSync('rewrite/reports/unit-visuals-20261021/selection-tests.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report));
