const canvas=document.getElementById('screen'),ctx=canvas.getContext('2d'),status=document.getElementById('status'),m=new Journey(),keys=new Set(),A={};let ready=false,last=0;
function box(x,y,w,h,c){ctx.fillStyle=c;ctx.fillRect(Math.round(x),Math.round(y),w,h);}
function img(n,x,y,w,h){ctx.drawImage(A[n],Math.round(x),Math.round(y),w||A[n].width,h||A[n].height);}
function text(t,x,y,size=15,c='#e9e3c9'){ctx.fillStyle=c;ctx.font=size+'px system-ui';ctx.textAlign='left';ctx.fillText(t,x,y);}
function firstTile(name,x,y){img(name,x,y,32,32);}
function terrain(start,end,floor){for(let x=start;x<end;x+=32){const f=floor(x);for(let y=f;y<640;y+=32)firstTile('s1-core',x,y);firstTile('s1-top',x,f);}}
function chapterOne(){
 // World overlay is opaque indoors; the city is visible only through its openings.
 img('s1-sky',1600+(m.camera-2000)*.96,-100+m.cameraY*.96);
 img('s1-city',2032+(m.camera-2000)*.85,-64+m.cameraY*.92);
 ctx.drawImage(A['scene-world'],0,0,4730,1,0,-32,4730,32);
 img('scene-world',0,0,4730,960);
 // Right wall opens only after the boss defeat dust has settled.
 for(let y=256;y<448;y+=32)for(let x=960;x<992;x+=32){let missing=m.phase!=='fight'&&m.phase!=='breaking';if(m.phase==='breaking'&&m.timer>.45)missing=((y/32+x/32)%3<m.timer*1.7);if(!missing)firstTile('s1-core',x,y);}
 if(m.phase==='fight'||m.phase==='breaking'){ctx.strokeStyle='#443e31';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(978,303);ctx.lineTo(969,350);ctx.lineTo(991,380);ctx.lineTo(974,435);ctx.stroke();}
 // The source boss room spans x0..960, with entry and ceiling just outside the camera.
 if(m.phase==='fight'||m.phase==='breaking'){img('boss-assembly',0,0);if(m.phase==='fight'){box(396,196,168,5,'#39312e');box(398,197,164*m.hp/3,3,'#be7754');}}

 // Empty staging space before the dug slope, reserved for the user's future dialogue.
 if(document.getElementById('anchor').checked){box(2990,318,6,6,'#f4dc8e');text('말풍선 배치 자리',2930,303,15,'#fff1bd');}
 // Grate is a real blocker at the end of the transition, not the chapter-start trigger itself.
 const gx=Journey.GATE,gy=544;
 box(gx-30,gy-120,60,10,'#475951');box(gx-30,gy-110,7,110,'#40534b');box(gx+27,gy-110,7,110,'#40534b');
 if(m.grateHp>0){
  const bars=m.grateHp===3?5:m.grateHp===2?4:2;
  for(let i=0;i<bars;i++){const bend=m.grateHp<3?(i%2?5:-5):0;box(gx-24+i*11+bend,gy-112,5,m.grateHp===1?64:112,m.grateFlash>0?'#e4e4c1':'#6a7d78');}
  box(gx-26,gy-102,54,6,m.grateFlash>0?'#f2edc8':'#536964');box(gx-26,gy-12,54,6,'#536964');
  if(m.x>gx-190)text('J 공격 · 철창 '+m.grateHp+'/3',gx-160,gy-136,16,'#eee4be');
 }else if(m.grateBreaking>0){for(let i=0;i<5;i++)box(gx+18+(1-m.grateBreaking/.65)*i*13,gy-98+i*18,5,30,'#6b807a');}
 else{box(gx+10,gy-10,35,5,'#677a72');if(m.x>gx-190)text('철창 파괴 · 오른쪽으로 진입 →',gx-210,gy-136,16,'#d8e7d0');}
}

