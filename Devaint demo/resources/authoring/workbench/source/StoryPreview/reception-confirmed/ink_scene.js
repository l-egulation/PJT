// All positions and flame poses use the scene's native pixel grid and paused clock.
class LibraryInkEffects {
 constructor(ctx) {
  this.ctx=ctx;
  this.wisps=__BLUE_WISP_FRAMES__;
  this.runes=[
   ['00100','01010','10001','01010','00100','01110','00100'],
   ['10001','01010','00100','01110','00100','00100','01010'],
   ['00100','01110','10001','00100','10001','01110','00100'],
   ['10000','11000','10100','10010','10100','11000','10000'],
   ['00100','10101','01110','00100','01110','10101','00100'],
   ['01100','10010','00100','01000','00100','10010','01100'],
   ['00100','01010','01010','11111','01010','01010','00100'],
   ['10001','11011','01010','00100','01010','11011','10001'],
  ].map(rows=>rows.join(''));
  this.flames=[
   ['00100','00100','00110','01110','01110','11111','11211','12221','01210'],
   ['00010','00110','00100','01100','01110','11110','11211','12221','01210'],
   ['00000','00100','01100','01100','01110','11111','11211','12221','01210'],
   ['01000','01100','00100','00110','01110','01111','11211','12221','01210'],
   ['00100','01100','01100','00100','01110','11110','11211','12221','01210'],
   ['00000','00010','00110','00110','01110','11111','11211','12221','01210'],
  ];
 }
 drawInk(time,bright=false) {
  const c=this.ctx,tick=Math.floor(time*7.5);
  c.save();c.filter='none';
  // Small written fragments dissolve and rise through the ink; architecture occludes them.
  for(let column=0;column<14;column++)for(let row=0;row<6;row++) {
   const phase=column*1.73+row*2.31;
   const xx=34+column*67+Math.round(Math.sin(time*.72+phase)*2);
   const yy=((row*83+column*29-Math.floor(tick/3))%500+500)%500-20;
   const pulse=.5+.5*Math.sin(time*1.3+phase);
   const glyph=this.runes[(column*3+row)%this.runes.length];
   c.globalAlpha=bright?.08+.23*pulse:.16+.48*pulse;c.fillStyle=bright?(row%3===0?'#5576b1':'#7487c5'):(row%3===0?'#bed7ff':'#8bbcff');
   for(let p=0;p<35;p++)if(glyph[p]==='1')c.fillRect(xx+p%5,yy+Math.floor(p/5),1,1);
   // Occasional residual ink, detached from the writing as it dissipates.
   if(pulse>.66){c.globalAlpha=(pulse-.66)*.5;c.fillRect(xx+1,yy+8+tick%2,1,2);}
  }
  c.restore();
 }
 drawWisp(o,time) {
  const c=this.ctx,frames=this.wisps[o.wispSize],index=(Math.floor(time*7.5)+Math.round(o.phase*3))%frames.length;
  const rows=frames[index],pulse=.5+.5*Math.sin(time*1.8+o.phase);
  const cx=Math.floor(o.w/2),cy=Math.floor(o.h*.65),radius=(o.w+6)*(1.0+.45*pulse);
  c.save();c.filter='none';
  const glow=c.createRadialGradient(cx,cy,0,cx,cy,radius);
  glow.addColorStop(0,'rgba(72,181,255,'+(.45+.15*pulse)+')');
  glow.addColorStop(.4,'rgba(71,156,255,.18)');glow.addColorStop(1,'rgba(71,156,255,0)');
  c.fillStyle=glow;c.fillRect(cx-radius,cy-radius,radius*2,radius*2);
  rows.forEach((line,y)=>Array.from(line).forEach((p,x)=>{
   if(p==='0')return;
   c.fillStyle=p==='2'?'#edffff':y<rows.length/2?'#86e8ff':'#46bcff';
   c.fillRect(x,y,1,1);
  }));
  c.restore();
 }
 drawFlame(o,time) {
  const c=this.ctx,index=(Math.floor(time*7.5)+Math.round(o.phase*3))%this.flames.length;
  c.save();c.translate(0,-(o.flameCropTop||0));c.globalAlpha=1;c.filter='none';
  // The original 13x32 lamp glass is x3..9 / y17..27; keep the gold housing intact.
  c.fillStyle='#28599c';c.fillRect(3,17,7,11);
  c.fillStyle='#427bb8';c.fillRect(3,17,1,11);c.fillRect(9,17,1,11);
  const frame=this.flames[index];
  frame.forEach((line,y)=>Array.from(line).forEach((pixel,x)=>{
   if(pixel==='0')return;c.fillStyle=pixel==='2'?'#e7ffff':y<4?'#70d9ff':'#40baff';c.fillRect(4+x,19+y,1,1);
  }));
  c.fillStyle='#b9f5ff';c.fillRect(6,25,1,2);
  c.restore();
 }
}

