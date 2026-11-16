import {Encounter,RULES,clamp} from './model.mjs?rev=open-arena-v12';
import {mixerPresentation,mixerCenterY,MIXER_SCALE,MIXER_START,MIXER_DISTANCE} from './mixer-pose.mjs?rev=open-arena-v12';
const $=s=>document.querySelector(s),canvas=$('#game'),ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;
let world=new Encounter({mode:'demo'}),assets={},manifest,loaded=false,last=0,accumulator=0,paused=false,speed=1,debug=false,soundOn=false,audio;
let effects=[],particles=[],numbers=[],shake=0,flash=0,keys=new Set(),pressed=new Set(),moveInput=0,renderTime=0;
const PALETTE=['#171716','#41413D','#575451','#817D77','#9C968D','#D6D0C7'];
const FX=['#575451','#9C968D','#D6D0C7'];
function loadImage(src){return new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>{assets[src]=image;resolve(image)};image.onerror=()=>reject(new Error('에셋 로드 실패: '+src));image.src=src;});}
function allPaths(v){if(typeof v==='string'&&/^assets\/.*\.png$/.test(v))return [v];if(Array.isArray(v))return v.flatMap(allPaths);if(v&&typeof v==='object')return Object.values(v).flatMap(allPaths);return [];}
function getImage(path){return assets[path];}
function image(path,x,y,w,h,alpha=1,flip=false){const im=getImage(path);if(!im)return;ctx.save();ctx.globalAlpha=alpha;ctx.translate(Math.round(x),Math.round(y));if(flip){ctx.scale(-1,1);x=-(w||im.width);}else x=0;ctx.drawImage(im,Math.round(x),0,w||im.width,h||im.height);ctx.restore();}
function rect(x,y,w,h,color,alpha=1){ctx.globalAlpha=alpha;ctx.fillStyle=color;ctx.fillRect(Math.round(x),Math.round(y),Math.round(w),Math.round(h));ctx.globalAlpha=1;}
function text(s,x,y,size=14,color='#f4eddf',align='left'){ctx.font=`${size}px "Malgun Gothic",system-ui`;ctx.textAlign=align;ctx.fillStyle=color;ctx.fillText(s,Math.round(x),Math.round(y));}
function line(x0,y0,x1,y1,color,width=2,alpha=1){x0=Math.round(x0);y0=Math.round(y0);x1=Math.round(x1);y1=Math.round(y1);const dx=Math.abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.abs(y1-y0),sy=y0<y1?1:-1;let err=dx+dy;
  for(let n=0;n<2000;n++){rect(x0,y0,width,width,color,alpha);if(x0===x1&&y0===y1)break;const e=2*err;if(e>=dy){err+=dy;x0+=sx}if(e<=dx){err+=dx;y0+=sy}}
}
function ring(x,y,r,color,progress=1,width=2){for(let i=0;i<120*progress;i++){const a=i/120*Math.PI*2-Math.PI/2,b=(i+1)/120*Math.PI*2-Math.PI/2;line(x+Math.cos(a)*r,y+Math.sin(a)*r,x+Math.cos(b)*r,y+Math.sin(b)*r,color,width);}}
function arrow(x,y,dir,color){line(x-dir*15,y,x+dir*15,y,color,3);line(x+dir*6,y-8,x+dir*15,y,color,3);line(x+dir*6,y+8,x+dir*15,y,color,3);}
function burst(x,y,count=12,power=100,palette=FX){for(let i=0;i<count;i++){let a=Math.random()*Math.PI*2,s=power*(.3+Math.random());particles.push({x,y,vx:Math.cos(a)*s,vy:Math.sin(a)*s-40,t:0,life:.3+Math.random()*.5,size:2+Math.floor(Math.random()*4),color:palette[i%palette.length]});}}
function addFX(type,x,y,scale=1,flip=false){if(manifest?.fx?.[type])effects.push({type,x,y,scale,flip,t:0});else burst(x,y,10,110);}
function tone(freq=160,duration=.08,type='triangle',volume=.045,end=50){if(!soundOn)return;audio??=new AudioContext();if(audio.state==='suspended')audio.resume();let o=audio.createOscillator(),g=audio.createGain();o.type=type;o.frequency.setValueAtTime(freq,audio.currentTime);o.frequency.exponentialRampToValueAtTime(Math.max(1,end),audio.currentTime+duration);g.gain.setValueAtTime(volume,audio.currentTime);g.gain.exponentialRampToValueAtTime(.001,audio.currentTime+duration);o.connect(g);g.connect(audio.destination);o.start();o.stop(audio.currentTime+duration);}
function onEvent(e){switch(e.type){
  case 'intro-impact':shake=22;flash=.08;addFX('dust',400,464,2);addFX('dust',560,464,2,true);burst(480,120,18,90);tone(44,.6,'triangle',.14,18);break;
  case 'intro-roar':shake=9;tone(46,1.1,'sawtooth',.055,85);burst(480,330,15,80);break;
  case 'pattern':tone(e.pattern==='fist'?180:e.pattern==='needle'?700:110,.17,'triangle',.045,130);break;
  case 'swing':tone(420,.06,'triangle',.02,120);addFX('slash',e.x,e.y,1,e.face<0);break;
  case 'boss-hit':addFX('hit',e.x,e.y);burst(e.x,e.y,8,130);shake=2;numbers.push({x:e.x,y:e.y,t:0,s:String(e.amount),color:'#eee4bf'});tone(110,.07,'square',.035,45);break;
  case 'player-hit':addFX('ink',e.x,e.y);shake=e.mixer?2:5;flash=.08;numbers.push({x:e.x,y:e.y-25,t:0,s:'−'+e.amount,color:'#c25f49'});tone(80,.13,'sawtooth',.055,25);break;
  case 'fist-launch':addFX('dust',e.side==='left'?110:850,464,2,e.side==='right');shake=4;tone(70,.28,'sawtooth',.07,25);break;
  case 'fist-stop':addFX('dust',e.x,e.y,2,e.side==='right');burst(e.x,e.y-70,28,190);shake=6;tone(45,.19,'triangle',.12,18);break;
  case 'needle-warn':tone(650+e.index*25,.035,'sine',.014,450);break;
  case 'needle-drop':tone(1000,.08,'triangle',.02,110);break;
  case 'needle-impact':addFX('needleImpact',e.x,e.y);addFX('dust',e.x,e.y,1);shake=Math.max(shake,1.5);tone(90,.06,'triangle',.025,30);break;
  case 'mixer-appear':addFX('ink',MIXER_START[e.side],mixerCenterY(MIXER_SCALE),2);tone(100,.5,'sawtooth',.05,220);shake=3;break;
  case 'mixer-end':addFX('dissolve',MIXER_START[world.pattern?.side==='left'?'left':'right']+(world.pattern?.side==='left'?1:-1)*MIXER_DISTANCE,mixerCenterY(MIXER_SCALE),2);tone(220,.35,'triangle',.04,30);break;
  case 'jump':addFX('dust',e.x,e.y,.5);tone(150,.08,'sine',.02,280);break;
  case 'land':addFX('dust',e.x,e.y,.5);break;
  case 'dash':burst(e.x-e.face*15,e.y-24,7,80,['#4a7775','#82a59a','#c8d0b7']);tone(260,.09,'triangle',.035,70);break;
  case 'enrage':shake=5;tone(70,1.0,'sawtooth',.055,180);addFX('ink',480,290,2);break;
  case 'victory':effects=[];particles=[];numbers=[];collapseHits.clear();shake=2;flash=0;tone(65,.35,'triangle',.07,24);break;
  case 'defeat':addFX('ink',world.player.x,world.player.y-30,1.3);tone(100,.6,'sawtooth',.06,20);break;
}}
function updateEffects(dt){updateCollapseImpacts();for(const f of effects)f.t+=dt;effects=effects.filter(f=>f.t<(manifest.fx[f.type].frames.length/(manifest.fx[f.type].fps||20)));
  for(const p of particles){p.t+=dt;p.x+=p.vx*dt;p.y+=p.vy*dt;p.vy+=380*dt;if(p.y>464){p.y=464;p.vx*=.7;p.vy*=-.2;}}particles=particles.filter(p=>p.t<p.life);
  for(const n of numbers)n.t+=dt;numbers=numbers.filter(n=>n.t<.8);shake=Math.max(0,shake-dt*22);flash=Math.max(0,flash-dt);
}
function drawEffects(){for(const f of effects){let clip=manifest.fx[f.type],i=Math.min(clip.frames.length-1,Math.floor(f.t*(clip.fps||20))),im=getImage(clip.frames[i]);if(!im)continue;let pv=clip.pivot||[im.width/2,im.height];const size=f.scale;const opacity=clip.fadeAfter===undefined?1:clamp(1-(f.t-clip.fadeAfter)/clip.fadeDuration,0,1);
    image(clip.frames[i],f.x-pv[0]*size,f.y-pv[1]*size,Math.round(im.width*size),Math.round(im.height*size),opacity,f.flip);
  }for(const p of particles)rect(p.x,p.y,p.size,p.size,p.color,1-p.t/p.life);for(const n of numbers)text(n.s,n.x,n.y-n.t*35,16,n.color,'center');}
