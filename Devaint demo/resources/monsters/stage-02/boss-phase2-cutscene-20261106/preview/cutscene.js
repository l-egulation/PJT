import {loadArt,sprite,sheet,bubble,clamp,SNAP_BUBBLE} from './art.mjs';
import {say} from './speech.mjs';
import {Battle,FLOOR} from './engine.mjs';
import {room,battle} from './render.mjs';
import {PILLAR,roarPillar,pillarShake} from './pillar.mjs';
const $=s=>document.querySelector(s),canvas=$('#stage'),ctx=canvas.getContext('2d'),input=new Set();
// ?embedded: this page runs inside the full boss-flow review. ?battle starts phase 2 at once with the player's X and HP
// from the phase 1 to 2 cutscene and the siblings on the phase2 floor-end markers; the parent hears when absorption ends.
const Q=new URLSearchParams(location.search),EMBEDDED=Q.has('embedded');let reported=false;
if(EMBEDDED){const st=document.createElement('style');st.textContent='html,body{margin:0;background:#000;overflow:hidden}main{margin:0!important;padding:0!important;max-width:none!important}main>:not(.stage){display:none!important}.stage{width:100%!important;border:0!important}';document.head.append(st);}
let A,g,mode='battle',branch='blue-first',time=0,playing=false,previous=0,initial={playerX:480,playerHp:100,roseX:720,blueX:170};
const movement=(id,age,span)=>{const m=A.motions[id],ms=m.durations.slice(0,-1).reduce((a,b)=>a+b,0);return ms/1000*clamp(age/span)+.001;};
function chooseBranch(id,params=null){branch=id;reported=false;if(EMBEDDED)parent.postMessage({type:'absorb-start',branch:id},location.origin);mode='cutscene';time=0;playing=true;initial=params??{playerX:480,playerHp:100,roseX:720,blueX:170};input.clear();setupBranch();canvas.focus({preventScroll:true});}
function setupBranch(){const C=A.timeline.branches[branch];$('#seek').max=C.duration;document.querySelectorAll('[data-branch]').forEach(b=>b.classList.toggle('selected',b.dataset.branch===branch));$('#cues').replaceChildren(...C.cues.map(c=>{const b=document.createElement('button');b.textContent=c.name;b.onclick=()=>{mode='cutscene';time=c.time;playing=false;};return b;}));$('#dialogues').replaceChildren(...C.dialogue.map(d=>{const box=document.createElement('label');box.className='line';const title=document.createElement('span');title.textContent=`${d.speaker==='rose'?'로즈':'블루'} · ${d.from}–${d.to}s`;const text=document.createElement('textarea');text.value=d.text;text.oninput=()=>d.text=text.value;box.append(title,text);return box;}));}
function pose(id,x,y,age=0,o={}){sprite(ctx,A,id,x,y,age,o);}
const headY=(id,y)=>y-(232-A.motions[id].frames[0].bounds[1]);
// Inside the full roar column Blue reads as a dark ink silhouette (approved pillar test).
const shade=document.createElement('canvas');shade.width=384;shade.height=256;
function silhouette(id,x,y,flip){const s=shade.getContext('2d');s.clearRect(0,0,384,256);sprite(s,A,id,192,232,0,{});s.globalCompositeOperation='source-atop';s.globalAlpha=.82;s.fillStyle='#19182d';s.fillRect(0,0,384,256);s.globalCompositeOperation='source-over';s.globalAlpha=1;
 ctx.save();ctx.translate(Math.round(x),Math.round(y));if(flip)ctx.scale(-1,1);ctx.drawImage(shade,-192,-232);ctx.restore();}
