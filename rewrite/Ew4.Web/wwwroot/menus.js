'use strict';
(function(root){
  class Menus {
    constructor(api){this.api=api;this.chapter=null;this.rating=null;this.recruiting=false;
      document.getElementById('campaign-back').onclick=()=>{document.getElementById('campaign-screen').hidden=true;};
      document.getElementById('hq-back').onclick=()=>{
        if(this.deployment){this.closeRoster();this.api.tutorialUi('winbtn_back');return;}
        if(this.recruiting){this.recruiting=false;this.drawRoster();return;}
        this.api.main();
      };
      document.getElementById('hq-recruit').onclick=()=>{this.recruiting=true;this.rating=0;this.offer=null;this.drawRoster();};
    }
    get manifest(){return this.api.manifest;}
    get catalog(){return this.api.catalog;}
    text(key){return this.manifest.strings[key]||key;}
    lettering(text,font='font_text_2_cn_hd',color='#404040',width,align,options){return this.api.text(text,font,color,width,align,options);}
    slideInSidebar(list){
      const rows=[...list.children];
      for(const node of [list,...rows])for(const animation of node.getAnimations())animation.cancel();
      if(matchMedia('(prefers-reduced-motion: reduce)').matches)return;
      const easing='cubic-bezier(.2,.7,.25,1)';
      // Separate translate from the selected row's transform so both offsets compose.
      list.animate([{translate:'180px 0'},{translate:'0 0'}],{duration:220,easing});
      rows.forEach((row,index)=>row.animate([{translate:'160px 0'},{translate:'0 0'}],
        {duration:300,delay:Math.min(index,5)*60,easing,fill:'backwards'}));
    }
    async open(mode){
      if(mode===4)return this.tutorialMenu();
      if(mode!==1)return false;
      const list=document.getElementById('scenarios');list.replaceChildren();list.className='chapter-list';
      const layout=this.manifest.layouts.form_selcampaign;
      const custom=this.catalog.chapters.filter(chapter=>!layout.children.some(e=>e.attributes.id==='btn_selcamp_'+chapter.id));
      if(custom.length)list.classList.add('extended-chapters');
      for(const chapter of this.catalog.chapters){const placement=layout.children.find(e=>e.attributes.id==='btn_selcamp_'+chapter.id)?.attributes;
        const b=this.api.button('',()=>this.previewChapter(chapter,b),list);b.className='chapter-entry';b.setAttribute('aria-label',chapter.name);
        if(placement){Object.assign(b.style,{left:placement.x+'px',top:placement.y+'px',width:placement.w+'px',height:placement.h+'px'});b.append(this.api.uiImage(placement.frm1,Number(placement.w),Number(placement.h)));}
        else{b.classList.add('custom-chapter');Object.assign(b.style,{left:'356px',top:(14+custom.indexOf(chapter)*48)+'px',width:'185px',height:'40px'});b.append(this.lettering(chapter.name,'font_text_2_cn_hd','#404040',170,'center'));}
        b.title=chapter.name+' '+chapter.start+'–'+chapter.end;
      }
      document.getElementById('scenario-menu').onclick=e=>{if(!e.target.closest('.chapter-entry,.chapter-preview'))document.querySelector('.chapter-preview')?.remove();};
      return true;
    }
    previewChapter(chapter,anchor){
      document.querySelector('.chapter-preview')?.remove();
      const popup=document.createElement('section');popup.className='chapter-preview';popup.setAttribute('aria-label',chapter.name+' '+chapter.start+'-'+chapter.end);
      const title=document.createElement('div');title.className='chapter-preview-title';title.append(this.lettering(chapter.name,'font_text_2_cn_hd','#ebe6e1',139,'center',{singleLine:true}));
      const years=document.createElement('div');years.className='chapter-preview-years';years.append(this.lettering(chapter.start+'-'+chapter.end,'font_char_2_hd','#404040'));
      const flags=document.createElement('div');flags.className='chapter-preview-flags';for(const country of chapter.countries)flags.append(this.api.uiImage(country+'1.png',26,15));
      popup.append(title,years,flags);const confirm=this.api.button('',()=>{popup.remove();return this.openChapter(chapter);},popup);confirm.className='chapter-preview-confirm';confirm.setAttribute('aria-label','进入 '+chapter.name);confirm.title='进入 '+chapter.name;confirm.append(this.api.uiImage('button_confrim.png',31,31.5));
      // The visible marker tip is (25, 54); its texture includes transparent shadow margins.
      const targetX=parseFloat(anchor.style.left)+25,targetY=parseFloat(anchor.style.top)+54;
      const left=Math.max(4,Math.min(425,targetX-69.5));
      popup.style.left=left+'px';popup.style.top=Math.max(24,targetY-69.5)+'px';popup.style.setProperty('--chapter-arrow-left',(targetX-left-16.5)+'px');
      document.getElementById('scenarios').append(popup);confirm.focus({preventScroll:true});
    }
    async openChapter(chapter){
      await this.api.refreshCatalog();
      await this.api.loadLibrary('strategicmap');this.chapter=chapter;
      // Chapter names are not all present in the limited title atlas. Use one complete
      // original font for the entire title instead of mixing different glyph designs.
      document.getElementById('campaign-title').replaceChildren(this.lettering(chapter.name,'font_text_2_cn_hd','#ebe6e1'));
      const list=document.getElementById('campaign-battles');list.replaceChildren();
      const chapterArt=['france','coalition','holyroman','east','usa','uk'][chapter.id-1]||'france';
      list.style.setProperty('--chapter-entry-image','url("/content/ui/image_menu_hd/button_choosestage_'+chapterArt+'.png")');
      for(const presentation of Object.values(this.catalog.presentations).filter(s=>s.chapter===chapter.id)){
        if(presentation.hidden&&!this.catalog.unlockedCampaigns?.includes(presentation.id))continue;
        const scenario=this.catalog.scenarios.find(s=>s.id===presentation.id);if(!scenario)continue;
        const b=this.api.button('',()=>this.chooseBattle(presentation,b),list);b.className='country-entry';b.setAttribute('aria-label',scenario.name);b.setAttribute('aria-pressed','false');b.append(this.lettering(scenario.name,'font_text_2_cn_hd','#404040',132,'center',{singleLine:true}));
      }
      document.getElementById('campaign-screen').hidden=false;list.scrollTop=0;list.firstElementChild?.click();
      this.slideInSidebar(list);
    }
    chooseBattle(presentation,button){
      this.battle=presentation;
      for(const b of document.getElementById('campaign-battles').children)b.setAttribute('aria-pressed',String(b===button));
      document.getElementById('campaign-description').replaceChildren(this.lettering(presentation.description.startsWith('desc_')?'':presentation.description,'font_text_1_cn_hd','#404040',267));
      document.getElementById('campaign-description').scrollTop=0;
      const commander=document.getElementById('campaign-commander');commander.replaceChildren();
      if(presentation.commander){commander.append(this.portrait(presentation.commander+'.png'));const name=document.createElement('span');name.append(this.lettering(this.text('name_'+presentation.commander),'font_text_2_cn_hd','#ebe6e1',70,'center'));commander.append(name);}
      document.getElementById('campaign-confirm').onclick=()=>this.confirmBattle(presentation).catch(e=>this.api.notice(e.message));
    }
    async confirmBattle(presentation){
      if(presentation.alternate&&presentation.countries.length){
        let chosen=0;const body=this.api.detail('country_label',234,150,()=>this.api.start(chosen?presentation.alternate:presentation.id,null));body.className='scenario-countries';
        presentation.countries.forEach((country,index)=>{const b=this.api.button('',()=>{chosen=index;for(const c of body.children)c.setAttribute('aria-pressed',String(c===b));},body);b.setAttribute('aria-label',this.text('name_'+country));b.append(this.api.uiImage(country+'1.png',51,30),this.api.labels('country_'+country,this.text('name_'+country)));if(!index)b.click();});
      }else await this.api.start(presentation.id,null);
    }
    draw(time){
      if(document.getElementById('campaign-screen').hidden||!this.battle)return;
      const surface=document.getElementById('campaign-map'),draw=surface.getContext('2d'),lib=this.api.libraries.get('strategicmap');if(!lib)return;
      const rect=surface.getBoundingClientRect(),ratio=Math.min(devicePixelRatio||1,3),w=Math.round(rect.width*ratio),h=Math.round(rect.height*ratio);
      if(surface.width!==w||surface.height!==h){surface.width=w;surface.height=h;}
      draw.setTransform(w/568,0,0,h/320,0,0);draw.clearRect(0,0,568,320);
      const definition=this.manifest.strategic[this.battle.id]||{},cx=Number(definition.centerx||410),cy=Number(definition.centery||288),scale=Number(definition.scale||1);
      draw.save();this.api.menuArt.drawStrategic(draw,definition,scale);
      draw.translate(205,128);draw.scale(scale,scale);if(lib.byName.has(definition.name))this.api.animation(lib,lib.byName.get(definition.name),time/180,0,draw);draw.restore();
    }
    tutorialMenu(){
      const body=this.api.detail('tutorial_label',250,200);body.className='tutorial-menu';
      for(const [title,action] of [['btn_basic',()=>this.beginTutorial('tutorials1')],['btn_classic',()=>this.beginTutorial('tutorials2')],['btn_notice',()=>this.playNotice()]]){
        const b=this.api.button(this.text(title),action,body);b.className='native-command';
      }
      return true;
    }
    async beginTutorial(id){document.getElementById('detail-modal').hidden=true;await this.api.start(id,null);}
    playNotice(){const body=this.api.detail('tutorial_label',400,225);body.className='play-notice';const p=document.createElement('p');
      // The source uses a small markup vocabulary; show its text without executing markup.
      p.textContent=this.text('html_notice').replace(/<[^>]*>/g,'').replace(/\\n/g,'\n');body.append(p);
    }
    portrait(name){
      const img=document.createElement('img');img.alt='';img.width=78;img.height=78;
      const atlas=['princesses_hd','commanders1_hd','commanders2_hd'].find(a=>this.manifest.atlases[a]?.[name]);
      if(atlas)img.src='/content/ui/'+atlas+'/'+name;else{img.src='/content/ui/image_ui_hd/button_add_generals.png';img.title=name;}
      return img;
    }
    async headquarters(deployment=null){
      this.data=await this.api.rpc('headquarters');this.deployment=deployment;this.recruiting=false;
      if(deployment)document.getElementById('play').hidden=true;
      for(const id of ['main-menu','scenario-menu','countries','campaign-screen'])document.getElementById(id).hidden=true;
      document.getElementById('hq-screen').hidden=false;document.getElementById('menu').hidden=false;
      this.drawRoster();
    }
    async deployGeneral(unit,revision){
      const available=await this.api.rpc('deployment',{unit,revision});
      await this.headquarters(unit);
      this.data.roster=this.data.roster.map(c=>({...c,locked:!available.commanders.includes(c.id)}));
      this.drawRoster();
    }
    closeRoster(){document.getElementById('hq-screen').hidden=true;if(this.deployment){document.getElementById('menu').hidden=true;document.getElementById('play').hidden=false;}this.deployment=null;}
    card(definition,held,action){
      const card=document.createElement('div');card.className='commander-card';card.setAttribute('role','group');card.setAttribute('aria-label',definition.name);card.append(this.portrait(definition.image));
      const name=document.createElement('span');name.className='commander-name';name.append(this.lettering(definition.name,'font_text_2_cn_hd','#ebe6e1',74,'center',{singleLine:true}));card.append(name);
      const rank=held?.learning.rank.level??definition.rank,title=held?.learning.title.level??definition.title;
      if(rank>0){const img=this.api.uiImage('rank_'+Math.min(rank,14)+'.png',21,24);img.className='commander-rank';card.append(img);}
      if(title>0){const img=this.api.uiImage('class_'+Math.min(title,9)+'.png',21,24);img.className='commander-title';card.append(img);}
      const open=document.createElement('button');open.className='commander-open';open.setAttribute('aria-label',definition.name);open.onclick=action;card.append(open);
      const info=document.createElement('button');info.className='commander-info';info.setAttribute('aria-label',definition.name+'信息');info.title='将领信息';info.onclick=e=>{e.stopPropagation();this.commanderInfo(definition,held);};info.append(this.api.uiImage('button_generalinfo_blue.png',20,20));card.append(info);
      if(held?.locked){card.dataset.locked='true';card.title='已派遣';}return card;
    }
    drawRoster(){
      const data=this.data,screen=document.getElementById('hq-screen');screen.classList.toggle('recruiting',this.recruiting);
      document.getElementById('hq-medals').replaceChildren(this.api.uiImage('medals.png',13,17),this.lettering(data.medals,'font_char_2_hd','#ebe6e1'),this.api.uiImage('badges.png',13,17),this.lettering(data.badges,'font_char_2_hd','#ebe6e1'));
      const recruit=document.getElementById('hq-recruit');recruit.replaceChildren(this.lettering(this.text('btn_college'),'font_text_3_cn_hd','#ebe6e1'));recruit.setAttribute('aria-label',this.text('btn_college'));recruit.hidden=!!this.deployment;
      const definitions=data.definitions,byId=new Map(definitions.map(d=>[d.id,d])),owned=new Map(data.roster.map(c=>[c.definitionId,c]));
      const entries=this.recruiting?(data.offers[this.rating]||[]).map(id=>byId.get(id)).filter(Boolean):data.roster.map(c=>byId.get(c.definitionId)).filter(Boolean);
      const filters=document.getElementById('hq-filters');filters.replaceChildren();
      if(this.recruiting)for(const [rating,count] of [6,4,2].entries()){
        const b=this.api.button('',()=>{this.rating=rating;this.offer=null;this.drawRoster();},filters);b.setAttribute('role','tab');b.setAttribute('aria-label',['一星招募区','二星招募区','三星招募区'][rating]);b.setAttribute('aria-selected',String(rating===this.rating));b.append(this.api.uiImage('button_general_'+count+'.png',152,55));
      }
      const roster=document.getElementById('hq-roster'),scroll=roster.scrollLeft;roster.replaceChildren();
      roster.dataset.count=this.recruiting?[6,4,2][this.rating]:entries.length;
      for(const definition of entries){
        const held=owned.get(definition.id),b=this.card(definition,held,()=>{
          if(this.recruiting){this.offer=definition;this.drawPurchases();for(const c of roster.children)c.setAttribute('aria-pressed',String(c===b));}
          else {this.rosterSelected=definition.id;for(const c of roster.children)c.setAttribute('aria-pressed',String(c===b));this.commanderInfo(definition,held);}
        });roster.append(b);
        if(!this.recruiting&&!this.deployment)this.promotionShortcut(b,held,()=>this.drawRoster());
        b.setAttribute('aria-pressed',String(definition.id===(this.recruiting?this.offer?.id:this.rosterSelected)));
      }
      const slots=this.recruiting?[6,4,2][this.rating]:Math.max(12,entries.length);
      for(let i=entries.length;i<slots;i++){const b=this.api.button('',()=>{if(!this.deployment){this.recruiting=true;this.rating=0;this.drawRoster();}},roster);b.className='commander-card empty-commander';b.setAttribute('aria-label','空将领位');b.append(this.api.uiImage('button_add_generals.png',78,78));const name=document.createElement('span');name.className='commander-name';b.append(name);b.disabled=this.recruiting;}
      if(!this.recruiting)for(const [i,card] of [...roster.children].entries()){card.style.gridRow=Math.floor(i/6)%2+1;card.style.gridColumn=Math.floor(i/12)*6+i%6+1;}
      for(const id of ['hq-recruit-band','hq-refresh','hq-buy-buttons'])document.getElementById(id)?.remove();
      if(this.recruiting){const band=document.createElement('div');band.id='hq-recruit-band';screen.insertBefore(band,roster);
        const refresh=this.api.button('',async()=>{this.offer=null;await this.transact({op:'refresh',group:this.rating});},screen);refresh.id='hq-refresh';refresh.className='native-command blue';refresh.setAttribute('aria-label','刷新将领');for(let i=0;i<3;i++)refresh.append(this.api.uiImage('button_refresh.png',13,16));this.drawPurchases();}
      roster.scrollLeft=scroll;this.headquartersControls();
    }
    drawPurchases(){
      document.getElementById('hq-buy-buttons')?.remove();if(!this.offer)return;
      const row=document.createElement('div');row.id='hq-buy-buttons';document.getElementById('hq-screen').append(row);
      for(const [currency,op,price,balance] of [['medals','recruit',this.offer.price,this.data.medals],['badges','recruit_badge',this.offer.badgePrice,this.data.badges]]){
        if(price<=0)continue;const b=this.api.button('',async()=>{const definition=this.offer;this.offer=null;await this.transact({op,commander:definition.id,group:this.rating});},row);b.className='native-command'+(currency==='badges'?' red':'');b.setAttribute('aria-label',(currency==='badges'?'徽章':'勋章')+'招募 '+price);b.disabled=balance<price;b.append(this.api.uiImage(currency+'.png',13,17),this.lettering(price,'font_text_3_cn_hd','#ebe6e1'));
      }
      this.api.popup(row);
    }
    async transact(args){this.data=await this.api.rpc('commander',{revision:this.data.revision,...args});this.drawRoster();}
    async saveMenu(saving,mode,tutorial){
      const slots=await this.api.rpc('saves',{mode}),layout=this.manifest.layouts.form_save;
      const body=this.api.detail(saving?'title_savegame':'title_loadgame',352,235);body.className='save-slots';
      const auto=document.createElement('div');auto.className='autosave-band';
      auto.append(this.lettering(this.text('text_autosave'),'font_text_2_cn_hd','#646464',100,'center'),this.api.uiImage('pattern_stage_intro.png',36,12));body.append(auto);
      for(const slot of slots){
        const attrs=layout.children.find(e=>e.attributes.id==='group_save_'+slot.slot).attributes;
        const panel=document.createElement('section');panel.className='save-slot';
        Object.assign(panel.style,{left:attrs.x+'px',top:(Number(attrs.y)-27)+'px',width:attrs.w+'px',height:attrs.h+'px'});
        const title=document.createElement('div');title.className='save-name';title.append(this.lettering(slot.corrupt?'存档损坏':slot.exists?slot.name:this.text('text_empty'),'font_text_2_cn_hd','#ebe6e1',110,'center'));panel.append(title);
        const date=document.createElement('div');date.className='save-date';
        if(slot.savedAt){const d=new Date(slot.savedAt),pad=n=>String(n).padStart(2,'0');date.append(this.lettering(d.getFullYear()+'/'+pad(d.getMonth()+1)+'/'+pad(d.getDate())+'\n'+pad(d.getHours())+':'+pad(d.getMinutes()),'font_char_1_hd','#404040',110,'center'));}panel.append(date);
        if(slot.country){const flag=this.api.uiImage(slot.country+'1.png',25.5,15);flag.className='save-country';panel.append(flag);}
        const titleText=(saving?'保存到':'载入')+(slot.slot===0?'自动存档':'存档 '+slot.slot)+(slot.exists?' '+slot.name+' 回合 '+slot.round:' 空');
        const command=this.api.button('',async()=>{
          if(saving){await this.api.rpc('save',{slot:slot.slot,mode,tutorial});document.getElementById('detail-modal').hidden=true;this.api.notice('已保存');}
          else await this.api.load(slot.slot,mode);
        },panel);
        command.className='save-slot-confirm';command.setAttribute('aria-label',titleText);command.title=titleText;
        command.disabled=saving?slot.slot===0:!slot.exists||slot.corrupt;
        command.append(this.api.uiImage('button_ok_gray_noshadow.png',30));body.append(panel);
      }
    }
    commanderInfo(definition,held,onClose){
      this.commanderReturn=onClose;
      const body=this.api.detail('headquarters_label',440,217,null,onClose||(()=>{this.drawRoster();}));body.className='commander-detail';
      const card=document.createElement('div');card.className='commander-detail-card';const portrait=this.card(definition,held,()=>{});card.append(portrait);body.append(card);
      if(!this.deployment)this.promotionShortcut(portrait,held);
      const equipment=document.createElement('div');equipment.className='commander-equipment';const title=document.createElement('div');title.className='commander-equipment-title';title.append(this.lettering(this.text('text_equipitem')));equipment.append(title);
      const items=document.createElement('div');items.className='commander-equipment-slots';for(let i=0;i<(this.data.equipmentCapacity??2);i++){const marker=this.data.items.find(m=>m.id===held?.items[i]);items.append(this.api.uiImage(marker?.image||'item_blank.png',45));}equipment.append(items);body.append(equipment);
      const skills=document.createElement('div');skills.className='commander-skills';
      for(let i=0;i<Math.max(this.data.skillLayout?0:4,definition.skills.length);i++){
        const row=document.createElement('div');row.className='skill-row';const skill=definition.skills[i];
        if(skill){
          row.append(Ew4SkillUI.icon(skill,19,this.api.uiImage),this.lettering(skill.name));
          const info=this.api.button('',()=>this.skillTip(row,skill,true),row);info.className='skill-info';info.setAttribute('aria-label',skill.name+'说明');info.append(this.api.uiImage('button_generalinfo_blue.png',18));
          row.onpointerenter=()=>this.skillTip(row,skill);row.onpointerleave=()=>{if(!this.skillPinned)this.dismissSkillTip();};
          info.onfocus=()=>this.skillTip(row,skill);info.onblur=()=>{if(!this.skillPinned)this.dismissSkillTip();};
        }skills.append(row);
      }Ew4SkillUI.layout(skills,this.data.skillLayout,definition.skills);body.append(skills);
      const ranks=document.createElement('div');ranks.className='commander-ranks';
      for(const [track,effect,value] of [['rank','hp',held?.healthBonus??definition.healthBonus],['title','recover',held?.healing??definition.healing]]){const row=document.createElement('div');row.append(this.progressionBadge(track,held?.learning[track]??{level:definition[track],experience:0}));const icon=this.api.uiImage('marker_'+effect+'_dark.png',17);icon.className='rank-effect';row.append(icon,this.lettering(value,'font_char_2_hd'));ranks.append(row);}body.append(ranks);
      const abilities=document.createElement('div');abilities.className='commander-abilities';const values=held?.learning.abilities||definition.abilities;
      for(const [key,icon] of [['infantry','upgrade_tag_infantry'],['cavalry','upgrade_tag_cavalry'],['artillery','upgrade_tag_artillery'],['naval','upgrade_tag_ship'],['fort','upgrade_tag_fortress'],['commerce','generalsinfo_trade'],['movement','generalsinfo_move'],['training','generalsinfo_training']]){
        const row=document.createElement('div');row.className='commander-ability';row.setAttribute('aria-label',key+' '+(key==='training'?(held?.training??definition.training):values[key]));row.append(this.api.uiImage(icon+'.png',22,21.5));const stars=document.createElement('div');stars.className='ability-stars';for(let i=0;i<(key==='training'?(held?.training??definition.training):values[key]);i++)stars.append(this.api.uiImage('star_middle.png',13.5,12.5));row.append(stars);abilities.append(row);
      }body.append(abilities);
      if(held&&!this.deployment){for(const [kind,text,action] of [['regroup','btn_regroup',()=>this.regroup(held.id)],['equip','btn_equip',()=>this.equipment(held.id)]]){const b=this.api.button('',action,equipment);b.className='native-command '+(kind==='equip'?'blue ':'')+kind;b.disabled=held.locked;b.setAttribute('aria-label',this.text(text));b.append(this.lettering(this.text(text),'font_text_3_cn_hd','#ebe6e1'));}}
      if(this.deployment&&held){const b=this.api.button('',async()=>{await this.api.deploy(this.deployment,held.id);document.getElementById('detail-modal').hidden=true;this.closeRoster();},equipment);b.className='native-command blue equip';b.disabled=held.locked;b.setAttribute('aria-label',this.text('btn_deploy'));b.append(this.lettering(this.text('btn_deploy'),'font_text_3_cn_hd','#ebe6e1'));}
    }
  }
  root.Ew4Menus={Menus};
})(globalThis);
