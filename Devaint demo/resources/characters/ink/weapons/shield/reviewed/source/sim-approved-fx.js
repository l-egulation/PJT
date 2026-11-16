function createShieldSimFx(ctx,art){
 const api={};

{const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),phase=(t,a,b)=>clamp((t-a)/(b-a)),fade=(t,a,b)=>1-phase(t,a,b),ease=t=>1-(1-t)**3;const shieldX=187,shieldY=121;const guardImage=art.parry,guardSurface=document.createElement("canvas");guardSurface.width=256;guardSurface.height=Math.round(256*guardImage.height/guardImage.width);const guardContext=guardSurface.getContext("2d");
function parryBloom(x,y,radius,alpha){
  if(alpha<=0)return;
  const glow=ctx.createRadialGradient(x,y,0,x,y,radius);
  glow.addColorStop(0,'rgba(255,255,248,.96)');
  glow.addColorStop(.20,'rgba(255,253,236,.76)');
  glow.addColorStop(.52,'rgba(255,241,207,.27)');
  glow.addColorStop(1,'rgba(255,241,207,0)');
  ctx.save();
  ctx.globalAlpha=alpha;
  ctx.fillStyle=glow;
  ctx.fillRect(x-radius,y-radius,radius*2,radius*2);
  ctx.restore();
}

function lightShard(x,y,angle,length,width,alpha){
  if(alpha<=0)return;
  ctx.save();
  ctx.translate(x,y);
  ctx.rotate(angle);
  ctx.globalAlpha=alpha;
  ctx.shadowColor='#fff2ce';
  ctx.shadowBlur=4;
  ctx.fillStyle='#fffaf0';
  ctx.beginPath();
  ctx.moveTo(-length*.50,0);
  ctx.lineTo(-length*.06,-width);
  ctx.lineTo(length*.66,0);
  ctx.lineTo(-length*.06,width);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

function auraPulse(age,power=1,withShards=true){
  if(age<0 || age>=.18)return;
  const expansion=ease(phase(age,0,.15));
  const visibility=fade(age,.025,.18);
  const radius=(13+expansion*33)*power;
  ctx.save();
  ctx.translate(shieldX+1,shieldY-9);
  ctx.strokeStyle='#f9e7c0';
  ctx.globalAlpha=visibility*.23;
  ctx.lineWidth=5*(1-expansion)+1.4;
  ctx.beginPath();
  ctx.ellipse(0,0,radius*.77,radius,0,0,Math.PI*2);
  ctx.stroke();
  ctx.strokeStyle='#fffbed';
  ctx.globalAlpha=visibility*.82;
  ctx.lineWidth=1.6-.9*expansion;
  for(const [a,b] of [[-1.40,.93],[1.20,2.62],[2.87,4.56]]){
    ctx.beginPath();
    ctx.ellipse(0,0,radius*.77,radius,0,a,b);
    ctx.stroke();
  }
  ctx.restore();

  const angles=withShards?[-1.34,-.88,-.38,.20,.73,1.18]:[];
  angles.forEach((angle,i)=>{
    const delay=i%2*.014;
    const pulseAge=age-delay;
    if(pulseAge<0)return;
    const travel=ease(phase(pulseAge,0,.12));
    const distance=(21+travel*(18+i%3*4))*power;
    const alpha=fade(pulseAge,.016,.13);
    const x=shieldX+Math.cos(angle)*distance;
    const y=shieldY-5+Math.sin(angle)*distance;
    lightShard(x,y,angle,(11+i%3*3)*(1-travel*.45)*power,1.4*power,alpha*.88);
  });
}

function guardFlash(t){
  if(t<0 || t>=.225)return;
  auraPulse(t,1,false);
  auraPulse(t-.045,.80,false);
  const open=ease(phase(t,0,.025));
  const opacity=fade(t,.105,.15);
  const settle=1+.085*fade(t,.025,.080)-.045*phase(t,.080,.15);
  const width=(39+open*15)*settle;
  const height=(73+open*9)*settle;
  const top=shieldY-42;
  const left=shieldX-2-width*.5;
  const w=guardSurface.width, h=guardSurface.height;

  guardContext.clearRect(0,0,w,h);
  guardContext.globalCompositeOperation='source-over';
  guardContext.drawImage(guardImage,0,0,w,h);
  const lineY=-36+phase(t,0,.12)*(h+72);
  const shine=guardContext.createLinearGradient(0,lineY-36,0,lineY+36);
  shine.addColorStop(0,'rgba(255,255,255,0)');
  shine.addColorStop(.5,'rgba(255,255,255,.46)');
  shine.addColorStop(1,'rgba(255,255,255,0)');
  guardContext.globalCompositeOperation='source-atop';
  guardContext.fillStyle=shine;
  guardContext.fillRect(0,lineY-36,w,72);
  const flash=Math.max(fade(t,.012,.055),t>=.056?fade(t-.056,.004,.034)*.54:0);
  guardContext.fillStyle='rgba(255,255,255,'+(flash*.82)+')';
  guardContext.fillRect(0,0,w,h);
  guardContext.globalCompositeOperation='source-over';

  parryBloom(shieldX+3,shieldY-3,33+open*9,flash*.83);
  ctx.save();
  const wave=phase(t,0,.075);
  ctx.globalAlpha=fade(t,.018,.11)*.74;
  ctx.strokeStyle='#fff8e6';
  ctx.lineWidth=2.1-1.2*wave;
  ctx.beginPath();
  ctx.ellipse(shieldX+7+wave*9,shieldY-2,12+wave*14,24+wave*15,0,-1.08,1.10);
  ctx.stroke();
  ctx.restore();

  ctx.save();
  ctx.globalAlpha=opacity;
  ctx.imageSmoothingEnabled=true;
  ctx.drawImage(guardSurface,left,top,width,height);
  ctx.globalAlpha=opacity*flash*.30;
  ctx.globalCompositeOperation='lighter';
  ctx.drawImage(guardSurface,left-2,top-2,width+4,height+4);
  ctx.restore();

  const echo=phase(t,.035,.20);
  const echoAlpha=phase(t,.02,.04)*fade(t,.045,.20)*.24;
  ctx.save();
  ctx.globalAlpha=echoAlpha;
  ctx.imageSmoothingEnabled=true;
  ctx.drawImage(guardImage,left-echo*7,top-echo*8,width+echo*18,height+echo*15);
  ctx.restore();
}

function parryImpact(age){
  if(age<0 || age>=.22)return;
  auraPulse(age,1.22);
  auraPulse(age-.027,.92);
  const fadeOut=fade(age,.012,.13);
  const core=fade(age,.012,.065);
  const flare=ctx.createRadialGradient(shieldX,shieldY,0,shieldX,shieldY,23);
  flare.addColorStop(0,'rgba(255,252,241,.91)');
  flare.addColorStop(.18,'rgba(255,244,217,.68)');
  flare.addColorStop(.58,'rgba(255,234,198,.17)');
  flare.addColorStop(1,'rgba(255,234,198,0)');
  parryBloom(shieldX+4,shieldY,43+phase(age,0,.08)*10,fade(age,.012,.095)*.88);
  ctx.save();
  ctx.fillStyle=flare;
  ctx.globalAlpha=fadeOut;
  ctx.fillRect(shieldX-23,shieldY-23,46,46);
  ctx.translate(shieldX,shieldY);
  ctx.globalAlpha=core;
  ctx.fillStyle='#fffbed';
  const reach=17+ease(phase(age,0,.027))*12;
  ctx.beginPath();
  ctx.moveTo(0,-reach);
  ctx.lineTo(2,-3);
  ctx.lineTo(reach*.73,0);
  ctx.lineTo(2,3);
  ctx.lineTo(0,reach*.86);
  ctx.lineTo(-2,3);
  ctx.lineTo(-reach*.45,0);
  ctx.lineTo(-2,-3);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

api.parry=(t,x,y,dir,impactAge)=>{ctx.save();ctx.translate(x,y);ctx.scale(dir,1);ctx.translate(-145,-154);if(t>=0)guardFlash(t);if(impactAge>=0)parryImpact(impactAge);ctx.restore();};}
{const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),phase=(t,a,b)=>clamp((t-a)/(b-a)),fade=(t,a,b)=>1-phase(t,a,b),ease=t=>1-(1-t)**3;const begin=.1,end=3.1,cx=172,cy=114,ground=154,sphere=art.sphere;
function energyRing(age,x,y,power=1){
  if(age<0 || age>=.23)return;
  const p=ease(phase(age,0,.23));
  ctx.save();ctx.globalAlpha=fade(age,.04,.23)*.8;ctx.strokeStyle='#fff9e8';ctx.lineWidth=2*(1-p)+.4;
  ctx.beginPath();ctx.ellipse(x,y,(14+31*p)*power,(18+34*p)*power,0,0,Math.PI*2);ctx.stroke();ctx.restore();
}

// Interpolate neighboring cells so the light filaments flow without hard frame jumps.
const orbCanvas=document.createElement('canvas');orbCanvas.width=orbCanvas.height=256;
const orbCtx=orbCanvas.getContext('2d');
let orbTime=-1;
function animatedOrb(age){
  if(age===orbTime)return orbCanvas;
  orbTime=age;
  const position=age/0.115;
  const index=Math.floor(position)%16,next=(index+1)%16,blend=position-Math.floor(position);
  const cellW=sphere.width/4,cellH=sphere.height/4;
  orbCtx.clearRect(0,0,256,256);
  orbCtx.save();orbCtx.imageSmoothingEnabled=true;
  orbCtx.globalCompositeOperation='lighter';
  orbCtx.translate(128,128);orbCtx.rotate(age*.40);orbCtx.translate(-128,-128);
  for(const [frame,alpha] of [[index,1-blend],[next,blend]]){
    orbCtx.globalAlpha=alpha;
    orbCtx.drawImage(sphere,(frame%4)*cellW,Math.floor(frame/4)*cellH,cellW,cellH,0,0,256,256);
  }
  orbCtx.setTransform(1,0,0,1,0,0);
  orbCtx.globalAlpha=1;orbCtx.globalCompositeOperation='destination-in';
  const mask=orbCtx.createRadialGradient(128,128,108,128,128,128);
  mask.addColorStop(0,'rgba(255,255,255,1)');mask.addColorStop(.65,'rgba(255,255,255,.65)');mask.addColorStop(1,'rgba(255,255,255,0)');
  orbCtx.fillStyle=mask;orbCtx.fillRect(0,0,256,256);
  orbCtx.restore();
  return orbCanvas;
}
function shell(t,front,broken){
  if(t<begin || t>=end+.10)return;
  const age=t-begin,reveal=ease(phase(age,0,.13));
  const opacity=fade(t,end-.13,end+.10)*reveal*(broken?.23:1);
  const scale=.85+.15*reveal+Math.sin(age*3.2)*.006;
  const width=120*scale,height=118*scale,orb=animatedOrb(age);
  ctx.save();ctx.translate(cx,cy-3);ctx.imageSmoothingEnabled=true;
  // The haze follows the whole animated shell, never isolated star-shaped sparkles.
  if(!front){
    ctx.globalAlpha=opacity*.36;ctx.filter='blur(2.2px)';
    ctx.drawImage(orb,-width*.535,-height*.535,width*1.07,height*1.07);
    ctx.filter='none';
  }
  ctx.globalAlpha=opacity*(front?.27:.77);
  ctx.drawImage(orb,-width/2,-height/2,width,height);
  ctx.restore();
}

function hitFlash(t,time){
  const age=t-time;
  if(age<0 || age>=.22)return;
  const x=cx+46,y=cy-2,p=ease(phase(age,0,.12));
  ctx.save();ctx.globalAlpha=fade(age,.025,.22);
  const glow=ctx.createRadialGradient(x,y,0,x,y,26);
  glow.addColorStop(0,'rgba(255,254,241,.83)');glow.addColorStop(.45,'rgba(255,245,218,.21)');glow.addColorStop(1,'rgba(255,245,218,0)');
  ctx.fillStyle=glow;ctx.fillRect(x-26,y-26,52,52);
  ctx.lineWidth=1.5*(1-p)+.5;ctx.strokeStyle='#fff8e2';
  ctx.beginPath();ctx.ellipse(x,y,4+p*10,7+p*17,0,-1.27,1.27);ctx.stroke();
  ctx.fillStyle='#fffbed';ctx.globalAlpha=fade(age,.018,.075);
  ctx.beginPath();ctx.moveTo(x,y-11);ctx.lineTo(x+2,y-2);ctx.lineTo(x+10,y);ctx.lineTo(x+2,y+2);ctx.lineTo(x,y+11);ctx.lineTo(x-2,y+2);ctx.lineTo(x-5,y);ctx.lineTo(x-2,y-2);ctx.closePath();ctx.fill();
  ctx.restore();
}

const fracture=createGuardFracture(ctx,animatedOrb);api.guard=(t,x,y,dir,front,hp,hitAge,brokenAge)=>{ctx.save();ctx.translate(x,y);ctx.scale(dir,1);ctx.translate(-172,-154);fracture.render(t,cx,cy-3,front,hp,hitAge,brokenAge);if(front){if(hp>0)energyRing(t-begin,cx,cy,1.15);if(hitAge>=0)hitFlash(t,t-hitAge);}ctx.restore();};}
{const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),phase=(t,a,b)=>clamp((t-a)/(b-a)),fade=(t,a,b)=>1-phase(t,a,b),ease=t=>1-(1-t)**3;const ground=220,moveStart=.15,moveEnd=.45,shieldW=166,shieldH=250,plantedShield=art.rush;
function bloom(x,y,r,a){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,"rgba(255,255,246,"+a+")");g.addColorStop(1,"rgba(255,255,246,0)");ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
function plantedBurst(age,x,final=false){
 if(age<0||age>.19)return;
 const p=clamp(age/.19),snap=1-(1-p)**3;
 ctx.save();ctx.globalAlpha=(1-p)**1.5;
 bloom(x,ground-5,final?48:38,.85);
 ctx.strokeStyle='#fffef5';ctx.lineWidth=3.6*(1-p)+.5;
 ctx.beginPath();ctx.ellipse(x,ground,9+snap*(final?70:56),3+snap*10,0,Math.PI,Math.PI*2);ctx.stroke();
 // A few strong chips, with a quick initial kick and gravity in the tail.
 for(let i=0;i<7;i++){
  const direction=i<4?-1:1,speed=28+(i%4)*13;
  const px=x+direction*snap*speed,py=ground-4-Math.sin(p*Math.PI)*(17+(i%3)*14);
  const w=(5+i%3*2)*(1-p),h=(2+i%2*2)*(1-p);
  ctx.fillStyle=i%3?'#c3b39c':'#fffdf0';
  ctx.beginPath();ctx.moveTo(px-w,py);ctx.lineTo(px,py-h);ctx.lineTo(px+w*.8,py+1);ctx.lineTo(px-w*.25,py+h);ctx.closePath();ctx.fill();
 }
 ctx.restore();
}
function shieldFragments(t,x){
 const age=t-.535;if(age<0||age>.16)return;
 const p=clamp(age/.16),kick=1-(1-p)**2;
 ctx.save();ctx.beginPath();ctx.rect(0,0,720,ground+1);ctx.clip();
 ctx.imageSmoothingEnabled=true;
 const shards=[
  [[.15,.05],[.59,.03],[.59,.38],[.28,.32]],
  [[.59,.03],[.90,.16],[.88,.50],[.59,.38]],
  [[.28,.32],[.59,.38],[.50,.67],[.30,.59]],
  [[.59,.38],[.88,.50],[.92,.82],[.50,.67]],
  [[.30,.59],[.50,.67],[.92,.82],[.89,.96],[.64,.85]]
 ];
 shards.forEach((polygon,i)=>{
  ctx.save();ctx.translate(x-2+(i%2?1:-1)*kick*(14+i*4),ground-175-kick*(9+(4-i)*4));
  ctx.globalAlpha=(1-p)**1.4;ctx.beginPath();
  polygon.forEach(([u,v],j)=>j?ctx.lineTo(u*shieldW,v*shieldH):ctx.moveTo(u*shieldW,v*shieldH));ctx.closePath();ctx.clip();
  ctx.drawImage(plantedShield,0,0,shieldW,shieldH);ctx.restore();
 });ctx.restore();
}
const auraCanvas=document.createElement('canvas');auraCanvas.width=512;auraCanvas.height=768;
const auraCtx=auraCanvas.getContext('2d');let auraReady=false;
function shieldAura(t,x,front){
 if(t<.135||t>.57)return;
 const ramp=clamp((t-.135)/.035),tail=1-clamp((t-.45)/.12),power=ramp*tail;
 if(!power)return;
 if(!auraReady){
  auraCtx.drawImage(plantedShield,0,0,512,768);
  auraCtx.globalCompositeOperation='source-in';auraCtx.fillStyle='#fffef5';auraCtx.fillRect(0,0,512,768);auraCtx.globalCompositeOperation='source-over';auraReady=true;
 }
 ctx.save();ctx.beginPath();ctx.rect(0,0,720,ground+1);ctx.clip();ctx.imageSmoothingEnabled=true;
 const pulse=.85+.15*Math.sin(t*49);
 if(front){
  // A breathing pearly corona hugs the actual generated shield contour.
  ctx.globalAlpha=power*.43*pulse;ctx.filter='blur(5px)';
  ctx.drawImage(auraCanvas,x-9,ground-183,shieldW+14,shieldH+16);
  ctx.filter='none';ctx.restore();return;
 }
 ctx.globalAlpha=power*.39;ctx.filter='blur(11px)';
 ctx.drawImage(auraCanvas,x-9,ground-180,shieldW+14,shieldH+10);ctx.filter='none';
 const roots=[[62,-157,130,15],[26,-118,180,26],[38,-93,163,24],[53,-62,165,24],[76,-34,140,18],[109,-91,140,20],[44,-135,150,20],[65,-48,154,20]];
 roots.forEach(([rx,ry,length,width],i)=>{
  const growth=.83+.17*Math.sin(t*28+i*1.3),L=length*growth*(.65+.35*ramp);
  const rootX=x+rx,rootY=ground+ry;
  const sample=(u,side)=>{
   const bend=(Math.sin(u*8-t*37+i*1.9)*6+Math.sin(u*15-t*54+i)*2)*Math.sin(u*Math.PI*.7);
   const spread=width*(.44+.56*Math.sin(u*Math.PI)**.7)*(1-u)**.48;
   return [rootX-u*L,rootY+bend+side*spread];
  };
  ctx.beginPath();for(let j=0;j<=24;j++){const [px,py]=sample(j/24,-1);j?ctx.lineTo(px,py):ctx.moveTo(px,py);}
  for(let j=24;j>=0;j--){const [px,py]=sample(j/24,1);ctx.lineTo(px,py);}ctx.closePath();
  const gradient=ctx.createLinearGradient(rootX-L,0,rootX,0);
  gradient.addColorStop(0,'rgba(255,255,247,0)');gradient.addColorStop(.23,'rgba(255,255,247,.20)');gradient.addColorStop(.60,'rgba(255,255,249,.72)');gradient.addColorStop(1,'rgba(255,255,252,.98)');
  ctx.fillStyle=gradient;ctx.globalAlpha=power*(.8+i%2*.1);
  ctx.save();ctx.filter='blur(5px)';ctx.fill();ctx.restore();ctx.globalAlpha=power*.74;ctx.fill();
  // Brief bright curls travel away from the plate, rather than sitting still beside it.
  const phase=(t*6.5+i*.163)%1;
  ctx.beginPath();for(let j=0;j<=10;j++){
   const u=clamp(phase+j*.017),[px,py]=sample(u,0);j?ctx.lineTo(px,py):ctx.moveTo(px,py);
  }
  ctx.globalAlpha=power*(1-phase)*.58;ctx.strokeStyle='#fffef8';ctx.lineWidth=2.7;ctx.stroke();
 });ctx.restore();
}
function pressure(t,x){
 if(t<.07||t>.72)return;
 const drop=clamp((t-.07)/.065),opacity=clamp((t-.07)/.014);
 const settling=t>=.135&&t<.175?Math.sin((t-.135)/.04*Math.PI)*5:0;
 if(t<.535){
  ctx.save();ctx.beginPath();ctx.rect(0,0,720,ground+1);ctx.clip();ctx.imageSmoothingEnabled=true;
  ctx.globalAlpha=opacity;
  const y=ground-175-105*(1-drop)**2+settling;
  ctx.drawImage(plantedShield,x-2,y,shieldW,shieldH);
  // Two short white pulses emphasize the buried plate striking the floor and braking.
  const flash=t>=.135&&t<.158?.46:t>=.505&&t<.535?.55:0;
  if(flash){ctx.globalCompositeOperation='screen';ctx.globalAlpha=flash;ctx.drawImage(plantedShield,x-2,y,shieldW,shieldH);}
  ctx.restore();
 }else shieldFragments(t,x);
 shieldAura(t,x,true);
 plantedBurst(t-.135,x+106);
 plantedBurst(t-.505,x+106,true);
 groundContact(t,x);
}
function groundContact(t,x){
 if(t<moveStart||t>.505)return;
 const age=t-moveStart,moving=t<moveEnd,strength=moving?1:1-clamp((t-moveEnd)/.055),contact=x+106;
 if(strength<=0)return;
 ctx.save();ctx.imageSmoothingEnabled=true;
 ctx.globalAlpha=strength;bloom(contact,ground-2,28,.9);
 // Dense low ploughing trail stays on the ground; no tall dust curtain.
 for(let i=0;i<12;i++){
  const q=(age*10+i*.091)%1,px=contact-7-q*(35+i*4),py=ground-1-Math.sin(q*Math.PI)*(4+i%4*3);
  ctx.globalAlpha=strength*(1-q)*.70;ctx.fillStyle=i%4===0?'#fffef4':'#b4a38c';
  const w=1.8+(i%3)*.7;ctx.beginPath();ctx.moveTo(px-w,py);ctx.lineTo(px+.5,py-1.8);ctx.lineTo(px+w,py+.6);ctx.lineTo(px-1,py+1.3);ctx.closePath();ctx.fill();
 }
 const gradient=ctx.createLinearGradient(x-20,ground,contact+20,ground);
 gradient.addColorStop(0,'rgba(255,254,244,0)');gradient.addColorStop(.8,'rgba(255,254,244,.8)');gradient.addColorStop(1,'rgba(255,254,244,0)');
 ctx.globalAlpha=strength;ctx.strokeStyle=gradient;ctx.lineWidth=3;
 ctx.beginPath();ctx.moveTo(x-20,ground);ctx.lineTo(contact+20,ground);ctx.stroke();ctx.restore();
}
function dust(t,x){}

api.rush=(t,x,y,dir,front)=>{ctx.save();ctx.translate(x,y);ctx.scale(dir,1);ctx.translate(-145,-220);if(front)pressure(t,77);else shieldAura(t,77,false);ctx.restore();};}
{const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),phase=(t,a,b)=>clamp((t-a)/(b-a)),fade=(t,a,b)=>1-phase(t,a,b),ease=t=>1-(1-t)**3;let decoyX=0,ground=0;function glow(x,y,r,a){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,"rgba(255,255,247,"+a+")");g.addColorStop(1,"rgba(255,255,247,0)");ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
function counterBurst(t){
 const result={burst:{radiusUnits:3},burstTime:0},age=t;
 if(!result.burst||age<0||age>.62)return;
 const q=clamp(age/.62),snap=1-(1-clamp(age/.13))**3,radius=result.burst.radiusUnits*32,cx=decoyX+3,cy=ground-24;
 ctx.save();ctx.imageSmoothingEnabled=true;
 // Fast expanding white front, followed by a thin, slower trailing ring.
 ctx.globalAlpha=(1-q)**1.2;glow(cx,cy,34+snap*radius,.85*(1-clamp(age/.13)));
 const r=6+snap*radius;
 const wash=ctx.createRadialGradient(cx,cy,Math.max(0,r-15),cx,cy,r+9);
 wash.addColorStop(0,'rgba(255,255,245,0)');wash.addColorStop(.52,'rgba(255,255,247,.85)');wash.addColorStop(1,'rgba(255,255,247,0)');
 ctx.fillStyle=wash;ctx.beginPath();ctx.arc(cx,cy,r+9,0,Math.PI*2);ctx.fill();
 ctx.strokeStyle='#fffef5';ctx.lineWidth=5*(1-q)+.5;ctx.beginPath();ctx.arc(cx,cy,r,0,Math.PI*2);ctx.stroke();
 for(let i=0;i<7;i++){
  const angle=i*Math.PI*2/7+.2,dist=snap*radius*.9+q*12,px=cx+Math.cos(angle)*dist,py=cy+Math.sin(angle)*dist*.8;
  ctx.globalAlpha=(1-q)**2;ctx.fillStyle='#fffdf0';const size=(6+i%2*3)*(1-q);
  ctx.save();ctx.translate(px,py);ctx.rotate(angle);ctx.beginPath();ctx.moveTo(-size,0);ctx.lineTo(0,-2.3);ctx.lineTo(size*.8,0);ctx.lineTo(0,2.3);ctx.closePath();ctx.fill();ctx.restore();
 }
 ctx.restore();
}
api.burst=(age,x,y)=>{decoyX=x;ground=y;counterBurst(age);};}
return api;}
