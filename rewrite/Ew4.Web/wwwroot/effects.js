'use strict';
(function(root){
  function track(points,value,key,axis='life',fallback=1){
    if(!points?.length)return fallback;
    let a=points[0],b=a;
    for(const p of points){if(Number(p[axis])<=value)a=p;else{b=p;break;}b=p;}
    const from=Number(a[key]??fallback),to=Number(b[key]??from),span=Number(b[axis])-Number(a[axis]);
    return span>0?from+(to-from)*Math.max(0,Math.min(1,(value-Number(a[axis]))/span)):from;
  }
  // Original 82084: weapon enum, defending category, damage band and sea tile.
  function impact(weapon,category,damage,sea,facing){
    const band=damage>40?3:damage>25?2:damage>10?1:0,level=(band>>1)+1,side=facing<0?' left':' right';
    if(weapon==='gun'||weapon==='mgun')return (category==='fort'?'stonestrike':category==='naval'?'woodstrike':'bodystrike')+level+side;
    if(weapon==='cold')return (category==='fort'||category==='naval'?'coldstonestrike':'coldstrike')+level;
    if(weapon==='guns'&&category!=='fort'&&category!=='naval')return 'bodystrike'+level+side;
    if(weapon==='guns')return 'strike'+(band+1);
    return (sea?'seastrike':'strike')+(band+1);
  }
  function inView(rect,x,y,radius){return !rect||x+radius>=rect.left&&x-radius<=rect.left+rect.width&&y+radius>=rect.top&&y-radius<=rect.top+rect.height;}
  class DamageNumbers {
    constructor(text){this.text=text;this.instances=[];this.cache=new Map();}
    clear(){this.instances=[];}
    add(amount,critical,x,y,speed=1){
      if(!Number.isFinite(amount)||amount<=0)return;
      const key=amount+':'+!!critical;
      if(!this.cache.has(key)){
        const ink=this.text.element(String(amount),'font_char_3_hd',critical?'#ff3030':'#ffffff').firstChild;
        const shadow=document.createElement('canvas');shadow.width=ink.width;shadow.height=ink.height;
        const d=shadow.getContext('2d');d.drawImage(ink,0,0);d.globalCompositeOperation='source-in';d.fillStyle='#161616';d.fillRect(0,0,shadow.width,shadow.height);
        const canvas=document.createElement('canvas');canvas.width=ink.width+4;canvas.height=ink.height+4;const draw=canvas.getContext('2d');
        for(const [dx,dy] of [[-2,0],[2,0],[0,-2],[0,2],[-1,-1],[1,-1],[-1,1],[1,1]])draw.drawImage(shadow,2+dx,2+dy);
        draw.drawImage(ink,2,2);this.cache.set(key,canvas);if(this.cache.size>128)this.cache.delete(this.cache.keys().next().value);
      }
      this.instances.push({amount,critical:!!critical,x,y,started:performance.now(),duration:1000/speed,canvas:this.cache.get(key)});
    }
    frame(draw,time,rect){
      this.instances=this.instances.filter(hit=>{
        const progress=Math.max(0,(time-hit.started)/hit.duration);if(progress>=1)return false;
        const y=hit.y-36-32*progress;if(!inView(rect,hit.x,y,64))return true;
        const width=hit.canvas.width*.6,height=hit.canvas.height*.6;
        draw.save();draw.globalAlpha*=Math.min(1,(1-progress)/.3);draw.drawImage(hit.canvas,hit.x-width/2,y-height/2,width,height);draw.restore();return true;
      });
    }
  }
  class Effects {
    constructor(api,data){this.api=api;this.data=data;this.instances=[];this.tints=new Map();this.voices=new Set();this.trails=new Map();this.serial=1;}
    motion(profile,action,facing){const source=this.data.motions[this.data.bindings[profile]],side=facing<0?'left':'right';
      return source?.motions.find(m=>m.type===action&&Number(m.index||0)===0&&(m.dir===side||(!m.dir||m.dir==='all')));}
    firstEvent(name){const events=this.data.timelines[name];return events?.length?Math.min(...events.map(e=>Number(e.at)||0)):null;}
    lastEvent(name){const events=this.data.timelines[name];return events?.length?Math.max(...events.map(e=>Number(e.at)||0)):null;}
    clear(){this.instances=[];this.trails.clear();for(const voice of this.voices){voice.pause();voice.src='';}this.voices.clear();}
    sound(file){if(!file||!this.data.sounds.includes(file)||!this.api.volume())return;
      const voice=new Audio('/content/sounds/'+file);voice.volume=this.api.volume()/100;this.voices.add(voice);
      voice.onended=()=>this.voices.delete(voice);voice.play().catch(()=>this.voices.delete(voice));}
    timeline(name,x,y,options={}){
      const events=this.data.timelines[name];if(!events)return 0;
      const instance={x,y,events,started:options.started??performance.now(),speed:options.speed||1,scale:options.scale||1,done:new Set(),particles:[],seed:this.serial++};
      this.instances.push(instance);return this.lastEvent(name)*1000/instance.speed;
    }
    effect(name,x,y,options={}){this.instances.push({x,y,events:[{type:'effect',name,at:0}],started:performance.now(),speed:options.speed||1,scale:options.scale||1,layer:options.layer,quantityScale:options.quantityScale??1,emissionLife:options.emissionLife,done:new Set(),particles:[],seed:this.serial++});}
    trail(id,x,y,category,sea,time){
      const last=this.trails.get(id)??time-50;if(time-last<50)return;this.trails.set(id,time);
      const kind=sea?4:category==='infantry'?1:category==='cavalry'?2:3;
      this.effect('effect_moving'+kind+'.xml',x,y,{emissionLife:.05,scale:.5,quantityScale:sea?1:.35,layer:'ground'});
    }
    emit(instance,event){
      this.sound(event.sound);
      if(event.type==='animation'||event.type==='image')return;
      const definitions=this.data.definitions[event.name];if(!definitions)return;
      let seed=instance.seed;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
      for(const emitter of definitions){
        const p=emitter.parameters,get=(key,fallback=0)=>{const a=p[key];return a?Number(a.min??fallback)+(Number(a.max??a.min??fallback)-Number(a.min??fallback))*random():fallback;};
        const emissionLife=Math.min(Number(emitter.life),instance.emissionLife??Infinity),step=1/60,angle=Number(event.rot||0)*Math.PI/180;
        const burst=p.settings?.mode==='once';
        for(let time=0,credit=burst?Math.max(0,track(p.timetrack,0,'quantity','time',0)):random();time<emissionLife;time+=step){
          if(!burst)credit+=Math.max(0,track(p.timetrack,time,'quantity','time',0))*Math.min(step,emissionLife-time)*(instance.quantityScale??1);
          while(credit>=1){credit--;
            const direction=get('angle')*Math.PI/180+angle,life=get('life',.5),speed=get('speed');
            const dx=(random()-.5)*Number(p.settings?.width||0),dy=(random()-.5)*Number(p.settings?.height||0);
            instance.particles.push({image:p.image,tracks:p.lifetrack,born:Number(event.at||0)+time,life,
              x:Number(event.x||0)+dx*Math.cos(angle)-dy*Math.sin(angle),y:Number(event.y||0)+dx*Math.sin(angle)+dy*Math.cos(angle),
              vx:Math.cos(direction)*speed,vy:Math.sin(direction)*speed,gravity:get('gravity'),scale:get('scale',1),rotation:get('rotangle')*Math.PI/180+angle,spin:get('rotspeed'),
              color:[get('r',255),get('g',255),get('b',255),get('a',255)]});
          }
          if(burst)break;
        }
      }
    }
    texture(name,color){
      const atlas=this.api.manifest.atlases.eff,sprite=atlas?.[name],image=this.api.image('images/eff.png');
      if(!sprite||!image.complete||!image.naturalWidth)return null;
      const rgb=color.slice(0,3).map(v=>Math.round(v/16)*16),key=name+rgb.join(',');
      if(!this.tints.has(key)){
        if(this.tints.size>256)this.tints.delete(this.tints.keys().next().value);
        const canvas=document.createElement('canvas');canvas.width=sprite.w;canvas.height=sprite.h;const d=canvas.getContext('2d');
        d.drawImage(image,sprite.x,sprite.y,sprite.w,sprite.h,0,0,sprite.w,sprite.h);d.globalCompositeOperation='multiply';d.fillStyle='rgb('+rgb.join(',')+')';d.fillRect(0,0,canvas.width,canvas.height);d.globalCompositeOperation='destination-in';d.drawImage(image,sprite.x,sprite.y,sprite.w,sprite.h,0,0,sprite.w,sprite.h);
        this.tints.set(key,canvas);
      }
      return this.tints.get(key);
    }
    frame(draw,time,layer='foreground',visibleRect=null){
      this.instances=this.instances.filter(instance=>{
        if((instance.layer||'foreground')!==layer)return true;
        const elapsed=(time-instance.started)/1000*instance.speed;
        if(elapsed<0)return true;
        instance.events.forEach((event,index)=>{if(elapsed>=Number(event.at||0)&&!instance.done.has(index)){instance.done.add(index);this.emit(instance,event);}});
        let visualAlive=false;
        for(const event of instance.events){
          const age=elapsed-Number(event.at||0);if(age<0)continue;
          if(event.type==='animation'){
            const lib=this.api.libraries.get(event.res),item=lib?.byName.get(event.name);if(!item||age*lib.fps>=item.duration)continue;
            visualAlive=true;
            const x=instance.x+Number(event.x||0)*instance.scale,y=instance.y+Number(event.y||0)*instance.scale;
            const bounds=Ew4Battlefield.animationBounds(lib,item,age*lib.fps),ratio=lib.pixelRatio||1;
            const radius=bounds?Math.hypot(Math.max(Math.abs(bounds.left),Math.abs(bounds.right))*Number(event.hscale||1),Math.max(Math.abs(bounds.top),Math.abs(bounds.bottom))*Number(event.vscale||1))*instance.scale/ratio:Infinity;
            if(!inView(visibleRect,x,y,radius))continue;
            draw.save();draw.translate(x,y);draw.scale(Number(event.hscale||1)*instance.scale,Number(event.vscale||1)*instance.scale);this.api.animation(lib,item,age*lib.fps,0,draw);draw.restore();
          }else if(event.type==='image'&&age<Number(event.life||0)){
            visualAlive=true;const x=instance.x+Number(event.x||0)*instance.scale,y=instance.y+Number(event.y||0)*instance.scale,sprite=this.api.manifest.atlases.eff?.[event.name];
            const radius=sprite?Math.hypot(sprite.w,sprite.h)*Math.abs(Number(event.scale||1)*instance.scale):Infinity;
            if(!inView(visibleRect,x,y,radius))continue;
            draw.save();draw.translate(x,y);draw.scale(Number(event.scale||1)*instance.scale,Number(event.scale||1)*instance.scale);this.api.sprite('eff',event.name,0,0,1,draw);draw.restore();
          }
        }
        for(const p of instance.particles){const age=elapsed-p.born;if(age<0||age>p.life)continue;const life=age/p.life,scale=p.scale*track(p.tracks,life,'scale'),s=instance.scale;
          const alpha=Math.max(0,Math.min(1,p.color[3]*track(p.tracks,life,'a')/255));if(alpha<=1/255)continue;
          const speed=track(p.tracks,life,'speed'),gravity=track(p.tracks,life,'gravity');
          const width=Number(p.image.width)*scale*s,height=Number(p.image.height)*scale*s;
          const x=instance.x+(p.x+p.vx*age*speed)*s,y=instance.y+(p.y+p.vy*age*speed+p.gravity*age*age*.5*gravity)*s;
          const radius=Math.hypot(width,height)/2;
          if(!inView(visibleRect,x,y,radius))continue;
          const color=p.color.map((v,i)=>v*track(p.tracks,life,['r','g','b','a'][i])),texture=this.texture(p.image?.file,color);if(!texture)continue;
          draw.save();draw.translate(x,y);draw.rotate(p.rotation+p.spin*age*track(p.tracks,life,'rotspeed'));draw.globalAlpha=alpha;draw.globalCompositeOperation=p.image.blend==='add'?'lighter':'source-over';draw.drawImage(texture,-width/2,-height/2,width,height);draw.restore();
        }
        return visualAlive||instance.done.size<instance.events.length||instance.particles.some(p=>elapsed<p.born+p.life);
      });
    }
  }
  root.Ew4Effects={Effects,DamageNumbers,impact,track};
})(globalThis);
