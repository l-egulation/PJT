import {platforms} from '../phase1-playtest/rules.mjs';
const $=s=>document.querySelector(s),c=$('#screen'),x=c.getContext('2d');
let gameActive=false,battleReady=false,pendingBattle=false;
const battle=$('#battle');
function startBattle(){pendingBattle=true;if(!battleReady){$('#status').textContent='전투 에셋 준비 중…';battle.contentWindow.postMessage({type:'boss-preview-probe'},location.origin);return;}pendingBattle=false;gameActive=true;playing=false;c.style.display='none';battle.style.display='block';battle.contentWindow.postMessage({type:'boss-preview-start',spawn:{x:448,y:236}},location.origin);battle.focus();$('#play').textContent='전투 일시정지';$('#status').textContent='1페이즈 · 중앙 장치 HP 1500 · A/D 이동, Space 점프, Shift 대시, Q 기본검 2타, W 강베기';}
function showCutscene(){pendingBattle=false;gameActive=false;battle.contentWindow.postMessage({type:'boss-preview-stop'},location.origin);battle.style.display='none';c.style.display='block';}
window.addEventListener('message',e=>{if(e.origin!==location.origin||e.source!==battle.contentWindow)return;if(e.data?.type==='phase1-ready'){battleReady=true;if(pendingBattle)startBattle();}if(e.data?.type==='phase1-state'&&gameActive){$('#status').textContent=e.data.text;$('#play').textContent=e.data.text.startsWith('전투 일시정지')?'전투 재생':'전투 일시정지';}});
$('#fight').onclick=startBattle;
const END=22.5,ground=444;let t=0,playing=false,ready=false,last=performance.now();
const im={};const clamp=v=>Math.max(0,Math.min(1,v));const ease=v=>{v=clamp(v);return v*v*(3-2*v)};const mix=(a,b,u)=>a+(b-a)*u;
const load=src=>new Promise((yes,no)=>{const a=new Image;a.onload=()=>yes(a);a.onerror=()=>no(Error('이미지를 읽을 수 없어: '+src));a.src=src});
const dialog=[
 {a:3.25,b:5.9,who:'Rose',text:'야. 마법 똑바로 쓰랬지?',color:'#d989a7'},
 {a:5.9,b:8.9,who:'Rose',text:'길목 하나 못 막아서 여기까지 왔잖아.',color:'#d989a7'},
 {a:8.9,b:11.8,who:'Blue',text:'나도 계속 쐈는데… 자꾸 피해 가서…',color:'#8caed6'},
 {a:11.8,b:14.1,who:'Rose',text:'뭐 해! 왔잖아. 준비 안 해?',color:'#d989a7'},
 {a:14.1,b:15.65,who:'Blue',text:'으, 응… 지금 할게.',color:'#8caed6'}];
