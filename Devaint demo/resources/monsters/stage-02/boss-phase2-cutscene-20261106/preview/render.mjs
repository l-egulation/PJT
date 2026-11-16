import {sprite,sheet,clamp} from './art.mjs';
import {FLOOR,W,U} from './engine.mjs';
const hash=(i,k)=>{const x=Math.sin((i*7.31+k*10.48)*12.9898)*43758.5453;return x-Math.floor(x);};
export function room(ctx,A){ctx.clearRect(0,0,960,540);const im=A.images['assets/room.png'];ctx.drawImage(im,0,0);ctx.imageSmoothingEnabled=false;
 // This old comparison background retained two rows of a purple effect. Composite the intact floor lip over them; do not edit the source image.
 ctx.drawImage(im,0,445,288,2,544,445,288,2);
}
function bar(ctx,label,x,y,w,n,max,color){ctx.fillStyle='#171c25e8';ctx.fillRect(x-2,y-17,w+4,30);ctx.font='700 11px Galmuri';ctx.fillStyle='#eee9dc';ctx.fillText(`${label} ${Math.ceil(n)} / ${max}`,x,y-5);ctx.fillStyle='#292e37';ctx.fillRect(x,y,w,9);ctx.fillStyle=color;ctx.fillRect(x,y,w*clamp(n/max),9);}
function volley(ctx,A,c,g){const a=g.t-c.start,V=g.V,rose=g.rose,left=rose.x<W/2,dir=left?1:-1,wall=left?0:W,fade=1-clamp((a-V.SwapAreaEnd)/.6),front=t=>V.SwapWidth*clamp((t-V.SwapTell)/V.SwapExpandSeconds);const shots=[];
 for(let i=0;i<3;i++)shots.push({t0:.1+i*.1,x:V.SwapWidth-.3+i*.3,angle:62+i*6});
 for(let i=0;i<70;i++){const land=.55+.85*((i+hash(i,1)*.8)/70),f=front(land),x=hash(i,2)<.7?Math.max(.3,f-hash(i,3)*2.2):.3+hash(i,3)*Math.max(0,f-.6);shots.push({t0:land-.18,x,angle:58+hash(i,4)*18});}
 for(const s of shots){const age=a-s.t0;if(age<0)continue;const r=s.angle*Math.PI/180,dx=Math.cos(r),dy=Math.sin(r),span=(V.SwapHeight+1.5)*U,x=wall+dir*s.x*U,angle=left?r:Math.PI-r;
  if(age<.18){const u=age/.18;sheet(ctx,A,'rose-rain-arrow-flight',x-dir*dx*span/dy*(1-u),FLOOR-span*(1-u)-.3*U,age,{angle,scale:1.6,flipY:!left});}
  else{sheet(ctx,A,'rose-rain-arrow-flight',x-dir*dx*.35*U,FLOOR-dy*.35*U,0,{angle,scale:1.6,flipY:!left,alpha:fade});sheet(ctx,A,'rose-rain-impact',x,FLOOR,age-.18,{scale:1.6});}}
}
function whirl(ctx,A,c,g,layer){const age=g.t-c.start,V=g.V,left=g.blue.x<W/2,dir=left?1:-1,w=V.SwapWidth*U*clamp((age-V.SwapTell)/V.SwapExpandSeconds),x=(left?0:W)+dir*Math.max(w/2,1.2*U);sheet(ctx,A,`ink-whirl-${layer}`,x,FLOOR,age,{flip:left});}
export function battle(ctx,A,g,hitboxes=false){
 room(ctx,A);const c=g.pattern,a=c?g.t-c.start:0,p=g.player;
 const press=c?.id===4?c:g.effects.find(e=>e.kind==='press-tail')?.pattern;
 if(press)whirl(ctx,A,press,g,'back');
 if(c?.id===1&&a<g.V.StunArrowTell){ctx.save();ctx.strokeStyle='#d995bb';ctx.globalAlpha=.35+.3*a/g.V.StunArrowTell;ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(g.rose.x+g.rose.face*18,FLOOR-57);ctx.lineTo(c.aim.x,c.aim.y);ctx.stroke();ctx.restore();}
 if(c?.id===3&&a<g.V.ExchangeTell)for(const b of[g.rose,g.blue])sheet(ctx,A,b.id==='blue'?'ink-charge':'homing-form',b.x+b.face*35,FLOOR-32,a,{scale:.5});
 for(const b of[g.rose,g.blue]){if(b.hidden)continue;let id=`${b.id}-idle`,age=0;
  if(g.t<b.stun){id=b.id==='blue'?'blue-collapse':'rose-collapse';age=9;}
  else if(b.id==='blue'&&c?.id===2&&a>=g.V.RootTell-g.V.BlueDropSeconds){id='blue-landing';age=0;}
  else if(b.id==='blue'&&(c?.id===1&&a<g.V.StunArrowTell||g.t-b.fire<.45)){id='blue-palm';age=c?.id===1?a:Math.max(0,g.t-b.fire+.35);}
  // Static shooting pose already contains its approved airborne offset; keep its native pixels.
  else if(b.id==='rose'&&g.t-b.fire<.3){id='rose-shot';}
  sprite(ctx,A,id,b.x,b.y,age,{flip:b.face>0,alpha:g.t<b.flash?.8:1});
  if(c?.id===1&&b.id==='blue'&&a<g.V.StunArrowTell)sheet(ctx,A,'ink-charge',b.x+b.face*28,FLOOR-60,a);
  if(c?.id===2&&b.id==='blue'&&a>=g.V.RootTell-g.V.BlueDropSeconds)sheet(ctx,A,'dive-trail',b.x,b.y,a-(g.V.RootTell-g.V.BlueDropSeconds));
  if(hitboxes){const q=g.bossBox(b);ctx.strokeStyle='#f5b8ed';ctx.strokeRect(q.x,q.y,q.w,q.h);}
 }
 sprite(ctx,A,'player',p.x,p.y,0,{flip:p.face>0,alpha:g.t<p.inv&&Math.floor(g.t*20)%2?.45:1});
 if(press){volley(ctx,A,press,g);whirl(ctx,A,press,g,'front');}
 for(const s of g.shots){const age=g.t-s.born,angle=Math.atan2(s.vy,s.vx);if(s.kind==='ink')sheet(ctx,A,'ink-fireball',s.x,s.y,age,{angle});else sheet(ctx,A,'rose-arrow-flight',s.x,s.y,age,{angle,flipY:s.vx<0});if(hitboxes){ctx.save();ctx.translate(s.x,s.y);ctx.rotate(angle);ctx.strokeStyle='#ffff80';ctx.strokeRect(-s.w/2,-s.h/2,s.w,s.h);ctx.restore();}}
 if(g.exchange?.stage==='flight')for(const s of g.exchange.items.filter(q=>q.active)){const age=g.t-g.exchange.flightAt;if(s.owner==='blue'){sheet(ctx,A,'blue-orb-trail',s.x,s.y,age,{angle:s.dir<0?Math.PI:0,scale:.8});sheet(ctx,A,'blue-orb-body',s.x,s.y,age,{scale:.8});}else sheet(ctx,A,'rose-arrow-flight',s.x,s.y,age,{angle:s.dir<0?Math.PI:0,scale:2,flipY:s.dir<0});bar(ctx,'',s.x-33,s.y-48,66,s.hp,s.max,s.owner==='rose'?'#d998bb':'#8cbde8');if(hitboxes){ctx.strokeStyle='#ffff80';ctx.strokeRect(s.x-U,s.y-U,U*2,U*2);}}
 const cast=p.cast;if(cast){const age=g.t-cast.start;if(cast.live||age<(cast.special?.25:.22)){ctx.save();ctx.translate(p.x,p.y-32);ctx.scale(cast.dir,1);ctx.strokeStyle=cast.live?'#fff4cf':'#f1db9b';ctx.lineWidth=cast.special?5:3;ctx.beginPath();ctx.arc(12,0,cast.special?100:55,-1.15,1.15);ctx.stroke();ctx.restore();}if(cast.live&&hitboxes){ctx.strokeStyle='#e5dc53';ctx.strokeRect(cast.box.x,cast.box.y,cast.box.w,cast.box.h);}}
 for(const e of g.effects){const age=g.t-e.born;if(e.kind==='damage'){ctx.font='700 15px Galmuri';ctx.fillStyle='#fff0dd';ctx.fillText(e.text,e.x,e.y-age*22);}else if(e.kind==='vanish')sheet(ctx,A,'blue-vanish',e.x,e.y,age);else if(e.kind!=='press-tail')sheet(ctx,A,e.kind,e.x,e.y,age,{scale:e.scale??1});if(hitboxes&&e.box){ctx.strokeStyle='#ff8f94';ctx.strokeRect(e.box.x,e.box.y,e.box.w,e.box.h);}}
 if(hitboxes){const b=g.body();ctx.strokeStyle='#b5ffe7';ctx.strokeRect(b.x,b.y,b.w,b.h);if(c?.id===4&&a>=g.V.SwapTell&&a<g.V.SwapAreaEnd){const w=g.V.SwapWidth*U*clamp((a-g.V.SwapTell)/g.V.SwapExpandSeconds);ctx.strokeStyle='#fa7e9b';ctx.strokeRect(0,FLOOR-g.V.SwapHeight*U,w,g.V.SwapHeight*U);ctx.strokeRect(W-w,FLOOR-g.V.SwapHeight*U,w,g.V.SwapHeight*U);}}
 bar(ctx,'ROSE',18,30,265,g.rose.hp,g.rose.max,'#b47191');bar(ctx,'BLUE',677,30,265,g.blue.hp,g.blue.max,'#6e96c1');bar(ctx,'PLAYER',18,513,180,p.hp,100,'#c8dac6');
 ctx.font='700 12px Galmuri';ctx.fillStyle='#fff7e7';ctx.textAlign='center';ctx.fillText(g.message,480,500);ctx.textAlign='left';ctx.fillStyle='#d5e3ec';ctx.fillText(`평타 25 · 강베기 35 · 피격 ${g.hits}회`,260,526);
 if(g.t<p.root){ctx.fillStyle='#bc9bd4';ctx.fillText('속박',p.x-14,p.y-75);}else if(g.t<p.stun){ctx.fillStyle='#e6bbce';ctx.fillText('기절',p.x-14,p.y-75);}
 const hurtAge=g.t-(g.damageAt??-99);if(hurtAge<.28){const alpha=.7*(1-hurtAge/.28),gr=ctx.createRadialGradient(480,270,170,480,270,510);gr.addColorStop(0,'#ff222200');gr.addColorStop(1,`rgba(200,15,35,${alpha})`);ctx.fillStyle=gr;ctx.fillRect(0,0,960,540);}
 if(g.state==='dead'||g.state==='ready'){ctx.fillStyle='#101826a8';ctx.fillRect(0,180,960,120);ctx.fillStyle='#fff9e9';ctx.textAlign='center';ctx.font='700 22px Galmuri';ctx.fillText(g.state==='dead'?'다시 도전!':'2페이즈 플레이 테스트',480,228);ctx.font='700 13px Galmuri';ctx.fillText('위의 전투 시작 버튼 · A/D 이동 · J 공격 · Shift 대시',480,265);ctx.textAlign='left';}
}
