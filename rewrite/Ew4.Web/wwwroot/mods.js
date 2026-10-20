'use strict';
(()=>{
  const el=id=>document.getElementById(id);let polling,working=false,lastStatus,reading=false,uploading=false,switching=false;
  async function api(body={}){const result=await fetch('/api/mods?q='+encodeURIComponent(JSON.stringify(body)),{cache:'no-store'}).then(r=>r.json());if(result.error||!Array.isArray(result.mods))throw new Error(result.error||result.message||'当前客户端不支持内容包管理');return result;}
  function message(text){el('mod-progress').textContent=text;}
  function setBusy(value){working=value;el('mod-import').disabled=value;el('mod-id').disabled=value;el('mod-name').disabled=value;}
  function row(name,meta,key,active,warnings=[]){
    const box=document.createElement('div');box.className='mod-row';box.dataset.modKey=key||'builtin';box.setAttribute('aria-label',name);const info=document.createElement('div');info.className='mod-row-info';
    const title=document.createElement('div');title.className='mod-row-name';title.textContent=name;const detail=document.createElement('div');detail.className='mod-row-meta';detail.textContent=meta;info.append(title,detail);
    if(warnings.length){const disclosure=document.createElement('details'),summary=document.createElement('summary');summary.textContent='兼容性报告';disclosure.append(summary);for(const warning of warnings){const p=document.createElement('p');p.textContent=warning;disclosure.append(p);}info.append(disclosure);}
    const button=document.createElement('button');button.className='native-command';button.textContent=active?'已启用':'启用';button.disabled=active||working;
    button.onclick=async()=>{switching=true;setBusy(true);button.disabled=true;message('正在校验并切换内容');try{await api({op:'activate',key});location.reload();}catch(e){switching=false;message(e.message);setBusy(false);lastStatus=null;}};
    box.append(info,button);el('mod-list').append(box);
  }
  function render(data){
    const p=data.import;setBusy(p.status==='working'||reading||uploading||switching);message(switching?'正在校验并切换内容':reading||uploading?'正在读取本地文件':p.message+(p.total?' ('+p.completed+'/'+p.total+')':''));
    const signature=JSON.stringify([data.active,data.mods,working]);if(signature===lastStatus)return;lastStatus=signature;el('mod-list').replaceChildren();
    row('原版','内置资源',null,!data.active);
    for(const m of data.mods)row(m.name,m.id+' · '+m.version+' · '+(m.bytes/1048576).toFixed(1)+' MiB',m.key,m.active,m.warnings);
  }
  async function refresh(){try{render(await api());}catch(e){message(e.message);}}
  el('mod-open').onclick=()=>{el('mod-manager').hidden=false;if(typeof popupOpen==='function')popupOpen(el('mod-manager').firstElementChild);refresh();clearInterval(polling);polling=setInterval(refresh,1000);};
  el('mod-close').onclick=()=>{el('mod-manager').hidden=true;clearInterval(polling);};
  window.ew4ModFileSelected=()=>{reading=true;setBusy(true);message('正在读取本地文件');};
  window.ew4ModFileQueued=()=>{reading=false;refresh();};
  window.ew4ModImportError=text=>{reading=false;message(text);setBusy(false);};
  el('mod-import').onclick=()=>{
    const id=el('mod-id').value.trim(),name=el('mod-name').value.trim();if(!/^[a-z0-9][a-z0-9_.-]{0,63}$/.test(id)||!name){message('请填写小写字母、数字或横线组成的标识及名称');return;}
    if(window.Ew4ModImport){message('请选择本地 APK 或 Mod 包');window.Ew4ModImport.choose(id,name);}else el('mod-file').click();
  };
  el('mod-file').onchange=async()=>{
    const file=el('mod-file').files[0];if(!file)return;uploading=true;setBusy(true);message('正在读取本地文件');
    try{const form=new FormData();form.append('file',file);form.append('id',el('mod-id').value.trim());form.append('name',el('mod-name').value.trim());
      const result=await fetch('/api/mod-import',{method:'POST',body:form}).then(r=>r.json());if(result.error)throw new Error(result.error);render(result);
    }catch(e){message(e.message);setBusy(false);}finally{uploading=false;el('mod-file').value='';}
  };
  api().then(()=>{el('mod-open').hidden=false;}).catch(()=>{});
})();
