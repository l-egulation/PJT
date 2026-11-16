const $=s=>document.querySelector(s),load=p=>new Promise((ok,no)=>{const i=new Image();i.onload=()=>ok(i);i.onerror=()=>no(new Error('Image failed: '+p));i.src=p;});
window.rainSim={ready:false,errors:[]};
try{
 const D=await (await fetch('sim.json')).json();const U=D.pixelsPerUnit,F=D.floorY,R=D.rain;
 const [room,player,idle]=await Promise.all(['../assets/room.png','../assets/player.png','../assets/fused-idle.png'].map(load));
 const sheet={};for(const [k,s] of Object.entries(D.sheets))sheet[k]={...s,img:await load(s.file)};
 for(const c of D.candidates)c.imgs=await Promise.all(c.frameUrls.map(u=>load(D.bodyFrames+u)));
 const step=R.landingSpan/(R.count-1);
 const arrows=fromRight=>Array.from({length:R.count},(_,i)=>{const k=fromRight?R.count-1-i:i;return{x:(R.firstCentreUnits+k*R.pitchUnits)*U,spawn:R.tell+i*step,blast:R.tell+R.fall+R.landingSpan+R.fuse+i*step};});
 function sheetFrame(s,t){const ms=t*1000,total=s.durationsMs.reduce((a,b)=>a+b,0);let u=s.loop?ms%total:Math.min(ms,total-0.001);for(let i=0;i<s.frames;i++){if(u<s.durationsMs[i])return i;u-=s.durationsMs[i];}return s.frames-1;}
 function drawSheet(ctx,name,t,x,y,rot=0){const s=sheet[name],f=sheetFrame(s,t),sx=f%s.columns*s.cellWidth,sy=Math.floor(f/s.columns)*s.cellHeight;ctx.save();ctx.translate(x,y);ctx.rotate(rot);ctx.drawImage(s.img,sx,sy,s.cellWidth,s.cellHeight,-s.pivotX,-s.pivotY,s.cellWidth,s.cellHeight);ctx.restore();}
 function bodyFrame(c,t){let ms=t*1000;if(ms>=c.totalMs)return c.imgs.length-1;for(let i=0;i<c.durationsMs.length;i++){if(ms<c.durationsMs[i])return i;ms-=c.durationsMs[i];}return c.imgs.length-1;}
 function phase(t){if(t<R.tell)return'경고';const a=arrows(false);const land=a[R.count-1].spawn+R.fall;if(t<land)return`낙하 ${a.filter(x=>t>=x.spawn).length}/12`;if(t<a[0].blast)return'박힘 · 폭발 대기';if(t<a[R.count-1].blast+R.explosionActive)return`폭발 ${a.filter(x=>t>=x.blast).length}/12`;return'끝';}
 function draw(card){const {ctx,c,state}=card,t=state.time,fromRight=$('#side').value==='right',bx=D.positions.bossX,px=D.positions.playerX;
  ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);ctx.drawImage(player,px-192,F-232);
  const fi=bodyFrame(c,t);ctx.drawImage(c.imgs[fi],bx-192,F-232);
  const list=arrows(fromRight);
  if(t<D.warning.until){const x=list[0].x,g=ctx.createRadialGradient(x,F-6,1,x,F-6,16);g.addColorStop(0,'rgba(196,150,255,.95)');g.addColorStop(1,'rgba(155,99,224,0)');ctx.fillStyle=g;ctx.fillRect(x-14,F-28,28,30);}
  const top=F-D.arenaTopUnitsAboveFloor*U,landY=F-R.landedCentreAboveFloorUnits*U;
  for(const a of list){if(t<a.spawn||t>=a.blast)continue;const u=Math.min(1,(t-a.spawn)/R.fall),y=top+(landY-top)*u;drawSheet(ctx,'p3-arrow-flight',t-a.spawn,a.x,y,Math.PI/2);}
  for(const a of list){const age=t-a.blast;if(age<0)continue;const s=sheet['p3-pillar'],dur=s.durationsMs.reduce((x,y)=>x+y,0)/1000;if(age<dur)drawSheet(ctx,'p3-pillar',age,a.x,F);}
  if($('#hit').checked){ctx.strokeStyle='rgba(255,80,80,.9)';ctx.lineWidth=1;for(const a of list){if(t>=a.spawn&&t<a.blast){const u=Math.min(1,(t-a.spawn)/R.fall),y=top+(landY-top)*u;ctx.strokeRect(a.x-.15*U,y-.5*U,.3*U,U);}if(t>=a.blast&&t<a.blast+R.explosionActive){ctx.strokeRect(a.x-R.explosionWidthUnits*U/2,F-R.explosionHeightUnits*U,R.explosionWidthUnits*U,R.explosionHeightUnits*U);}}}
  card.info.textContent=`${(t).toFixed(2)}초 · ${phase(t)} · 몸 원본 ${String(c.sourceFrames[fi]).padStart(2,'0')}${t*1000>=c.totalMs?' (끝 자세 유지)':''} · ${state.playing?'재생 중':'정지'}`;
  card.seek.value=Math.round(t*1000);card.toggle.textContent=state.playing?'Ⅱ 일시정지':'▶ 재생';}
 const cards=D.candidates.map(c=>{const el=document.createElement('article');el.className='card';el.innerHTML=`<h2>${c.id}. ${c.name}</h2><canvas width="960" height="540"></canvas><p class="number info"></p><div class="local-controls"><button class="toggle">▶ 재생</button><button class="restart">↺ 처음부터</button><button class="back">◀ 0.05초</button><button class="fwd">0.05초 ▶</button><label><input class="loop" type="checkbox"> 반복</label></div><label class="local-seek">시간 <input type="range" min="0" max="${D.totalMs}" step="1" value="0"></label>`;$('#cards').append(el);
  const card={el,c,ctx:el.querySelector('canvas').getContext('2d'),info:el.querySelector('.info'),seek:el.querySelector('input[type=range]'),toggle:el.querySelector('.toggle'),state:{time:0,playing:false,loop:false}};
  card.toggle.onclick=()=>{if(card.state.playing)card.state.playing=false;else{for(const o of cards)o.state.playing=false;if(card.state.time*1000>=D.totalMs)card.state.time=0;card.state.playing=true;}};
  el.querySelector('.restart').onclick=()=>{for(const o of cards)o.state.playing=false;card.state.time=0;card.state.playing=true;};
  el.querySelector('.back').onclick=()=>{card.state.playing=false;card.state.time=Math.max(0,card.state.time-.05);};
  el.querySelector('.fwd').onclick=()=>{card.state.playing=false;card.state.time=Math.min(D.totalMs/1000,card.state.time+.05);};
  el.querySelector('.loop').onchange=e=>card.state.loop=e.target.checked;
  card.seek.oninput=e=>{card.state.playing=false;card.state.time=Number(e.target.value)/1000;};
  return card;});
 $('#playAll').onclick=()=>{for(const o of cards){o.state.time=0;o.state.playing=true;}};
 $('#pauseAll').onclick=()=>{for(const o of cards)o.state.playing=false;};
 let last=performance.now();
 function tick(now){const dt=(now-last)/1000*Number($('#speed').value);last=now;for(const card of cards){const s=card.state;if(s.playing){s.time+=dt;if(s.time*1000>=D.totalMs){if(s.loop)s.time=0;else{s.time=D.totalMs/1000;s.playing=false;}}}draw(card);}requestAnimationFrame(tick);}
 requestAnimationFrame(tick);
 window.rainSim={ready:true,errors:[],cards:cards.length,state:()=>cards.map(c=>({id:c.c.id,time:c.state.time,playing:c.state.playing,info:c.info.textContent})),renderAt:t=>cards.map(c=>{c.state.playing=false;c.state.time=t;draw(c);return c.info.textContent;})};
 $('#status').textContent=`몸 ${D.candidates.map(c=>c.imgs.length).join('·')}장, 화살·폭발 시트 2종 로딩 완료.`;
}catch(e){window.rainSim.errors.push(String(e));$('#status').textContent=String(e);$('#status').className='errors';throw e;}
