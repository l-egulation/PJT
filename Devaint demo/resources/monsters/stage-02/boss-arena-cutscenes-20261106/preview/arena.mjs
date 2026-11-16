// Shared pieces for the Rose and Blue arena cutscene reviews (entrance and phase 1 to 2). Characters, room, speech bubble
// and VFX sheets come from the absorption cutscene review so all three cutscenes read as one set; approved pixels are
// never resized. Arena geometry follows data/combat/chapter2_rose_blue_arena.json in the main repository.
import {loadArt,sprite,sheet,bubble,clamp} from '../../boss-phase2-cutscene-20261106/preview/art.mjs';
export {sprite,sheet,bubble,clamp};
export {eyeGlint,GLINT_SECONDS} from './fx.mjs';
const here=new URL('.',import.meta.url).href;
const SHARED=new URL('../../boss-phase2-cutscene-20261106/preview/',here).href,LINEUP=new URL('../../boss-siblings-lineup-20261103/',here).href;
const SWORD=new URL('../../../../characters/ink/weapons/sword/runtime/approved-eleven-package/character/',here).href;
export const FLOOR=445,U=32,top=y=>540-y*U;
// phase1 platforms are 6u wide; machine 2x3u centred at x 15u; markers use bottom-left y-up units.
export const PLATFORMS=[[6.5,4.5],[17.5,4.5],[2,9],[12,9],[22,9],[6.5,13.5],[17.5,13.5]].map(([x,y])=>({x:x*U,y:top(y+.5),w:6*U}));
export const MACHINE={x:15*U,y:top(9.5)},PLAYER_PHASE1={x:7*U,y:top(5)},ROSE_PHASE2={x:2*U},BLUE_PHASE2={x:28.75*U};
export const ease=v=>{v=clamp(v);return v*v*(3-2*v);},mix=(a,b,u)=>a+(b-a)*u,step4=u=>Math.floor(clamp(u)*4)/4;
function load(src){return new Promise((yes,no)=>{const i=new Image;i.onload=()=>yes(i);i.onerror=()=>no(Error(src));i.src=src;});}
// Unity RoseBlueEncounter.ProceduralArt("bubble"): pale fill, two rim bands and one curved glint, point sampled.
function makeBubble(d){const c=document.createElement('canvas');c.width=c.height=d;const g=c.getContext('2d'),data=g.createImageData(d,d),h=(d-1)/2;
 for(let y=0;y<d;y++)for(let x=0;x<d;x++){const dx=(x-h)/h,dy=(h-y)/h,r=Math.hypot(dx,dy);if(r>=.98)continue;let col=[.38,.50,.88,.12];
  if(r>.92)col=[.66,.73,1,.78];else if(r>.85)col=[.39,.46,.84,.32];if(r>.77&&r<.83&&dx<-.28&&dy>.25)col=[.83,.88,1,.88];
  data.data.set([col[0]*255,col[1]*255,col[2]*255,col[3]*255],(y*d+x)*4);}
 g.putImageData(data,0,0);return c;}
export async function loadArena(){
 const A=await loadArt(SHARED),names={walk:['01','02','04','05','07','08','10','11'],jump:['02','03','04','06','07'],land:['03','04','05','07','09','12']};
 const frames={};for(const [k,list] of Object.entries(names))frames[k]=await Promise.all(list.map(n=>load(`${SWORD}${k}_${n}.png`)));
 const [machine,tile]=await Promise.all([load(LINEUP+'unity-phase1/machine.png'),load(LINEUP+'unity-phase1/tile.png')]);
 const room=A.images['assets/room.png'],wallBand=document.createElement('canvas');wallBand.width=960;wallBand.height=96;
 // Wall rows directly above the floor, reused where the floor tiles are missing.
 wallBand.getContext('2d').drawImage(room,0,FLOOR-96,960,96,0,0,960,96);
 return {A,frames,img:{machine,tile,room,wallBand},bubbleArt:makeBubble(86)};}
// Sword-set player frames (128px canvas) share the idle body anchor (75,121); approved size, no scaling.
export function playerFrame(ctx,im,x,y,flip=false){ctx.save();ctx.translate(Math.round(x),Math.round(y));if(flip)ctx.scale(-1,1);ctx.drawImage(im,-75,-121);ctx.restore();}
export const headY=(A,id,y)=>y-(232-A.motions[id].frames[0].bounds[1]);
export function ghosts(ctx,A,id,x,age,dir,flip,leaving){const u=clamp(age/.2);for(let i=3;i>0;i--)sprite(ctx,A,id,x+dir*(i*12+(leaving?u:1-u)*22),FLOOR,0,{flip,alpha:(leaving?1-u:u)*.18/i});sprite(ctx,A,id,x+dir*(leaving?u*20:(1-u)*22),FLOOR,0,{flip,alpha:leaving?1-u:u});}
// Floor columns leave (or return) one tile column at a time in quantised steps; soft fades read wrong in this game.
// gone(col) returns 0 for intact floor and 1 for a missing column.
export function floor(ctx,R,gone){for(let col=0;col<30;col++){const g=gone(col);if(g<=0)continue;const s=step4(g);if(!s)continue;
 ctx.drawImage(R.img.wallBand,col*U,0,U,96,col*U,FLOOR,U,96);
 if(s<1){ctx.globalAlpha=1-s;ctx.drawImage(R.img.room,col*U,FLOOR,U,96,col*U,FLOOR+Math.round(s*8),U,96);ctx.globalAlpha=1;}
 if(g>0&&g<.6){ctx.fillStyle='#8f86d6';for(let k=0;k<3;k++)ctx.fillRect(col*U+((col*7+k*11)%28),FLOOR-2-Math.round(g*14)-k*3,2,2);}}}