function cutscene(){const C=A.timeline.branches[branch],t=time,isBlue=branch==='blue-first',rx=clamp(initial.roseX,160,800),bx0=clamp(initial.blueX,80,880),bx=rx>480?Math.min(860,rx+135):Math.max(85,rx-135),flip=bx<rx;
 room(ctx,A);const sh=$('#shake').checked?pillarShake(t-C.roarStart):0;ctx.save();ctx.translate(Math.round(sh),0);
 const playerX=initial.playerX+(480-initial.playerX)*clamp(t/.3);pose('player',playerX,FLOOR,0,{flip:rx>480});
 let blueId='blue-idle',blueAge=0,blueX=bx0,blueY=FLOOR,blueFlip=bx0<rx,blueVisible=true;
 if(isBlue){blueId='blue-collapse';blueAge=movement(blueId,t,C.koEnd);if(t>=C.riseStart){blueId='blue-rise';blueAge=movement(blueId,t-C.riseStart,C.riseEnd-C.riseStart);}}
 if(t>=C.teleportStart){blueX=bx;blueFlip=flip;}
 if(t>=C.teleportStart&&t<C.teleportEnd)blueVisible=false;
 if(!isBlue&&t>=C.landingStart&&t<C.landingEnd){blueId='blue-landing';const a=t-C.landingStart;blueAge=a;blueY=FLOOR-358*(1-clamp(a/.14)**2);if(a>=.14)blueAge=.14+(a-.14)*.9;sheet(ctx,A,'dive-trail',blueX,blueY,a);sheet(ctx,A,'dive-impact',blueX,FLOOR,a-.14);}
 if(!isBlue&&t>=C.landingEnd){blueId='blue-recovery';blueAge=movement(blueId,t-C.landingEnd,C.recoveryEnd-C.landingEnd);}
 if(t>=C.castStart){blueId=!isBlue&&A.motions['blue-absorb-bridge']?'blue-absorb-bridge':'blue-cast';blueAge=movement(blueId,t-C.castStart,(C.castEnd??C.castStart+.9)-C.castStart);}
 if(t>=C.roarStart){blueId='blue-roar';blueAge=0;}
 if(t>=C.transformStart){blueId=t<C.idleStart?'fused-arrival':'fused-idle';blueAge=0;}
 let roseId=isBlue?'rose-idle':'rose-collapse',roseAge=isBlue?0:movement('rose-collapse',t,C.koEnd),roseY=FLOOR,roseX=rx,captureBlend=1;
 const captured=t>=C.captureStart,absorbProgress=clamp((t-C.absorbStart)/(C.absorbEnd-C.absorbStart));
 if(captured){roseId='rose-capture';
  if(isBlue)roseAge=movement(roseId,t-C.captureStart,.65);
  else{
   // Rose is already down and her bow is on the floor. The spell lifts her into
   // the approved suspended frames; do not restart the standing/drop-weapon lead-in.
   captureBlend=clamp((t-C.captureStart-.08)/.24);captureBlend=captureBlend*captureBlend*(3-2*captureBlend);
   roseAge=.57+clamp((t-C.captureStart-.12)/.4)*.08;
   roseY+=Math.round(18*(1-captureBlend));
   const pull=clamp(absorbProgress/.7);roseX+=Math.sign(bx-rx)*Math.min(65,Math.abs(bx-rx)*.45)*pull*pull*(3-2*pull);
  }
  roseY+=Math.round(Math.sin(t*34)*1.2);
 }
 // Horizontal teleport afterimages are composited; original PNGs remain byte-identical.
 const teleportAge=t-C.teleportStart;if(teleportAge>=0&&teleportAge<.22){for(let i=0;i<4;i++){pose(isBlue?'blue-rise':'blue-idle',bx0+(i+1)*17,FLOOR,9,{flip:bx0<rx,alpha:(1-teleportAge/.22)*(.22-i*.04)});}sheet(ctx,A,'blue-vanish',bx0,FLOOR,teleportAge);}
 if(t>=C.teleportEnd&&t<C.teleportEnd+.3)sheet(ctx,A,'blue-vanish',bx,FLOOR,t-C.teleportEnd);
 const fragmentProgress=isBlue?absorbProgress:clamp((absorbProgress-.42)/.58);
 if(((isBlue&&t>=C.captureStart+.36)||(!isBlue&&captured))&&t<C.absorbEnd){
  const m=A.motions[isBlue?'rose-capture':'rose-collapse'],im=A.images[m.frames[isBlue?5:7].file];
  const [sx,sy,sw,shh,dx,dy]=isBlue?[116,232,88,24,rx-76,FLOOR-24]:[128,214,36,18,rx-64,FLOOR-18];
  // The dropped crossbow follows Rose into Blue's hand a beat later, so it can return as the fused crossbow sigil.
  const bowProgress=clamp((fragmentProgress-.18)/.82);
  if(bowProgress===0)ctx.drawImage(im,sx,sy,sw,shh,dx,dy,sw,shh);
  else{const tx=bx+(flip?28:-28),ty=FLOOR-76;
   for(let y=0;y<shh;y+=4)for(let x=0;x<sw;x+=4){const lag=((x*5+y*11)%13)/60,q=clamp((bowProgress-lag)/(1-lag)),u=q*q*(3-2*q),px=dx+x,py=dy+y;
    ctx.globalAlpha=q>.88?(1-q)/.12:1;ctx.drawImage(im,sx+x,sy+y,4,4,Math.round(px+(tx-px)*u),Math.round(py+(ty-py)*u-26*Math.sin(q*Math.PI)),4,4);}
   ctx.globalAlpha=1;}
 }
 if(t<C.absorbEnd&&fragmentProgress===0){
  if(!isBlue&&captured&&captureBlend<1)pose('rose-collapse',rx,FLOOR-Math.round(22*clamp((t-C.captureStart)/.2)),9,{alpha:1-captureBlend});
  pose(roseId,roseX,roseY,roseAge,{alpha:!isBlue&&captured?captureBlend:1});
 }
 else if(t<C.absorbEnd){const progress=fragmentProgress,m=A.motions[roseId],im=A.images[m.frames.at(-1).file],bounds=m.frames.at(-1).bounds;
  // Pull small unscaled pixel pieces toward Blue's casting hand; never shrink the whole actor.
  for(let y=bounds[1];y<bounds[3];y+=5)for(let x=bounds[0];x<bounds[2];x+=5){const lag=((x*7+y*13)%17)/80,q=clamp((progress-lag)/(1-lag)),u=q*q*(3-2*q),sx=roseX+x-192,sy=roseY+y-232,tx=bx+(flip?28:-28),ty=FLOOR-76;
   ctx.globalAlpha=q>.88?(1-q)/.12:1;ctx.drawImage(im,x,y,5,5,Math.round(sx+(tx-sx)*u),Math.round(sy+(ty-sy)*u-20*Math.sin(q*Math.PI)),5,5);}
  ctx.globalAlpha=1;
 }
 const roarAge=t-C.roarStart;
 if(blueVisible&&roarAge>=0&&roarAge<PILLAR.end)roarPillar(ctx,roarAge,Math.round(blueX+sh),FLOOR,[Math.round(blueX+sh)+2,headY('blue-roar',FLOOR)+14],(c,dark)=>dark?silhouette(blueId,blueX,blueY,blueFlip):pose(blueId,blueX,blueY,blueAge,{flip:blueFlip}));
 else if(blueVisible)pose(blueId,blueX,blueY,blueAge,{flip:blueFlip});
 if(t>=C.captureStart&&t<C.absorbEnd){const a=t-C.captureStart,progress=t>=C.absorbStart?clamp((t-C.absorbStart)/(C.absorbEnd-C.absorbStart)):0;ctx.strokeStyle='#9d5bba';ctx.lineWidth=2;
  // Binding strings belong to Rose's body: they thin out as her pieces leave, so none hang in empty air.
  ctx.globalAlpha=1-clamp(fragmentProgress/.75);
  for(let i=0;i<2;i++){const y=roseY-80-i*20;ctx.beginPath();ctx.moveTo(roseX-12,y);ctx.bezierCurveTo(roseX-22,y-8,roseX+23,y-8,roseX+18,y+1);ctx.stroke();ctx.beginPath();ctx.moveTo(roseX+18,y);ctx.quadraticCurveTo((roseX+bx)/2,y-15+Math.sin(a*8+i)*7,bx+(flip?28:-28),FLOOR-76);ctx.stroke();}
  if(!isBlue){
   // A visible travelling stream connects the lifted body to Blue's casting hand.
   for(let i=0;i<5;i++){const u=(a*1.45+i/5)%1,x=roseX+(bx+(flip?28:-28)-roseX)*u,y=roseY-90+(14*u)-Math.sin(u*Math.PI)*10;
    ctx.fillStyle=i%2?'#a56cbe':'#503b70';ctx.fillRect(Math.round(x),Math.round(y),3+Math.round(u*4),2);}
  }
  ctx.globalAlpha=1;
  sheet(ctx,A,'ink-charge',bx+(flip?28:-28),FLOOR-76,(t-C.captureStart)%.4,{scale:.5+progress*.25});
 }
 say(ctx,C,t,d=>d.speaker==='rose'?{x:roseX,y:captured?roseY-156:roseY-(isBlue?130:89)}:{x:blueX,y:headY(blueId,blueY)-6});
 ctx.restore();const bars=32*(1-clamp((t-C.releaseStart)/.55));ctx.fillStyle='#07080c';ctx.fillRect(0,0,960,Math.round(bars));ctx.fillRect(0,540-Math.round(bars),960,Math.round(bars));
 ctx.font='700 11px Galmuri';ctx.fillStyle='#ddd5e4';ctx.fillText(`PLAYER ${initial.playerHp} / 100`,17,523);}
