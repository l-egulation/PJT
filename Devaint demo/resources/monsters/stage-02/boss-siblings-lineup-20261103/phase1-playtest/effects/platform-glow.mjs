// Soft surface illumination shared by both warnings.
export function drawPlatformGlow(ctx,p,progress,kind='ink',scale=1){
 const t=Math.max(0,Math.min(1,progress));
 const rgb=kind==='trap'?'222,85,93':kind==='rose'?'224,103,160':'155,99,224';
 const bright=kind==='trap'?'255,180,166':kind==='rose'?'255,199,224':'222,191,255';
 const strength=(.52+.38*t)*(.95+.05*Math.sin(t*Math.PI*5));
 const cx=p.x+p.w/2;
 ctx.save();
 ctx.translate(cx,p.y);ctx.scale(p.w*.66,(24+12*t)*scale);
 let glow=ctx.createRadialGradient(0,0,0,0,0,1);
 glow.addColorStop(0,`rgba(${rgb},${strength*.78})`);
 glow.addColorStop(.32,`rgba(${rgb},${strength*.54})`);
 glow.addColorStop(.65,`rgba(${rgb},${strength*.20})`);
 glow.addColorStop(1,`rgba(${rgb},0)`);
 ctx.fillStyle=glow;ctx.fillRect(-1,-1,2,2);
 ctx.restore();
 ctx.save();
 ctx.translate(cx,p.y+2*scale);ctx.scale(p.w*.56,9*scale);
 glow=ctx.createRadialGradient(0,0,0,0,0,1);
 glow.addColorStop(0,`rgba(${bright},${strength*.80})`);
 glow.addColorStop(.45,`rgba(${rgb},${strength*.65})`);
 glow.addColorStop(1,`rgba(${rgb},0)`);
 ctx.fillStyle=glow;ctx.fillRect(-1,-1,2,2);
 ctx.restore();
}
