// Accepted ranged effects, shared by the 11-weapon gallery.
const RangedFX=(()=>{
 const W=480,H=220,OX=12,OY=50,GROUND=172, sum=a=>a.reduce((n,s)=>n+s.ms,0),fxcache={};
 const byId=Object.fromEntries(FXD.weapons.map(w=>[w.id,w]));
 const load=src=>new Promise((ok,no)=>{const im=new Image();im.onload=()=>ok(im);im.onerror=no;im.src=src;});
 function pick(seq,t){for(const s of seq){if(t<s.ms)return s;t-=s.ms;}return seq.at(-1);}
function events(w,mode){let t=0;const out=[];for(const s of w.modules[mode].steps){if(s.frame===w.fireFrames[mode])out.push({start:t,end:t+s.ms});t+=s.ms;}return out;}
function frameAt(v,age,loop=false){const duration=v.durations.reduce((a,b)=>a+b,0);if(loop)age%=duration;if(age<0||age>=duration)return -1;for(let i=0;i<v.durations.length;i++){if(age<v.durations[i])return i;age-=v.durations[i];}return -1;}

function sample(w,v,clock,mode,eventList=events(w,mode)){
 const duration=sum(w.modules[mode].steps),t=clock.t,body=(t<0||t>=duration)?pick(w.modules.idle.steps,Math.max(0,t-duration)%sum(w.modules.idle.steps)).frame:pick(w.modules[mode].steps,t).frame;
 const effects=[],projectiles=[],beams=[];
 for(const event of eventList){
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

 const ready=Promise.all(FXD.weapons.map(async w=>{fxcache[w.id]={};for(const v of w.variants){const fc={frames:await Promise.all(v.frames.map(load))};if(v.muzzle)fc.muzzle=await Promise.all(v.muzzle.frames.map(load));if(v.projectile)fc.projectile=await load(v.projectile);fxcache[w.id][v.id]=fc;}}));
 function eventEnd(w,v,e){return w.id==='laser'?e.end:e.start+(v.travelMs||0)+v.durations.reduce((a,b)=>a+b,0);}
 function endTime(full,key){const w=byId[full.id];if(!w||!['attack','single'].includes(key))return 0;const mode=key==='single'?'single':'burst',v=w.variants[0];return Math.max(0,...events(w,mode).map(e=>eventEnd(w,v,e)));}
 function samples(full,state){
  const w=byId[full.id];if(!w||!['attack','single'].includes(state.key))return [];
  const mode=state.key==='single'?'single':'burst',v=w.variants[0],ev=events(w,mode);
  const one=(event,t)=>sample(w,v,{t,laserAge:t-event.start,laserHeld:w.id==='laser'&&t>=event.start&&t<event.end},mode,[event]);
  if(state.manual)return state.history.flatMap(inst=>{const event=ev[inst.number-1];if(!event)return [];const h=full.hits[inst.number-1];return [one(event,h.start+state.t-inst.started)];});
  if(state.selectedHit){const event=ev[state.selectedHit-1];return event?[one(event,state.fxTime)]:[];}
  return ev.map(e=>one(e,state.fxTime));
 }
 function draw(ctx,full,state,scale){
  const w=byId[full.id];if(!w)return;const v=w.variants[0];
  ctx.save();ctx.scale(scale,scale);ctx.translate(D.ground[0]-96,D.ground[1]-172);
  for(const s of samples(full,state)){
     for(const b of s.beams){const x=Math.round(b.x),y=Math.round(b.y),frames=fxcache[w.id][v.id].frames;ctx.save();ctx.imageSmoothingEnabled=true;ctx.globalAlpha=1-b.mix;ctx.drawImage(frames[b.f],x,y-48,b.end-x,96);ctx.globalAlpha=b.mix;ctx.drawImage(frames[b.next],x,y-48,b.end-x,96);ctx.restore();ctx.fillStyle='#fff';ctx.fillRect(x,y-Math.floor(b.width/2),b.end-x,b.width);}
  for(const p of s.projectiles){const im=fxcache[w.id][v.id].projectile;ctx.save();ctx.globalAlpha=p.alpha;ctx.translate(Math.round(p.x),Math.round(p.y));ctx.rotate(p.angle*Math.PI/180);ctx.drawImage(im,-im.width*p.scale/2,-im.height*p.scale/2,im.width*p.scale,im.height*p.scale);ctx.restore();}
  for(const e of s.effects){const pivot=e.part==='muzzle'?v.muzzle.pivot:v.pivot,im=fxcache[w.id][v.id][e.part][e.f];ctx.save();ctx.translate(Math.round(e.x),Math.round(e.y));ctx.rotate(e.angle*Math.PI/180);ctx.drawImage(im,-pivot[0]*e.scale,-pivot[1]*e.scale,im.width*e.scale,im.height*e.scale);ctx.restore();}
  }
  ctx.restore();
 }
 return {ready,draw,samples,endTime,events,sample};
})();
