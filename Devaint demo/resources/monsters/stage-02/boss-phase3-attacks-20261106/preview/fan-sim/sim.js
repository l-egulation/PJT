const $=s=>document.querySelector(s),load=p=>new Promise((ok,no)=>{const i=new Image();i.onload=()=>ok(i);i.onerror=()=>no(new Error('Image failed: '+p));i.src=p;});
window.fanSim={ready:false,errors:[]};
try{
 const D=await (await fetch('sim.json')).json();const U=D.pixelsPerUnit,F=D.floorY,Z=D.fan,BX=D.positions.bossX,PX=D.positions.playerX;
 const [room,player]=await Promise.all(['../assets/room.png','../assets/player.png'].map(load));
 const sheet={};for(const [k,s] of Object.entries(D.sheets))sheet[k]={...s,img:await load(s.file)};
 for(const c of D.candidates)c.imgs=await Promise.all(c.frameUrls.map(u=>load(D.bodyFrames+u)));
 const hash=(i,k)=>{const v=Math.sin((i*7.31+k*10.48)*12.9898)*43758.5453;return v-Math.floor(v);};
 function frameOf(s,t){const ms=t*1000,total=s.durationsMs.reduce((a,b)=>a+b,0);if(!s.loop&&ms>=total)return -1;let u=s.loop?ms%total:ms;for(let i=0;i<s.frames;i++){if(u<s.durationsMs[i])return i;u-=s.durationsMs[i];}return s.frames-1;}
 function drawSheet(ctx,name,t,x,y,rot=0,scale=1,alpha=1,flipY=false){const s=sheet[name],f=frameOf(s,t);if(f<0)return;const sx=f%s.columns*s.cellWidth,sy=Math.floor(f/s.columns)*s.cellHeight;ctx.save();ctx.globalAlpha=alpha;ctx.translate(x,y);ctx.rotate(rot);ctx.scale(scale,flipY?-scale:scale);ctx.drawImage(s.img,sx,sy,s.cellWidth,s.cellHeight,-s.pivotX,-s.pivotY,s.cellWidth,s.cellHeight);ctx.restore();}
 function bodyFrame(c,t){let ms=t*1000;if(ms>=c.totalMs)return c.imgs.length-1;for(let i=0;i<c.durationsMs.length;i++){if(ms<c.durationsMs[i])return i;ms-=c.durationsMs[i];}return c.imgs.length-1;}
 const target={x:PX,y:F-32};
 function fanAt(i){const a=(90-Z.arc/2+i*Z.arc/(Z.count-1))*Math.PI/180;return{x:BX+Math.cos(a)*Z.radius*U,y:F-Z.blueHeightUnits*U-Math.sin(a)*Z.radius*U};}
 function whirlArrows(t,back){const age=t-Z.tell,out=[];if(age<0||age>Z.expand+.3)return out;const g=Math.min(1,Math.max(0,age/Z.expand)),end=Math.min(1,Math.max(0,(age-Z.expand)/.3));if(g<=0)return out;
  for(let i=0;i<Z.whirlArrows;i++){const tt=age+Z.tell,hw=Z.width/2*g,hh=D.arenaTopUnitsAboveFloor*g,yf=.04+.92*hash(i,1),speed=5+4.5*hash(i,2);const a=hash(i,3)*Math.PI*2+tt*speed,r=hw*(.5+.5*yf)*(.75+.45*hash(i,4))*(1+end*.8);
   const y=hh*yf+Math.sin(tt*(2+3*hash(i,5))+i)*.5*g;const isBack=Math.sin(a)>0;if(isBack!==back)continue;const ux=r*Math.cos(a),uy=y+.45*Math.sin(a)*(.6+.4*g);
   const ang=Math.atan2(.45*Math.cos(a),-Math.sin(a)*Math.max(r,.01))+(hash(i,7)-.5)*.5;const sc=(.75+.6*hash(i,6))*(isBack?.8:1.1);
   out.push({x:BX+ux*U,y:F-uy*U,rot:-ang,scale:sc,alpha:(isBack?.5:1)*(1-end),age:age+i*.05,flipY:Math.cos(ang)<0});}
  return out;}
 function phase(t){if(t<Z.tell)return'전조';if(t<Z.tell+Z.expand)return'광역 확장';if(t<Z.fire[0])return`화살 생성 ${Math.min(6,Math.floor((t-2.5)/.1)+1)}/6`;const n=Z.fire.filter(x=>t>=x).length;return n<6||t<Z.fire[5]+.6?`발사 ${n}/6`:'끝';}
 function draw(card){const {ctx,c,state}=card,t=state.time;ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);
  if(t<Z.tell){ctx.fillStyle='rgba(155,99,224,.7)';ctx.fillRect(BX-Z.width*U/2,F-3,Z.width*U,3);}
  drawSheet(ctx,'aoe-whirl-back',t,BX,F,0,2);
  for(const w of whirlArrows(t,true))drawSheet(ctx,'p3-arrow-flight',w.age,w.x,w.y,w.rot,w.scale,w.alpha,w.flipY);
  const fi=bodyFrame(c,t);ctx.drawImage(c.imgs[fi],BX-192,F-232);ctx.drawImage(player,PX-192,F-232);
  drawSheet(ctx,'aoe-whirl-front',t,BX,F,0,2);
  for(const w of whirlArrows(t,false))drawSheet(ctx,'p3-arrow-flight',w.age,w.x,w.y,w.rot,w.scale,w.alpha,w.flipY);
  for(let i=0;i<Z.count;i++){const form=2.5+i*Z.createGap;if(t<form)continue;const p=fanAt(i);const fire=Z.fire[i];let x=p.x,y=p.y,dx=target.x-p.x,dy=target.y-p.y;const len=Math.hypot(dx,dy);dx/=len;dy/=len;
   if(t>=fire){const d=(t-fire)*Z.speed*U;x+=dx*d;y+=dy*d;if(x<-60||x>1020||y>600||y<-60)continue;}
   drawSheet(ctx,'p3-arrow-flight',t-form,x,y,Math.atan2(dy,dx),1,1,dx<0);}
  card.info.textContent=`${t.toFixed(2)}초 · ${phase(t)} · 몸 원본 ${String(c.sourceFrames[fi]).padStart(2,'0')}${t*1000>=c.totalMs?' (끝 자세 유지)':''} · ${state.playing?'재생 중':'정지'}`;
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
 window.fanSim={ready:true,errors:[],cards:cards.length,renderAt:t=>cards.map(c=>{c.state.playing=false;c.state.time=t;draw(c);return c.info.textContent;})};
 $('#status').textContent=`몸 ${D.candidates.map(c=>c.imgs.length).join('·')}장, 회오리·화살 시트 3종 로딩 완료.`;
}catch(e){window.fanSim.errors.push(String(e));$('#status').textContent=String(e);$('#status').className='errors';throw e;}
