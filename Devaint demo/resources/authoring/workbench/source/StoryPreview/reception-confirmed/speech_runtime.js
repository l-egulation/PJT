// Keep standalone speech previews usable when no high-resolution overlay is attached.
function drawSceneText(ctx,text,x,y){
 const layer=ctx.highResolutionText;
 if(layer&&layer.enabled)layer.draw(ctx,text,x,y);else ctx.fillText(text,x,y);
}
function coverSceneText(ctx,x,y,w,h){
 const layer=ctx.highResolutionText;if(layer&&layer.enabled)layer.cover(x,y,w,h);
}
// Asset-backed 9-slice speech bubbles. No text is baked into the PNGs.
class HeadSpeech {
 constructor(ctx,body,tail,config,sprites={}){this.ctx=ctx;this.body=body;this.tail=tail;this.config=config;this.skins={};for(const [role,theme] of Object.entries(config.themes||{}))this.skins[role]={...theme,body:sprites[theme.body.replace('.png','')]||body,tail:sprites[theme.tail.replace('.png','')]||tail};this.active=false;this.pages=[];this.index=0;this.elapsed=0;this.visible=0;this.lastBox=null;this.lastLines=[];this.font=`${config.font_px||13}px "Malgun Gothic", sans-serif`;this.nameFont=`600 ${config.name_font_px||15}px "Malgun Gothic", sans-serif`}
 roleFor(page){const role=page.role||this.config.speaker_roles?.[page.speaker]||(page.anchor==='hero'?'hero':'general');return this.config.themes?.[role]?role:'general'}
 wrap(text,maxWidth){
  const c=this.ctx;c.font=this.font;const lines=[];
  for(const paragraph of String(text).replace(/\r/g,'').split('\n')){
   if(!paragraph){lines.push('');continue}let line='';
   for(const token of paragraph.match(/\S+|\s+/gu)||[]){
    if(/^\s+$/u.test(token)){if(line&&!line.endsWith(' '))line+=' ';continue}
    if(c.measureText(line+token).width<=maxWidth){line+=token;continue}
    if(c.measureText(token).width<=maxWidth){if(line.trim())lines.push(line.trimEnd());line=token;continue}
    for(const char of Array.from(token)){if(line&&c.measureText(line+char).width>maxWidth){lines.push(line.trimEnd());line=char}else line+=char}
   }
   if(line)lines.push(line.trimEnd());
  }return lines.length?lines:[''];
 }
 begin(entries){this.pages=[];for(const entry of entries){const lines=this.wrap(entry.text,this.config.max_width-24);for(let i=0;i<lines.length;i+=this.config.max_lines_per_page)this.pages.push({...entry,lines:lines.slice(i,i+this.config.max_lines_per_page)})}this.index=0;this.active=this.pages.length>0;this.elapsed=0;this.visible=0}
 get page(){return this.pages[this.index]}
 close(){this.active=false;this.lastBox=null}
 next(){if(!this.active)return;const total=Array.from(this.page.lines.join('\n')).length;if(this.visible<total){this.visible=total;this.elapsed=total/35;return}if(++this.index>=this.pages.length){this.close();return}this.elapsed=0;this.visible=0}
 update(dt){if(this.active){this.elapsed+=dt;this.visible=Math.min(Array.from(this.page.lines.join('\n')).length,Math.floor(this.elapsed*35))}}
 nineSlice(x,y,w,h,body=this.body){coverSceneText(this.ctx,x,y,w,h);const c=this.ctx,s=body,b=8,xs=[0,b,s.width-b,s.width],ys=[0,b,s.height-b,s.height],dx=[x,x+b,x+w-b,x+w],dy=[y,y+b,y+h-b,y+h];
  for(let row=0;row<3;row++)for(let col=0;col<3;col++){const sx=col===1?Math.floor(s.width/2):xs[col],sy=row===1?Math.floor(s.height/2):ys[row],sw=col===1?1:xs[col+1]-xs[col],sh=row===1?1:ys[row+1]-ys[row];c.drawImage(s,sx,sy,sw,sh,dx[col],dy[row],dx[col+1]-dx[col],dy[row+1]-dy[row])}
 }
 draw(heroX){if(!this.active)return;const c=this.ctx,p=this.page,anchorX=p.anchorX??(p.anchor==='book'?775:heroX),anchorY=p.anchorY??(p.anchor==='book'?342:352),role=this.roleFor(p),skin=this.skins[role]||{body:this.body,tail:this.tail,name:'#e4c77f',text:'#f7f0df'};
  c.save();c.filter='none';c.globalAlpha=1;c.imageSmoothingEnabled=false;c.font=this.font;
  const showName=role!=='hero'&&Boolean(p.speaker);
  const widest=Math.max(...p.lines.map(line=>c.measureText(line).width));c.font=this.nameFont;const nameWidth=showName?c.measureText(p.speaker).width:0;
  const lineHeight=this.config.line_height||19,textBaseline=showName?(this.config.text_baseline||50):(this.config.text_baseline_without_name||24);
  const w=Math.ceil(Math.max(this.config.min_width,Math.min(this.config.max_width,Math.max(widest,nameWidth)+24))),h=textBaseline+(p.lines.length-1)*lineHeight+18;
  const bx=Math.round(Math.max(8,Math.min(952-w,anchorX-w/2))),by=Math.round(Math.max(8,anchorY-h-14)),tailX=Math.round(Math.max(bx+14,Math.min(bx+w-14,anchorX)));
  this.nineSlice(bx,by,w,h,skin.body);c.drawImage(skin.tail,tailX-10,by+h-this.config.tail_overlap_px);
  c.textAlign='left';c.textBaseline='alphabetic';if(showName){c.fillStyle=skin.name;c.font=this.nameFont;drawSceneText(c,p.speaker,bx+12,by+(this.config.name_baseline||24));}
  c.fillStyle=skin.text;c.font=this.font;let remaining=this.visible;this.lastLines=[];
  p.lines.forEach((line,i)=>{const chars=Array.from(line),shown=chars.slice(0,Math.max(0,remaining)).join('');remaining-=chars.length+1;drawSceneText(c,shown,bx+12,by+textBaseline+i*lineHeight);this.lastLines.push(shown)});
  if(this.visible>=Array.from(p.lines.join('\n')).length){c.fillStyle=skin.name;const xx=bx+w-16,yy=by+h-10;c.fillRect(xx,yy,5,1);c.fillRect(xx+1,yy+1,3,1);c.fillRect(xx+2,yy+2,1,1)}
  this.lastBox={x:bx,y:by,width:w,height:h,tailX,anchorX,anchorY,role,nameVisible:showName,nameFontPx:showName?this.config.name_font_px:null,bodyFontPx:this.config.font_px,nameBaseline:showName?by+(this.config.name_baseline||24):null,textBaseline:by+textBaseline,page:this.index+1,pages:this.pages.length};c.restore();
 }
}
