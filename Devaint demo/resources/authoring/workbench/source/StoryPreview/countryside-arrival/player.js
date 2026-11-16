const canvas=document.getElementById('game'),ctx=canvas.getContext('2d'),stage=document.getElementById('stage');
const statusEl=document.getElementById('status'),pauseBtn=document.getElementById('pause'),replayBtn=document.getElementById('replay');
let images={},ready=false,paused=false,time=0,last=0,completeLine=-1,lastPhase='';
let flow='library',transitionStart=0,heroX=230,facing=-1,moving=false,showOutro=true;
const held=new Set();
const labels={establish:'시골 마을',run:'할머니집으로',brake:'문 앞 도착',prepare:'숨 고르기',breath:'헐떡임'};
function load(src){return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(new Error('이미지를 불러오지 못했습니다.'));im.src=src;});}
function nine(im,x,y,w,h,b,d=b){let sw=im.width,sh=im.height;let sx=[0,b,sw-b],sy=[0,b,sh-b],szx=[b,sw-2*b,b],szy=[b,sh-2*b,b],dx=[x,x+d,x+w-d],dy=[y,y+d,y+h-d],dwx=[d,w-2*d,d],dhy=[d,h-2*d,d];for(let j=0;j<3;j++)for(let i=0;i<3;i++)ctx.drawImage(im,sx[i],sy[j],szx[i],szy[j],dx[i],dy[j],dwx[i],dhy[j]);}
function portrait(im,x,y,dir=1){ctx.save();ctx.translate(Math.round(x),y);ctx.scale(dir,1);ctx.drawImage(im,-64,-120);ctx.restore();}
function panel(text,elapsed,key,cinematic){
 const x=cinematic?96:60,y=cinematic?390:445,w=cinematic?768:840,h=cinematic?68:84;
 nine(images.dialogue,x,y,w,h,20,15);nine(images.nameplate,x+24,y-27,156,38,9,7);
 ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillStyle='#9ed3df';ctx.font='bold 16px system-ui';ctx.fillText('주인공',x+102,y-7);
 const count=completeLine===key?text.length:Math.max(0,Math.floor(elapsed/65));
 const lines=text.slice(0,count).split('\n');ctx.fillStyle='#f7eed8';ctx.textAlign='left';ctx.font='20px system-ui';
 for(let i=0;i<lines.length;i++)ctx.fillText(lines[i],x+27,y+h/2+(i-(lines.length-1)/2)*23);
}
function enterVillage(){if(!ready||flow!=='library')return;flow='transition';transitionStart=time;showOutro=false;held.clear();moving=false;completeLine=-1;}
function draw(){
 const cut=flow==='library'?null:Cinematic.sample(time-transitionStart);
 ctx.imageSmoothingEnabled=false;ctx.fillStyle='#0b0d12';ctx.fillRect(0,0,960,540);
 let phase;
 if(!cut||cut.scene==='library'){
  ctx.fillStyle='#1d3043';ctx.fillRect(0,0,960,540);ctx.drawImage(images.library,0,0);portrait(moving?images.walk[Math.floor(time/65)%12]:images.idle,heroX,312,facing);
  ctx.font='16px system-ui';ctx.textAlign='left';ctx.textBaseline='middle';ctx.fillStyle='#e3d7ae';
  if(!cut){
   if(showOutro)panel(RECORD_OUTRO,time,'outro',false);
   else{ctx.fillText('← 마을로 출발',48,343);if(heroX<130)ctx.fillText('E · 도서관 나가기',48,374);}
  }
  phase=cut?'도서관 나가기':showOutro?'할머니 기록 확인 직후':'왼쪽 통로로 출발';
 }else{
  const s=Arrival.sample(cut.arrivalTime),offsetY=-24;
  ctx.drawImage(images.background,0,offsetY);ctx.fillStyle='#14140e88';ctx.fillRect(s.x-13,386+offsetY,26,3);
  portrait(images[s.clip][s.frame],s.x,s.y+offsetY);
  if(s.line)panel(s.line,cut.arrivalTime-s.lineStart,s.lineStart,true);
  phase=cut.done?labels[s.phase]:'시골 마을로 전환';
 }
 if(cut){ctx.fillStyle='#000';const bar=Math.round(cut.bars);ctx.fillRect(0,0,960,bar);ctx.fillRect(0,540-bar,960,bar);if(cut.fade>0){ctx.globalAlpha=cut.fade;ctx.fillRect(0,0,960,540);ctx.globalAlpha=1;}}
 if(phase!==lastPhase){statusEl.textContent=phase;lastPhase=phase;}
}
function tick(now){if(!last)last=now;const dt=Math.min(now-last,80);if(ready&&!paused&&!document.hidden){time+=dt;if(flow==='library'&&!showOutro){const direction=(held.has('d')||held.has('arrowright')?1:0)-(held.has('a')||held.has('arrowleft')?1:0);moving=direction!==0;if(moving){facing=direction;heroX=Math.max(45,Math.min(880,heroX+direction*dt*.22));if(heroX<=65)enterVillage();}}}last=now;if(ready)draw();requestAnimationFrame(tick);}
function reset(){time=0;paused=false;flow='library';heroX=230;facing=-1;moving=false;showOutro=true;held.clear();completeLine=-1;lastPhase='';last=performance.now();pauseBtn.textContent='일시정지 · Space';draw();}
function toggle(){if(!ready)return;paused=!paused;last=performance.now();pauseBtn.textContent=paused?'계속 재생 · Space':'일시정지 · Space';}
let playScreen=false,nativeSession=false,screenRequest=false;
function fit(){if(playScreen){const scale=Math.min(innerWidth/960,innerHeight/540);canvas.style.width=(960*scale)+'px';canvas.style.height=(540*scale)+'px';}else{canvas.style.width='960px';canvas.style.height='auto';}}
function setPlayScreen(on){playScreen=on;stage.classList.toggle('screen-mode',on);document.body.classList.toggle('playing-screen',on);document.getElementById('fullscreen').textContent=on?'전체화면 닫기 · Esc':'전체화면 · F';fit();}
async function fullscreen(){
 if(screenRequest)return;
 if(playScreen){setPlayScreen(false);if(document.fullscreenElement===stage){try{await document.exitFullscreen();}catch(e){}}return;}
 setPlayScreen(true);screenRequest=true;
 try{if(stage.requestFullscreen&&document.fullscreenEnabled!==false)await stage.requestFullscreen();}
 catch(e){/* Embedded browsers can deny native fullscreen; viewport play mode remains usable. */}
 finally{screenRequest=false;fit();}
}
replayBtn.onclick=reset;pauseBtn.onclick=toggle;document.getElementById('fullscreen').onclick=fullscreen;document.getElementById('exit').onclick=fullscreen;
document.addEventListener('fullscreenchange',()=>{if(document.fullscreenElement===stage){nativeSession=true;setPlayScreen(true);}else if(nativeSession){nativeSession=false;setPlayScreen(false);}fit();});window.addEventListener('resize',fit);document.addEventListener('visibilitychange',()=>{last=performance.now();});
window.addEventListener('keydown',e=>{if(e.repeat||e.ctrlKey||e.metaKey||e.altKey)return;const k=e.key.toLowerCase();if(k==='escape'&&playScreen){e.preventDefault();fullscreen();return;}if(k==='f'){e.preventDefault();fullscreen();}if(!ready)return;if(['a','d','arrowleft','arrowright'].includes(k)){e.preventDefault();held.add(k);}if(k==='r')reset();if(e.code==='Space'&&e.target.tagName!=='BUTTON'){e.preventDefault();toggle();}if(k==='e'&&!paused){if(flow==='library'){if(showOutro){if(completeLine==='outro'||time>=RECORD_OUTRO.length*65){showOutro=false;moving=false;}else completeLine='outro';}else if(heroX<130)enterVillage();}else completeLine=Arrival.sample(Cinematic.sample(time-transitionStart).arrivalTime).lineStart;}});
window.addEventListener('keyup',e=>held.delete(e.key.toLowerCase()));window.addEventListener('blur',()=>{held.clear();moving=false;});
if(document.getElementById('transition-preview'))document.getElementById('transition-preview').onclick=()=>{if(!ready)return;reset();heroX=70;enterVillage();};
// Called after the record sequence is complete and the player crosses the library exit.
window.StoryArrival={enterFromLibraryExit:enterVillage};
window.addEventListener('story:library-exit',enterVillage);
(async()=>{try{for(const [key,value]of Object.entries(ASSETS)){images[key]=Array.isArray(value)?await Promise.all(value.map(load)):await load(value);}ready=true;document.getElementById('load').hidden=true;replayBtn.disabled=pauseBtn.disabled=false;last=performance.now();}catch(e){document.getElementById('load').textContent=e.message;statusEl.textContent='불러오기 실패';}})();
requestAnimationFrame(tick);
