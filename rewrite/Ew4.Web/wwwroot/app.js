'use strict';
const $ = id => document.getElementById(id);
const canvas = $('map'), ctx = canvas.getContext('2d', {alpha: false});
let catalog, manifest, cityNames={}, state, selected, selection, busy = false, mode = 2;
let conquestPages, conquestPage=0, conquestEntering=false, conquestRender=0;
let originalTextRenderer, menuArt, imageRevision=0;
let startupReady=false,startupRunning=false,startupError='';
let effects,damageNumbers;
let originalTextQueued=false;
const originalTextChanges=new MutationObserver(()=>{
  if(!originalTextRenderer||originalTextQueued)return;originalTextQueued=true;
  requestAnimationFrame(()=>{originalTextQueued=false;originalTextRenderer.paintTree(document.body);});
});
originalTextChanges.observe(document.body,{childList:true,characterData:true,attributes:true,attributeFilter:['hidden'],subtree:true});
let view = {x: 0, y: 0, zoom: 1}, viewport = {w: 0, h: 0, dpr: 1};
const images = new Map(), libraries = new Map();
let errorTimer, aiRunning = false;
let skipAi = false, mapBounds, finishPresentation;
const presentationWaiters = new Set();
const motions = new Map(), facings = new Map(), arrivals = new Map(), combatMotions = new Map();
const deaths = new Map();
const battlefield = new Ew4Battlefield.Renderer({image,sprite,animation,libraries,get imageRevision(){return imageRevision;},get effects(){return effects;},get manifest(){return manifest;},get geographicLanguage(){return settings.geographicLanguage;},cityLabel:(name,cell)=>{
  const translated=manifest.maps[state.map].cityNames?.[cell];
  const text=translated?.name===name?translated[settings.cityLanguage]:Ew4MenuArt.cityName(name,settings.cityLanguage,cityNames);
  return originalTextRenderer.element(text,'font_city_labels_hd','#ffffff');
}});
const menus = new Ew4Menus.Menus({rpc,button,uiImage,labels:labelImage,text:(value,font,color,width,align,options)=>originalTextRenderer?.element(value,font,color,width,align,options)||document.createTextNode(value),detail:detailWindow,start,loadLibrary,image,animation,libraries,notice,main:showMain,
  get manifest(){return manifest;},get catalog(){return catalog;},refreshCatalog:async()=>{catalog=await rpc('catalog');},tutorialUi,popup:popupOpen,load:(slot,mode)=>load(slot,mode),deploy:(unit,commander)=>act('deploy',{unit,commander}),act,
  refresh:async()=>{if(state&&!$('play').hidden){await installState(await rpc('action',{op:'state',revision:state.revision}));}},get menuArt(){return menuArt;},closeDetail});
