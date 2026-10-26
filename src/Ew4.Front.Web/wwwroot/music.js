'use strict';
// 背景音乐：进入战役/征服的局后循环播放，离局停止。曲目由 start/load 响应的 audio.bgm 决定。
// 战斗音效不在这里（effects.js 按战斗时间轴与动画同步播放，不循环）。
(function(root){
  function create(volume){
    let audio=null,track=null,retry=null;
    const level=()=>Math.max(0,Math.min(1,Number(volume())/100||0));
    function stop(){
      track=null;
      if(retry){removeEventListener('pointerdown',retry,true);retry=null;}
      if(audio){audio.pause();audio.src='';audio=null;}
    }
    function attempt(){
      if(!audio)return;
      audio.play().catch(()=>{
        // 浏览器要求先有用户操作；下一次点击再试。
        if(retry||!audio)return;
        retry=()=>{removeEventListener('pointerdown',retry,true);retry=null;attempt();};
        addEventListener('pointerdown',retry,true);
      });
    }
    function play(bgm){
      if(!bgm?.track){stop();return;}
      if(audio&&track===bgm.track){audio.loop=bgm.loop!==false;refresh();return;}
      stop();
      track=bgm.track;
      audio=new Audio('/content/'+bgm.track);
      audio.loop=bgm.loop!==false;
      audio.volume=level();
      attempt();
    }
    function refresh(){
      if(!audio)return;
      audio.volume=level();
      if(audio.volume>0&&audio.paused)attempt();
    }
    return {play,stop,refresh,get track(){return track;}};
  }
  root.Ew4Music={create};
})(globalThis);