export const columnOrder=col=>((col*11)%30)/30;
export function platform(ctx,R,p,shown){const s=step4(shown);if(!s)return;ctx.globalAlpha=s;
 for(let x=0;x<p.w;x+=96)ctx.drawImage(R.img.tile,0,0,Math.min(96,p.w-x),16,p.x+x,p.y+Math.round((1-s)*10),Math.min(96,p.w-x),16);ctx.globalAlpha=1;
 if(shown>0&&shown<1){ctx.fillStyle='#a79ae6';ctx.fillRect(p.x,p.y-1,p.w,2);}}
export function machine(ctx,R,shown,dx=0,at=MACHINE){const s=step4(shown);if(!s)return;ctx.globalAlpha=s;ctx.drawImage(R.img.machine,at.x-30+dx,at.y-80);ctx.globalAlpha=1;}
// Speech bubbles (size, name tags, letter motion, cut-offs, exits) live with the shared art: speech.mjs.
export {say} from '../../boss-phase2-cutscene-20261106/preview/speech.mjs';
export function bars(ctx,u){const b=Math.round(32*clamp(u));ctx.fillStyle='#090b0c';ctx.fillRect(0,0,960,b);ctx.fillRect(0,540-b,960,b);}
// Review page wiring shared by both cutscenes: play/seek/cues, editable dialogue and the stage2QA capture hook.
// ?embedded: the page sits inside the full boss-flow review; it plays at once, shows only the stage and tells the parent
// when it ends (done() supplies extra fields such as the player's X and HP).
export const query=new URLSearchParams(location.search),embedded=query.has('embedded');
if(embedded){document.documentElement.classList.add('embedded');const st=document.createElement('style');st.textContent='html.embedded,html.embedded body{margin:0;background:#000;overflow:hidden}html.embedded main{margin:0;padding:0;max-width:none}html.embedded main>:not(.stage){display:none!important}html.embedded .stage{width:100%;border:0}';document.head.append(st);}
export function mountPlayer(T,render,exportName,done=()=>({})){
 const $=s=>document.querySelector(s);let time=0,playing=embedded,previous=0,reported=false;
 $('#seek').max=T.duration;
 $('#cues').replaceChildren(...T.cues.map(c=>{const b=document.createElement('button');b.textContent=c.name;b.onclick=()=>{time=c.time;playing=false;};return b;}));
 $('#dialogues').replaceChildren(...T.dialogue.map(d=>{const box=document.createElement('label');box.className='line';const title=document.createElement('span');title.textContent=`${d.speaker==='rose'?'로즈':'블루'} ${d.from}–${d.to}s`;const text=document.createElement('textarea');text.value=d.text;text.oninput=()=>d.text=text.value;box.append(title,text);return box;}));
 $('#play').onclick=()=>{if(time>=T.duration)time=0;playing=!playing;};$('#restart').onclick=()=>{time=0;playing=true;};$('#seek').oninput=e=>{time=Number(e.target.value);playing=false;};
 $('#export-dialogue').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(T,null,2)],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download=exportName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
 document.addEventListener('keydown',e=>{if(/INPUT|TEXTAREA|SELECT/.test(e.target.tagName))return;if(e.key===' '){e.preventDefault();playing=!playing;}if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();time=clamp(time+(e.key==='ArrowLeft'?-.1:.1),0,T.duration);playing=false;}});
 const draw=()=>render(time);
 window.stage2QA={get time(){return time;},pause(){playing=false;},cutscene(){time=0;playing=false;},seek(t){time=clamp(t,0,T.duration);playing=false;},render:draw};
 const at=Number(new URLSearchParams(location.search).get('at'));if(Number.isFinite(at)&&at>0)time=clamp(at,0,T.duration);
 function frame(now){const dt=Math.min(.05,(now-previous)/1000||0);previous=now;
  if(playing){time=Math.min(T.duration,time+dt*Number($('#speed').value));if(time>=T.duration){if($('#loop').checked)time=0;else playing=false;}}
  if(embedded&&!reported&&time>=T.duration){reported=true;parent.postMessage({type:'cutscene-done',...done()},location.origin);}
  draw();$('#seek').value=time;$('#clock').textContent=`${time.toFixed(2)} / ${T.duration.toFixed(2)}s`;$('#play').textContent=playing?'일시정지':'재생';
  const cur=T.cues.filter(c=>time>=c.time).at(-1);$('#status').textContent=cur?.name??'';document.querySelectorAll('#cues button').forEach((b,i)=>b.classList.toggle('active',T.cues[i]===cur));
  requestAnimationFrame(frame);}
 $('#status').textContent='준비 완료';requestAnimationFrame(frame);}
export function showError(e){const el=document.querySelector('#error');el.hidden=false;el.textContent=`불러오지 못한 에셋이 있습니다.\n${e.message}`;console.error(e);}