function bossTransforms(){const w=world,q=w.pattern;const t=Math.floor(w.time/.05)*.05;let body=-Math.round(5*(1-Math.cos(t*Math.PI*2/1.2))/2),hand=-Math.round(5*(1-Math.cos((t-.15)*Math.PI*2/1.2))/2),rise=0,alpha=1;
  if(w.status==='intro'){
    const t=w.intro,d=clamp((t-RULES.intro.dropStart)/.5,0,1);
    rise=-490*(1-d*d*d);body=hand=0;
    if(t>=RULES.intro.impact){const a=t-RULES.intro.impact;rise=16*Math.sin(a*16)*Math.exp(-a*5);}
    if(t>=RULES.intro.roar&&t<3.9){const a=t-RULES.intro.roar;body=Math.round(Math.sin(a*34)*3*(1-a/1.3));hand=-body;}
  }
  if(w.status==='victory'){rise=Math.min(130,w.endAge*w.endAge*65);alpha=clamp(1-(w.endAge-.4)/1.6,0,1);}
  return {body:body+rise,hand:hand+rise,rise,alpha};}
function drawPart(id,x=0,y=0,alpha=1){image(manifest.boss.parts[id],288+x,35+y,undefined,undefined,alpha);
  if(world.boss.flash>0&&['body','ink_lower','dial','fist_left','fist_right'].includes(id)){
    const im=getImage(manifest.boss.parts[id]);if(whiteMasks.has(im)){ctx.save();ctx.globalAlpha=.65*alpha;ctx.drawImage(whiteMasks.get(im),Math.round(288+x),Math.round(35+y));ctx.restore();}
  }
}
// Fixed ceiling anchor, rigid-size interlocking links. Only the number of visible
// links and their path change; the ceiling and link dimensions never stretch.
function drawTether(side,dx,dy,alpha){
  const ax=side==='left'?401:559,ay=111,bx=(side==='left'?376:584)+dx,by=298+dy;
  const distance=Math.hypot(bx-ax,by-ay),count=Math.max(1,Math.ceil(distance/13));
  const ux=(bx-ax)/distance,uy=(by-ay)/distance,nx=-uy,ny=ux;
  ctx.save();ctx.globalAlpha=alpha;
  for(let i=count;i>=0;i--){const d=Math.min(distance,i*13),x=ax+ux*d,y=ay+uy*d;
    const wide=i%2===0?6:3;
    // Four sides with rounded pixel corners, open center, consistent link gauge.
    const pts=[[-wide+2,-10],[wide-2,-10],[wide,-7],[wide,7],[wide-2,10],[-wide+2,10],[-wide,7],[-wide,-7]];
    for(let k=0;k<pts.length;k++){const u=pts[k],v=pts[(k+1)%pts.length];
      const x0=x+nx*u[0]+ux*u[1],y0=y+ny*u[0]+uy*u[1],x1=x+nx*v[0]+ux*v[1],y1=y+ny*v[0]+uy*v[1];
      line(x0-1,y0-1,x1-1,y1-1,'#171716',5,alpha);line(x0,y0,x1,y1,k<4?'#817D77':'#41413D',3,alpha);
    }
  }ctx.restore();
}
function mixerPose(){const q=world.pattern;return q?.type==='mixer'?mixerPresentation(q.age,q.side):null;}
function drawBoss(){const q=world.pattern,tr=bossTransforms(),death=world.status==='victory',a=tr.alpha;
  if(world.status==='victory'){drawCollapse();return;}
  if(world.status==='intro'&&world.intro<RULES.intro.dropStart)return;
  if(a<=0){drawPart('ceiling',0,0,death?clamp(1-world.endAge/3,0,1):1);return;}
  const assemblyRise=world.status==='intro'?tr.rise:0;
  if(!mixerPose())drawPart('chain_center',0,assemblyRise,a);
  for(const side of ['left','right']){const offset=world.sideOffset(side);let dx=offset.x,dy=offset.y+tr.rise;
    if(death){dx+=(side==='left'?-1:1)*world.endAge*35;dy+=world.endAge**2*100;}
    if(Math.abs(dx)>.1)drawTether(side,dx,dy,a);else drawPart('chain_'+side,0,assemblyRise,a);
    drawPart('fist_'+side,dx,dy,a);
  }
  // Ceiling covers the first link: the attachment stays rooted even during launch.
  drawPart('ceiling',0,assemblyRise,death?clamp(1-world.endAge/3,0,1):1);
  if(mixerPose())return;
  // The giant arm reaches out from the wall behind the clock body, so the body stays readable.
  drawGiant();
  drawPart('body',0,tr.body,a);drawPart('ink_lower',0,tr.body,a);drawPart('dial',0,tr.body,a);
  // The same separated original hands remain in every state.
  const turn=world.elapsed/240*Math.PI*2;
  drawClockHands(480,280+tr.body,1,turn,q?.type==='needle'?q.age*.7:turn*.15,a);
  if(world.enragePulse>0){ring(480,280+tr.body,65+(2-world.enragePulse)*55,'#b66c4d',1,3);}
}
const whiteMasks=new Map();
function drawClockHands(x,y,scale,minuteAngle,hourAngle=minuteAngle*.22,alpha=1){
  for(const [id,angle] of [['hand_minute',minuteAngle],['hand_hour',hourAngle],['hand_hub',0]]){
    const im=getImage(manifest.boss.parts[id]);if(!im)continue;
    ctx.save();ctx.globalAlpha=alpha;ctx.translate(Math.round(x),Math.round(y));
    ctx.rotate(angle);ctx.scale(scale,scale);ctx.drawImage(im,-192,-245);ctx.restore();
  }
}
// Ink seeps from the ceiling line over each locked landing point (needleInk FX frames).
// Drawn in front of the boss so a column under the clock body stays readable.
function drawWarnings(){const clip=manifest.fx.needleInk;
  for(const n of world.needles)if(n.age<1)image(fxFrame('needleInk',n.age),n.x-clip.pivot[0],clip.ceilingY-clip.pivot[1]);
}
function drawGiant(){const g=world.fistGeometry();if(!g||g.w<1)return;
  const fist=getImage(manifest.boss.giantFist),tile=getImage(manifest.boss.armTile);
  const [px,py]=manifest.boss.giantFistPivot,dir=g.side==='left'?1:-1,tip=g.side==='left'?RULES.left+g.w:RULES.right-g.w;
  ctx.save();
  // Only the arena edge (screen edge) occludes the arm. Never slice a fist at the hitbox top/bottom.
  ctx.beginPath();ctx.rect(RULES.left,0,RULES.right-RULES.left,540);ctx.clip();
  ctx.translate(Math.round(tip),368);ctx.scale(dir,1);
  const rear=-px+38;
  if(tile)for(let x=rear-g.w-96;x<rear;x+=tile.width)ctx.drawImage(tile,Math.round(x),-74);
  ctx.drawImage(fist,-px,-py);ctx.restore();
  if(g.active)for(let k=0;k<5;k++)line(tip-dir*(95+k*17),290+k*32,tip-dir*32,290+k*32,'#D6D0C7',3);
}
function drawNeedles(){for(const n of world.needles){if(n.age<1)continue;let tip=40+424*clamp((n.age-1)/.3,0,1),im=getImage(manifest.boss.needle),alpha=n.age<1.3?1:clamp(1-(n.age-1.3)/.5,0,1);if(im){image(manifest.boss.needle,n.x-im.width/2,tip-im.height,undefined,undefined,alpha);}
  if(n.age<1.3){line(n.x-9,tip-170,n.x-9,tip-130,'#D6D0C7',3);line(n.x+11,tip-160,n.x+11,tip-125,'#9C968D',2);}
}}
function drawMixerBlades(x,y,scale,age,spinning){
  // Long cutting tips correspond to the 192px collision radius. Trails share 3 FX colours.
  const accel=clamp((age-.66)/.34,0,1),base=spinning?(age-1)*44+3:accel*3;
  for(const [id,angle,length] of [['minute',base,185],['hour',-base*1.31+2.1,148]]){
    if(spinning)for(let trail=10;trail>=1;trail--){
      const a=angle-trail*.14*(id==='minute'?1:-1),b=a-.09*(id==='minute'?1:-1);
      const r=length*scale;
      line(x-Math.sin(a)*r,y+Math.cos(a)*r,x-Math.sin(b)*r,y+Math.cos(b)*r,trail<3?'#D6D0C7':'#9C968D',trail<3?3:2,(1-trail/12)*.65);
    }
    const im=getImage(manifest.boss['blade_'+id]);ctx.save();ctx.translate(Math.round(x),Math.round(y));ctx.rotate(angle);ctx.scale(scale,scale);ctx.drawImage(im,-32,-15);ctx.restore();
  }
  image(manifest.boss.bladeHub,x-16*scale,y-16*scale,32*scale,32*scale);
}
function drawMixer(){const p=mixerPose();if(!p||p.phase==='hidden')return;const q=world.pattern;
  const foreground=['approach','active','recede'].includes(p.phase);
  ctx.save();
  if(foreground){
    ctx.beginPath();ctx.rect(0,0,960,RULES.floor);ctx.clip();
    const im=getImage(manifest.boss.foregroundBody),[px,py]=manifest.boss.foregroundPivot,k=p.scale/MIXER_SCALE;
    // New large native sprite is 1:1 at its combat size; only approach/recede scale.
    const ox=Math.round(p.x-px*k),oy=Math.round(p.y-py*k),w=Math.round(im.width*k),h=Math.round(im.height*k);
    image(manifest.boss.foregroundBody,ox,oy,w,h);
    if(world.boss.flash>0){ctx.save();ctx.globalAlpha=.65;ctx.drawImage(whiteMasks.get(im),ox,oy,w,h);ctx.restore();}
    drawMixerBlades(p.x,p.y,k,q.age,p.phase==='active');

  }else{
    const ox=Math.round(p.x-192*p.scale),oy=Math.round(p.y-245*p.scale);
    for(const id of ['body_merged','dial'])image(manifest.boss.parts[id],ox,oy,Math.round(384*p.scale),Math.round(416*p.scale));
    drawClockHands(p.x,p.y,p.scale,world.elapsed/240*Math.PI*2);
  }
  ctx.restore();
}
// Ink warnings in FX frames. Players learn each sweep by playing, so only the source is marked.
function fxFrame(id,age){const c=manifest.fx[id],n=c.frames.length,loop=c.loopFrom??n-1;let i=Math.max(0,Math.floor(age*c.fps));
  if(i>=n)i=loop+(i-loop)%(n-loop);return c.frames[i];}