function chapterTwo(){
 for(let y=-32;y<540;y+=480)for(let x=0;x<1920;x+=512)img('s2-wall',x,y,512,480);
 img('s2-portal',360,-4,346,447);img('s2-arch',360,-4,346,447);img('s2-pipe',1260,115,96,288);
 for(let x=0;x<1920;x+=32){img('s2-top',x,448,32,32);img('s2-inner',x,480,32,32);box(x,512,32,100,'#202c2d');}
 box(0,288,50,160,'#0c171b');
}
function draw(){ctx.imageSmoothingEnabled=false;box(0,0,960,540,'#111b21');ctx.save();ctx.translate(-Math.round(m.camera),14-Math.round(m.cameraY));if(m.scene===1)chapterOne();else chapterTwo();
 ctx.save();ctx.translate(Math.round(m.x),Math.round(m.y));ctx.scale(m.facing,1);img('player',-80,-137,192,160);ctx.restore();
 for(const p of m.dust){ctx.globalAlpha=Math.min(1,p.life*.65);box(p.x,p.y,Math.round(p.size),Math.round(p.size*.8),p.rock?'#7f7b68':'#b7ad90');}ctx.globalAlpha=1;ctx.restore();
 if(m.phase==='entering'){const opacity=m.timer<.4?m.timer/.4:(.8-m.timer)/.4;box(0,0,960,540,'rgba(4,13,17,'+Math.max(0,Math.min(1,opacity))+')');}
 if(m.scene===2&&m.timer<2.5){ctx.globalAlpha=Math.min(1,Math.max(0,2.5-m.timer));text('2챕터 · 지하수로',370,90,24);ctx.globalAlpha=1;}
 let label=m.phase==='fight'?'보스방 · 보스를 처치해 오른쪽 길을 여세요':m.phase==='breaking'?'오른쪽 벽 붕괴 · 먼지가 걷히는 중':m.phase==='entering'?'철창 너머 · 2챕터 진입 중':m.scene===2?'2챕터 · 지하수로':m.x<2650?'1챕터 · 석조 통로를 지나 바깥으로':m.x<3130?'1챕터 · 도시 외곽 / 땅 앞 대사 자리':m.x<4090?'다시 굴 안 · 수로 입구로 이동':m.grateHp>0?'수로 입구 · 가까이에서 J로 철창 파괴':'수로 입구 · 열린 철창 너머로 이동';status.textContent=label;document.getElementById('defeat').disabled=m.phase!=='fight';
}
function frame(t){const dt=last?Math.min(.05,(t-last)/1000):0;last=t;if(ready){m.step(dt,{left:keys.has('ArrowLeft')||keys.has('KeyA'),right:keys.has('ArrowRight')||keys.has('KeyD')});draw();}requestAnimationFrame(frame);}
function reset(){keys.clear();m.reset();draw();canvas.focus();}document.getElementById('defeat').onclick=()=>{m.defeat();canvas.focus();};document.getElementById('reset').onclick=reset;canvas.onpointerdown=()=>canvas.focus();
for(const b of document.querySelectorAll('[data-go]'))b.onclick=()=>{m.reset();m.preview(Number(b.dataset.go));keys.clear();canvas.focus();};
window.addEventListener('keydown',e=>{if(['KeyA','KeyD','ArrowLeft','ArrowRight','Space','KeyJ','KeyR'].includes(e.code)){e.preventDefault();if(!e.repeat){if(e.code==='KeyJ')m.attack();if(e.code==='Space')m.step(0,{jump:true});if(e.code==='KeyR')reset();}keys.add(e.code);}});window.addEventListener('keyup',e=>keys.delete(e.code));window.addEventListener('blur',()=>keys.clear());
document.getElementById('attack').onclick=()=>{m.attack();canvas.focus();};
for(const b of document.querySelectorAll('[data-key]')){b.onpointerdown=e=>{e.preventDefault();b.setPointerCapture(e.pointerId);keys.add(b.dataset.key);};b.onpointerup=b.onpointercancel=()=>keys.delete(b.dataset.key);}
Promise.all(Object.entries(ASSET_URLS).map(([k,v])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{A[k]=im;resolve();};im.onerror=reject;im.src=v;}))).then(()=>{ready=true;draw();requestAnimationFrame(frame);}).catch(()=>status.textContent='이미지를 읽지 못했습니다');