function startBattle(immediate=false){mode='battle';playing=true;input.clear();g.mode=Number($('#pattern').value);g.invincible=$('#invincible').checked;g.start();if(immediate)g.begin(g.mode||1);canvas.focus({preventScroll:true});}
function frame(now){const dt=Math.min(.05,(now-previous)/1000||0);previous=now;if(A){if(playing){if(mode==='battle'){let remain=dt*Number($('#speed').value);while(remain>0&&g.state==='active'){const step=Math.min(remain,1/120);g.step(step,input);remain-=step;}if(g.ko)chooseBranch(g.ko.branch,g.ko);}else{time=Math.min(A.timeline.branches[branch].duration,time+dt*Number($('#speed').value));if(time>=A.timeline.branches[branch].duration){if(EMBEDDED&&!reported){reported=true;parent.postMessage({type:'cutscene-done',branch},location.origin);}if($('#loop').checked)time=0;else playing=false;}}}
  $('#cues').hidden=mode==='battle';
  if(mode==='battle'){battle(ctx,A,g,$('#hitbox').checked);$('#status').textContent=`전투 · ${g.t.toFixed(1)}s · ${g.message}`;$('#seek').disabled=true;$('#clock').textContent=`전투 ${g.t.toFixed(1)}s`;document.querySelectorAll('[data-branch]').forEach(b=>b.classList.remove('selected'));}
  else{cutscene();const C=A.timeline.branches[branch];$('#status').textContent=C.cues.filter(c=>time>=c.time).at(-1)?.name??C.name;$('#seek').disabled=false;$('#seek').value=time;$('#clock').textContent=`${time.toFixed(2)} / ${C.duration.toFixed(2)}s`;document.querySelectorAll('#cues button').forEach((b,i)=>b.classList.toggle('active',time>=C.cues[i].time&&(i===C.cues.length-1||time<C.cues[i+1].time)));}
  $('#play').textContent=playing?'일시정지':'재생';}requestAnimationFrame(frame);}