class LibrarySceneUI {
 constructor(ctx,sprites){this.ctx=ctx;this.sprites=sprites;this.questBackdrop=this.objectiveBackdrop();}
 objectiveBackdrop(){
  const canvas=document.createElement('canvas');canvas.width=272;canvas.height=80;
  const c=canvas.getContext('2d'),fade=c.createLinearGradient(0,0,272,0);
  fade.addColorStop(0,'rgba(9,20,37,0)');fade.addColorStop(.22,'rgba(9,20,37,.025)');
  fade.addColorStop(.68,'rgba(9,20,37,.44)');fade.addColorStop(1,'rgba(9,20,37,.68)');
  c.fillStyle=fade;
  for(let y=0;y<80;y++){
   c.globalAlpha=Math.min(1,y/12,(79-y)/12);c.fillRect(0,y,272,1);
  }
  return canvas;
 }
 frame(sprite,x,y,w,h){
  coverSceneText(this.ctx,x,y,w,h);
  const c=this.ctx,s=this.sprites[sprite],b=9;
  const sx=[0,b,s.width-b,s.width],sy=[0,b,s.height-b,s.height];
  const dx=[x,x+b,x+w-b,x+w],dy=[y,y+b,y+h-b,y+h];
  for(let row=0;row<3;row++)for(let col=0;col<3;col++)
   c.drawImage(s,col===1?Math.floor(s.width/2):sx[col],row===1?Math.floor(s.height/2):sy[row],col===1?1:sx[col+1]-sx[col],row===1?1:sy[row+1]-sy[row],dx[col],dy[row],dx[col+1]-dx[col],dy[row+1]-dy[row]);
 }
 interaction(center,y,label){
  const c=this.ctx;c.save();c.font='12px "Malgun Gothic", sans-serif';
  const labelWidth=Math.ceil(c.measureText(label).width)+26,total=32+4+labelWidth;
  const left=Math.round(Math.max(8,Math.min(952-total,center-total/2)));
  this.frame('ui_keycap_normal',left,y,32,30);
  this.frame('ui_interaction_label_frame',left+36,y,labelWidth,30);
  c.fillStyle='#f5f2e7';c.textAlign='center';c.textBaseline='alphabetic';
  c.font='14px "Malgun Gothic", sans-serif';drawSceneText(c,'E',left+16,y+20);
  c.font='12px "Malgun Gothic", sans-serif';drawSceneText(c,label,left+36+labelWidth/2,y+19);
  c.drawImage(this.sprites.ui_prompt_pointer,Math.round(center)-9,y+31);c.restore();
 }
 objective(complete){
  const c=this.ctx,y=160,right=938;c.save();
  // Flush to the right edge, below the hanging banner; fade into the scene on the left.
  c.drawImage(this.questBackdrop,688,y);
  c.textAlign='right';c.textBaseline='alphabetic';
  c.shadowBlur=0;c.shadowOffsetX=0;c.shadowOffsetY=0;
  c.font='11px "Malgun Gothic", sans-serif';
  c.fillStyle=complete?'#a8d9c0':'#d3d4d7';drawSceneText(c,complete?'대화 완료':'다음 할 일',right,y+25);
  c.font='14px "Malgun Gothic", sans-serif';c.fillStyle=complete?'#bce5d0':'#f4eddb';
  drawSceneText(c,'접수 안내서와 대화',right,y+48);
  c.restore();
 }
}