let chosenScenario, chosenCountry, countryChoices = [], showGrid = false, selectedAt = 0;
let tutorial, tutorialRect;
let settings={music:50,sound:50,speed:2,hd:true,screen:100,eventSize:100,cityLanguage:'en',geographicLanguage:'en'};
try { settings={...settings,...JSON.parse(localStorage.getItem('ew4.settings')||'{}')}; } catch (_) {}
settings.eventSize=Number.isFinite(settings.eventSize)?Math.max(60,Math.min(180,settings.eventSize)):100;
try { showGrid = localStorage.getItem('ew4.showGrid') === 'true'; } catch (_) { /* Storage can be unavailable in restricted WebViews. */ }
function sizeUi() {
  const reserve=startupReady&&!$('startup-status').hidden?Math.ceil($('startup-status').getBoundingClientRect().height)+24:0;
  document.documentElement.style.setProperty('--startup-reserve',reserve+'px');
  const scale = Math.min(innerWidth / 568, Math.max(1,innerHeight-reserve) / 320)*settings.screen/100;
  document.documentElement.style.setProperty('--ui-scale', scale);
  document.documentElement.style.setProperty('--battle-scale', Math.max(.8, scale));
  const baseEventScale = Math.min(1.5, innerWidth*.84/360, innerHeight*.74/264)*settings.screen/100;
  const eventHeight=innerHeight>=innerWidth?Math.min(400,innerHeight*.74/baseEventScale):264;
  // Bound the slider itself so each step still enlarges the popup on small screens.
  const maxEventSize=Math.min(180,Math.floor(Math.min(innerWidth*.96/360,innerHeight*.92/eventHeight)/baseEventScale*100));
  const eventSize=Math.min(settings.eventSize,maxEventSize);
  const eventScale=baseEventScale*eventSize/100;
  document.documentElement.style.setProperty('--event-scale', eventScale);
  document.documentElement.style.setProperty('--event-available-height', eventHeight+'px');
  document.documentElement.style.setProperty('--settings-scale',Math.min(scale,(innerWidth-16)/362,(innerHeight-16)/340));
  $('event-size').max=maxEventSize;$('event-size').value=eventSize;
  $('event-size-value').textContent=eventSize+'%';
  clampView();
}
function labelImage(key, text, host) {
  if(!host&&key.startsWith('country_'))return originalTextRenderer.element(text,'font_text_2_cn_hd','#373737',84,'left',{size:11.5,singleLine:true});
  return host?originalTextRenderer.forElement(host,text):originalTextRenderer.element(text,'font_text_4_cn_hd','#ffffff');
}
function originalLabels() {
  for (const element of document.querySelectorAll('[data-label]')) {
    const text = element.textContent; element.setAttribute('aria-label', text);
    element.replaceChildren(labelImage(element.dataset.label, text, element));
  }
}
function showMain() {
  $('play').hidden = true; $('menu').hidden = false; $('main-menu').hidden = false;
  $('scenario-menu').hidden = true; $('countries').hidden = true;
  $('campaign-screen').hidden=true;$('hq-screen').hidden=true;
  $('tutorial-talk').hidden=true;$('tutorial-focus').hidden=true;
}
function notice(text) { $('notice').textContent = text; $('notice').hidden = false; clearTimeout(errorTimer); errorTimer = setTimeout(() => $('notice').hidden = true, 5500); }
async function rpc(route, args = {}) {
  const response = await fetch('/api/' + route + '?q=' + encodeURIComponent(JSON.stringify(args)), {method: 'POST', cache: 'no-store'});
  if (!response.ok) throw Error('连接失败 (' + response.status + ')');
  const result = await response.json();
  if (!result.ok) throw Error(failure(result.code, result.message));
  return result.data;
}
function failure(code, message) {
  const labels = {'client.player_turn':'请等待己方回合','client.stale':'状态已更新，请重新选择','client.ai_turn':'当前并非电脑回合',
    'action.dialogue_pending':'请先确认当前对白','action.phase':'回合尚未就绪','turn.finished':'本局已经结束',
    'training.unit':'请先选择已任职将领的己方部队','client.no_session':'请先开始游戏',
    'client.undo_move':'该部队目前没有可以撤回的移动：已攻击、原位被占用或相关地块已发生变化'};
  return labels[code] || message;
}
function image(path) {
  if (!images.has(path)) { const img = new Image(); img.addEventListener('load',()=>imageRevision++);img.src = '/content/' + path; images.set(path, img); }
  return images.get(path);
}
async function loadImage(path) {
  const img = image(path);
  if (img.complete && img.naturalWidth) return img;
  await img.decode(); return img;
}
function button(text, action, parent) {
  const result = document.createElement('button'); result.textContent = text;
  result.onclick = async () => {if(result.disabled)return;result.disabled=true;try{await action();await tutorialUi(result.dataset.tutorialId,Number(result.dataset.tutorialRow??-1));}catch(error){notice(error.message);}finally{result.disabled=false;}};
  parent.append(result); return result;
}
function scenarioList() {
  const list=$('scenarios'),render=++conquestRender;list.replaceChildren();list.className='scenario-list';
  $('conquest-pagination').hidden=mode!==2;
  if(mode!==2){
    for(const scenario of catalog.scenarios.filter(s=>s.mode===mode)){
      const row=button(scenario.name,()=>chooseScenario(scenario),list);row.className='scenario-entry';
    }
    return;
  }
  conquestPage=Math.min(conquestPage,conquestPages.pages.length-1);
  const slots=conquestPages.pages[conquestPage].slots;
  for(let index=0;index<6;index++){
    const slot=slots[index],scenario=slot&&catalog.scenarios.find(s=>s.mode===2&&s.id===slot.scenario);
    const row=scenario?button('',()=>chooseScenario(scenario),list):document.createElement('div');
    row.className='scenario-entry';
    row.style.setProperty('--entry-offset',index%2?'568px':'-568px');
    row.style.setProperty('--entry-delay',Math.floor(index/2)*60+'ms');
    if(!scenario){row.classList.add('scenario-placeholder');row.setAttribute('aria-hidden','true');list.append(row);continue;}
    row.setAttribute('aria-label',scenario.name);row.dataset.scenario=scenario.id;
    if(slot.art){const art=document.createElement('img');art.alt='';art.className='scenario-art';art.src='/content/'+slot.art;row.append(art);}
    else row.classList.add('scenario-fallback');
    const label = document.createElement('span'); label.className = 'conquest-caption';
    for (const name of slot.labels||[]) {
      const s=manifest.atlases.image_menu_hd['conquest_text_'+name+'.png'];if(!s)continue;
      const text = document.createElement('img'); text.alt = ''; text.src = '/content/ui/image_menu_hd/conquest_text_' + name + '.png';
      text.width=s.w/2;text.height=s.h/2;label.append(text);
    }
    if(!label.childElementCount)label.append(originalTextRenderer.element(scenario.name,'font_text_2_cn_hd','#ebe6e1'));
    const line = document.createElement('span'); line.className = 'conquest-line'; row.append(label,line);
  }
  const animations=list.getAnimations({subtree:true});conquestEntering=animations.length>0;updateConquestPagination();
  Promise.all(animations.map(a=>a.finished.catch(()=>{}))).then(()=>{
    if(render!==conquestRender)return;conquestEntering=false;updateConquestPagination();
  });
}
function updateConquestPagination(){
  const count=conquestPages.pages.length;
  $('conquest-page-number').replaceChildren(originalTextRenderer.element((conquestPage+1)+' / '+count,'font_char_2_hd','#ebe6e1',42,'center'));
  $('conquest-page-number').setAttribute('aria-label','第'+(conquestPage+1)+'页，共'+count+'页');
  $('conquest-prev').disabled=conquestEntering||conquestPage===0;
  $('conquest-next').disabled=conquestEntering||conquestPage===count-1;
}
function turnConquestPage(offset){
  const next=conquestPage+offset;
  if(mode!==2||conquestEntering||next<0||next>=conquestPages.pages.length)return;
  conquestPage=next;scenarioList();
}
async function chooseScenario(scenario) {
  if (busy) return;
  if (scenario.mode !== 2) return start(scenario.id, null);
  busy = true;
  try {
    const countries = await rpc('countries', {scenario:scenario.id});
    countryChoices = countries;
    await loadLibrary('strategicmap');
    chosenScenario = scenario.id; chosenCountry = undefined; $('confirm-country').disabled = true;
    $('countries').hidden = false; $('country-list').replaceChildren(); $('country-list').scrollTop = 0;
    for (const country of countries) {
      const row = button('', () => {
        chosenCountry = country.id;
        for (const entry of $('country-list').children) entry.setAttribute('aria-pressed', String(entry === row));
        $('confirm-country').disabled = false;
        drawStrategicMap();
      }, $('country-list'));
      row.className = 'country-entry'; row.setAttribute('aria-label', country.name); row.setAttribute('aria-pressed', 'false');
      const flag = document.createElement('img'); flag.alt = ''; flag.className = 'country-flag'; flag.src = '/content/ui/image_flag_hd/' + country.id + '1.png';
      row.append(flag, labelImage('country_' + country.id, country.name));
    }
    $('country-list').firstElementChild?.click();
    menus.slideInSidebar($('country-list'));
  } finally { busy = false; }
}
async function loadLibrary(name) {
  if (libraries.has(name)) return libraries.get(name);
  const data = await (await fetch('/content/animations/' + name + '.json')).json();
  data.byName = new Map(data.items.map(item => [item.name, item]));
  data.sprites = new Map(data.atlas.sprites.map(sprite => [sprite.name, sprite]));
  data.texture = await loadImage('images/' + data.atlas.textures[0].name.replace(/\.(?:pkm|pvr)$/i, '.png'));
  libraries.set(name, data); return data;
}
async function installState(next, center = false) {
  center=center||!!(state&&(state.map!==next.map||state.scenario!==next.scenario||state.width!==next.width||state.height!==next.height));
  if(center){effects?.clear();damageNumbers?.clear();deaths.clear();}
  const keepUnit=center?null:selected?.id,keepCell=center?null:selection?.cell;
  const used = [...new Set(next.units.map(u => manifest.profiles[u.profile]?.library).filter(Boolean))];
  await Promise.all([...used, 'choose', 'maptext', 'anim_fire', 'anim_upgrade', 'anim_smoke'].map(loadLibrary));
  if(!center&&next.combat&&!(aiRunning&&skipAi)){
    // A combined AI command must arrive at its firing position while both
    // combatants still exist in the presentation scene.
    const moves=next.combat.movements?.length?next.combat.movements:next.movements||[];
    if(moves.length){
      state={...state,units:state.units.map(u=>{const move=moves.find(m=>m.unit===u.id);return move?{...u,cell:move.path.at(-1)}:u;})};
      await playMovements(moves);
    }
    await playCombat(next.combat,new Set(state.units.filter(u=>!next.units.some(v=>v.id===u.id)).map(u=>u.id)));
  }
  if(!center&&!(aiRunning&&skipAi)){
    const destroyed=(next.notices||[]).filter(n=>n.kind==='unit.destroyed').map(n=>n.subject);
    const victims=state.units.filter(u=>!next.units.some(v=>v.id===u.id)&&(next.combat||destroyed.includes(u.id)));
    if(victims.length){for(const unit of victims)beginDeath(unit.id);await presentationDelay(Math.max(0,...victims.map(u=>{const d=deaths.get(u.id);return d.started+d.duration-performance.now();})));}
  }
  deaths.clear();
  state = next; selected = undefined; selection = undefined; $('selection').hidden = true;
  if (center || !mapBounds) mapBounds = Ew4Battlefield.cameraBounds(state, manifest.maps[state.map]);
  $('menu').hidden = true; $('play').hidden = false; $('pause').hidden = true;
  if (center) centerPlayer(); else clampView();
  refreshHud();
  motions.clear(); arrivals.clear();
  const now=performance.now(), stepMs=movementStepMs();
  if (!center && !(aiRunning && skipAi)) {
    if(!next.combat)for(const move of next.movements || [])if(move.path.length>1){motions.set(move.unit,{path:move.path,started:now,stepMs});const a=point(move.path.at(-2)),b=point(move.path.at(-1));facings.set(move.unit,b.x<a.x?-1:1);}
    for(const id of next.recruited || []){arrivals.set(id,now);const unit=state.units.find(u=>u.id===id),p=unit&&point(unit.cell);if(p)effects?.effect('effect_recruit.xml',p.x,p.y,{speed:Math.sqrt(settings.speed)});}
    const duration=Math.max(next.recruited?.length?Ew4Battlefield.recruitDuration(Math.sqrt(settings.speed)):0,...[...motions.values()].map(m=>(m.path.length-1)*stepMs),...([...arrivals.keys()].map(id=>{
      const unit=state.units.find(u=>u.id===id),p=manifest.profiles[unit?.profile],lib=libraries.get(p?.library),item=lib?.byName.get(p?.motions.ready);
      return (item?.duration||1)/(lib?.fps||24)*1000;
    })));
    if(duration)await presentationDelay(duration);
  }
  motions.clear();arrivals.clear();
  if(!center&&state.activeFaction===state.playerFaction&&!state.dialogue){const unit=state.units.find(u=>u.id===keepUnit);const cell=unit?.cell??(keepUnit?null:keepCell);if(cell!=null)await selectCell(cell,true);}
}
function movementStepMs(){return 255/Math.sqrt(settings.speed);}
function presentationDelay(duration){return new Promise(resolve=>{const timer=setTimeout(done,duration);function done(){clearTimeout(timer);presentationWaiters.delete(done);if(!presentationWaiters.size)finishPresentation=null;resolve();}presentationWaiters.add(done);finishPresentation=()=>{for(const finish of [...presentationWaiters])finish();};});}
function beginDeath(id){if(!deaths.has(id))deaths.set(id,{started:performance.now(),duration:560/Math.sqrt(settings.speed)});}
async function playMovements(moves){
  const started=performance.now(),stepMs=movementStepMs();
  for(const move of moves)if(move.path.length>1)motions.set(move.unit,{path:move.path,started,stepMs});
  await presentationDelay(Math.max(0,...moves.map(m=>(m.path.length-1)*stepMs)));motions.clear();
}
async function playCombat(combat,destroyed=new Set()){
  // The backend has already resolved both hits. Animation timing cannot change the outcome.
  const pairs=[[combat.attacker,combat.defender,combat.damage,combat.critical]];
  if(combat.counterattack)pairs.push([combat.defender,combat.attacker,combat.counterDamage,combat.counterCritical]);
  if(aiRunning&&skipAi)return;
  const speed=Math.sqrt(settings.speed),plans=[];
  for(const [id,targetId,damage,critical] of pairs){
    const unit=state.units.find(u=>u.id===id),target=state.units.find(u=>u.id===targetId);
    if(!unit||!target)continue;
    const profile=manifest.profiles[unit.profile],lib=libraries.get(profile?.library);if(!lib)continue;
    const facing=point(target.cell).x<point(unit.cell).x?-1:1;
    const segments=[],firing=effects?.motion(unit.profile,'attack',facing),timeline=(firing?.effect||'')+(facing<0?' left':' right'),firstShot=effects?.firstEvent(timeline)??0,lastShot=effects?.lastEvent(timeline);let duration=0;
    for(const action of ['attack','reload','finish']){const motion=effects?.motion(unit.profile,action,facing),name=motion?.name||profile.motions[action],item=lib.byName.get(name);if(!item)continue;const rate=speed*Number(motion?.speed||1);let length=item.duration/lib.fps*1000/rate;if(action==='attack'&&lastShot!=null)length=Math.min(length,(lastShot+.2+(unit.category==='artillery'?.2:0))*1000/speed);segments.push({name,start:duration,end:duration+length,speed:rate,directional:motion?.dir==='left'||motion?.dir==='right'});duration+=length;}
    if(!duration)continue;
    const hitAt=lastShot==null?segments[0].end:Math.max(segments[0].end,(lastShot+.2)*1000/speed);
    plans.push({id,targetId,damage,critical,unit,target,profile,facing,timeline,segments,duration,hitAt,firstShot:firstShot*1000/speed});
  }
  // Source windups differ (artillery 1.7s, fortress 0s). Align the actual
  // salvo, then keep both attack poses until the shared backend hit is shown.
  const firstShot=Math.max(0,...plans.map(p=>p.firstShot));
  for(const plan of plans)plan.delay=firstShot-plan.firstShot;
  const impactAt=Math.max(0,...plans.map(p=>p.delay+p.hitAt));
  const started=performance.now();
  await Promise.all(plans.map(async plan=>{
    const {id,targetId,damage,critical,unit,target,profile,facing,timeline,segments,delay}=plan;
    const hold=impactAt-delay-segments[0].end;
    segments[0].end+=hold;
    for(const segment of segments.slice(1)){segment.start+=hold;segment.end+=hold;}
    const duration=delay+plan.duration+hold;
    facings.set(id,facing);
    const p=point(unit.cell),q=point(target.cell);
    effects?.timeline(timeline,p.x+facing*profile.x,p.y+profile.y,{speed,scale:profile.scale,started:started+delay});
    combatMotions.set(id,{started,delay,speed,segments});
    await presentationDelay(Math.max(0,started+impactAt-performance.now()));
    if(!(aiRunning&&skipAi)){
      damageNumbers?.add(damage,critical,q.x,q.y,speed);
      if(destroyed.has(targetId))beginDeath(targetId);
      if(id===combat.attacker&&combat.splashTarget){const splash=state.units.find(u=>u.id===combat.splashTarget);if(splash){const hit=point(splash.cell);damageNumbers?.add(combat.splashDamage,false,hit.x,hit.y,speed);if(destroyed.has(splash.id))beginDeath(splash.id);}}
    }
    if(!(aiRunning&&skipAi)&&damage>0){const weapon=profile.stats.weapon,rocket=profile.kind==='Rocket',name=rocket?'rocketstrike':Ew4Effects.impact(weapon,target.category,damage,state.cells[target.cell].sea,facing);effects?.timeline(name,q.x+(rocket?30*facing:0),q.y,{speed});}
    if(!(aiRunning&&skipAi))await presentationDelay(Math.max(0,started+duration-performance.now()));
    combatMotions.delete(id);
  }));
}
async function start(scenario, country) {
  busy = true; $('countries').querySelectorAll('button').forEach(b => b.disabled = true);
  try { await withLoading(async()=>{tutorial=null; await installState(await rpc('start', {scenario, country}), true); await beginTutorial();}); }
  finally { busy = false; $('countries').querySelectorAll('button').forEach(b => b.disabled = false); if(state)refreshHud(); }
}
async function withLoading(action){
  const started=performance.now(),tips=Object.entries(manifest.strings).filter(([key])=>/^text_tips_\d+$/.test(key));
  const tip=tips[Math.floor(Math.random()*tips.length)]?.[1]||'';
  $('loading-tip').replaceChildren(originalTextRenderer.element(tip,'font_text_2_cn_hd','#ebe6e1',527));
  $('loading-word').replaceChildren(originalTextRenderer.element('LOADING...','font_char_3_hd','#ebe6e1'));
  $('loading-screen').hidden=false;
  try{await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));await action();await new Promise(resolve=>setTimeout(resolve,Math.max(0,700-(performance.now()-started))));}
  finally{$('loading-screen').hidden=true;}
}
function refreshHud() {
  const active = state.factions.find(f => f.id === state.activeFaction);
  const player = state.factions.find(f => f.id === state.playerFaction);
  $('turn').replaceChildren(originalTextRenderer.element('回合  ' + state.round,'font_text_2_cn_hd','#404040'));
  const country = player.flag;
  $('player-flag').src = '/content/ui/image_flag_hd/' + country + '1.png';
  $('resources').replaceChildren();
  for (const [label, icon, value] of [['金钱','money', player.resources.money], ['工业','industry',player.resources.industry], ['粮食','food',player.resources.food]]) {
    const span = document.createElement('span'); span.className='resource-item';span.setAttribute('aria-label', label + ' ' + value);
    const marker = document.createElement('img'); marker.src = '/content/ui/image_ui_hd/marker_' + icon + '.png'; marker.alt = '';
    span.append(marker, originalTextRenderer.element(value,'font_char_2_hd','#ffffff')); $('resources').append(span);
  }
  $('end-turn').disabled = busy || !active.human || state.phase !== 'Active' || !!state.dialogue;
  $('undo-move').hidden = !active.human;
  $('undo-move').disabled = busy || !(selected ? selection?.undo : state.undoUnit);
  $('skip-ai').hidden = active.human || state.phase === 'Finished' || !!state.dialogue;
  $('skip-ai').disabled = skipAi;
  $('ai-action').hidden = active.human || state.phase === 'Finished' || !!state.dialogue;
  $('ai-flag').src = '/content/ui/image_flag_hd/' + active.flag + '1.png';
  $('dialogue').hidden = !state.dialogue || !!state.scenarioPopup;
  $('scenario-event').hidden = !state.scenarioPopup;
  if(state.scenarioPopup){
    const popup=state.scenarioPopup;
    const content=$('scenario-event-content'), choices=$('scenario-event-choices'), event=$('scenario-event');
    const sameEvent=event.dataset.event===popup.id;
    const contentScroll=sameEvent?content.scrollTop:0, choicesScroll=sameEvent?choices.scrollTop:0;
    event.dataset.event=popup.id;
    content.classList.toggle('image-only',!!popup.image&&!popup.text);
    $('scenario-event-title').textContent=popup.title;
    const picture=$('scenario-event-image');
    picture.hidden=!popup.image;
    if(popup.image){
      const source='/content/extensions/'+popup.image.slice('rewrite/'.length).split('/').map(encodeURIComponent).join('/');
      if(picture.getAttribute('src')!==source)picture.src=source;
      picture.alt=popup.imageAlt||popup.title;
    }else picture.removeAttribute('src');
    const eventText=$('scenario-event-text');
    eventText.textContent=Ew4OriginalUI.availableText(popup.text);
    void Ew4OriginalUI.prepareText(popup.text).then(()=>{if(state?.scenarioPopup===popup)eventText.textContent=Ew4OriginalUI.availableText(popup.text);});
    $('scenario-event-text').hidden=!popup.text;
    choices.replaceChildren();
    for(const choice of popup.choices){
      const b=button(choice.text,()=>act('event_choice',{offer:popup.id,target:choice.id}),$('scenario-event-choices'));
      b.dataset.choice=choice.id;b.disabled=busy||!choice.available;
    }
    content.scrollTop=contentScroll;choices.scrollTop=choicesScroll;
  }else delete $('scenario-event').dataset.event;
  if(state.dialogue&&!state.scenarioPopup){
    const talk=state.dialoguePresentation;
    $('dialogue-ok').classList.toggle('speaker-right',talk?.left===false);
    $('dialogue-text').replaceChildren(originalTextRenderer.element(state.dialogue,'font_text_1_cn_hd','#404040',216));
    $('dialogue-speaker').replaceChildren(originalTextRenderer.element(talk?.name||'','font_text_2_cn_hd','#646464',216,'center'));
    $('dialogue-portrait').replaceWith(Object.assign(talk?.portrait?menus.portrait(talk.portrait):document.createElement('img'),{id:'dialogue-portrait',alt:talk?.name||'',hidden:!talk?.portrait}));
    $('dialogue-text').scrollTop=0;
  }
  $('status').textContent = state.phase === 'Finished' ? (state.outcome === 'Victory' ? '胜利' : '本局结束') : '';
}
async function act(op, extra = {}) {
  if (busy || !state) return;
  busy = true; refreshHud();
  try {
    const next = await rpc('action', {op, revision:state.revision, unit:selected?.id, ...extra});
    await installState(next);
  } finally { busy = false; refreshHud(); }
  if (op === 'end' || op === 'dialogue' || op === 'event_choice') await runAi();
}
async function runAi() {
  if (aiRunning) return;
  aiRunning = true; skipAi = false;
  try {
    while (state && !state.dialogue && state.phase !== 'Finished' && !state.factions.find(f => f.id === state.activeFaction).human) {
      busy = true; refreshHud();
      await installState(await rpc('action', {op:'ai', revision:state.revision}));
      if(!skipAi)await new Promise(resolve => setTimeout(resolve, 180/settings.speed));
    }
  } catch (error) { notice(error.message); }
  finally { busy = false; aiRunning = false; skipAi = false; refreshHud(); }
}
async function selectCell(cell,restore=false) {
  if ((!restore&&busy) || !state || cell < 0 || cell >= state.cells.length || !state.cells[cell].active) return;
  if(!restore&&tutorial?.wait?.name==='wait area'&&Number(tutorial.wait.id)!==cell)return;
  const target = state.units.find(u => u.cell === cell);
  if (!restore && selected && selection && state.activeFaction === state.playerFaction && !state.dialogue) {
    if (target && selection.targets?.includes(cell))
      return act('attack', {target:target.id}).then(()=>tutorial?.notify({type:'area',cell}));
    if (!target && selection.range.includes(cell)) return act('move', {cell}).then(()=>tutorial?.notify({type:'area',cell}));
  }
  selection = await rpc('action', {op:'select', cell, revision:state.revision});
  selected = target;if(!restore)selectedAt = performance.now(); const site = state.cells[cell];
  $('selection').hidden = false;
  const own = target?.owner === state.playerFaction;
  $('actions').replaceChildren(); $('terrain-info').replaceChildren();
  const terrainRows=Ew4Battlefield.terrainRows(site);
  $('terrain-info').hidden=!terrainRows.length;
  const incomeRows=terrainRows.filter(r=>!r.defense),defenseRows=terrainRows.filter(r=>r.defense);
  $('terrain-info').classList.toggle('with-defense',defenseRows.length>0&&incomeRows.length>0);
  for(const rows of [incomeRows,defenseRows]){
    if(!rows.length)continue;
    const column=document.createElement('div');column.className='terrain-column';
    for(const row of rows){
      const entry=document.createElement('div');entry.className='terrain-row';
      entry.append(uiImage(row.icon,18),originalTextRenderer.element(row.text,'font_char_2_hd','#eeeeee'));column.append(entry);
    }
    $('terrain-info').append(column);
  }
  if(selection.deploy) toolButton('general','任命将领',()=>menus.deployGeneral(target.id,state.revision));
  if(selection.upgrade) toolButton('buildupgrade','升级建筑',()=>confirmUnitAction('upgrade','升级建筑',{cell}));
  if(selection.installations.length) toolButton('builddefense','防御',()=>openDefense(cell));
  if(own&&target.commanderId) toolButton('training','培训',()=>confirmUnitAction('train','培训'));
  if(selection.offers.length) toolButton(({city:'city',industry:'factory',stable:'stable',port:'dock'})[site.buildingKind]||'buildfortress',site.buildingKind?'雇佣':'建造要塞',()=>openRecruitment(cell));
  if(selection.transport) toolButton('buyship','购买运输',()=>confirmUnitAction('transport','购买运输'));
  if(selection.tavern)toolButton('bar','酒馆',()=>menus.tavern(cell));
  if(selection.shop)toolButton('shop','商店',()=>menus.shop(cell));
  if(selection.market)toolButton('trade','市场',()=>menus.exchange(cell));
  if(target)toolButton('info','部队信息',()=>openUnitInfo(target));
  if(own&&selection.disband)toolButton('disband','解散部队',()=>confirmDisband(target,selection.disband));
  for(const entry of selection.skills||[]){
    const skill=entry.skill,b=toolButton('info',skill.name,()=>openSkillAction(target,entry));
    b.classList.add('active-skill-button');b.disabled=!entry.available;
    b.title=skill.name+(entry.remainingCooldown?' ('+entry.remainingCooldown+')':'');
    b.replaceChildren(Ew4SkillUI.icon(skill,23,uiImage));
  }
  $('actions').hidden=!$('actions').childElementCount;
  refreshHud();
  if(!restore)await tutorial?.notify({type:'area',cell});
}
function uiImage(name,width,height=width){
  const atlas=['image_recruit_hd','image_recruit_hd2','image_recruit_hd3','image_ui_hd','image_menu_hd','image_item_hd','image_newgame_hd','princesses_hd','princesses_sketch_hd','commanders1_hd','commanders2_hd','image_ui'].find(a=>manifest.atlases[a]?.[name]);
  const img=document.createElement('img');img.alt='';img.width=width;img.height=height;
  if(atlas)img.src='/content/ui/'+atlas+'/'+name;else console.warn('Missing original UI image',name);return img;
}
function toolButton(icon,title,action){const b=button('',action,$('actions'));b.title=title;b.setAttribute('aria-label',title);
  if(icon==='disband'){const img=new Image(33,33);img.alt='';img.src='/disband.png';b.append(img);}
  else b.append(uiImage('button_'+(icon==='general'?'generals':icon)+'.png',33));
  b.dataset.tutorialId=({general:'btn_general',city:'btn_city',factory:'btn_factory',stable:'btn_stable',dock:'btn_dock',buildfortress:'btn_fortress',builddefense:'btn_defense',buyship:'btn_ship',training:'btn_training',buildupgrade:'btn_upgrade'})[icon]||'';return b;}