function crop(a){const s=document.createElement('canvas');s.width=a.width;s.height=a.height;const q=s.getContext('2d');q.drawImage(a,0,0);const d=q.getImageData(0,0,s.width,s.height).data;let l=s.width,top=s.height,r=-1,b=-1;for(let y=0;y<s.height;y++)for(let xx=0;xx<s.width;xx++)if(d[(y*s.width+xx)*4+3]>128){l=Math.min(l,xx);r=Math.max(r,xx);top=Math.min(top,y);b=Math.max(b,y)}if(r<l)throw Error('플레이어 투명 영역이 비어 있어');const out=document.createElement('canvas');out.width=r-l+1;out.height=b-top+1;out.getContext('2d').drawImage(a,l,top,out.width,out.height,0,0,out.width,out.height);return out}
function sprite(a,px,foot,h,flip=false,alpha=1,angle=0){x.save();x.globalAlpha=clamp(alpha);x.translate(Math.round(px),Math.round(foot));if(flip)x.scale(-1,1);x.rotate(angle);const w=a.width*h/a.height;x.drawImage(a,Math.round(-w/2),-Math.round(h),Math.round(w),Math.round(h));x.restore()}
function ellipse(px,py,rx,ry,color,width=1){x.strokeStyle=color;x.lineWidth=width;x.beginPath();x.ellipse(Math.round(px),Math.round(py),rx,ry,0,0,Math.PI*2);x.stroke()}
function tiled(px,py,w,alpha=1){x.save();x.globalAlpha*=clamp(alpha);for(let i=0;i<w;i+=96){const n=Math.min(96,w-i);x.drawImage(im.tile,0,0,n,16,Math.round(px+i),Math.round(py),n,16)}x.restore()}
function stoneFloor(alpha){
 x.save();x.globalAlpha=clamp(alpha);
 const tops=[600,602,604,606,608];
 for(let col=0;col<30;col++)for(let row=0;row<3;row++){
  const id=row<2?tops[(col*7+Math.floor(col/3))%tops.length]+row:0;
  x.drawImage(im.floor,(id%32)*32,Math.floor(id/32)*32,32,32,col*32,ground+row*32,32,32);
 }
 x.restore();
}
function speech(d,speakerX,headY){
 x.save();x.font='700 12px Galmuri';x.textBaseline='top';
 const words=d.text.split(' '),lines=[];let line='';
 for(const word of words){const next=line?line+' '+word:word;if(line&&x.measureText(next).width>184){lines.push(line);line=word}else line=next}if(line)lines.push(line);
 const w=Math.ceil(Math.max(...lines.map(v=>x.measureText(v).width)))+20,h=lines.length*15+18,bx=Math.round(Math.max(8,Math.min(960-w-8,speakerX-w/2))),by=Math.round(Math.max(53,headY-h-10));
 function panel(l,top,width,height,color){x.fillStyle=color;x.fillRect(l+4,top,width-8,height);x.fillRect(l+2,top+2,width-4,height-4);x.fillRect(l,top+4,width,height-8)}
 panel(bx,by,w,h,'#1f1b19');panel(bx+2,by+2,w-4,h-4,'#fbf9f2');
 const tail=Math.round(Math.max(bx+13,Math.min(bx+w-13,speakerX))),bottom=by+h;
 x.fillStyle='#1f1b19';for(let n=0;n<6;n++)x.fillRect(tail-7+n,bottom-2+n*2,14-n*2,2);
 x.fillStyle='#fbf9f2';for(let n=0;n<4;n++)x.fillRect(tail-5+n,bottom-2+n*2,10-n*2,2);
 const labelW=Math.ceil(x.measureText(d.who).width)+12;x.fillStyle='#1f1b19';x.fillRect(bx+10,by-13,labelW,15);x.fillStyle='#fbf9f2';x.fillText(d.who,bx+16,by-11);
 let remaining=Math.max(0,Math.floor((t-d.a)*30));x.fillStyle='#1f1b19';
 for(let i=0;i<lines.length;i++){const visible=lines[i].slice(0,remaining);x.fillText(visible,bx+10,by+9+i*15);remaining=Math.max(0,remaining-lines[i].length-1)}
 x.restore();
}
function ninja(a,px,foot,h,age,exiting=false,flip=false){const u=clamp(age/.18);for(let i=3;i>0;i--)sprite(a,px+(exiting?-1:1)*(i*11+(1-u)*20),foot,h,flip,(exiting?1-u:u)*.16/(i*.5),0);sprite(a,px+(exiting?-u*18:(1-u)*22),foot,h,flip,exiting?1-u:u)}
function bubble(px,py,age,alpha){x.save();x.globalAlpha=clamp(alpha);const r=43;const fill=x.createRadialGradient(px-14,py-15,3,px,py,r);fill.addColorStop(0,'#9faeff12');fill.addColorStop(.8,'#7183d725');fill.addColorStop(1,'#8b97e640');x.fillStyle=fill;x.beginPath();x.arc(Math.round(px),Math.round(py),r,0,Math.PI*2);x.fill();ellipse(px,py,r,r,'#b0b4f0c0',2);ellipse(px,py,r-3,r-3,'#6c76b77a',1);x.strokeStyle='#e1e3ffcf';x.lineWidth=2;x.beginPath();x.arc(px,py,r-7,Math.PI*1.08,Math.PI*1.4);x.stroke();for(let i=0;i<4;i++){const a=age*1.5+i*Math.PI/2;x.fillStyle='#b3b7ee';x.fillRect(Math.round(px+Math.cos(a)*(r+5)),Math.round(py+Math.sin(a)*(r+5)),2,2)}x.restore()}
function magic(blueX){const grow=ease((t-14.6)/1);x.save();x.globalAlpha=(1-ease((t-18.6)/.7))*grow;ellipse(blueX-45,ground-115,13+Math.sin(t*5)*2,8,'#899ad5b0',2);x.restore()}
function frame(){x.clearRect(0,0,960,540);x.imageSmoothingEnabled=false;x.drawImage(im.bg,0,0,960,540);
 const switchU=ease((t-16)/1.9);stoneFloor(1-switchU);
 for(let i=0;i<platforms.length;i++){const p=platforms[i],u=ease((t-16.2-i*.075)/.65);tiled(p.x,p.y+(1-u)*13,p.w,u);if(u>0&&u<1){x.fillStyle=`rgba(156,143,225,${Math.sin(u*Math.PI)*.5})`;x.fillRect(p.x,p.y,p.w,3)}}
 const deviceU=ease((t-17.3)/.65);sprite(im.device,480,236,96,false,deviceU);
 let px=mix(210,480,ease(t/1.8)),py=ground;const lift=ease((t-16.5)/2.9);px=mix(px,448,lift);py=mix(ground,236,lift);
 const visible=ease((t-2.3)/.18),exit=ease((t-20.4)/.18);let roseX=252+ease((t-5.9)/.4)*13,blueX=730+ease((t-8.9)/.35)*12;
 if(t>=2.3&&t<20.58){
  if(t<2.48){ninja(im.rose,roseX,ground,120,t-2.3,false,true);ninja(im.blue,blueX,ground,139,t-2.3)}
  else if(t>20.4){ninja(im.rose,roseX,ground,120,t-20.4,true,true);ninja(im.blueCast,blueX,ground,170,t-20.4,true)}
  else {sprite(im.rose,roseX,ground,120,t<11.8,visible*(1-exit),t>=5.9&&t<8.9?-.035:0);
   const shy=t>=8.9&&t<14.3,flinch=t>=14.1&&t<14.5?Math.sin((t-14.1)*20)*2:0;
   if(t>=14.7){sprite(im.blueCast,blueX,ground,170,false,visible,t<15.2?-.025:0)}
   else sprite(im.blue,blueX+flinch,ground+(shy?2:0),139,false,visible,shy?-.06:0);
  }
 }
 if(t>=14.6&&t<19.3)magic(blueX);
 // Player is drawn after the device; no artificial frame-by-frame resizing.
 const bob=t<1.8?Math.sin(t*18)*1.3:0;sprite(im.player,px,py-bob,64,t>=3.25&&t<11.8&&t%4>2.7);
 if(t>=15.8&&t<20.3){const a=Math.min(ease((t-15.8)/.4),1-ease((t-19.6)/.7));bubble(px,py-32,t-15.8,a)}
 const bars=ease(t/.38)*(1-ease((t-20.7)/.7));x.fillStyle='#05070b';x.fillRect(0,0,960,Math.round(32*bars));x.fillRect(0,540-Math.round(32*bars),960,Math.round(32*bars));
 const d=dialog.find(v=>t>=v.a&&t<v.b);if(d)speech(d,d.who==='Rose'?roseX:blueX,ground-(d.who==='Rose'?120:139));
 if(t>21.3){x.save();x.globalAlpha=ease((t-21.3)/.4);x.fillStyle='#e2caa4';x.font='bold 22px system-ui';x.textAlign='center';x.fillText('PHASE 1',480,56);x.font='16px system-ui';x.fillStyle='#e7e7df';x.fillText('중앙 장치를 파괴하세요',480,81);x.restore()}
 $('#clock').textContent=t.toFixed(1)+' / 22.5초';$('#time').value=String(t);
 $('#status').textContent=t<2.3?'① 입장 · 검은 띠와 중앙 자동 이동':t<3.25?'② 양옆 등장 · 짧은 수평 잔상':t<14.1?'③ 말다툼 · Rose의 추궁과 Blue의 위축':t<15.8?'④ Blue 시전 · 허둥대며 마법 준비':t<19.4?'⑤ 방울로 감싸 상승 · 바닥 소멸과 발판 생성':t<21.3?'⑥ 안착 · 방울 해제 · 남매 퇴장':'⑦ 조작 복귀 · 1페이즈 시작 지점';
}
function play(){if(gameActive){battle.contentWindow.postMessage({type:'boss-preview-pause'},location.origin);return;}if(!ready)return;if(t>=END)t=0;playing=!playing;$('#play').textContent=playing?'일시정지':'재생';last=performance.now()}
$('#play').onclick=play;$('#restart').onclick=()=>{showCutscene();t=0;playing=true;$('#play').textContent='일시정지';last=performance.now()};$('#time').oninput=e=>{showCutscene();t=Number(e.target.value);playing=false;$('#play').textContent='재생';if(ready)frame()};
document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{showCutscene();t=Number(b.dataset.time);playing=false;$('#play').textContent='재생';frame()});
document.addEventListener('keydown',e=>{if(e.code==='Space'&&!['INPUT','SELECT','BUTTON'].includes(e.target.tagName)){e.preventDefault();play()}});
function loop(now){const dt=Math.min(.06,(now-last)/1000);last=now;if(ready&&!gameActive){if(playing){t=Math.min(END,t+dt*Number($('#speed').value));if(t>=END){playing=false;startBattle();}}if(!gameActive)frame()}requestAnimationFrame(loop)}
try{const assets={bg:'../map-preview/assets/D.png',player:'../map-preview/assets/approved-player.png',rose:'../unity-phase1/rose-standing.png',blue:'../unity-phase1/blue-standing.png',device:'../unity-phase1/machine.png',tile:'../unity-phase1/tile.png',floor:'assets/waterway-floor-atlas.png',bluePalm:'../unity-phase1/blue-palm.png'};await Promise.all(Object.entries(assets).map(async([key,url])=>im[key]=await load(url)));await document.fonts.load('700 12px Galmuri');im.player=crop(im.player);const cast=document.createElement('canvas');cast.width=320;cast.height=200;cast.getContext('2d').drawImage(im.bluePalm,0,0,320,200,0,0,320,200);im.blueCast=crop(cast);ready=true;$('#play').disabled=false;$('#play').textContent='재생';frame();window.cutscenePreview={seek(v){t=Math.max(0,Math.min(END,v));playing=false;frame()},get time(){return t},get ready(){return ready}};requestAnimationFrame(loop)}catch(e){$('#error').textContent=e.message}
