'use strict';
(function(root){
  class TextRenderer {
    constructor(manifest){this.fonts=manifest.fonts;}
    async prepare(){
      await document.fonts.load('32px "EW4 Extended"');
      this.measure=document.createElement('canvas').getContext('2d');
    }
    layout(text,font,width=Infinity,options={}){
      // Font roles determine size; imported bitmap ink, outlines and metrics are never used.
      const heights={font_text_1_cn_hd:24,font_text_2_cn_hd:24,font_text_3_cn_hd:26,font_text_4_cn_hd:32,font_char_1_hd:18,font_char_2_hd:24,font_char_3_hd:28,font_char_4_hd:42,font_ascii:16,font_city:13,font_city_hd:26,font_city_labels_hd:26};
      let height=options.size?options.size*2:heights[font]||24;
      const lines=[{glyphs:[],width:0}],limit=Number.isFinite(width)?Math.max(1,width*2-8):Infinity,extended=true;
      let value=String(text).replace(/\\n/g,'\n');
      const setFont=()=>{this.measure.font=height+'px "EW4 Extended"';this.measure.fontKerning='none';};
      setFont();
      if(options.singleLine){
        value=value.replace(/[\r\n]+/g,' ');
        const natural=[...value].reduce((sum,char)=>sum+this.measure.measureText(char).width,0);
        const heightLimit=options.maxHeight?Math.max(1,options.maxHeight*2-10)/height:1;
        height*=Math.min(1,natural?limit/natural:1,heightLimit);setFont();
      }
      for(const char of value){
        if(char==='\r')continue;
        if(char==='\n'){lines.push({glyphs:[],width:0});continue;}
        const advance=this.measure.measureText(char).width;
        let line=lines.at(-1);if(!options.singleLine&&line.width&&line.width+advance>limit){line={glyphs:[],width:0};lines.push(line);}
        line.glyphs.push({char,x:line.width});line.width+=advance;
      }
      const natural=Math.max(1,...lines.map(l=>l.width));
      return {lines,height,extended,width:Number.isFinite(limit)?options.shrinkWidth?Math.min(limit,natural):limit:natural};
    }
    element(text,font='font_text_2_cn_hd',color='#404040',width,align='left',options={}){
      const span=document.createElement('span');span.className='original-text';span.setAttribute('aria-label',String(text));
      const canvas=document.createElement('canvas');canvas.setAttribute('aria-hidden','true');span.append(canvas);
      const layout=this.layout(text,font,width,options),padding=4,outlined=font.startsWith('font_city');
      span.dataset.font=font;span.dataset.typeface=layout.extended?'EW4 Extended':font;
      span.dataset.color=outlined?'#ffffff':color;span.dataset.size=String(layout.height/2);
      canvas.width=Math.ceil(layout.width+padding*2);canvas.height=Math.ceil(layout.lines.length*(layout.height+2)+padding*2);
      canvas.style.width=canvas.width/2+'px';canvas.style.height=canvas.height/2+'px';
      const redraw=()=>{
        const draw=canvas.getContext('2d');draw.clearRect(0,0,canvas.width,canvas.height);
        for(const [row,line] of layout.lines.entries()){
          const offset=align==='center'?(layout.width-line.width)/2:align==='right'?layout.width-line.width:0;
          for(const {char,x} of line.glyphs){
            const y=row*(layout.height+2)+padding;
            draw.font=layout.height+'px "EW4 Extended"';draw.fontKerning='none';draw.textBaseline='top';
            if(outlined){draw.strokeStyle='#000000';draw.lineWidth=2;draw.lineJoin='round';draw.strokeText(char,padding+offset+x,y);}
            draw.fillStyle=outlined?'#ffffff':color||'#404040';draw.fillText(char,padding+offset+x,y);
          }
        }
      };
      redraw();return span;
    }
    replace(element,text,font,color,width,align){element.replaceChildren(this.element(text,font,color,width,align));}
    forElement(element,text){
      const style=getComputedStyle(element),number=/^[\d.,%+\-/ ]+$/.test(text);
      const singleLine=!!element.closest('button,h1,h2,.commander-name,.native-caption')||element.hasAttribute('data-label');
      const paragraph=!!element.closest('p,#notice,.play-notice');
      const px=value=>parseFloat(value)||0;
      let width=element.clientWidth||px(style.width);
      width-=px(style.paddingLeft)+px(style.paddingRight);
      if(singleLine){
        // Reserve icon and sibling space before fitting text into a fixed control.
        const siblings=[...element.children].filter(child=>!child.matches('.original-text')&&getComputedStyle(child).position!=='absolute');
        for(const child of siblings){const css=getComputedStyle(child);width-=child.offsetWidth+px(css.marginLeft)+px(css.marginRight);}
        width-=siblings.length*px(style.columnGap);
      }
      const font=number?'font_char_2_hd':element.closest('h1,h2')?'font_text_4_cn_hd':'font_text_2_cn_hd';
      const bounded=(singleLine||paragraph)&&width>8?width:undefined;
      return this.element(text,font,style.color,bounded,style.textAlign,{size:px(style.fontSize)||12,singleLine,shrinkWidth:singleLine});
    }
    paintTree(rootElement){
      const walker=document.createTreeWalker(rootElement,NodeFilter.SHOW_TEXT),nodes=[];
      while(walker.nextNode()){const node=walker.currentNode;if(!node.textContent.trim()||node.parentElement.closest('.original-text,script,style,canvas,select,option,textarea,[hidden]')||!node.parentElement.getClientRects().length)continue;nodes.push(node);}
      for(const node of nodes)node.replaceWith(this.forElement(node.parentElement,node.textContent));
    }
  }
  root.Ew4OriginalUI={TextRenderer};
  if(typeof module!=='undefined')module.exports=root.Ew4OriginalUI;
})(globalThis);