try{A=await loadArt();g=new Battle(A.V,A.P);setupBranch();$('#status').textContent='준비 완료';
 $('#battle-start').onclick=()=>startBattle();$('#practice').onclick=()=>startBattle(true);$('#invincible').onchange=()=>g.invincible=$('#invincible').checked;$('#pattern').onchange=()=>g.mode=Number($('#pattern').value);
 document.querySelectorAll('[data-branch]').forEach(b=>b.onclick=()=>chooseBranch(b.dataset.branch));
 document.querySelectorAll('[data-ko]').forEach(b=>b.onclick=()=>{if(mode!=='battle'||g.state!=='active')startBattle();if(b.dataset.ko==='both'){g.rose.hp=g.blue.hp=0;g.absorb('rose');}else g.damageBoss(b.dataset.ko,100000,true);chooseBranch(g.ko.branch,g.ko);});
 $('#play').onclick=()=>playing=!playing;$('#restart').onclick=()=>mode==='battle'?startBattle():chooseBranch(branch,initial);$('#seek').oninput=e=>{time=Number(e.target.value);playing=false;};
 $('#export-dialogue').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(A.timeline,null,2)],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='stage2-absorption-dialogue.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
 document.addEventListener('keydown',e=>{if(/INPUT|TEXTAREA|SELECT/.test(e.target.tagName)||e.target.isContentEditable)return;const k=e.key.toLowerCase();if([' ','arrowleft','arrowright','shift'].includes(k))e.preventDefault();input.add(k.startsWith('arrow')?e.key:k);if(e.repeat)return;if(k==='p'){playing=!playing;return;}if(mode==='battle'){if(k===' ')g.jump();if(k==='shift')g.dash();if(k==='j')g.attack();if(k==='k')g.attack(true);}else{if(k===' ')playing=!playing;if(k==='arrowleft'||k==='arrowright'){time=clamp(time+(k==='arrowleft'?-.1:.1),0,A.timeline.branches[branch].duration);playing=false;}}});
 document.addEventListener('keyup',e=>{input.delete(e.key);input.delete(e.key.toLowerCase());});window.addEventListener('blur',()=>{input.clear();playing=false;});
 window.stage2QA={get game(){return g;},get art(){return A;},get mode(){return mode;},get time(){return time;},get branch(){return branch;},pause(){playing=false;},start:startBattle,cutscene:chooseBranch,seek(t){mode='cutscene';time=clamp(t,0,A.timeline.branches[branch].duration);playing=false;},step(dt){g.step(dt,input);},render(){mode==='battle'?battle(ctx,A,g,$('#hitbox').checked):cutscene();},sampleCutscene:cutscene};
 if(Q.has('battle')){startBattle();if(Q.has('playerX'))g.player.x=Number(Q.get('playerX'));if(Q.has('hp'))g.player.hp=Number(Q.get('hp'));g.rose.x=64;g.blue.x=920;}
 const query=new URLSearchParams(location.search),scene=query.get('scene');if(A.timeline.branches[scene]){chooseBranch(scene);const at=Number(query.get('at'));if(Number.isFinite(at)&&at>0)time=clamp(at,0,A.timeline.branches[scene].duration);}
 requestAnimationFrame(frame);
}catch(e){$('#error').hidden=false;$('#error').textContent=`불러오지 못한 에셋이 있습니다.\n${e.message}`;console.error(e);}
