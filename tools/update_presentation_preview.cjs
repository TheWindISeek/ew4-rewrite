'use strict';
const fs=require('node:fs/promises');
const {execFileSync}=require('node:child_process');
const assert=require('node:assert/strict');
const root='E:/Workplace/game/o4mod',folder=root+'/rewrite/reports/content_bundle_20261019';
const profile=root+'/rewrite/reports/frontend_restoration_20261019/test-profile';
async function rpc(route,command){const r=await fetch('http://127.0.0.1:5184/api/'+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(command)});const result=await r.json();assert.ok(result.ok,result.message);return result.data;}
(async()=>{
  await fs.mkdir(folder,{recursive:true});
  const verifyOnly=process.argv.includes('--verify-only');
  const before=verifyOnly?JSON.parse(await fs.readFile(folder+'/preview-before.json','utf8')):await rpc('action',{op:'state'});
  if(!verifyOnly){
    await rpc('save',{slot:0,mode:0,tutorial:before.tutorialCheckpoint});
    const stamp=new Date().toISOString().replace(/[:.]/g,'-');
    await fs.cp(profile,folder+'/preview-profile-backup-'+stamp,{recursive:true});
    await fs.writeFile(folder+'/preview-before.json',JSON.stringify(before));
  }
  const command=`$listener=Get-NetTCPConnection -LocalPort 5184 -State Listen -ErrorAction Stop
$server=Get-CimInstance Win32_Process -Filter ('ProcessId='+$listener.OwningProcess)
if ($server.CommandLine -notlike '*Ew4.Web.dll*' -or $server.CommandLine -notlike '*frontend_restoration_20261019/test-profile*') { throw 'Unexpected preview process' }
Stop-Process -Id $listener.OwningProcess -ErrorAction Stop
$runtimeArgs=@('${root}/rewrite/local/content-bundle-runtime/Ew4.Web.dll','--urls','http://127.0.0.1:5184/','--root','${root}','--save-root','${profile}','--contentRoot','${root}/rewrite/Ew4.Web','--Logging:LogLevel:Default','Warning')
$newServer=Start-Process -FilePath 'C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe' -ArgumentList $runtimeArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput '${folder}/preview-stdout.log' -RedirectStandardError '${folder}/preview-stderr.log'
`;
  if(!verifyOnly)execFileSync('powershell.exe',['-NoProfile','-Command',command],{stdio:'ignore'});
  const pid=Number(execFileSync('powershell.exe',['-NoProfile','-Command','(Get-NetTCPConnection -LocalPort 5184 -State Listen).OwningProcess'],{encoding:'utf8'}).trim());
  let after;
  for(let i=0;i<30;i++){
    try{after=await rpc(verifyOnly?'action':'load',verifyOnly?{op:'state'}:{slot:0,mode:0});break;}catch(e){if(i===29)throw e;await new Promise(r=>setTimeout(r,250));}
  }
  await fs.writeFile(folder+'/preview-after.json',JSON.stringify(after));
  const checks={};
  for(const key of ['scenario','revision','round','phase','activeFaction','playerFaction','dialogue'])checks[key]=JSON.stringify(before[key])===JSON.stringify(after[key]);
  const units=view=>view.units.map(u=>Object.fromEntries(Object.entries(u).filter(([key])=>!['objective','objectiveMarker'].includes(key))));
  checks.units=JSON.stringify(units(before))===JSON.stringify(units(after));
  checks.resources=JSON.stringify(before.factions.map(f=>[f.id,f.resources]))===JSON.stringify(after.factions.map(f=>[f.id,f.resources]));
  const changes=after.cells.flatMap((c,i)=>c.owner!==before.cells[i].owner?[{cell:i,center:c.jurisdictionCenter,previous:before.cells[i].owner,owner:c.owner}]:[]);
  const weilburg=after.cells.findIndex(c=>c.name==='WEILBURG');
  const report={pid,checks,scenario:after.scenario,revision:after.revision,units:after.units.length,changes,
    weilburg:weilburg<0?null:{center:weilburg,cells:after.cells.filter(c=>c.jurisdictionCenter===weilburg).map(c=>c.owner)},
    independentPorts:after.cells.filter(c=>c.buildingKind==='port').every(c=>c.jurisdictionCenter===null)};
  await fs.writeFile(folder+'/preview-restart.json',JSON.stringify(report,null,2));
  assert.ok(Object.values(checks).every(Boolean),'preview non-territory state changed');assert.equal(changes.length,0,'preview territory state changed');assert.ok(report.independentPorts,'ports must be independent');
  await rpc('save',{slot:0,mode:0,tutorial:after.tutorialCheckpoint});
  console.log(JSON.stringify({...report,changes:changes.length}));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
