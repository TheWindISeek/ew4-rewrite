'use strict';
(()=>{
  const el=id=>document.getElementById(id);let polling,working=false,lastStatus,reading=false,uploading=false,switching=false,exporting=false,deleting=false,deleteTarget=null,statusEpoch=0,prepared=null;
  const menuPanels=['main-menu','scenario-menu','countries','campaign-screen','hq-screen','editor-screen'];
  function hideMenuPanels(){for(const id of menuPanels)el(id).hidden=true;}
  function showMainMenu(){hideMenuPanels();el('mod-screen').hidden=true;el('mod-packages-screen').hidden=true;el('main-menu').hidden=false;}
  function showModHub(){hideMenuPanels();el('mod-packages-screen').hidden=true;el('mod-screen').hidden=false;}
  function packagesOpen(){return !el('mod-packages-screen').hidden;}
  function openPackages(){hideMenuPanels();el('mod-screen').hidden=true;el('mod-packages-screen').hidden=false;prepared=null;syncImportMeta();refresh();clearInterval(polling);polling=setInterval(refresh,1000);}
  function closePackages(){clearInterval(polling);showModHub();}
  async function api(body={}){const result=await fetch('/api/mods?q='+encodeURIComponent(JSON.stringify(body)),{cache:'no-store'}).then(r=>r.json());if(result.error||!Array.isArray(result.mods))throw new Error(result.error||result.message||'当前客户端不支持内容包管理');return result;}
  function message(text){el('mod-progress').textContent=text;}
  function syncImportMeta(){
    const ready=!!prepared&&!working;
    el('mod-import-meta').hidden=!prepared;
    el('mod-id').disabled=!ready;
    el('mod-name').disabled=!ready;
    el('mod-import').disabled=!ready;
    if(!prepared){el('mod-id').value='';el('mod-name').value='';el('mod-file-name').textContent='';message('');}
  }
  function setBusy(value){working=value;el('mod-select').disabled=value;syncImportMeta();if(value)for(const button of el('mod-list').querySelectorAll('button'))button.disabled=true;}
  function row(name,meta,key,active,warnings=[],allowExportApk){
    if(allowExportApk===undefined)allowExportApk=true;
    const box=document.createElement('div');box.className='mod-row';box.dataset.modKey=key||'builtin';box.setAttribute('aria-label',name);const info=document.createElement('div');info.className='mod-row-info';
    const title=document.createElement('div');title.className='mod-row-name';title.textContent=name;const detail=document.createElement('div');detail.className='mod-row-meta';detail.textContent=meta;info.append(title,detail);
    if(warnings.length){const warn=document.createElement('div');warn.className='mod-row-warn';warn.textContent=warnings.join(' · ');info.append(warn);}
    const button=document.createElement('button');button.className='native-command';button.textContent=active?'已启用':'启用';button.disabled=active||working;
    button.onclick=async()=>{switching=true;setBusy(true);button.disabled=true;message('正在校验并切换内容');try{await api({op:'activate',key});location.reload();}catch(e){switching=false;message(e.message);setBusy(false);lastStatus=null;}};
    const actions=document.createElement('div');actions.className='mod-row-actions';actions.append(button);
    if(key){
      if(allowExportApk){const apkLabel=typeof ew4t==='function'?ew4t('ui.export_apk','导出 APK'):'导出 APK';
        const apk=document.createElement('button');apk.className='native-command mod-export-apk';apk.textContent=apkLabel;apk.setAttribute('aria-label',apkLabel);apk.disabled=working;
        apk.onclick=async()=>{if(working)return;exporting=true;setBusy(true);message(typeof ew4t==='function'?ew4t('ui.export_apk_working','正在导出 APK…'):'正在导出 APK…');
          try{const payload=await fetch('/api/export-apk',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({op:'export',modId:key,allowLossy:true})}).then(r=>r.json());
            if(!payload.ok)throw new Error(payload.message||payload.code||'导出失败');
            const url=payload.data?.downloadUrl;if(!url)throw new Error('无下载地址');
            const a=document.createElement('a');a.href=url;a.download=payload.data?.fileName||'export.apk';a.rel='noopener';document.body.append(a);a.click();a.remove();
            message(typeof ew4t==='function'?ew4t('ui.export_apk_done','APK 已生成，开始下载'):'APK 已生成，开始下载');
          }catch(e){message(e.message);}finally{exporting=false;setBusy(false);}};
        actions.append(apk);}
      const remove=document.createElement('button');remove.className='native-command red';remove.textContent='删除';remove.setAttribute('aria-label','删除 '+name);remove.disabled=working;
      remove.onclick=()=>{if(working)return;deleteTarget={key,name,active};el('mod-delete-description').textContent='确定删除“'+name+'”？\n已安装内容将被移除，存档会保留。'+(active?'\n此 Mod 当前已启用，删除后将切换到原版。':'');el('mod-delete-confirm').showModal();};actions.append(remove);}
    box.append(info,actions);el('mod-list').append(box);
  }
  function render(data){
    const p=data.import;setBusy(p.status==='working'||reading||uploading||switching||exporting||deleting);
    if(!exporting)message(deleting?'正在删除 Mod':switching?'正在校验并切换内容':reading||uploading?'正在读取本地文件':p.message+(p.total?' ('+p.completed+'/'+p.total+')':''));
    const allowExportApk=data.exportApkSupported!==false;
    const signature=JSON.stringify([data.active,data.mods,allowExportApk,working]);if(signature===lastStatus)return;lastStatus=signature;el('mod-list').replaceChildren();
    row('原版','内置资源',null,!data.active,[],allowExportApk);
    for(const m of data.mods)row(m.name,m.id+' · '+m.version+' · '+(m.bytes/1048576).toFixed(1)+' MiB',m.key,m.active,m.warnings,allowExportApk);
  }
  async function refresh(){const epoch=statusEpoch;try{const data=await api();if(epoch===statusEpoch)render(data);}catch(e){if(epoch===statusEpoch)message(e.message);}}
  el('mod-open').onclick=()=>showModHub();
  el('mod-screen-back').onclick=()=>{closePackages();showMainMenu();};
  el('mod-packages-back').onclick=()=>closePackages();
  el('mod-hub-packages').onclick=()=>openPackages();
  el('mod-hub-editor').onclick=()=>{if(window.Ew4Editor)window.Ew4Editor.open();};
  el('mod-delete-cancel').onclick=()=>el('mod-delete-confirm').close();
  el('mod-delete-confirm').onclose=()=>{deleteTarget=null;};
  el('mod-delete-submit').onclick=async()=>{
    if(!deleteTarget||working)return;
    const target=deleteTarget;deleting=true;statusEpoch++;setBusy(true);clearInterval(polling);el('mod-delete-confirm').close();message('正在删除 Mod');
    try{const data=await api({op:'delete',key:target.key});if(data.mods.some(m=>m.key===target.key))throw new Error('删除未完成，请更新客户端后重试');
      if(target.active){location.reload();return;}deleting=false;render(data);
    }catch(e){deleting=false;await refresh();message(e.message);}
    finally{deleting=false;if(packagesOpen())polling=setInterval(refresh,1000);}
  };
  window.ew4ModFileSelected=()=>{reading=true;setBusy(true);message('正在读取本地文件');};
  window.ew4ModFileQueued=()=>{reading=false;refresh();};
  window.ew4ModImportError=text=>{reading=false;message(text);setBusy(false);};
  window.ew4ModFilePrepared=async data=>{
    const previous=prepared;prepared=data;reading=false;uploading=false;
    el('mod-id').value=data.identity.id;el('mod-name').value=data.identity.name;
    el('mod-file-name').textContent=data.identity.fileName;setBusy(false);message('');
    if(previous&&previous.token!==data.token)try{await api({op:'discard',token:previous.token});}catch{}
  };
  el('mod-select').onclick=()=>{
    if(window.Ew4ModImport)window.Ew4ModImport.choose('','');else el('mod-file').click();
  };
  el('mod-import').onclick=async()=>{
    if(working||!prepared)return;
    const id=el('mod-id').value.trim(),name=el('mod-name').value.trim();if(!/^[a-z0-9][a-z0-9_.-]{0,63}$/.test(id)||!name){message('请填写有效的标识及名称');return;}
    reading=true;statusEpoch++;setBusy(true);message('正在导入内容包…');
    try{
      const form=new FormData();form.append('token',prepared.token);form.append('id',id);form.append('name',name);
      const result=await fetch('/api/mod-import',{method:'POST',body:form}).then(r=>r.json());
      if(result.error||!Array.isArray(result.mods))throw new Error(result.error||result.message||'导入失败');
      prepared=null;reading=false;el('mod-file-name').textContent='';render(result);location.reload();
    }
    catch(e){reading=false;setBusy(false);lastStatus=null;message(e.message);}
  };
  el('mod-file').onchange=async()=>{
    const file=el('mod-file').files[0];if(!file)return;
    uploading=true;setBusy(true);message('正在读取本地文件');
    try{const form=new FormData();form.append('file',file);
      const result=await fetch('/api/mod-prepare',{method:'POST',body:form}).then(r=>r.json());if(result.error||!result.token||!result.identity)throw new Error(result.error||'当前客户端不支持自动识别，请更新客户端');await window.ew4ModFilePrepared(result);
    }catch(e){message(e.message);setBusy(false);}finally{uploading=false;el('mod-file').value='';}
  };
  window.ew4ModUi={showMainMenu,showModHub,openPackages,closePackages,packagesOpen};
  syncImportMeta();
  api().then(()=>{el('mod-open').hidden=false;}).catch(()=>{});
})();
