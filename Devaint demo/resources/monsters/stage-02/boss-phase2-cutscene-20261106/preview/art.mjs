export const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
export async function loadArt(base=''){
 const get=async p=>{const r=await fetch(base+p);if(!r.ok)throw Error(`${p}: ${r.status}`);return r.json();};
 const [motions,sheets,combat,player,timeline]=await Promise.all(['motions.json','sheets.json','combat.json','player.json','timeline.json'].map(get));
 const images={}; const load=async p=>{const im=new Image();im.src=base+p;await im.decode();images[p]=im;};
 await Promise.all([...Object.values(motions.motions).flatMap(m=>m.frames.map(f=>f.file)),...sheets.sheets.map(s=>`assets/vfx/${s.name}.png`),'assets/room.png'].map(load));
 await document.fonts.load('700 12px Galmuri');
 return {motions:motions.motions,sheets:Object.fromEntries(sheets.sheets.map(s=>[s.name,s])),images,V:combat.values,P:player,timeline};
}
export function timed(ms,age,loop=false){let t=Math.max(0,age*1000),total=ms.reduce((a,b)=>a+b,0);if(loop)t%=total;for(let i=0;i<ms.length;i++){if(t<ms[i])return i;t-=ms[i];}return loop?0:ms.length-1;}
export function sprite(ctx,A,id,x,y,age=0,options={}){
 const m=A.motions[id];if(!m)return;const order=m.sequence||m.frames.map((_,i)=>i),k=options.frame??order[timed(m.durations,age,options.loop)];
 ctx.save();ctx.translate(Math.round(x),Math.round(y));if(options.flip)ctx.scale(-1,1);ctx.globalAlpha=options.alpha??1;
 ctx.drawImage(A.images[m.frames[Math.min(k,m.frames.length-1)].file],-192,-232);ctx.restore();
}
export function sheet(ctx,A,name,x,y,age=0,o={}){
 const s=A.sheets[name];if(!s||age<0)return;const total=s.durationsMs.reduce((a,b)=>a+b,0)/1000;if(!s.loop&&age>=total&&!o.hold)return;
 const i=timed(s.durationsMs,age,s.loop),k=(s.scale||1)*(o.scale??1),im=A.images[`assets/vfx/${name}.png`];
 ctx.save();ctx.translate(Math.round(x),Math.round(y));ctx.rotate(o.angle||0);ctx.scale(o.flip?-1:1,o.flipY?-1:1);ctx.globalAlpha=o.alpha??1;
 ctx.drawImage(im,(i%s.columns)*s.cellWidth,Math.floor(i/s.columns)*s.cellHeight,s.cellWidth,s.cellHeight,-s.pivotX*k,-s.pivotY*k,s.cellWidth*k,s.cellHeight*k);ctx.restore();
}
// Same pixel speech bubble as Unity's MaintenanceBubbleView (960x540 space): Galmuri11 Bold 12px, 184px wrap,
// 15px lines, stepped 2px ink border, ink name tag over the top-left edge, 14px stepped tail toward the head.
// hx/hy is the speaker's head; count is how many characters have been typed so far.
// look: optional {fill,ink,text,tagText,shake} — the red snapped-Blue bubble uses it; shake is a pixel jitter offset.
export const SNAP_BUBBLE={fill:'#b3202c',ink:'#2a0709',text:'#fff1ee',tagText:'#ff9c9c'};
export function bubble(ctx,text,hx,hy,speaker,count=Infinity,top=53,look=null){
 ctx.save();if(look?.shake)ctx.translate(look.shake[0],look.shake[1]);ctx.font='700 12px Galmuri';ctx.textBaseline='top';ctx.textAlign='left';const W=s=>ctx.measureText(s).width,lines=[];
 for(const para of text.split('\n')){let line='';for(const word of para.split(' ')){const next=line?line+' '+word:word;if(W(next)<=184){line=next;continue;}if(line)lines.push(line);line='';for(const ch of word){if(line&&W(line+ch)>184){lines.push(line);line='';}line+=ch;}}lines.push(line);}
 const w=Math.ceil(Math.max(...lines.map(W)))+20,h=lines.length*15+18,x=Math.round(clamp(hx-w/2,8,952-w)),y=Math.round(Math.max(top,hy-h-10)),tail=Math.round(clamp(hx-x,12,w-12));
 const fill=look?.fill??'#ffffff',ink=look?.ink??'#1f1b19',R=(px,py,pw,ph,c)=>{ctx.fillStyle=c;ctx.fillRect(x+px,y+py,pw,ph);};
 R(2,0,w-4,h,ink);R(0,2,w,h-4,ink);R(1,1,w-2,h-2,ink);R(3,2,w-6,h-4,fill);R(2,3,w-4,h-6,fill);
 for(let r=0;r<7;r++)R(tail-7+r,h-2+r,14-2*r,1,ink);
 for(let r=0;r<5;r++)R(tail-5+r,h-2+r,10-2*r,1,fill);
 if(speaker){const tag=Math.ceil(W(speaker))+12;R(6,-14,tag,16,ink);R(5,-13,tag+2,14,ink);ctx.fillStyle=look?.tagText??fill;ctx.fillText(speaker,x+12,y-12);}
 ctx.fillStyle=look?.text??ink;let left=count;lines.forEach((l,i)=>{ctx.fillText(l.slice(0,Math.max(0,left)),x+10,y+9+i*15);left-=l.length;});ctx.restore();
}