let detailReturn,detailRevision=0;
function confirmDisband(unit,refund){
  const body=detailWindow('解散部队',260,146,()=>act('disband',{unit:unit.id}));body.className='disband-confirmation';
  const prompt=document.createElement('p');prompt.append(originalTextRenderer.element('确认解散该部队？','font_text_2_cn_hd','#404040'));body.append(prompt);
  const values=document.createElement('div');values.className='disband-refund';values.setAttribute('aria-label',`返还金钱 ${refund.money}，工业 ${refund.industry}，粮食 ${refund.food}`);
  for(const [key,icon] of [['money','money'],['industry','industry'],['food','food']]){const value=document.createElement('span');value.append(uiImage('marker_'+icon+'.png',18),originalTextRenderer.element(refund[key],'font_char_2_hd','#404040'));values.append(value);}
  body.append(values);const explanation=document.createElement('p');explanation.append(originalTextRenderer.element('返还招募价的50% × 剩余血量比例','font_text_1_cn_hd','#404040',240));body.append(explanation);
}
function popupOpen(element){element.classList.remove('popup-opening');void element.offsetWidth;element.classList.add('popup-opening');}
function closeDetail(){const back=detailReturn;detailReturn=null;menus.dismissSkillTip();$('detail-modal').hidden=true;if(back)back();}
function detailWindow(title,width,height,confirm,onClose){
  const generation=++detailRevision;
  menus.dismissSkillTip();detailReturn=onClose;
  const dialog=$('detail-modal').firstElementChild;dialog.style.width=width+'px';dialog.style.height=height+'px';
  dialog.classList.remove('native-dialog');
  const titleText=({headquarters_label:'指挥部',tutorial_label:'教程',country_label:'选择国家',recruit_label:'招募',info_label:'信息',defense_label:'防御'})[title]||manifest.strings[title]||title;
  $('detail-title').replaceChildren(labelImage(title,titleText,$('detail-title')));$('detail-content').replaceChildren();$('detail-content').nativeLayout=null;$('detail-content').className='';$('detail-modal').hidden=false;
  $('detail-confirm').dataset.tutorialId='winbtn_ok';$('detail-confirm').hidden=!confirm;$('detail-confirm').onclick=async()=>{try{await confirm();if(generation===detailRevision)$('detail-modal').hidden=true;await tutorialUi($('detail-confirm').dataset.tutorialId);}catch(e){notice(e.message);}};
  popupOpen(dialog);return $('detail-content');
}
const formationColors=['#646464','#326478','#40803b'];
function tintedUi(name,color,width,height){
  const source=uiImage(name,width,height),result=document.createElement('canvas');result.width=width*2;result.height=height*2;result.style.width=width+'px';result.style.height=height+'px';
  function paint(){const draw=result.getContext('2d');draw.clearRect(0,0,result.width,result.height);draw.globalCompositeOperation='source-over';draw.drawImage(source,0,0,result.width,result.height);draw.globalCompositeOperation='multiply';draw.fillStyle=color;draw.fillRect(0,0,result.width,result.height);draw.globalCompositeOperation='destination-in';draw.drawImage(source,0,0,result.width,result.height);draw.globalCompositeOperation='source-over';}
  if(source.complete&&source.naturalWidth)paint();else source.onload=paint;return result;
}
function unitCard(profile,cost){
  const p=manifest.profiles[profile];const card=document.createElement('div');card.className='unit-card';
  card.style.setProperty('--formation-color',formationColors[p.grade]||formationColors[0]);
  card.append(uiImage(p.card,72,54));const marker=uiImage(p.cardMarker,19,19);marker.className='card-marker';card.append(marker);
  const strip=document.createElement('span');strip.className='cost-strip';
  const balance=state?.factions.find(f=>f.id===state.playerFaction)?.resources;
  for(const [kind,icon] of [['money','money'],['industry','industry']]){strip.append(uiImage('marker_'+icon+'.png',10));const value=document.createElement('span');value.textContent=cost?.[kind]??0;if(balance&&balance[kind]<(cost?.[kind]??0))value.style.color='#ff3200';strip.append(value);}
  card.append(strip);const frame=tintedUi('build_frame.png',formationColors[p.grade]||formationColors[0],72,65);frame.className='recruit-frame';card.append(frame);return card;
}
function abilityGrid(profile,unit){
  const p=manifest.profiles[profile],s=p.stats,grid=document.createElement('div');grid.className='ability-grid';
  const health=Number(s.strength)===-999?'一次性':unit?.health??s.strength;
  for(const [icon,value] of [['attack',s.minatk+'-'+s.maxatk],['soldiernumber',p.grade+1],['hp',health],['food',s.consumption],['range',s.maxatkrange],['move',unit?.remainingMovement??s.movement]]){
    const marker=document.createElement('span');marker.className='ability-icon';marker.append(uiImage('infomarker_'+icon+'.png',21));const number=document.createElement('span');number.textContent=value;grid.append(marker,number);
    if(icon==='soldiernumber'){number.replaceChildren();number.className='formation-count';for(let i=0;i<3;i++)number.append(uiImage('infomarker_soldiernumber'+(i<value?'':'_empty')+'.png',14,18));}
  }return grid;
}
function descriptionBox(parent,name,text){const desc=document.createElement('section');desc.className='unit-description';const h=document.createElement('h3');h.textContent=name;const p=document.createElement('p');p.textContent=text;desc.append(h,p);parent.append(desc);}
function openUnitInfo(unit){
  const p=manifest.profiles[unit.profile],entries=[...(unit.traits||[]),...(unit.skills||[]),...(unit.statuses||[])],body=detailWindow('info_label',330,entries.length?300:187);
  const card=unitCard(unit.profile);card.classList.add('info-unit-card');body.append(card);
  const grid=abilityGrid(unit.profile,unit);grid.classList.add('info-abilities');body.append(grid);
  descriptionBox(body,p.name,p.description);
  if(entries.length){
    body.classList.add('has-extension-skills');const list=document.createElement('div');list.className='unit-skill-list';
    for(const skill of entries){const row=button('',()=>openSkillDescription(skill),list);row.className='unit-skill-row';row.title=skill.description;
      row.append(Ew4SkillUI.icon(skill,19,uiImage),originalTextRenderer.element(skill.name,'font_text_2_cn_hd','#404040'));}
    Ew4SkillUI.layout(list,catalog.extensionLayouts?.unit,entries);body.append(list);
  }
}
function openSkillDescription(skill){
  const body=detailWindow('info_label',300,180);
  const box=document.createElement('div');box.className='skill-action-description';
  box.append(originalTextRenderer.element(skill.name,'font_text_2_cn_hd','#404040'),originalTextRenderer.element(skill.description,'font_text_2_cn_hd','#404040',270));body.append(box);
  detailReturn=()=>{if(selected)openUnitInfo(selected);};
}
function openSkillAction(unit,entry){
  const skill=entry.skill,isCell=skill.target==='cell',choices=isCell?(entry.cells||[]):entry.targets;let target=choices[0];
  const body=detailWindow('info_label',300,220,()=>act('skill',{offer:skill.instanceId,...(isCell?{cell:target}:{target})}));
  $('detail-confirm').disabled=!entry.available||target===undefined;
  const box=document.createElement('div');box.className='skill-action-description';
  box.append(originalTextRenderer.element(skill.name,'font_text_2_cn_hd','#404040'),originalTextRenderer.element(skill.description,'font_text_2_cn_hd','#404040',270));
  const cost=document.createElement('div');cost.className='skill-action-cost';
  for(const [key,icon] of [['money','money'],['industry','industry'],['food','food']]){const span=document.createElement('span');span.append(uiImage('marker_'+icon+'.png',16),originalTextRenderer.element(skill.cost[key]||0,'font_char_2_hd','#404040'));cost.append(span);}box.append(cost);
  const targets=document.createElement('div');targets.className='skill-targets';
  for(const id of choices){const t=isCell?null:state.units.find(u=>u.id===id),cell=isCell?id:t.cell,b=button('',()=>{target=id;for(const n of targets.children)n.setAttribute('aria-pressed',String(n===b));},targets);
    b.setAttribute('aria-pressed',String(id===target));b.append(originalTextRenderer.element((isCell?'':manifest.profiles[t.profile].name+' ')+'('+(cell%state.width)+','+Math.floor(cell/state.width)+')','font_text_2_cn_hd','#404040'));}
  box.append(targets);body.append(box);
}
function confirmUnitAction(op,title,extra={}){
  const body=detailWindow('info_label',250,150,()=>act(op,extra));body.className='action-confirmation';
  const name=document.createElement('p');name.textContent=title;body.append(name);
  if(selected)body.append(unitCard(selected.profile));$('detail-confirm').dataset.tutorialId='btn_done';
}
function openRecruitment(cell){
  const offers=selection.offers;let chosen=offers[0];
  if(!chosen)return;
  const body=detailWindow('recruit_label',441,185,()=>act('recruit',{cell,offer:chosen.id}));
  const row=document.createElement('div');row.className='recruit-cards';body.append(row);const details=document.createElement('div');details.className='recruit-description';body.append(details);
  function choose(offer,b){chosen=offer;for(const c of row.children)c.setAttribute('aria-pressed',String(c===b));details.replaceChildren(abilityGrid(offer.profile));descriptionBox(details,manifest.profiles[offer.profile].name,manifest.profiles[offer.profile].description);const funds=state.factions.find(f=>f.id===state.playerFaction).resources;$('detail-confirm').disabled=['money','industry','food'].some(key=>(offer.cost[key]||0)>funds[key]);}
  const groups=new Map();for(const offer of offers){const key=manifest.profiles[offer.profile].unitIndex;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(offer);}
  [...groups.values()].forEach((group,index)=>{group.sort((a,b)=>manifest.profiles[a.profile].grade-manifest.profiles[b.profile].grade);let grade=0;
    function render(b,animate){
      const outgoing=animate?b.querySelector('.unit-card'):null;
      b.replaceChildren(unitCard(group[grade].profile,group[grade].cost));
      for(let layer=1;layer<group.length;layer++){const next=manifest.profiles[group[(grade+layer)%group.length].profile];const side=tintedUi('build_side.png',formationColors[next.grade],72-layer*2,3);side.className='formation-layer';side.style.left=layer*2+'px';side.style.top=-layer*3+'px';b.append(side);}
      if(outgoing){
        // Only the old portrait layer lifts off; costs and formation markers update in place.
        outgoing.querySelectorAll('.cost-strip,.card-marker').forEach(n=>n.remove());
        outgoing.classList.add('formation-outgoing');outgoing.setAttribute('aria-hidden','true');b.append(outgoing);
        outgoing.animate([{transform:'translateY(0)',opacity:1},{transform:'translateY(-39px)',opacity:0}],{duration:240,easing:'ease-out'}).finished.then(()=>outgoing.remove());
      }
    }
    const b=button('',()=>{const cycle=chosen===group[grade]&&group.length>1;if(cycle)grade=(grade+1)%group.length;chosen=group[grade];render(b,cycle);choose(chosen,b);},row);
    b.dataset.tutorialId='lbox_unit';b.dataset.tutorialRow=index;b.setAttribute('aria-label',manifest.profiles[group[0].profile].name);render(b,false);});
  while(row.children.length<6){const empty=document.createElement('div');empty.className='empty-recruit-card';row.append(empty);}
  choose(chosen,row.firstElementChild);
}
function openDefense(){
  let chosen=selection.installations[0];const body=detailWindow('defense_label',300,185,()=>act('installation',{offer:chosen}));const row=document.createElement('div');row.className='defense-cards';body.append(row);
  for(const [index,id] of selection.installations.entries()){const b=button('',()=>{chosen=id;for(const c of row.children)c.setAttribute('aria-pressed',String(c===b));},row);b.dataset.tutorialId='lbox_defense';b.dataset.tutorialRow=index;b.setAttribute('aria-label',id);b.append(uiImage('build_'+({trench:'moat',fence:'fences',bunker:'bunker'})[id]+'.png',94,64));}
}
function centerPlayer() {
  const unit = state.units.find(u => u.owner === state.playerFaction);
  const cell = unit?.cell ?? state.cells.findIndex(c => c.owner === state.playerFaction);
  const p = point(Math.max(0, cell)); view.x = p.x; view.y = p.y; view.zoom = Math.min(innerWidth / 568, innerHeight / 320); clampView();
}
function clampView() {
  if (!state || !manifest) return;
  const bounds = mapBounds || manifest.maps[state.map];
  view = Ew4Camera.constrain(view, {w:canvas.clientWidth || innerWidth, h:canvas.clientHeight || innerHeight}, bounds);
}
function point(cell) { return Ew4Battlefield.point(cell,state.width); }
function screenToWorld(x, y) { return {x:(x - viewport.w/2)/view.zoom + view.x, y:(y - viewport.h/2)/view.zoom + view.y}; }
function cellAt(x, y) {
  const p = screenToWorld(x, y); const approximateRow = Math.round((p.y+18) / 54); let best = -1, distance = Infinity;
  for (let r = approximateRow - 1; r <= approximateRow + 1; r++) {
    if (r < 0 || r >= state.height) continue;
    const approximateCol = Math.round((p.x - (r%2)*32)/64);
    for (let col = approximateCol - 1; col <= approximateCol + 1; col++) {
      if (col < 0 || col >= state.width) continue;
      const cell = r*state.width + col, q = point(cell), d = (q.x-p.x)**2+(q.y-p.y)**2;
      if (d < distance) { best=cell; distance=d; }
    }
  }
  return best;
}
function hex(x, y) { ctx.beginPath(); for(const [dx,dy] of [[0,-36],[32,-18],[32,18],[0,36],[-32,18],[-32,-18]]) { if(dx===0&&dy===-36)ctx.moveTo(x+dx,y+dy);else ctx.lineTo(x+dx,y+dy); } ctx.closePath(); }
function sprite(atlas, name, x, y, scale=1, draw=ctx) {
  let ratio=1;if(manifest.atlases[atlas+'_hd']?.[name]){atlas+='_hd';ratio=2;}
  const s=manifest.atlases[atlas]?.[name], texture=image('images/'+atlas+'.png');
  if(s && texture.complete && texture.naturalWidth) draw.drawImage(texture,s.x,s.y,s.w,s.h,x-s.refx*scale/ratio,y-s.refy*scale/ratio,s.w*scale/ratio,s.h*scale/ratio);
}
const unresolved = new Set();
function animation(lib, item, tick, depth=0, draw=ctx, frameFor=null) {
  if(!item || depth>20)return;
  draw.save();
  if(depth===0){const ratio=lib.pixelRatio||1;draw.scale(1/ratio,1/ratio);}
  if(item.kind) {
    const s=lib.sprites.get(item.name+'.png');
    if(s)draw.drawImage(lib.texture,+s.x,+s.y,+s.w,+s.h,item.pivot[0],item.pivot[1],+s.w,+s.h);
    else if(!unresolved.has(item.name)){unresolved.add(item.name);console.warn('Unresolved original sprite',item.name);}
  } else {
    const frame=frameFor?.(item) ?? Math.floor(tick)%item.duration;
    for(const layer of [...item.layers].reverse()) {
      let entry=layer[0];for(const candidate of layer){if(candidate.key<=frame)entry=candidate;else break;}
      for(const [elementId, childId] of entry.indices) {
        const e=lib.elements[elementId], child=lib.items[childId];if(!child)continue;
        const localTick=e.playback_mode===2?e.start_frame:e.playback_mode===1?Math.min(e.start_frame+frame-entry.key, child.duration-1):e.start_frame+frame-entry.key;
        draw.save();draw.transform(...e.transform);draw.globalAlpha*=e.alpha;animation(lib,child,localTick,depth+1,draw,frameFor);draw.restore();
      }
    }
  }
  draw.restore();
}
function drawStrategicMap(time=performance.now()) {
  const surface=$('strategic-map'), draw=surface.getContext('2d'), lib=libraries.get('strategicmap');
  if(!lib || !chosenScenario)return;
  const definition=manifest.strategic[chosenScenario]||{}, scale=Number(definition.scale||1)*.92;
  const cx=Number(definition.centerx||410),cy=Number(definition.centery||288);
  const rect=surface.getBoundingClientRect(),ratio=settings.hd?Math.min(devicePixelRatio||1,3):1;
  const width=Math.max(1,Math.round(rect.width*ratio)),height=Math.max(1,Math.round(rect.height*ratio));
  if(surface.width!==width||surface.height!==height){surface.width=width;surface.height=height;}
  draw.setTransform(surface.width/568,0,0,surface.height/320,0,0);draw.clearRect(0,0,568,320);draw.save();
  menuArt.drawStrategic(draw,definition,scale,205,160);
  draw.translate(205,160);draw.scale(scale,scale);
  const strategic=lib.byName.get(definition.name||('conquest '+chosenScenario.slice(8)));if(strategic)animation(lib,strategic,time/180,0,draw);draw.restore();
}
function render(time) {
  requestAnimationFrame(render);
  if(manifest)menus.draw(time);
  if(tutorial&&!$('play').hidden)positionTutorial();
  if(tutorial?.wait?.name==='wait action'&&!busy&&!aiRunning)tutorial.notify({type:'action'}).catch(e=>notice(e.message));
  if(!$('countries').hidden&&!$('menu').hidden)drawStrategicMap(time);
  if(!state || $('play').hidden)return;
  const dpr=settings.hd?Math.min(window.devicePixelRatio||1,3):1, w=canvas.clientWidth,h=canvas.clientHeight;
  if(canvas.width!==Math.round(w*dpr)||canvas.height!==Math.round(h*dpr)){canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);}
  viewport={w,h,dpr};clampView();ctx.setTransform(dpr,0,0,dpr,0,0);ctx.fillStyle='#688e91';ctx.fillRect(0,0,w,h);
  ctx.translate(w/2,h/2);ctx.scale(view.zoom,view.zoom);ctx.translate(-view.x,-view.y);
  const min=screenToWorld(0,0),max=screenToWorld(w,h);
  battlefield.frame({draw:ctx,state,map:manifest.maps[state.map],visibleRect:{left:min.x,top:min.y,width:max.x-min.x,height:max.y-min.y},
    zoom:view.zoom,showGrid,selection,selected,selectedAt,motions,facings,arrivals,arrivalSpeed:Math.sqrt(settings.speed),deaths,combatMotions},time);
  effects?.frame(ctx,time,'foreground',battlefield.visibleRect);
  damageNumbers?.frame(ctx,time,battlefield.visibleRect);
}
async function tutorialUi(id,row=-1){if(id)await tutorial?.notify({type:'ui',id,row});}
async function beginTutorial(checkpoint){
  $('tutorial-talk').hidden=true;$('tutorial-focus').hidden=true;tutorialRect=null;
  const source=manifest.tutorials?.[state.scenario];if(!source){tutorial=null;return;}
  tutorial=new Ew4Tutorial.Runner(source,async command=>{
    const cell=Number(command.id);
    switch(command.name){
      case 'rand seed':break;
      case 'show text':$('tutorial-text').replaceChildren(originalTextRenderer.element(manifest.strings['desc_tutorials_word_'+command.id]||'','font_text_1_cn_hd','#404040',216));$('tutorial-speaker').replaceChildren(originalTextRenderer.element(manifest.strings.name_Napoleon||'拿破仑','font_text_2_cn_hd','#646464'));$('tutorial-portrait').src=menus.portrait('Napoleon.png').src;$('tutorial-talk').hidden=false;break;
      case 'hide text':$('tutorial-talk').hidden=true;break;
      case 'moveto area':{const p=point(cell);view.x=p.x;view.y=p.y;clampView();break;}
      case 'sel area':await selectCell(cell);break;
      case 'unsel area':selected=undefined;selection=undefined;$('selection').hidden=true;break;
      case 'draw rect':case 'draw ui rect':tutorialRect=command;positionTutorial();break;
      case 'clear rect':tutorialRect=null;$('tutorial-focus').hidden=true;break;
      case 'exit':tutorialRect=null;$('tutorial-talk').hidden=true;$('tutorial-focus').hidden=true;showMain();break;
      default:throw Error('Unknown tutorial command '+command.name);
    }
  });
  await tutorial.begin(checkpoint);
}
function positionTutorial(){
  if(!tutorialRect||!state){$('tutorial-focus').hidden=true;return;}
  const command=tutorialRect;let rect;
  if(command.name==='draw rect'){
    const p=point(Number(command.id));rect={left:(p.x-view.x+Number(command.x||0))*view.zoom+viewport.w/2,top:(p.y-view.y+Number(command.y||0))*view.zoom+viewport.h/2,width:Number(command.w)*view.zoom,height:Number(command.h)*view.zoom};
  }else{
    const controls={group_res:'#resources',group_incom:'#terrain-info',btn_next:'#end-turn',btn_undo:'#undo-move',winbtn_close:'#close-detail',winbtn_ok:'#detail-confirm',btn_done:'#detail-confirm',winbtn_back:'#hq-back',grid_general:'#hq-roster',lbox_unit:'.recruit-cards>button',lbox_defense:'.defense-cards>button'};
    const selector=controls[command.string]||'[data-tutorial-id="'+command.string+'"]';const candidates=[...document.querySelectorAll(selector)].filter(e=>e.getClientRects().length);
    const target=candidates[command.string==='grid_general'?0:command.row===undefined?0:Number(command.row)];
    if(target){rect=target.getBoundingClientRect();
      if(command.string==='grid_general'&&command.row!==undefined)rect={left:rect.left,top:rect.top+Number(command.row)*rect.height/2,width:rect.width,height:rect.height/2};
    }
  }
  $('tutorial-focus').hidden=!rect;if(!rect)return;
  Object.assign($('tutorial-focus').style,{left:rect.left+'px',top:rect.top+'px',width:Math.max(1,rect.width)+'px',height:Math.max(1,rect.height)+'px'});
}
let tutorialTouchTime=-Infinity;
document.addEventListener('pointerup',event=>{
  if(!tutorial||$('play').hidden||!$('pause').hidden||tutorial.wait?.name!=='wait touch')return;
  tutorialTouchTime=performance.now();event.preventDefault();event.stopPropagation();tutorial.notify({type:'touch'}).catch(e=>notice(e.message));
  pointers.clear();dragged=true;
},true);
document.addEventListener('click',event=>{if(performance.now()-tutorialTouchTime<350){event.preventDefault();event.stopPropagation();}},true);
let pointers=new Map(),dragged=false,lastDistance=0;
canvas.addEventListener('pointerdown',e=>{canvas.setPointerCapture(e.pointerId);pointers.set(e.pointerId,{x:e.clientX,y:e.clientY,startX:e.clientX,startY:e.clientY});if(pointers.size===1)dragged=false;else{dragged=true;lastDistance=0;}});
canvas.addEventListener('pointermove',e=>{
  const last=pointers.get(e.pointerId);if(!last)return;
  const dx=e.clientX-last.x,dy=e.clientY-last.y;pointers.set(e.pointerId,{...last,x:e.clientX,y:e.clientY});
  if(pointers.size===1){view.x-=dx/view.zoom;view.y-=dy/view.zoom;clampView();if(Math.hypot(e.clientX-last.startX,e.clientY-last.startY)>4)dragged=true;}
  else{const[a,b]=[...pointers.values()],distance=Math.hypot(a.x-b.x,a.y-b.y);if(lastDistance)zoom(distance/lastDistance,(a.x+b.x)/2,(a.y+b.y)/2);lastDistance=distance;}
});
canvas.addEventListener('pointerup',e=>{pointers.delete(e.pointerId);if(!dragged&&pointers.size===0)selectCell(cellAt(e.clientX,e.clientY)).catch(error=>notice(error.message));});
canvas.addEventListener('pointercancel',e=>{pointers.delete(e.pointerId);dragged=true;});
function zoom(factor,x=viewport.w/2,y=viewport.h/2){const before=screenToWorld(x,y);view.zoom*=factor;clampView();const after=screenToWorld(x,y);view.x+=before.x-after.x;view.y+=before.y-after.y;clampView();}
canvas.addEventListener('wheel',e=>{e.preventDefault();zoom(Math.exp(-e.deltaY*.001),e.clientX,e.clientY);},{passive:false});
document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=async()=>{if(!startupReady)return;mode=Number(b.dataset.mode);if(mode===4){menus.tutorialMenu();return;}$('main-menu').hidden=true;$('scenario-menu').hidden=false;$('countries').hidden=true;$('conquest-pagination').hidden=mode!==2;$('scenario-menu').classList.toggle('campaign-mode',mode===1);menuArt.apply(mode);$('mode-title').replaceChildren(labelImage(b.dataset.label,b.getAttribute('aria-label')));if(!await menus.open(mode))scenarioList();});
$('conquest-prev').onclick=()=>turnConquestPage(-1);
$('conquest-next').onclick=()=>turnConquestPage(1);
$('cancel-country').onclick=()=>$('countries').hidden=true;
$('confirm-country').onclick=()=>{if(chosenScenario&&chosenCountry&&!busy)start(chosenScenario,chosenCountry).catch(e=>notice(e.message));};
$('menu-back').onclick=showMain;
$('headquarters').onclick=()=>{if(startupReady)menus.headquarters().catch(e=>notice(e.message));};
const languageControls=[['city-language','cityLanguage','城市名称'],['geographic-language','geographicLanguage','地理名称']];
for(const [id,key,title] of languageControls){
  const row=document.createElement('div');row.className='setting-row language-row '+id;
  const caption=document.createElement('span');caption.textContent=title;row.append(caption);
  const choice=button('',()=>{settings[key]=settings[key]==='zh'?'en':'zh';syncLanguages();
    if(key==='cityLanguage')battlefield.cityLabels.clear();else battlefield.mapLabels.clear();persistSettings();},row);
  choice.id=id;choice.title=title+'语言';$('settings-content').append(row);
}
function syncLanguages(){for(const [id,key,title] of languageControls){const choice=$(id),text=settings[key]==='zh'?'中文':'English';
  choice.setAttribute('aria-label',title+'：'+text);choice.dataset.language=settings[key];
  if(originalTextRenderer)choice.replaceChildren(originalTextRenderer.element(text,'font_text_2_cn_hd','#404040'));
  else choice.textContent=text;
}}
function openSettings(){for(const [id,key] of [['music-volume','music'],['sound-volume','sound'],['game-speed','speed'],['screen-size','screen']])$(id).value=settings[key];syncLanguages();$('hd-mode').checked=settings.hd;$('show-grid').checked=showGrid;$('settings').hidden=false;}
$('main-settings').onclick=()=>{if(startupReady)openSettings();};$('pause-settings').onclick=openSettings;
$('close-settings').onclick=()=>$('settings').hidden=true;
$('confirm-settings').onclick=()=>$('settings').hidden=true;
for(const [id,key] of [['music-volume','music'],['sound-volume','sound'],['game-speed','speed'],['screen-size','screen'],['event-size','eventSize']])$(id).oninput=e=>{settings[key]=Number(e.target.value);persistSettings();sizeUi();};
$('hd-mode').onchange=e=>{settings.hd=e.target.checked;persistSettings();};
function persistSettings(){try{localStorage.setItem('ew4.settings',JSON.stringify(settings));}catch(_){} }
$('close-detail').onclick=async()=>{closeDetail();await tutorialUi('winbtn_close');};
$('show-grid').onchange=e=>{showGrid=e.target.checked;try{localStorage.setItem('ew4.showGrid',String(showGrid));}catch(_){notice('无法保存设定');}};
$('menu-button').onclick=()=>$('pause').hidden=false;$('resume').onclick=()=>$('pause').hidden=true;
$('end-turn').onclick=()=>act('end').catch(e=>notice(e.message));
$('undo-move').onclick=()=>act('undo',{unit:selected?.id||state.undoUnit}).then(()=>tutorialUi('btn_undo')).catch(e=>notice(e.message));
$('skip-ai').onclick=()=>{skipAi=true;motions.clear();arrivals.clear();deaths.clear();effects?.clear();damageNumbers?.clear();finishPresentation?.();refreshHud();};
$('dialogue-ok').onclick=()=>act('dialogue').catch(e=>notice(e.message));
$('save').onclick=()=>menus.saveMenu(true,catalog.scenarios.find(s=>s.id===state.scenario).mode,tutorial?.checkpoint()).catch(e=>notice(e.message));
async function load(slot=0,saveMode=mode){if(busy)return;busy=true;try{await withLoading(async()=>{tutorial=null;await installState(await rpc('load',{slot,mode:saveMode}),true);$('detail-modal').hidden=true;await beginTutorial(state.tutorialCheckpoint);});}catch(e){notice(e.message);}finally{busy=false;if(state){refreshHud();await runAi();}}}
$('continue').onclick=()=>menus.saveMenu(false,mode).catch(e=>notice(e.message));
$('restart').onclick=()=>{if(state&&!busy)start(state.scenario,state.factions.find(f=>f.id===state.playerFaction).country).catch(e=>notice(e.message));};
$('return-menu').onclick=()=>{if(busy){notice('请等待当前行动完成');return;}showMain();};
window.ew4Back=()=>{
  const visible=id=>!$(id).hidden;
  const modal=document.querySelector('dialog[open]');
  if(modal){modal.close();return 'handled';}
  for(const [panel,close] of [['settings','close-settings'],['detail-modal','close-detail'],['mod-manager','mod-close']]){
    if(visible(panel)){$(close).click();return 'handled';}
  }
  if(visible('loading-screen'))return 'handled';
  if(visible('hq-screen')){$('hq-back').click();return 'handled';}
  if(visible('play')){$(visible('pause')?'resume':'menu-button').click();return 'handled';}
  if(visible('campaign-screen')){$('campaign-back').click();return 'handled';}
  if(visible('countries')){$('cancel-country').click();return 'handled';}
  const preview=document.querySelector('.chapter-preview');
  if(preview){preview.remove();return 'handled';}
  if(visible('scenario-menu')){$('menu-back').click();return 'handled';}
  return visible('main-menu')?'exit':'handled';
};
window.addEventListener('resize',sizeUi);sizeUi();
function startupStatus(){
  const failures=Ew4OriginalUI.fontStatus.failures;
  $('startup-status').hidden=startupReady&&!failures.length&&!startupRunning;
  $('startup-message').textContent=startupRunning?'正在载入…':startupError||
    (failures.length?'字体载入失败，已使用内置备用字体：\n'+failures.map(face=>face.file+'：'+face.message).join('\n'):'');
  $('startup-retry').hidden=startupRunning||!startupError&&!failures.length;
  sizeUi();
}
async function startupJson(url){
  const response=await fetch(url,{cache:'no-store'});
  if(!response.ok)throw Error('资源载入失败：'+url+' ('+response.status+')');
  return response.json();
}
async function initializeClient(){
  if(startupRunning)return;
  startupRunning=true;startupError='';startupStatus();
  document.querySelectorAll('.main-entry').forEach(button=>button.disabled=true);$('continue').disabled=true;
  try{
    const [c,m,names,fx,pages]=await Promise.all([rpc('catalog'),startupJson('/content/manifest.json'),startupJson('city-names.json'),startupJson('/content/effects.json'),startupJson('conquest-pages.json')]);
    const text=new Ew4OriginalUI.TextRenderer(m);await text.prepare();
    const art=new Ew4MenuArt.MenuArt({manifest:m,image});await art.prepare();
    catalog=c;manifest=m;cityNames=names;conquestPages=pages;menuArt=art;
    effects=new Ew4Effects.Effects({image,sprite,animation,libraries,get manifest(){return manifest;},volume:()=>settings.sound},fx);
    originalTextRenderer=text;damageNumbers=new Ew4Effects.DamageNumbers(text);originalLabels();showMain();
    startupReady=true;
    document.querySelectorAll('.main-entry').forEach(button=>button.disabled=false);$('continue').disabled=false;
  }catch(error){startupError=error.message||String(error);}
  finally{startupRunning=false;startupStatus();}
}
$('startup-retry').onclick=async()=>{
  if(startupRunning)return;
  startupRunning=true;startupError='';startupStatus();
  try{await Ew4OriginalUI.retryFonts();}
  catch(error){startupError=error.message||String(error);}
  finally{startupRunning=false;startupStatus();}
  if(!startupReady&&!startupError){for(const [key,img] of images)if(img.complete&&!img.naturalWidth)images.delete(key);await initializeClient();}
};
window.addEventListener('ew4fontstatus',startupStatus);
window.addEventListener('ew4fontsloaded',()=>{battlefield.mapLabels.clear();});
void initializeClient();
requestAnimationFrame(render);
