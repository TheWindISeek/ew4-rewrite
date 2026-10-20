'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),{spawnSync}=require('node:child_process');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'rewrite/reports/jurisdiction-extension-20261020/tool-'+Date.now());fs.mkdirSync(folder,{recursive:true});
const dotnet='C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe',tool=path.join(root,'rewrite/Ew4.ModTool/bin/Release/net10.0/Ew4.ModTool.dll');
const source=path.join(root,'rewrite/data/original/conquest1.btl'),base='--base='+path.join(root,'rewrite/data');let checks=0;
function call(args,ok=true){const result=spawnSync(dotnet,[tool,...args,base],{encoding:'utf8'});assert.equal(result.status,ok?0:1,result.stderr);checks++;return result;}
const json=path.join(folder,'auto.json');call(['jurisdiction-export',source,json]);const doc=JSON.parse(fs.readFileSync(json,'utf8'));
assert.equal(doc.cellCenters.length,doc.width*doc.height);checks++;
const core=JSON.parse(fs.readFileSync(path.join(root,'rewrite/reports/jurisdiction-extension-20261020/core.json'),'utf8'));
const local=(Math.floor(core.changedCell/doc.mapWidth)-doc.top)*doc.width+core.changedCell%doc.mapWidth-doc.left;
doc.cellCenters[local]=core.manualCenter;
const edited=path.join(folder,'manual.json');fs.writeFileSync(edited,JSON.stringify(doc));
const output=path.join(folder,'nested/conquest1.btl');call(['jurisdiction-apply',source,edited,output]);
const original=fs.readFileSync(source),binary=fs.readFileSync(output);assert(binary.subarray(0,original.length).equals(original));checks++;
assert.equal(binary.readInt32LE(original.length+96+local*4),core.manualCenter);checks++;
const reexport=path.join(folder,'again.json');call(['jurisdiction-export',output,reexport]);assert.deepEqual(JSON.parse(fs.readFileSync(reexport,'utf8')),doc);checks++;
const stripped=path.join(folder,'stripped.btl');call(['jurisdiction-strip',output,stripped]);assert(fs.readFileSync(stripped).equals(original));checks++;
call(['jurisdiction-apply',source,edited,output],false);assert(fs.readFileSync(output).equals(binary));checks++;
call(['jurisdiction-strip',source,source],false);assert(fs.readFileSync(source).equals(original));checks++;
function bad(name,value,match){const file=path.join(folder,name+'.json'),out=path.join(folder,name+'.btl');fs.writeFileSync(file,typeof value==='string'?value:JSON.stringify(value));const r=call(['jurisdiction-apply',source,file,out],false);assert.match(r.stderr,match);assert(!fs.existsSync(out));checks+=2;}
bad('bad-hash',{...doc,baseSha256:'0'.repeat(64)},/BTL主体/);
bad('bad-count',{...doc,cellCenters:doc.cellCenters.slice(1)},/区域信息/);
bad('unknown-field',{...doc,ignoredOption:true},/ignoredOption/);
bad('duplicate-field',JSON.stringify(doc).replace('"schema":1','"schema":1,"schema":1'),/重复字段/);
const invalid=doc.cellCenters.slice();invalid[local]=core.changedCell;bad('bad-center',{...doc,cellCenters:invalid},new RegExp(String(core.changedCell)));
fs.writeFileSync(path.join(folder,'results.json'),JSON.stringify({checks,errors:0,folder},null,2));console.log(JSON.stringify({checks,errors:0,folder}));
