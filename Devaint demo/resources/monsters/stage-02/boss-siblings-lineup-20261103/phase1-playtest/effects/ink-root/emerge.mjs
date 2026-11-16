// Native-pixel translation through the ground plane; never scale the player.
export function drawRoot(ctx,image,cx,foot,scale,age,remaining=2){
 const steps=[30,24,15,6,-2,-1,0];
 const step=Math.min(steps.length-1,Math.floor(Math.max(0,age)/.045));
 const sink=remaining<.15?Math.round((1-remaining/.15)*30):0;
 const shift=steps[step]+sink;
 ctx.save();ctx.imageSmoothingEnabled=false;
 ctx.beginPath();ctx.rect(Math.round(cx-37*scale),Math.round(foot-37*scale),70*scale,39*scale);ctx.clip();
 ctx.drawImage(image,Math.round(cx-32*scale),Math.round(foot+(-28+shift)*scale),56*scale,30*scale);
 // A brief upward splash accompanies emergence, then drops back to the floor.
 if(age>.045&&age<.27){
  const t=(age-.045)/.225;
  ctx.fillStyle='#63476f';
  for(const [dx,peak] of [[-29,15],[23,11],[-7,21]]){
   const rise=Math.round(4*peak*t*(1-t));
   ctx.fillRect(Math.round(cx+(dx+(dx<0?-1:1)*t*3)*scale),Math.round(foot-(2+rise)*scale),2*scale,2*scale);
  }
 }
 ctx.restore();
}
