// 界面文字：key → 当前语言的文字。表来自后端 play.language（内容包 client/lang/<code>.json 的合并结果）。
// 查不到就用调用处给的默认文字（简体），所以没翻译的地方不会坏。
(function(root){
  'use strict';
  let table={},current='zh-Hans',available=[];
  const has=(key)=>Object.prototype.hasOwnProperty.call(table,key);
  // ew4t('ui.round','回合  {0}',[3]) → 当前语言的 "Round 3"
  function ew4t(key,fallback,args){
    let text=has(key)?table[key]:(fallback===undefined?key:fallback);
    if(args)text=text.replace(/\{(\d+)\}/g,(match,index)=>args[index]===undefined?match:String(args[index]));
    return text;
  }
  // data-i18n="key" 替换文字；data-i18n-attr="title:key;aria-label:key" 替换属性。
  function apply(scope){
    const host=scope||document;
    for(const element of host.querySelectorAll('[data-i18n]'))if(has(element.dataset.i18n))element.textContent=table[element.dataset.i18n];
    for(const element of host.querySelectorAll('[data-i18n-attr]'))for(const pair of element.dataset.i18nAttr.split(';')){
      const [name,key]=pair.split(':');if(name&&key&&has(key))element.setAttribute(name,table[key]);
    }
  }
  async function load(rpc){
    try{const data=await rpc('language');table=data.strings||{};current=data.language||current;available=data.available||[];}
    catch(_){table={};}
    document.documentElement.lang=current==='zh-Hans'?'zh-CN':current==='zh-Hant'?'zh-TW':current;
    apply();
  }
  async function set(rpc,code){await rpc('language',{op:'set',language:code});}
  root.Ew4I18n={t:ew4t,apply,load,set,get language(){return current;},get strings(){return table;},get available(){return available;}};
  root.ew4t=ew4t;
})(window);
