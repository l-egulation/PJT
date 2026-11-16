'use strict';
const $=id=>document.getElementById(id),sum=a=>a.reduce((n,s)=>n+s.ms,0),cache={},fxcache={},cards=[],sections={},manual={};
const W=480,H=220,OX=12,OY=50,GROUND=172;
let time=0,last=null,playing=true,ready=false,selections={cannon:'A',laser:'B',shotgun:'A',grenade:'B'};
try{selections={...JSON.parse(localStorage.getItem('ink-ranged-fx-approved-20261021')||'{}'),cannon:'A',laser:'B',shotgun:'A',grenade:'B'};}catch{}
function load(src){return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src;});}
function pick(seq,t){for(const s of seq){if(t<s.ms)return s;t-=s.ms;}return seq.at(-1);}
function events(w,mode){let t=0;const out=[];for(const s of w.modules[mode].steps){if(s.frame===w.fireFrames[mode])out.push({start:t,end:t+s.ms});t+=s.ms;}return out;}
function frameAt(v,age,loop=false){const duration=v.durations.reduce((a,b)=>a+b,0);if(loop)age%=duration;if(age<0||age>=duration)return -1;for(let i=0;i<v.durations.length;i++){if(age<v.durations[i])return i;age-=v.durations[i];}return -1;}
function maxCycle(){return Math.max(...FXD.weapons.map(w=>sum(w.modules[$('mode').value].steps)))+1050;}
function clockFor(w){const mode=$('mode').value,state=manual[w.id],duration=sum(w.modules[mode].steps),ev=events(w,mode)[0];let elapsed;
 if(state){elapsed=time-state.start;if(w.id==='laser'){
  if(state.held)return {t:Math.min(elapsed,ev.end-1),laserAge:elapsed-ev.start,laserHeld:elapsed>=ev.start};
  if(state.release!==null)return {t:ev.end+time-state.release,laserAge:-1,laserHeld:false};
 }}else elapsed=($('repeat').checked?time%(duration+1050):time)-350;
 return {t:elapsed,laserAge:elapsed-ev.start,laserHeld:w.id==='laser'&&elapsed>=ev.start&&elapsed<ev.end};
}
function sample(w,v,clock,mode){
 const duration=sum(w.modules[mode].steps),t=clock.t,body=(t<0||t>=duration)?pick(w.modules.idle.steps,Math.max(0,t-duration)%sum(w.modules.idle.steps)).frame:pick(w.modules[mode].steps,t).frame;
 const effects=[],projectiles=[],beams=[];
 for(const event of events(w,mode)){
  let age=t-event.start;if(age<0)continue;const muzzle=w.muzzles[w.fireFrames[mode]],x=muzzle[0]+OX,y=muzzle[1]+OY;
  if(w.id==='laser'){
   if(clock.laserHeld){const phase=(clock.laserAge%480)/80,f=Math.floor(phase);beams.push({x,y,end:W,width:v.coreWidth,inner:v.innerWidth,f,next:(f+1)%6,mix:phase-f});}
  }else if(w.id==='shotgun'){
   const f=frameAt(v,age),attached=w.muzzles[body]||muzzle;
   if(f>=0)effects.push({part:'frames',f,x:attached[0]+OX,y:attached[1]+OY,angle:0,scale:1});
  }else if(w.id==='grenade'){
   const f=frameAt(v.muzzle,age),attached=w.muzzles[body]||muzzle;
   if(f>=0)effects.push({part:'muzzle',f,x:attached[0]+OX,y:attached[1]+OY,angle:0,scale:.4});
   if(age<v.travelMs){const p=age/v.travelMs;projectiles.push({x:x+(360-x)*p,y:y+(GROUND-y)*p-66*4*p*(1-p),angle:-35+140*p,alpha:1,scale:1});}
   const blast=frameAt(v,age-v.travelMs);if(blast>=0)effects.push({part:'frames',f:blast,x:360,y:GROUND,angle:0,scale:1});
  }else{const f=frameAt(v,age);if(f>=0)effects.push({part:'frames',f,x,y,angle:w.angle,scale:1});}
 }
 return {body,effects,projectiles,beams,t,duration};
}
function background(ctx,w){const selected=$('background').value,dark=selected==='dark'||(selected==='auto'&&w.id==='laser');ctx.fillStyle=dark?'#111b29':'#fff';ctx.fillRect(0,0,W,H);if(selected==='checker'){ctx.fillStyle='#edf0f2';for(let y=0;y<H;y+=8)for(let x=0;x<W;x+=8)if((x+y)%16===0)ctx.fillRect(x,y,8,8);}ctx.fillStyle=dark?'#405064':'#d8dfe3';ctx.fillRect(8,GROUND,W-16,1);}
function drawCard(card){const {w,v,ctx}=card,s=sample(w,v,clockFor(w),$('mode').value);ctx.imageSmoothingEnabled=false;background(ctx,w);ctx.drawImage(cache[w.id][s.body],OX,OY);
 if($('effects').checked){
  for(const b of s.beams){const x=Math.round(b.x),y=Math.round(b.y),frames=fxcache[w.id][v.id].frames;ctx.save();ctx.imageSmoothingEnabled=true;ctx.globalAlpha=1-b.mix;ctx.drawImage(frames[b.f],x,y-48,b.end-x,96);ctx.globalAlpha=b.mix;ctx.drawImage(frames[b.next],x,y-48,b.end-x,96);ctx.restore();ctx.fillStyle='#fff';ctx.fillRect(x,y-Math.floor(b.width/2),b.end-x,b.width);}
  for(const p of s.projectiles){const im=fxcache[w.id][v.id].projectile;ctx.save();ctx.globalAlpha=p.alpha;ctx.translate(Math.round(p.x),Math.round(p.y));ctx.rotate(p.angle*Math.PI/180);ctx.drawImage(im,-im.width*p.scale/2,-im.height*p.scale/2,im.width*p.scale,im.height*p.scale);ctx.restore();}
  for(const e of s.effects){const pivot=e.part==='muzzle'?v.muzzle.pivot:v.pivot,im=fxcache[w.id][v.id][e.part][e.f];ctx.save();ctx.translate(Math.round(e.x),Math.round(e.y));ctx.rotate(e.angle*Math.PI/180);ctx.drawImage(im,-pivot[0]*e.scale,-pivot[1]*e.scale,im.width*e.scale,im.height*e.scale);ctx.restore();}
 }
 card.status.textContent=s.t<0?'대기':s.beams.length?'지속 발사':w.id==='grenade'&&s.effects.some(e=>e.part==='frames')?'착탄 · 폭발':s.projectiles.length?'투사체 이동':s.effects.length?'총구 발사':s.t>=s.duration?'회수 완료':'준비 · 회수';
}
function draw(){if(!ready)return;for(const c of cards)if($('weapon').value==='all'||$('weapon').value===c.w.id)drawCard(c);$('seek').value=String(Math.round((time%maxCycle())/maxCycle()*1000));}
function choose(id,variant){const fixed=FXD.weapons.find(w=>w.id===id)?.fixedVariant;if(fixed)variant=fixed;selections[id]=variant;try{localStorage.setItem('ink-ranged-fx-approved-20261021',JSON.stringify(selections));}catch{}for(const c of cards)if(c.w.id===id){const chosen=selections[id]===c.v.id;c.article.classList.toggle('chosen',chosen);c.choose.setAttribute('aria-pressed',String(chosen));c.choose.textContent=fixed?`${fixed}안 확정`:chosen?'선택됨':'이 후보 선택';}}
function fire(id){manual[id]={start:time,held:id==='laser',release:null};playing=true;$('play').textContent='일시정지';draw();}
function release(id){if(manual[id]?.held){manual[id].held=false;manual[id].release=time;draw();}}
function reset(){time=0;last=null;for(const key of Object.keys(manual))delete manual[key];playing=true;$('play').textContent='일시정지';draw();}
function tick(now){if(last!==null&&playing)time+=Math.min(100,Math.max(0,now-last))*Number($('speed').value);last=now;if(!$('repeat').checked&&!Object.keys(manual).length&&time>=maxCycle()){time=maxCycle();playing=false;$('play').textContent='재생';}draw();requestAnimationFrame(tick);}
async function init(){
 for(const w of FXD.weapons){
  const section=document.createElement('section');section.className='weapon'+(w.fixedVariant?' fixed':'');section.innerHTML=`<header><h2>${w.name}</h2><button class="shoot">${w.id==='laser'?'눌러서 지속 발사':'직접 발사'}</button><span>${w.fixedVariant?`${w.fixedVariant}안 확정`:'좌우 크기와 갈라지는 모양이 다른 폭발'}</span></header><div class="cards"></div>`;$('weapons').append(section);sections[w.id]=section;
  const shoot=section.querySelector('.shoot');if(w.id==='laser'){shoot.onpointerdown=e=>{e.preventDefault();shoot.setPointerCapture?.(e.pointerId);fire(w.id);};shoot.onpointerup=shoot.onpointercancel=()=>release(w.id);shoot.onkeydown=e=>{if(['Space','Enter'].includes(e.code)&&!e.repeat){e.preventDefault();fire(w.id);}};shoot.onkeyup=e=>{if(['Space','Enter'].includes(e.code)){e.preventDefault();release(w.id);}};}else shoot.onclick=()=>fire(w.id);
  const used=new Set(Object.values(w.modules).flatMap(m=>m.steps.map(s=>s.frame)));cache[w.id]={};await Promise.all([...used].map(async id=>cache[w.id][id]=await load(w.frames[id])));fxcache[w.id]={};
  for(const v of w.variants){
   const fc={frames:await Promise.all(v.frames.map(load))};if(v.muzzle)fc.muzzle=await Promise.all(v.muzzle.frames.map(load));if(v.projectile)fc.projectile=await load(v.projectile);fxcache[w.id][v.id]=fc;
   const article=document.createElement('article');article.className='card';article.innerHTML=`<h3>${v.id} · ${v.name}</h3><p>${v.note}</p><canvas width="480" height="220" aria-label="${w.name} ${v.id} 재생"></canvas><footer><button class="choose" aria-pressed="false">이 후보 선택</button><span class="state"></span><a href="이펙트_스프라이트/${w.name}/${v.id}/${w.id==='laser'?'full_beam/':''}sheet.png" target="_blank">시트</a></footer>`;section.querySelector('.cards').append(article);const canvas=article.querySelector('canvas'),card={w,v,article,ctx:canvas.getContext('2d'),status:article.querySelector('.state'),choose:article.querySelector('.choose')};card.choose.onclick=()=>choose(w.id,v.id);cards.push(card);
  }
  if(selections[w.id])choose(w.id,selections[w.id]);
 }
 ready=true;$('status').textContent='기본포 A · 레이저 B · 샷건 A · 유탄 비대칭 B 확정';draw();requestAnimationFrame(tick);
}
$('play').onclick=()=>{playing=!playing;last=null;$('play').textContent=playing?'일시정지':'재생';};$('replay').onclick=reset;$('mode').onchange=reset;
$('weapon').onchange=()=>{for(const [id,section] of Object.entries(sections))section.hidden=$('weapon').value!=='all'&&$('weapon').value!==id;draw();};$('effects').onchange=$('background').onchange=draw;
$('seek').oninput=()=>{for(const k of Object.keys(manual))delete manual[k];time=Number($('seek').value)/1000*maxCycle();playing=false;$('play').textContent='재생';draw();};
document.addEventListener('visibilitychange',()=>{if(document.hidden){for(const id of Object.keys(manual))release(id);last=null;}});
window.fxAPI={data:FXD,events,frameAt,sample,clockFor,fire,release,reset,draw,tick,getTime:()=>time,setTime:t=>{time=t;draw();},isReady:()=>ready};
init().catch(e=>{$('status').textContent='이미지를 불러오지 못했어.';console.error(e);});
