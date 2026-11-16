import {drawPlatformGlow} from '../platform-glow.mjs';
// Visual only. Damage remains the specification's two ticks in rules.mjs.
export function drawRain(ctx,bolt,p,age,scale=1){
 if(age<0||age>=2)return;
 const x=p.x,y=p.y,w=p.w;
 ctx.save();ctx.imageSmoothingEnabled=false;
 if(age<1){
  drawPlatformGlow(ctx,p,age,'rose',scale);
 }else{
  ctx.beginPath();ctx.rect(x,y-152*scale,w,152*scale);ctx.clip();
  const t=age-1;
  for(let wave=0;wave<7;wave++)for(let j=0;j<5;j++){
   const seed=(wave*37+j*19+Math.round(x/scale))%97;
   const delay=((seed*13)%29)/1000;
   const life=t-wave*.112-delay;
   const flight=.145+(seed%5)*.009;
   if(life<0||life>flight+.045)continue;
   const jitter=((seed*7)%11)-5;
   const cx=x+(9+j*18+jitter)*scale;
   const height=116+(seed%5)*7;
   const drift=((seed%7)-3)*scale;
   if(life<flight){
    const u=life/flight;
    const tip=y-height*scale+u*height*scale;
    ctx.save();ctx.translate(Math.round(cx+drift*(1-u)),Math.round(tip-12*scale));ctx.rotate(-Math.PI/2+drift/(height*scale));
    ctx.globalAlpha=.16;ctx.drawImage(bolt,-20*scale,-3*scale,24*scale,6*scale);ctx.globalAlpha=1;
    ctx.drawImage(bolt,-12*scale,-3*scale,24*scale,6*scale);ctx.restore();
   }else{
    const fade=1-(life-flight)/.045;ctx.globalAlpha=fade;ctx.fillStyle='#ce8aa8';
    ctx.fillRect(cx-3*scale,y-2*scale,7*scale,2*scale);ctx.fillRect(cx-scale,y-5*scale,2*scale,3*scale);ctx.globalAlpha=1;
   }
  }
 }
 ctx.restore();
}
