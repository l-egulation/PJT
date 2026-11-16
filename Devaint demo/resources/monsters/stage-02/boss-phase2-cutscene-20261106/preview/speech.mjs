// Speech bubbles for all three Rose and Blue cutscenes (entrance, phase 1 to 2, absorption), drawn at an integer
// SCALE of the original 12px Galmuri bubble so the pixels stay crisp. The tail sits near the bubble's left end for Rose
// and its right end for Blue, so bubbles lean toward the middle of the screen; it swaps sides only when the bubble
// would leave the screen. Each dialogue line in timeline.json may add:
//   style  'normal' | 'dark' (black bubble, light letters)
//   cps    typing speed (default 30 characters/s); type:'instant' shows the whole line at once (Blue's cold voice)
//   enter  'pop' (springs out) | 'slam' (drops in hard)
//   fx     [{fx:'shake'|'wave'|'bounce'|'droop'|'tremble', match?:'substring', amp?}] per-letter motion
//   color  text colour
//   cover  id of a line this bubble slams over: that line freezes, is dimmed, jolts and stays under it (cutting it off)
//   exit   {type:'fade', seconds} fades out from `to`
//   flash  white flash when the line starts; quake: screen shake amplitude (px) while the line types
// Name tags can be switched off to compare (stage2Speech.tags, remembered in localStorage).
import {clamp} from './art.mjs';
export const SCALE=2;
const FONT='700 12px Galmuri',WRAP=184,LINE=15,TAIL_IN=18;
export const SPEAKERS={rose:{name:'로즈',tag:'#a8325f',tagText:'#ffe6f0'},blue:{name:'블루',tag:'#2f4f9e',tagText:'#e4ecff'}};
const STYLES={normal:{fill:'#ffffff',ink:'#1f1b19',text:'#1f1b19'},dark:{fill:'#16121f',ink:'#030207',text:'#efeaff'}};
const TAG_KEY='stage2-speech-tags',store=(()=>{try{return localStorage;}catch{return null;}})();
let tags=store?.getItem(TAG_KEY)!=='0';
globalThis.stage2Speech={get tags(){return tags;},set tags(v){tags=!!v;try{store?.setItem(TAG_KEY,tags?'1':'0');}catch{}}};
const fract=v=>v-Math.floor(v),hash=(a,b)=>fract(Math.sin(a*12.9898+b*78.233)*43758.5453),stepT=(t,f)=>Math.floor(t*f)/f;
const mctx=document.createElement('canvas').getContext('2d');
function wrap(text){mctx.font=FONT;const W=s=>mctx.measureText(s).width,lines=[];
 for(const para of text.split('\n')){let line='';for(const word of para.split(' ')){const next=line?line+' '+word:word;if(W(next)<=WRAP){line=next;continue;}if(line)lines.push(line);line='';
  for(const ch of word){if(line&&W(line+ch)>WRAP){lines.push(line);line='';}line+=ch;}}lines.push(line);}
 return {lines,W};}
// Per-letter offset in bubble pixels.
function letterOffset(d,i,lineText,col,t,appear){let dx=0,dy=0;
 for(const f of d.fx??[]){if(f.match){let hit=false,from=0;for(;;){const k=lineText.indexOf(f.match,from);if(k<0)break;if(col>=k&&col<k+f.match.length){hit=true;break;}from=k+1;}if(!hit)continue;}
  const amp=f.amp??1;
  if(f.fx==='shake'){const s=Math.floor(t*20);dx+=Math.round((hash(i,s)*2-1)*amp);dy+=Math.round((hash(i+.5,s)*2-1)*amp);}
  if(f.fx==='tremble'){const s=Math.floor(t*30);if(hash(i,s)>.55)dx+=hash(i,s+1)>.5?amp:-amp;}
  if(f.fx==='wave')dy+=Math.round(Math.sin(stepT(t,12)*9-i*.9)*1.6*amp);
  if(f.fx==='bounce'){const a=t-appear;if(a>=0&&a<.2)dy-=Math.round(Math.sin(stepT(a,30)/.2*Math.PI)*4*amp);}
  if(f.fx==='droop')dy+=Math.round(clamp((t-appear)/.25)*Math.min(3,1+col*.5)*amp);}
 return [dx,dy];}
function layoutLine(d,head,boxes,top){
 const st=STYLES[d.style??'normal']??STYLES.normal,{lines,W}=wrap(d.text),w=Math.ceil(Math.max(...lines.map(W)))+20,h=lines.length*LINE+18,S=SCALE,PW=w*S,PH=h*S;
 // Rose's tail near the left end, Blue's near the right end; swap only if that pushes the bubble off screen.
 let left=d.speaker!=='blue',x=left?head.x-TAIL_IN*S:head.x-(w-TAIL_IN)*S;
 if(left&&x+PW>952){left=false;x=head.x-(w-TAIL_IN)*S;}else if(!left&&x<8){left=true;x=head.x-TAIL_IN*S;}
 x=Math.round(clamp(x,8,952-PW));let y=Math.round(Math.max(top,head.y-PH-10*S));
 // A covering bubble keeps its own side and drops onto the covered bubble's text.
 const target=d.cover&&boxes[d.cover];if(target)y=Math.round(clamp(target.y+9*S,top,540-40-PH));
 const tail=Math.round(clamp((head.x-x)/S,12,w-12));
 return {d,st,lines,W,w,h,PW,PH,x,y,tail};}
