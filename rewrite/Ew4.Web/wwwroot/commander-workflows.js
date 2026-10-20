'use strict';
(function(){
  const proto=Ew4Menus.Menus.prototype;
  proto.native=function(id,onClose){return new Ew4NativeUI.Layout(this.api,id,onClose);};
  proto.held=function(id){return this.data.roster.find(c=>c.id===id);};
  proto.definition=function(held){return this.data.definitions.find(d=>d.id===held.definitionId);};
  proto.reopenCommander=function(id){const held=this.held(id);if(held)this.commanderInfo(this.definition(held),held,this.commanderReturn);};
  proto.updateAccount=async function(args){this.data=await this.api.rpc('commander',{revision:this.data.revision,...args});this.drawRoster();};
  proto.headquartersControls=function(){
    for(const id of ['hq-princess','hq-shop'])document.getElementById(id)?.remove();
    if(this.recruiting||this.deployment)return;
    const layout=this.manifest.layouts.form_deploygeneral;
    for(const [native,id,title,action] of [['btn_princess','hq-princess',this.text('btn_princess'),()=>this.princesses()],['btn_shop','hq-shop',this.text('title_shop'),()=>this.shop()]]){
      const a=layout.children.find(e=>e.attributes.id===native).attributes,b=this.api.button('',action,document.getElementById('hq-screen'));
      b.id=id;b.className='native-command'+(native==='btn_shop'?' blue':'');b.setAttribute('aria-label',title);
      Object.assign(b.style,{position:'absolute',left:a.x+'px',top:a.y+'px',width:a.w+'px',height:a.h+'px'});
      b.append(native==='btn_shop'?this.api.uiImage('image_shop.png',20,16):this.lettering(title,'font_text_3_cn_hd','#ebe6e1'));
    }
  };
  proto.dismissSkillTip=function(){this.skillPopup?.remove();this.skillEvents?.abort();this.skillPopup=null;this.skillAnchor=null;this.skillPinned=false;};
  proto.skillTip=function(anchor,skill,pin=false){
    if(this.skillAnchor===anchor&&this.skillPopup){if(pin){if(this.skillPinned)this.dismissSkillTip();else this.skillPinned=true;}return;}
    this.dismissSkillTip();this.skillAnchor=anchor;this.skillPinned=pin;
    const tip=document.createElement('div');tip.id='commander-skill-tip';tip.className='skill-tooltip';tip.setAttribute('role','tooltip');tip.append(this.lettering(skill.description,'font_text_2_cn_hd','#ebe6e1',145));this.skillPopup=tip;
    const dialog=document.getElementById('detail-modal').firstElementChild;dialog.append(tip);
    const a=anchor.getBoundingClientRect(),d=dialog.getBoundingClientRect(),scale=d.width/dialog.offsetWidth;
    const left=a.left-d.left>170*scale;
    tip.classList.toggle('tip-right',!left);tip.style.left=((left?a.left-d.left-165*scale:a.right-d.left+8*scale)/scale)+'px';
    tip.style.top=Math.max(27,Math.min(dialog.offsetHeight-tip.offsetHeight-4,(a.top-d.top)/scale+(anchor.offsetHeight-tip.offsetHeight)/2))+'px';
    tip.style.setProperty('--tip-arrow-top',Math.max(0,Math.min(tip.offsetHeight-19,(a.top+a.bottom-2*d.top)/2/scale-parseFloat(tip.style.top)-9.5))+'px');
    tip.onclick=()=>this.dismissSkillTip();this.skillEvents=new AbortController();const signal=this.skillEvents.signal;
    document.addEventListener('pointerdown',e=>{if(!anchor.contains(e.target)&&!tip.contains(e.target))this.dismissSkillTip();},{signal});
    document.addEventListener('keydown',e=>{if(e.key==='Escape'){e.stopPropagation();this.dismissSkillTip();}},{signal,capture:true});
  };
  proto.promotionShortcut=function(card,held,onClose){
    if(!held||held.locked||held.learning.rank.level>=Math.max(...Object.keys(this.data.rankHealth).map(Number))&&held.learning.title.level>=Math.max(...Object.keys(this.data.titleHealing).map(Number)))return;
    const b=document.createElement('button');b.className='promote-commander';b.setAttribute('aria-label','晋升');b.title='晋升';b.append(this.api.uiImage('button_lvlup.png',22,25));
    b.onclick=e=>{e.stopPropagation();this.promotion(held.id,onClose);};card.append(b);
  };
  proto.princesses=function(){
    const l=this.native('form_princess',()=>this.drawRoster());
    for(const [index,p] of this.data.princesses.entries()){
      const d=this.data.definitions.find(d=>d.id===p.definitionId),card=this.card({...d,image:p.unlocked?p.image:p.image.replace('.png','_sketch.png')},null,()=>this.commanderInfo(d,this.data.roster.find(c=>c.definitionId===d.id),()=>this.princesses()));
      card.querySelector('.commander-info').onclick=e=>{e.stopPropagation();this.commanderInfo(d,this.data.roster.find(c=>c.definitionId===d.id),()=>this.princesses());};
      card.classList.toggle('reward-claimed',p.claimed);l.get(index<4?'lbox_princess_up':'lbox_princess_dn').append(card);
      l.action('btn_go_'+(index+1),p.claimed?'已领取':'领取 '+d.name,async()=>{await this.updateAccount({op:'claim',offer:p.id});this.princesses();},p.unlocked&&!p.claimed);
      if(p.claimed)l.text('btn_go_'+(index+1),'已领取');
    }
  };
  proto.itemButton=function(item,quantity,select){
    const b=this.api.button('',select,document.createElement('div'));b.className='native-item';b.setAttribute('aria-label',item?.name||'空物品位');
    b.append(this.api.uiImage(item?.image||'item_blank.png',45));
    if(quantity>1){const n=document.createElement('span');n.className='item-quantity';n.append(this.lettering(quantity,'font_char_2_hd','#ebe6e1'));b.append(n);}
    return b;
  };
  proto.equipment=function(id){
    const held=this.held(id),definition=this.definition(held),l=this.native('form_deployitem',()=>this.reopenCommander(id));
    l.fill('tcmder',this.card(definition,held,()=>{}));
    const list=this.data.roster,index=list.indexOf(held);
    for(const [name,offset] of [['btn_prev',-1],['btn_next',1]])l.action(name,offset<0?'上一位将领':'下一位将领',()=>this.equipment(list[index+offset].id),index+offset>=0&&index+offset<list.length);
    for(const [key,kind] of [['rank_military','rank'],['rank_nobility','class']]){const level=held.learning[kind==='rank'?'rank':'title'].level;l.fill(key,...(level?[this.api.uiImage(kind+'_'+level+'.png',21,24)]:[]));}
    l.text('text_life',held.healthBonus);l.text('text_apply',held.healing);
    let chosen=null,removing=false;
    const choose=(item,remove,b)=>{chosen=item;removing=remove;for(const n of l.body.querySelectorAll('.native-item'))n.setAttribute('aria-pressed',String(n===b));l.text('intitle_desc',item.name);l.text('text_desc',item.description);l.text('btn_equip',this.text(remove?'text_unequip':'btn_equip').replace('text_unequip','卸下'));l.action('btn_equip',remove?'卸下 '+item.name:'装备 '+item.name,async()=>{await this.updateAccount({op:remove?'unequip':'equip',commander:id,item:item.id});this.equipment(id);},!held.locked&&(remove||!item.consumable&&held.items.length<(this.data.equipmentCapacity??Infinity)&&!held.items.includes(item.id)));};
    for(let i=0;i<(this.data.equipmentCapacity??2);i++){
      const item=this.data.items.find(d=>d.id===held.items[i]);let b=this.itemButton(item,0,()=>item&&choose(item,true,b));b.disabled=!item;l.get('lbox_equipitem').append(b);
    }
    for(const item of this.data.items.filter(i=>i.available>0)){
      let b=this.itemButton(item,item.available,()=>choose(item,false,b));l.get('grid_items').append(b);
    }
    l.action('btn_equip',this.text('btn_equip'),()=>{},false);l.text('intitle_desc','');
  };
  proto.promotion=function(id,onClose){
    const held=this.held(id),definition=this.definition(held),l=this.native('form_generalupgrade',onClose||(()=>this.reopenCommander(id)));
    l.fill('tcmder',this.card(definition,held,()=>{}));
    for(const [kind,track,price,effects,prefix] of [['military','rank',held.rankPrice,this.data.rankHealth,'rank_'],['nobility','title',held.titlePrice,this.data.titleHealing,'class_']]){
      const level=held.learning[track].level,next=price===null?level:level+1,path='group_'+kind+'/';
      l.fill(path+'rank_from',...(level?[this.api.uiImage(prefix+level+'.png',21,24)]:[]));l.fill(path+'rank_to',...(next?[this.api.uiImage(prefix+next+'.png',21,24)]:[]));
      l.text(path+'text_lifefrom',effects[level]);l.text(path+'text_lifeto',effects[next]);l.text(path+'btn_'+kind+'full/text_medal',price??'MAX');
      l.action(path+'btn_'+kind+'full',track==='rank'?'晋升军衔':'晋升爵位',async()=>{await this.updateAccount({op:track,commander:id});this.promotion(id,onClose);},!held.locked&&price!==null&&price<=this.data.medals);
    }
    l.text('group_fullrank/btn_allfull/text_medal',held.fullPrice||'MAX');
    l.action('btn_allfull','军衔爵位升至满级',async()=>{await this.updateAccount({op:'full',commander:id});this.promotion(id,onClose);},!held.locked&&held.fullPrice>0&&held.fullPrice<=this.data.medals);
  };
  proto.regroup=async function(id,donorId){
    const receiver=this.held(id),definition=this.definition(receiver),l=this.native('form_regroup',()=>this.reopenCommander(id));
    l.fill('tcmder_source',this.card(definition,receiver,()=>{}));
    l.text('text_notice',this.text('text_regroupnotice'));
    const donors=this.data.roster.filter(c=>c.id!==id&&!c.locked);donorId=donors.some(c=>c.id===donorId)?donorId:donors[0]?.id;
    for(const held of donors){const b=this.card(this.definition(held),held,()=>this.regroup(id,held.id));b.setAttribute('aria-pressed',String(held.id===donorId));l.get('lbox_general').append(b);}
    if(!donorId){l.action('btn_regroup',this.text('btn_regroup'),()=>{},false);return;}
    const donor=this.held(donorId);l.fill('tcmder_target',this.card(this.definition(donor),donor,()=>{}));
    for(let i=0;i<(this.data.equipmentCapacity??2);i++)l.get('lbox_equipitem').append(this.api.uiImage(this.data.items.find(d=>d.id===donor.items[i])?.image||'item_blank.png',45));
    const preview=await this.api.rpc('commander',{op:'preview_inherit',revision:this.data.revision,commander:id,target:donorId});
    if(!l.current())return;
    for(const [key,track,kind] of [['rank_military','rank','rank'],['rank_nobility','title','class']]){const level=preview.learning[track].level;l.fill(key,...(level?[this.api.uiImage(kind+'_'+level+'.png',21,24)]:[]));}
    const grid=l.get('grid_preview');for(const [key,value] of Object.entries(preview.learning.abilities).filter(([key,value])=>value>receiver.learning.abilities[key])){const row=document.createElement('div');row.append(this.api.uiImage((key==='naval'?'upgrade_tag_ship':key==='fort'?'upgrade_tag_fortress':'upgrade_tag_'+key)+'.png',20),this.lettering(value,'font_char_2_hd'));grid.append(row);}
    l.action('btn_regroup',this.text('btn_regroup'),()=>{
      const confirm=this.api.detail('title_notice',320,170,async()=>{await this.updateAccount({op:'inherit',commander:id,target:donorId});this.reopenCommander(id);},()=>this.regroup(id,donorId));confirm.className='regroup-confirm';
      confirm.append(this.lettering(this.text('text_regroup'),'font_text_2_cn_hd','#404040'),this.card(this.definition(donor),donor,()=>{}),this.lettering(this.text('text_regroupnotice'),'font_text_1_cn_hd','#a02320',270));
    },!receiver.locked&&preview.price<=this.data.medals);
  };
  proto.commerceStars=function(layout,level){
    const list=layout.get('lbox_stars');list.replaceChildren();list.setAttribute('aria-label','经商 '+level+' / 5');
    for(let i=0;i<5;i++){const star=this.api.uiImage('star_middle.png',13.5,12.5);star.classList.toggle('commerce-star-empty',i>=level);list.append(star);}
  };
  proto.exchangeQuote=function(layout,key,resource,left,right){
    const quote=document.createElement('span');quote.className='exchange-quote';
    const icons=key.startsWith('buy')?['money',resource]:[resource,'money'];
    quote.append(this.api.uiImage('marker_'+icons[0]+'.png',12,16),this.lettering(left,'font_char_2_hd','#ebe6e1'),this.lettering('=','font_char_2_hd','#ebe6e1'),this.api.uiImage('marker_'+icons[1]+'.png',12,16),this.lettering(right,'font_char_2_hd','#ebe6e1'));
    layout.get('btn_'+key).replaceChildren(quote);
  };
  proto.shop=async function(cell=-1,snapshot){
    if(!this.data)this.data=await this.api.rpc('headquarters');
    const data=snapshot||await this.api.rpc('commerce',{cell}),l=this.native('form_shop',async()=>{this.data=await this.api.rpc('headquarters');this.drawRoster();await this.api.refresh();});
    let chosen,buying;
    const choose=(item,buy,b,offer)=>{
      chosen={item,offer};buying=buy;for(const n of l.body.querySelectorAll('.native-item'))n.setAttribute('aria-pressed',String(n===b));
      l.get('btn_buy').style.width=b.offsetWidth+'px';
      l.text('group_info/text_name',item.name);l.text('text_desc',item.description);l.text('text_price',buy?offer.price:data.sales.find(s=>s.id===item.id)?.price||0);l.text('btn_buy',this.text(buy?'text_buy':'text_sell'));
      const price=buy?offer.price:0;
      const command=l.action('btn_buy',this.text(buy?'text_buy':'text_sell')+' '+item.name,async()=>{
        const next=await this.api.rpc('commerce',{cell,revision:data.revision,op:buying?'buy':'sell',offer:chosen.offer?.id,item:chosen.item.id});this.data=await this.api.rpc('headquarters');this.drawRoster();this.shop(cell,next);
      },buy?offer.available&&data.medals>=price:item.available>0);
      const itemRect=b.getBoundingClientRect(),body=l.body.getBoundingClientRect(),scale=body.width/l.width;
      const itemTop=(itemRect.top-body.top)/scale,itemLeft=(itemRect.left-body.left)/scale;
      const above=b.offsetTop>=command.offsetHeight;
      Object.assign(command.style,{left:itemLeft+'px',top:(above?itemTop-command.offsetHeight:itemTop+b.offsetHeight)+'px'});
      this.api.popup(command);
    };
    for(let slot=0;slot<14;slot++){const offer=data.offers.find(o=>o.slot===slot),item=offer&&data.items.find(i=>i.id===offer.itemId);let b=this.itemButton(item,offer?.remaining,()=>offer&&choose(item,true,b,offer));b.disabled=!offer;if(offer)b.classList.toggle('unavailable-item',!offer.available);l.get('grid_seller').append(b);}
    for(const item of data.items.filter(i=>i.quantity>0)){let b=this.itemButton(item,item.quantity,()=>choose(item,false,b));l.get('grid_buyer').append(b);}
    for(const key of ['grid_seller','grid_buyer']){const grid=l.get(key);while(grid.children.length<14){const b=this.itemButton(null,0,()=>{});b.disabled=true;grid.append(b);}}
    l.text('group_seller/text_name',this.text('name_Seller'),{color:'235,230,225,255'});
    l.text('group_info/text_name','');l.text('text_desc',this.text('text_sellersays'),{color:'235,230,225,255'});l.text('text_price','');l.text('text_discount',data.commerce?'-'+data.commerce*4+'%':'');l.hide('btn_buy');
    const merchant=l.get('group_seller');merchant.setAttribute('aria-label',this.text('name_Seller'));
    for(const b of l.body.querySelectorAll('.native-item:not(:disabled)')){const click=b.onclick;b.onclick=e=>{l.hide('btn_buy',false);click(e);};}
    this.commerceStars(l,data.commerce);
  };
  proto.exchange=async function(cell,snapshot){
    const data=snapshot||await this.api.rpc('exchange',{cell}),l=this.native('form_exchange',()=>this.api.refresh());
    l.text('group_seller/text_name',this.text('name_Seller'),{color:'235,230,225,255'});
    l.text('text_rate','x'+data.rate.toFixed(1),{color:'235,230,225,255'});
    for(const [index,offer] of data.offers.entries())for(const [op,allowed] of [['buy',offer.canBuy],['sell',offer.canSell]]){
      const key=op+'_'+(index+1);l.text('text_'+key+'a',op==='buy'?offer.buyMoney:offer.sellAmount);l.text('text_'+key+'b',op==='buy'?offer.buyAmount:offer.sellMoney);
      this.exchangeQuote(l,key,offer.resource,op==='buy'?offer.buyMoney:offer.sellAmount,op==='buy'?offer.buyAmount:offer.sellMoney);
      l.action('btn_'+key,(op==='buy'?'购买':'出售')+(offer.resource==='industry'?'工业':'粮食')+' '+(op==='buy'?offer.buyAmount:offer.sellAmount),async()=>{const next=await this.api.rpc('exchange',{cell,revision:data.revision,op,offer:offer.id});await this.api.refresh();await this.exchange(cell,next);},allowed);
    }
    if(this.data){const held=this.held(data.commander);if(held)l.fill('tcmder_player',this.card(this.definition(held),held,()=>this.commanderInfo(this.definition(held),held,()=>this.exchange(cell))));}
    this.commerceStars(l,data.commerce);
  };
  proto.tavern=async function(cell){
    this.data=await this.api.rpc('headquarters');const data=await this.api.rpc('commerce',{op:'tavern',cell}),l=this.native('form_recruitgeneral',()=>this.api.refresh());
    const groups=[...l.nodes].filter(([key])=>/^group_gen_\d+$/.test(key));
    l.rows(groups.slice(0,Math.max(1,data.candidates.length)).map(([key])=>key));
    groups.forEach(([key,el],index)=>{
      const candidate=data.candidates[index];if(!candidate){el.hidden=true;return;}
      const d=this.data.definitions.find(d=>d.id===candidate.definitionId),card=this.card(d,null,()=>this.commanderInfo(d,null,()=>this.tavern(cell)));card.querySelector('.commander-info').onclick=e=>{e.stopPropagation();this.commanderInfo(d,null,()=>this.tavern(cell));};l.fill(key+'/tcmder',card);
      card.classList.add('portrait-only');card.classList.toggle('unavailable-candidate',!candidate.available);
      l.text(key+'/text_name',d.name);l.text(key+'/group/text_medal',candidate.medals);l.text(key+'/group/text_money',candidate.cost.money);l.text(key+'/group/text_indus',candidate.cost.industry);
      for(const [name,kind,level] of [['rank_military','rank',d.rank],['rank_nobility','class',d.title]])l.fill(key+'/'+name,...(level?[this.api.uiImage(kind+'_'+level+'.png',21,24)]:[]));
      l.action(key+'/btn_rec_'+(index+1),this.text('btn_recruit')+' '+d.name,async()=>{await this.api.act('hire',{unit:data.unit,offer:candidate.id});await this.tavern(cell);},candidate.available);
      if(data.round<candidate.availableRound)l.text(key+'/btn_rec_'+(index+1),this.text('btn_round_word')+' '+data.round+'/'+candidate.availableRound);
    });
  };
})();
