'use strict';
window.Ew4SkillUI={
  icon(skill,size,uiImage){
    const legacy=/^ew4\.skill\.(\d+)$/.exec(skill.id||'');
    const number=legacy?Number(legacy[1])+1:0;
    const icon=skill.icon||(number>=1&&number<=40?'skill_'+String(number).padStart(2,'0')+'.png':null);
    if(!icon)return uiImage('button_generalinfo.png',size);
    if(!icon.startsWith('extensions/'))return uiImage(icon,size);
    const img=new Image(size,size);img.alt='';img.src='/content/'+icon;return img;
  },
  layout(container,layout,slots){
    if(!layout&&slots.length<=4)return;
    const config=layout||{mode:'list',columns:1,gap:2,iconSize:19,maxHeight:74};
    container.classList.add('extension-skills');container.dataset.layout=config.mode;
    container.style.setProperty('--skill-columns',config.mode==='grid'?config.columns:1);
    container.style.setProperty('--skill-gap',config.gap+'px');
    container.style.setProperty('--skill-icon',config.iconSize+'px');
    container.style.setProperty('--skill-max-height',config.maxHeight+'px');
    for(const key of ['left','top','width','height'])if(config[key]!=null)container.style[key]=config[key]+'px';
    if(config.height!=null)container.style.maxHeight=config.height+'px';
    if(config.mode==='positioned'){
      const surface=document.createElement('div');surface.className='positioned-skill-surface';
      for(const [i,row] of [...container.children].entries()){
        row.style.left=(slots[i]?.x??0)+'px';row.style.top=(slots[i]?.y??i*(config.iconSize+config.gap))+'px';surface.append(row);
      }
      surface.style.height=Math.max(40,...slots.map((s,i)=>(s.y??i*(config.iconSize+config.gap))+Math.max(20,config.iconSize)))+'px';
      surface.style.width=Math.max(150,...slots.map(s=>(s.x??0)+150))+'px';container.append(surface);
    }
  }
};
