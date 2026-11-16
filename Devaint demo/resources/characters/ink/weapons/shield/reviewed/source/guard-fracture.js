// Persistent damage belongs to the shell surface, not to the moving inner pattern.
function createGuardFracture(ctx,animatedOrb){
 const surface=document.createElement('canvas');surface.width=surface.height=256;
 const g=surface.getContext('2d');let cachedAge=-1,cachedHp=-1;
 const clamp=n=>Math.max(0,Math.min(1,n));
 const cracks=[
  {at:.01,p:[[.73,.06],[.48,-.02],[.27,.11],[.03,.02],[-.20,.20]]},
  {at:.12,p:[[.48,-.02],[.43,-.28],[.23,-.44],[.26,-.70]]},
  {at:.27,p:[[.27,.11],[.31,.38],[.10,.56],[.13,.81]]},
  {at:.36,p:[[.03,.02],[-.18,-.18],[-.48,-.24],[-.69,-.47]]},
  {at:.47,p:[[-.20,.20],[-.42,.40],[-.68,.32],[-.88,.48]]},
  {at:.57,p:[[.43,-.28],[.69,-.40],[.66,-.64],[.82,-.65]]},
  {at:.66,p:[[-.18,-.18],[-.23,-.43],[-.11,-.63],[-.22,-.88]]},
  {at:.75,p:[[.31,.38],[.61,.48],[.68,.70],[.50,.84]]},
  {at:.84,p:[[-.42,.40],[-.38,.65],[-.48,.81]]}
 ];
 function path(points,progress){
  const count=(points.length-1)*progress;g.beginPath();
  g.moveTo(128+points[0][0]*116,128+points[0][1]*116);
  for(let i=1;i<points.length;i++){
   const q=clamp(count-i+1),a=points[i-1],b=points[i];
   if(q<=0)break;g.lineTo(128+(a[0]+(b[0]-a[0])*q)*116,128+(a[1]+(b[1]-a[1])*q)*116);
  }
 }
 function damagedOrb(age,hp){
  if(age===cachedAge&&hp===cachedHp)return surface;
  cachedAge=age;cachedHp=hp;const damage=clamp(1-hp/30);
  g.clearRect(0,0,256,256);g.drawImage(animatedOrb(age),0,0);
  g.save();g.beginPath();g.arc(128,128,115,0,Math.PI*2);g.clip();
  for(const c of cracks){
   const growth=clamp((damage-c.at)/.22);if(growth<=0)continue;
   // Cut a narrow real gap, bordered by pearl-white fracture faces.
   g.globalCompositeOperation='destination-out';g.lineWidth=1.2+damage*3;
   path(c.p,growth);g.stroke();
   g.globalCompositeOperation='source-over';g.strokeStyle='rgba(249,255,255,.96)';g.lineWidth=1.4+damage;
   g.save();g.translate(-1.8,-1);path(c.p,growth);g.stroke();g.restore();
   g.strokeStyle='rgba(221,239,247,.72)';g.lineWidth=.8;
   g.save();g.translate(1.5,1);path(c.p,growth);g.stroke();g.restore();
  }
  g.restore();return surface;
 }
 function residual(t,x,y,front){
  if(!front)return;ctx.save();
  // An understated body veil indicates the surviving damage-reduction buff.
  const alpha=.13+.025*Math.sin(t*5);
  for(const side of [-1,1]){
   const grad=ctx.createLinearGradient(x+side*12,0,x+side*22,0);
   grad.addColorStop(0,'rgba(245,253,255,0)');grad.addColorStop(.5,'rgba(245,253,255,'+alpha+')');grad.addColorStop(1,'rgba(245,253,255,0)');
   ctx.fillStyle=grad;ctx.fillRect(x+(side<0?-22:12),y-8,10,48);
  }
  ctx.restore();
 }
 function shatter(age,orb,x,y,width,height,front){
  if(age<0||age>.38)return;
  const q=clamp(age/.38),travel=1-(1-q)**2,opacity=(1-q)**1.4;
  const cuts=[-.40,.53,1.43,2.42,3.50,4.50,5.88];
  for(let i=0;i<6;i++){
   if((i%2===0)!==front)continue;
   const mid=(cuts[i]+cuts[i+1])/2,dx=Math.cos(mid)*travel*(13+i%2*7),dy=Math.sin(mid)*travel*13+q*q*22;
   ctx.save();ctx.translate(x+dx,y+dy);ctx.rotate((i%2?1:-1)*q*.19);
   ctx.globalAlpha=opacity*(front?.82:.65);ctx.imageSmoothingEnabled=true;
   ctx.beginPath();ctx.moveTo(3,-1);
   for(let j=0;j<=8;j++){const angle=cuts[i]+(cuts[i+1]-cuts[i])*j/8;ctx.lineTo(Math.cos(angle)*width*.49,Math.sin(angle)*height*.49);}
   ctx.closePath();ctx.clip();ctx.drawImage(orb,-width/2,-height/2,width,height);ctx.restore();
  }
  if(front&&age<.065){
   const flash=1-age/.065,g=ctx.createRadialGradient(x+25,y,0,x+25,y,31);
   g.addColorStop(0,'rgba(255,255,249,'+flash*.55+')');g.addColorStop(1,'rgba(255,255,249,0)');ctx.save();ctx.fillStyle=g;ctx.fillRect(x-6,y-31,62,62);ctx.restore();
  }
 }
 return {render(t,x,y,front,hp,hitAge,brokenAge){
  const age=t-.1;if(age<0||t>=3.2)return;
  const reveal=1-(1-clamp(age/.13))**3,width=120*(.85+.15*reveal),height=118*(.85+.15*reveal);
  if(hp<=0){
   residual(t,x,y,front);
   // Freeze the pattern at the fracture moment while the pieces separate.
   shatter(brokenAge,damagedOrb(Math.max(0,age-brokenAge),.01),x,y,width,height,front);return;
  }
  const orb=damagedOrb(age,hp),opacity=(1-clamp((t-2.97)/.23))*reveal;
  ctx.save();ctx.translate(x,y);ctx.imageSmoothingEnabled=true;
  if(!front){ctx.globalAlpha=opacity*.30;ctx.filter='blur(2.2px)';ctx.drawImage(orb,-width*.535,-height*.535,width*1.07,height*1.07);ctx.filter='none';}
  ctx.globalAlpha=opacity*(front?.34:.77);ctx.drawImage(orb,-width/2,-height/2,width,height);
  // A brief local glint makes newly extended cracks readable on impact.
  if(front&&hitAge>=0&&hitAge<.09){ctx.globalAlpha=(1-hitAge/.09)*.32;ctx.globalCompositeOperation='screen';ctx.drawImage(orb,-width/2,-height/2,width,height);}
  ctx.restore();
 }};
}