// Draws a clip whose pivot x=0 is the origin wall; right-side attacks mirror it in place.
function drawFromWall(id,age,origin,dir,y){const path=fxFrame(id,age),im=getImage(path);if(!im)return;
  image(path,dir>0?origin:origin-im.width,y-manifest.fx[id].pivot[1],undefined,undefined,1,dir<0);}
function drawFistTelegraph(q){
  // The wall boils only after the small fist has left, so exit -> boil -> giant fist read in order.
  const age=q.age-manifest.fx.wallInk.startAt;if(age<0||q.age>=1.3)return;
  const dir=q.side==='left'?1:-1;drawFromWall('wallInk',age,dir>0?RULES.left:RULES.right,dir,RULES.floor);
}
function drawMixerTelegraph(q){if(q.age>=5.5)return;
  // Ink clock face at the 192 px hit radius: traced at the appearance point, then follows the clock.
  const c=q.age<1?{x:MIXER_START[q.side],y:mixerCenterY(MIXER_SCALE)}:world.mixerGeometry();if(!c)return;
  ctx.save();ctx.beginPath();ctx.rect(0,0,960,RULES.floor);ctx.clip();
  const ring=fxFrame('clockRing',q.age),[px,py]=manifest.fx.clockRing.pivot;image(ring,Math.round(c.x)-px,Math.round(c.y)-py);
  ctx.restore();
}
function drawTelegraphs(){if(world.status!=='fight')return;const q=world.pattern;
  if(q?.type==='fist')drawFistTelegraph(q);else if(q?.type==='mixer')drawMixerTelegraph(q);
}
// Death reference: large mass loses support, leans, lands, then huge dust conceals collapse.
const collapsePieces=[],collapseHits=new Set();
function prepareCollapse(){
  collapsePieces.length=0;
  const body=document.createElement('canvas');body.width=384;body.height=416;
  const bc=body.getContext('2d');
  for(const id of ['body_merged','dial','hand_minute','hand_hour','hand_hub'])bc.drawImage(getImage(manifest.boss.parts[id]),0,0);
  function piece(im,polygon,kind,vx,spin,delay){
    const full=document.createElement('canvas');full.width=384;full.height=416;
    const fc=full.getContext('2d');fc.imageSmoothingEnabled=false;
    fc.beginPath();polygon.forEach(([x,y],i)=>i?fc.lineTo(x,y):fc.moveTo(x,y));fc.closePath();fc.clip();fc.drawImage(im,0,0);
    const a=fc.getImageData(0,0,384,416).data;let x0=384,y0=416,x1=0,y1=0;
    for(let y=0;y<416;y++)for(let x=0;x<384;x++)if(a[(y*384+x)*4+3]){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x+1);y1=Math.max(y1,y+1);}
    if(x1<=x0)return;
    const c=document.createElement('canvas');c.width=x1-x0;c.height=y1-y0;c.getContext('2d').drawImage(full,x0,y0,c.width,c.height,0,0,c.width,c.height);
    collapsePieces.push({im:c,x:x0,y:y0,w:c.width,h:c.height,kind,vx,spin,delay});
  }
  // Three large anatomical pieces: roof, dial/body, lower ink. No rectangular grid cuts.
  const seam=[[0,184],[112,184],[124,190],[146,186],[160,193],[194,189],[214,195],[240,186],[268,190],[384,190]];
  piece(body,[[0,0],[384,0],...seam.slice().reverse()],'body',-20,-.4,.66);
  piece(body,[...seam,[384,324],[250,324],[235,332],[213,325],[191,334],[164,325],[144,333],[0,328]],'body',32,.25,.66);
  piece(body,[[0,328],[144,333],[164,325],[191,334],[213,325],[235,332],[250,324],[384,324],[384,416],[0,416]],'body',-12,-.1,.66);
  for(const [id,dir] of [['fist_left',-1],['fist_right',1]])piece(getImage(manifest.boss.parts[id]),[[0,0],[384,0],[384,416],[0,416]],'fist',dir*55,dir*.35,.57);
  for(const id of ['chain_left','chain_right','chain_center'])piece(getImage(manifest.boss.parts[id]),[[0,0],[384,0],[384,416],[0,416]],'chain',id==='chain_left'?-30:30,.8,.7);
}
function collapsePose(p){
  const age=world.endAge,anticipate=clamp(age/.66,0,1),t=Math.max(0,age-p.delay);
  // All pieces share the same lean before release, preserving a single heavy silhouette.
  const lean=anticipate*anticipate*.17;
  const x=p.vx*Math.min(t,.7),angle=p.spin*Math.min(.65,Math.max(0,t-.18));
  const cx=288+p.x+p.w/2+x,cy=35+p.y+p.h/2;
  const centerY=400+(cx-480)*Math.sin(lean)+(cy-400)*Math.cos(lean);
  const extent=Math.abs(Math.sin(lean+angle))*p.w/2+Math.abs(Math.cos(lean+angle))*p.h/2;
  const drop=Math.max(0,(464-centerY-extent)/Math.cos(lean));
  const descent=Math.min(drop,1050*t*t),land=Math.max(0,t-Math.sqrt(drop/1050));
  return {lean,x,y:descent-(land<.2?Math.sin(land/.2*Math.PI)*3:0),angle};
}
function updateCollapseImpacts(){if(world.status!=='victory')return;
  const age=world.endAge;
  if(age>=.86&&!collapseHits.has('hands')){collapseHits.add('hands');shake=8;tone(52,.18,'triangle',.055,22);}
  if(age>=.96&&!collapseHits.has('body')){
    collapseHits.add('body');addFX('collapsePlume',490,464,1);shake=24;tone(42,.65,'triangle',.13,16);
  }
}
function drawCollapse(){
  drawPart('ceiling');
  ctx.save();ctx.beginPath();ctx.rect(0,0,960,464);ctx.clip();
  for(const p of collapsePieces){const q=collapsePose(p);
    ctx.save();ctx.translate(480,400);ctx.rotate(q.lean);ctx.translate(-480,-400);
    ctx.translate(Math.round(288+p.x+p.w/2+q.x),Math.round(35+p.y+p.h/2+q.y));ctx.rotate(q.angle);
    // Remainder settles into rubble behind the dust, never explodes into ink particles.
    ctx.drawImage(p.im,-p.w/2,-p.h/2);ctx.restore();
  }
  ctx.restore();
}
function drawHero(){const p=world.player;let state='idle';if(world.status==='defeat')state='death';else if(p.dash>0)state='dash';else if(p.attack>0)state='attack';else if(p.y<463)state=p.vy<0?'jump':'fall';else if(moveInput!==0||(world.mode==='demo'&&Math.abs(p.x-480)>8))state='run';
  let clip=manifest.hero[state]||manifest.hero.idle;if(!clip)return;const count=clip.frames.length,fps=clip.fps||12;
  let clock=state==='attack'?.32-p.attack:state==='death'?world.endAge:state==='run'?p.walk:world.time;
  let idx=state==='death'?Math.min(count-1,Math.floor(clock*fps)):Math.floor(clock*fps)%count;
  const im=getImage(clip.frames[idx]),pv=clip.pivot||[64,121],flip=p.face<0;if(!im)return;
  if(p.invuln>0&&Math.floor(world.time*18)%2===0)ctx.globalAlpha=.5;
  // Every clip retains the same foot origin, never normalize by silhouette height.
  ctx.save();ctx.translate(Math.round(p.x),Math.round(p.y));if(flip)ctx.scale(-1,1);ctx.drawImage(im,-pv[0],-pv[1]);if(p.hurt>0&&whiteMasks.has(im)){ctx.globalAlpha=.8;ctx.drawImage(whiteMasks.get(im),-pv[0],-pv[1]);}ctx.restore();ctx.globalAlpha=1;
}
function hud(){const w=world,p=w.player,q=w.pattern;rect(21,19,172,51,'#202b26',.94);text('잉크',32,35,11,'#c3cbbb');text(`${Math.ceil(p.hp)} / 100`,180,35,11,'#e4ded0','right');rect(32,44,148,7,'#485148');rect(32,44,148*p.hp/100,7,'#a8b7a0');rect(32,58,148*(1-p.dashCd),3,p.dashCd>0?'#657c75':'#b7d4c6');
  rect(255,15,450,48,'#202b26',.96);text('시 계 탑  집 행 자',480,32,13,'#e8dfca','center');rect(270,42,420,7,'#485148');rect(270,42,420*w.boss.hp/RULES.bossHp,7,w.enraged?'#bb7656':'#d2b57a');
  // Encounter clock: 12 at entry, 6 at 120 s, back to 12 at 240 s.
  rect(782,12,166,99,'#202b26',.94);const cx=829,cy=59;
  ring(cx,cy,34,w.enraged?'#b75c46':'#aaa28d',1,2);
  for(let i=0;i<12;i++){const a=i*Math.PI/6-Math.PI/2;line(cx+Math.cos(a)*28,cy+Math.sin(a)*28,cx+Math.cos(a)*31,cy+Math.sin(a)*31,'#817D77',1);}
  text('12',cx,23,10,'#e8dfca','center');text('6',cx,101,10,'#e8dfca','center');
  const angle=clamp(w.elapsed/240,0,1)*Math.PI*2;
  const hand=getImage(manifest.boss.parts.hand_minute);ctx.save();ctx.translate(cx,cy);ctx.rotate(angle);ctx.scale(.78,.78);ctx.drawImage(hand,-192,-245);ctx.restore();
  rect(cx-2,cy-2,4,4,'#D6D0C7');
  text(`${Math.floor(Math.max(0,240-w.elapsed)/60)}:${String(Math.max(0,Math.floor((240-w.elapsed)%60))).padStart(2,'0')}`,900,51,19,'#e5dac0','center');
  text(w.enraged?'6시 · 광폭화':'6시 · 120초',900,73,11,w.enraged?'#f08e70':'#a7b5a5','center');
  text('12시 · 즉사',900,91,11,'#c27764','center');
  let label='',sub='',color='#e7c98f';if(w.status==='intro'){label='시계탑 집행자';sub='천장에 걸린 마지막 시간';}
  else if(q){const names={fist:'긴 주먹',needle:'시계침 낙하',mixer:'시간의 믹서기'};label=names[q.type];const stages={warn:'전조',strike:'공격 중',hold:'유지 · 반격 가능',retract:'회수 · 반격 가능',return:'중앙 복귀',recovery:'빈틈 · 반격 기회',wait:'다음 패턴 준비'};sub=stages[q.stage];if(q.stage==='recovery'||q.stage==='hold'||q.stage==='retract')color='#b8d1b1';}
  if(label&&debug){rect(352,78,256,47,'#202b26',.9);text(label,480,97,14,color,'center');text(sub,480,115,11,'#bcc3b3','center');}
  rect(19,503,210,22,'#202b26',.85);text(w.mode==='demo'?'자동 시연 · 무적 / 직접 도전 가능':w.practice?'패턴 연습 · 피해 적용':'도전 중 · P 일시정지',29,518,11,'#e3ddcb');
  if(w.enragePulse>0)text('시간이 빨라진다 — 광폭화',480,158,19,'#a14834','center');
  if(paused){rect(0,0,960,540,'#17251f',.65);text('일시정지',480,263,30,'#efe6cf','center');text('P 또는 일시정지 버튼으로 계속',480,294,13,'#b3c4b7','center');}
  if(debug){text(`${w.elapsed.toFixed(2)}s / ${q?.type||'-'} ${q?.age.toFixed(2)||''} / ×${q?.speed||1}`,25,492,12,'#202922');}
}
function draw(){if(!loaded)return;ctx.clearRect(0,0,960,540);ctx.save();
  const intro=world.status==='intro',t=world.intro;
  if(intro){const p=clamp(t/1.4,0,1),e=p*p*(3-2*p),z=1.13-.13*e;
    ctx.translate(480,270);ctx.scale(z,z);ctx.translate(-480+45*(1-e),-270+10*(1-e));}
  if(shake>0&&!paused)ctx.translate(Math.round((Math.random()-.5)*shake),Math.round((Math.random()-.5)*shake));
  image(manifest.room,0,0,960,540);drawTelegraphs();drawBoss();drawWarnings();drawNeedles();drawMixer();if(world.status==='victory'){drawEffects();drawHero();}else{drawHero();drawEffects();}
  if(debug){for(const b of world.bossHurtboxes())rect(b.x,b.y,b.w,b.h,'#47bb85',.16);let b=world.playerBox();rect(b.x,b.y,b.w,b.h,'#498ebe',.3);if(world.player.attack>0){b=world.swingBox();rect(b.x,b.y,b.w,b.h,'#e7bb68',.35);}const g=world.fistGeometry();if(g?.active)rect(g.x,g.y,g.w,g.h,'#e04444',.3);}
  ctx.restore();if(flash>0)rect(0,0,960,540,'#e6d5ba',flash*2);
  if(intro){const enter=clamp(t/.6,0,1),exit=1-clamp((t-4)/.8,0,1),k=enter*exit;
    rect(0,0,960,62*k,'#0b0d0c');rect(0,540-62*k,960,62*k,'#0b0d0c');
    if(t>2.65&&t<4.35){ctx.save();ctx.globalAlpha=Math.min(clamp((t-2.65)/.3,0,1),clamp((4.35-t)/.4,0,1));text('시 계 탑  집 행 자',480,514,21,'#e5dac0','center');ctx.restore();}
    if(paused){rect(0,0,960,540,'#17251f',.6);text('일시정지',480,270,30,'#efe6cf','center');}
  }else hud();
}
function input(){const move=(keys.has('right')?1:0)-(keys.has('left')?1:0);moveInput=move;const r={move,jump:pressed.has('jump'),dash:pressed.has('dash'),attack:keys.has('attack')||pressed.has('attack')};pressed.clear();return r;}
function frame(time){const dt=Math.min((time-last)/1000||0,.05);last=time;renderTime+=dt;
  if(loaded&&!paused){accumulator+=dt*speed;let first=true;while(accumulator>=1/120){world.update(1/120,input());world.drain().forEach(onEvent);updateEffects(1/120);accumulator-=1/120;first=false;}
    const terminal=world.status==='victory'||world.status==='defeat';if(terminal&&world.endAge>(world.status==='victory'?5.2:2.6)){if(world.mode==='demo'){reset('demo');}else showResult();}
  }
  draw();document.querySelectorAll('[data-pattern]').forEach(el=>el.classList.toggle('active',el.dataset.pattern===world.pattern?.type));requestAnimationFrame(frame);
}
function reset(mode='play',practice=null){world=new Encounter({mode,practice,seed:Math.floor(Math.random()*10000)+1});effects=[];particles=[];numbers=[];collapseHits.clear();paused=false;accumulator=0;shake=0;flash=0;keys.clear();pressed.clear();$('#result').hidden=true;$('#pause').textContent='일시정지';canvas.focus({preventScroll:true});if(soundOn){audio??=new AudioContext();audio.resume();}}
function showResult(){if(!$('#result').hidden)return;const win=world.status==='victory';$('#result-kicker').textContent=win?'TIME RECLAIMED':'TRY AGAIN';$('#result-title').textContent=win?'멈춘 시계':'다시, 한 번 더';$('#result-copy').textContent=`${Math.floor(world.elapsed)}초 · 적중 ${world.stats.hits}회 · 받은 피해 ${world.stats.taken}`;$('#retry').textContent=win?'다시 도전':'재도전';$('#result').hidden=false;}
function togglePause(){paused=!paused;world.paused=paused;keys.clear();pressed.clear();$('#pause').textContent=paused?'계속하기':'일시정지';}
const mapping={KeyA:'left',ArrowLeft:'left',KeyD:'right',ArrowRight:'right',Space:'jump',ShiftLeft:'dash',ShiftRight:'dash',KeyJ:'attack',KeyZ:'attack'};
window.addEventListener('keydown',e=>{if(['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;if(mapping[e.code]){e.preventDefault();if(!e.repeat)pressed.add(mapping[e.code]);keys.add(mapping[e.code]);}else if(!e.repeat&&e.code==='KeyP'){e.preventDefault();togglePause();}else if(!e.repeat&&e.code==='KeyR')reset(world.mode,world.practice);});
window.addEventListener('keyup',e=>{if(mapping[e.code])keys.delete(mapping[e.code]);});
window.addEventListener('blur',()=>{keys.clear();pressed.clear();if(loaded&&world.mode==='play'&&!paused&&['intro','fight'].includes(world.status))togglePause();});
document.addEventListener('visibilitychange',()=>{if(document.hidden&&loaded&&!paused)togglePause();last=0;accumulator=0;});
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;pressed.add('attack');keys.add('attack');canvas.focus();});window.addEventListener('pointerup',()=>{keys.delete('attack');});
document.querySelectorAll('[data-key]').forEach(b=>{b.onpointerdown=e=>{e.preventDefault();const k=b.dataset.key;pressed.add(k);keys.add(k);b.setPointerCapture(e.pointerId);};b.onpointerup=b.onpointercancel=()=>keys.delete(b.dataset.key);});
$('#play').onclick=()=>reset('play');$('#demo').onclick=()=>reset('demo');$('#retry').onclick=()=>reset('play',world.practice);$('#pause').onclick=togglePause;
$('#sound').onclick=()=>{soundOn=!soundOn;$('#sound').textContent=soundOn?'소리 끄기':'소리 켜기';tone(440,.1,'sine',.04,600);};
$('#fullscreen').onclick=()=>document.fullscreenElement?document.exitFullscreen():$('.stage').requestFullscreen();
$('#speed').onchange=e=>{speed=Number(e.target.value)};$('#hitboxes').onchange=e=>debug=e.target.checked;
document.querySelectorAll('.practice').forEach(b=>b.onclick=()=>reset('play',b.dataset.mode));
$('#enrage').onclick=()=>{if(world.status==='intro')world.enterFight();world.elapsed=119.99;};$('#stun').onclick=()=>{world.stun();};$('#death').onclick=()=>{if(world.status==='intro')world.enterFight();world.boss.hp=0;};$('#timeout').onclick=()=>{if(world.status==='intro')world.enterFight();world.elapsed=239.99;};
async function boot(){try{manifest=await (await fetch('assets/asset-manifest.json?rev=open-arena-v12',{cache:'no-store'})).json();await Promise.all([...new Set(allPaths(manifest))].map(loadImage));
  for(const im of Object.values(assets)){const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const cc=c.getContext('2d');cc.drawImage(im,0,0);cc.globalCompositeOperation='source-in';cc.fillStyle='#fff5dc';cc.fillRect(0,0,c.width,c.height);whiteMasks.set(im,c);}
  prepareCollapse();loaded=true;$('#loading').style.display='none';last=0;requestAnimationFrame(frame);
  window.bossBattle={get world(){return world},get manifest(){return manifest},reset,step:(seconds,input={})=>{for(let t=0;t<seconds;t+=1/120){world.update(1/120,input);world.drain().forEach(onEvent);updateEffects(1/120);}draw();},draw,setPaused:v=>{paused=v;world.paused=v},get revision(){return 'open-arena-v12'},get ready(){return loaded}};
}catch(e){$('#loading').textContent='불러오기 실패. 로컬 서버에서 index.html을 열어주세요. '+e.message;console.error(e);}}
fetch('research.json').then(r=>r.json()).then(r=>{const target=$('#research');target.textContent='';for(const item of r.patternMappings||[]){const el=document.createElement('article'),h=document.createElement('h3'),p=document.createElement('p'),fx=document.createElement('p');h.textContent=item.title;p.textContent=item.ourApplication;fx.textContent='효과: '+item.originalFx;el.append(h,p,fx);for(const id of item.sourceIds){const source=r.sources.find(s=>s.id===id);if(!source)continue;const a=document.createElement('a');a.textContent=source.game+' · '+(id==='naamah-visual'?'개발자 영상 34:28 / 34:51':'공격 자료')+' ↗';a.href=source.url;a.target='_blank';a.rel='noopener noreferrer';el.append(a,document.createElement('br'));}target.append(el);}const note=document.createElement('p');note.textContent='Rogue Legacy 2 개발자 영상은 실제 재생 화면을 확인했습니다. 나머지 패턴은 연결된 전투 문서를 조사했으며, 다른 게임의 정확한 프레임 시간까지 측정한 것은 아닙니다.';target.append(note);}).catch(()=>$('#research').textContent='참고 자료는 함께 제공한 research.json을 확인해 주세요.');
boot();