function shape(B,R){const {w,h,tail,st}=B,ink=st.ink,fill=st.fill;
 R(2,0,w-4,h,ink);R(0,2,w,h-4,ink);R(1,1,w-2,h-2,ink);R(3,2,w-6,h-4,fill);R(2,3,w-4,h-6,fill);
 for(let r=0;r<7;r++)R(tail-7+r,h-2+r,14-2*r,1,ink);for(let r=0;r<5;r++)R(tail-5+r,h-2+r,10-2*r,1,fill);}
// Draws one bubble with its tag and letters (screen space). count: letters typed; dim: covered shade.
function paint(c,B,t,count,dim,jolt){
 const {d,st,lines,W,w,h,x,y,tail}=B,S=SCALE,sp=SPEAKERS[d.speaker],age=t-d.from;
 let sc=1,oy=0;
 if(d.enter==='pop')sc=age<.05?.7:age<.1?1.12:1;
 if(d.enter==='slam'){sc=age<.04?1.28:age<.08?.92:age<.12?1.04:1;oy=age<.04?-6:0;}
 c.save();c.translate(x+(jolt?.[0]??0)*S,y+(oy+(jolt?.[1]??0))*S);
 if(sc!==1){c.translate(tail*S,h*S);c.scale(sc,sc);c.translate(-tail*S,-h*S);}
 c.scale(S,S);c.imageSmoothingEnabled=false;
 const R=(px,py,pw,ph,col)=>{c.fillStyle=col;c.fillRect(px,py,pw,ph);};
 shape(B,R);
 // Name tag: coloured inside an ink border, the name centred on its real glyph height.
 c.font=FONT;
 if(tags&&sp){const m=c.measureText(sp.name),tw=Math.ceil(m.width)+12,asc=m.actualBoundingBoxAscent,gh=asc+m.actualBoundingBoxDescent;
  R(5,-16,tw+2,17,st.ink);R(6,-17,tw,19,st.ink);R(7,-15,tw-2,14,sp.tag);
  c.textBaseline='alphabetic';c.fillStyle=sp.tagText;c.fillText(sp.name,13,Math.round(-15+(14-gh)/2+asc));}
 c.textBaseline='top';c.fillStyle=d.color??st.text;const cps=d.type==='instant'?Infinity:(d.cps??30);let i=0;
 lines.forEach((l,li)=>{[...l].forEach((ch,col)=>{if(i<count){const [dx,dy]=letterOffset(d,i,l,col,t,d.from+i/cps);c.fillText(ch,10+W(l.slice(0,col))+dx,9+li*LINE+dy);}i++;});});
 if(dim>0){c.globalAlpha=dim;R(0,0,w,h,'#1a1626');}
 c.restore();}
// Draws every line on screen at t. anchor(d) is the speaker's head {x,y}.
export function say(ctx,T,t,anchor,top=72){
 const all=T.dialogue,boxes={},canvas=ctx.canvas;let quake=0,flash=0;
 for(const d of all){
  const coverer=all.find(c=>c.cover===d.id),covered=!!coverer&&t>=coverer.from,ex=d.exit?.seconds??0;
  const live=covered?t<coverer.to:t>=d.from&&t<d.to+ex;if(!live||t<d.from)continue;
  const B=layoutLine(d,anchor(d),boxes,top);boxes[d.id]=B;
  const cps=d.type==='instant'?Infinity:(d.cps??30),count=Math.floor(((covered?coverer.from:t)-d.from)*cps)+1;
  const ja=covered?t-coverer.from:-1,jolt=ja>=0&&ja<.18?[Math.round((hash(1,Math.floor(ja*30))*2-1)*3),Math.round((hash(2,Math.floor(ja*30))*2-1)*2)]:null;
  const fade=!covered&&t>=d.to&&ex>0?clamp(1-(t-d.to)/ex):1;
  ctx.save();ctx.globalAlpha=fade;paint(ctx,B,t,count,covered?.22:0,jolt);ctx.restore();
  if(d.quake&&t<d.from+d.text.length/cps+.3)quake=Math.max(quake,d.quake);
  if(d.flash&&t-d.from<.18)flash=Math.max(flash,(1-(t-d.from)/.18)*.7);}
 if(flash>0){ctx.fillStyle=`rgba(255,250,240,${flash})`;ctx.fillRect(0,0,canvas.width,canvas.height);}
 const s=Math.floor(t*30);canvas.style.transform=quake?`translate(${Math.round((hash(3,s)*2-1)*quake)}px,${Math.round((hash(4,s)*2-1)*quake)}px)`:'';}
