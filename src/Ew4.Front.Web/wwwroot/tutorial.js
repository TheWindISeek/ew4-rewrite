'use strict';
(function(root){
  const supported=new Set(['rand seed','show text','hide text','draw rect','draw ui rect','clear rect','moveto area','sel area','unsel area','wait area','wait ui','wait touch','wait action','exit']);
  class Runner {
    constructor(source,effect){
      if(!source||!Array.isArray(source.commands)||source.commands.some(c=>!supported.has(c.name)))throw Error('Unsupported tutorial script');
      this.source=source;this.effect=effect;this.cursor=0;this.visual={};this.finished=false;this.working=false;
    }
    checkpoint(){return {sha256:this.source.sha256,cursor:this.cursor,visual:{...this.visual},finished:this.finished};}
    async begin(saved){
      if(saved&&saved.sha256===this.source.sha256&&Number.isInteger(saved.cursor)&&saved.cursor>=0&&(saved.cursor<this.source.commands.length||saved.finished&&saved.cursor===this.source.commands.length)){
        this.cursor=saved.cursor;this.finished=!!saved.finished;this.visual={...saved.visual};
        if(!this.finished)for(const command of Object.values(this.visual))if(command)await this.effect(command);
      }
      await this.advance();
    }
    get wait(){const command=this.source.commands[this.cursor];return !this.finished&&command?.name.startsWith('wait ')?command:null;}
    accepts(event){const wait=this.wait;if(!wait)return false;
      return wait.name==='wait touch'&&event.type==='touch'
        ||wait.name==='wait area'&&event.type==='area'&&Number(wait.id)===event.cell
        ||wait.name==='wait action'&&event.type==='action'
        ||wait.name==='wait ui'&&event.type==='ui'&&wait.string===event.id&&(wait.row===undefined||Number(wait.row)===event.row);
    }
    async notify(event){if(this.working||!this.accepts(event))return false;this.cursor++;await this.advance();return true;}
    async advance(){
      if(this.finished||this.working)return;this.working=true;
      try{
        while(this.cursor<this.source.commands.length){const command=this.source.commands[this.cursor];if(command.name.startsWith('wait '))break;
          if(command.name==='show text')this.visual.text=command;
          if(command.name==='hide text')delete this.visual.text;
          if(command.name==='draw rect'||command.name==='draw ui rect')this.visual.rect=command;
          if(command.name==='clear rect')delete this.visual.rect;
          if(command.name==='moveto area')this.visual.focus=command;
          await this.effect(command);this.cursor++;
          if(command.name==='exit'){this.finished=true;break;}
        }
        if(this.cursor>=this.source.commands.length)this.finished=true;
      }finally{this.working=false;}
    }
  }
  root.Ew4Tutorial={Runner,supported};
  if(typeof module!=='undefined')module.exports=root.Ew4Tutorial;
})(globalThis);
