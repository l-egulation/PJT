// Contact-only shield bash VFX. Does not move the actor or resolve damage.
function shieldBasicContact(ctx,age,x,y){
 if(age<0||age>=.19)return;
 const clamp=n=>Math.max(0,Math.min(1,n));
 const kick=1-(1-clamp(age/.026))**3;
 const coreFade=1-clamp((age-.038)/.070);
 const tailFade=(1-clamp((age-.065)/.125))**2;
 ctx.save();ctx.translate(x,y);ctx.imageSmoothingEnabled=true;
 // Short, vertically compressed impact on the shield face, with feathered edges.
 const halo=ctx.createRadialGradient(2,0,1,2,0,25);
 halo.addColorStop(0,'rgba(255,255,250,.88)');
 halo.addColorStop(.30,'rgba(255,255,249,.55)');
 halo.addColorStop(1,'rgba(255,255,249,0)');
 ctx.save();ctx.scale(.85,1);ctx.globalAlpha=coreFade;ctx.fillStyle=halo;
 ctx.fillRect(-25,-25,54,50);ctx.restore();
 if(coreFade>0){
  const w=3+kick*6,h=6+kick*10;
  ctx.globalAlpha=coreFade;ctx.fillStyle='#fffef9';
  ctx.beginPath();ctx.moveTo(-w*.55,-h*.55);
  ctx.quadraticCurveTo(2,-h-2,w*.75,-h*.77);
  ctx.quadraticCurveTo(w+5,-h*.2,w+3,1);
  ctx.quadraticCurveTo(w+2,h*.7,1,h);
  ctx.quadraticCurveTo(-w,h*.55,-w*.55,-h*.55);ctx.fill();
 }
 // Three compact forward impulses grow from contact, then taper away in place.
 for(let i=0;i<3;i++){
  const a=age-[.005,0,.010][i];if(a<0)continue;
  const launch=1-(1-clamp(a/.055))**3;
  const fade=1-clamp((a-.040)/.105),thin=1-clamp((a-.030)/.12);
  if(fade<=0)continue;
  const length=[21,30,23][i]*launch,width=[3.3,4.4,3.0][i]*thin;
  const angle=[-.53,0,.48][i];
  ctx.save();ctx.translate(3,[-3,0,3][i]);ctx.rotate(angle);
  const wake=ctx.createLinearGradient(0,0,Math.max(1,length),0);
  wake.addColorStop(0,'rgba(255,255,250,.86)');
  wake.addColorStop(.60,'rgba(255,255,250,.96)');
  wake.addColorStop(1,'rgba(255,255,250,0)');
  ctx.fillStyle=wake;ctx.globalAlpha=fade;
  ctx.beginPath();ctx.moveTo(0,-width*.5);
  ctx.quadraticCurveTo(length*.35,-width,length*.80,-width*.24);
  ctx.lineTo(length,0);ctx.lineTo(length*.80,width*.24);
  ctx.quadraticCurveTo(length*.35,width,0,width*.5);ctx.closePath();
  ctx.save();ctx.filter='blur(2px)';ctx.globalAlpha=fade*.5;ctx.fill();ctx.restore();ctx.fill();
  ctx.restore();
 }
 // A pair of little contact flecks disappear before they can float like debris.
 if(age>.015&&age<.105){
  const q=clamp((age-.015)/.09);ctx.globalAlpha=(1-q)*.8;ctx.strokeStyle='#fffef9';ctx.lineWidth=2.2*(1-q)+.2;
  for(const side of [-1,1]){
   ctx.beginPath();ctx.moveTo(2+q*6,side*(10+q*5));ctx.lineTo(6+q*9,side*(13+q*7));ctx.stroke();
  }
 }
 ctx.globalAlpha=tailFade*.32;
 const residue=ctx.createRadialGradient(7,0,0,7,0,12+kick*7);
 residue.addColorStop(0,'rgba(255,255,250,.7)');residue.addColorStop(1,'rgba(255,255,250,0)');
 ctx.fillStyle=residue;ctx.fillRect(-13,-20,42,40);
 ctx.restore();
}
