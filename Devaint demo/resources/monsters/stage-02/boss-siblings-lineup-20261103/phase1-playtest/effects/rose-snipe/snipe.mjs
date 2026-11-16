// Visual-only reticle and instantaneous strike. No travelling projectile.
export function drawSnipeAim(ctx,x,y,progress,locked=false,scale=1){
 const t=Math.max(0,Math.min(1,progress));
 const radius=locked?32:43-11*t;
 ctx.save();ctx.translate(Math.round(x),Math.round(y));ctx.scale(scale,scale);
 const g=ctx.createRadialGradient(0,0,12,0,0,50);
 g.addColorStop(0,'rgba(197,99,118,0)');g.addColorStop(.7,`rgba(197,99,118,${locked?.42:.24})`);g.addColorStop(1,'rgba(197,99,118,0)');ctx.fillStyle=g;ctx.fillRect(-51,-51,102,102);
 ctx.lineWidth=locked?4:3;
 const rotation=locked?0:t*1.4;
 for(let j=0;j<4;j++){
  const a=rotation+j*Math.PI/2;
  ctx.beginPath();ctx.arc(0,0,radius,a+.12,a+1.18);ctx.strokeStyle='#54283e';ctx.lineWidth=locked?7:6;ctx.stroke();ctx.strokeStyle=locked?'#fff0f6':'#df9aa8';ctx.lineWidth=locked?4:3;ctx.stroke();
 }
 ctx.fillStyle=locked?'#fff0f6':'#e9b0bc';
 for(const sign of [-1,1]){ctx.fillRect(sign*13-2,-2,5,4);ctx.fillRect(-2,sign*13-2,4,5)}
 if(locked){ctx.fillRect(-3,-3,6,6);ctx.strokeStyle='#c56376';ctx.lineWidth=2;ctx.beginPath();ctx.arc(0,0,radius+6,0,Math.PI*2);ctx.stroke();}
 ctx.restore();
}
export function drawSnipeImpact(ctx,x,y,age,scale=1){
 if(age<0||age>.16)return;
 // Tiny seed -> rapid expansion -> broad, fading afterglow.
 const grow=Math.min(1,age/.035);
 const size=age<.035?.12+.88*grow:age<.085?1:1+(age-.085)*1.5;
 const t=Math.max(0,(age-.085)/.075);
 const fade=age<.115?1:.55;
 ctx.save();ctx.translate(Math.round(x),Math.round(y));ctx.scale(scale*size,scale*size);
 // Rose's costume accent sampled from the approved lineup: #c56376.
 let g=ctx.createRadialGradient(0,0,0,0,0,66+10*t);
 g.addColorStop(0,`rgba(255,255,255,${fade})`);
 g.addColorStop(.13,`rgba(255,247,249,${fade*.98})`);
 g.addColorStop(.35,`rgba(229,158,172,${fade*.16})`);
 g.addColorStop(.62,`rgba(197,99,118,${fade*.16})`);
 g.addColorStop(1,'rgba(197,99,118,0)');
 ctx.fillStyle=g;ctx.fillRect(-78,-78,156,156);
 // Expanding broken shock ring, followed by sharp directional impact rays.
 const ring=24+37*Math.min(1,age/.22);
 ctx.globalAlpha=(age<.10?.7:0);ctx.strokeStyle='#fce8ee';ctx.lineWidth=1;
 for(let j=0;j<3;j++){const a=j*Math.PI*2/3+.2;ctx.beginPath();ctx.arc(0,0,ring,a,a+1.62);ctx.stroke();}
 ctx.rotate(-.48);
 const length=66;
 const needle=age<.085?1:Math.max(.12,1-t);
 ctx.scale(1,needle);
 g=ctx.createRadialGradient(0,0,0,0,0,length);
 g.addColorStop(0,`rgba(255,255,255,${fade})`);
 g.addColorStop(.3,`rgba(255,251,252,${fade})`);
 g.addColorStop(.62,`rgba(229,158,172,${fade*.65})`);
 g.addColorStop(1,'rgba(197,99,118,0)');
 // Only the halo is soft; the piercing silhouette stays crisp.
 ctx.globalAlpha=1;ctx.fillStyle=g;
 ctx.beginPath();ctx.moveTo(-69,0);ctx.lineTo(-8,-2);ctx.lineTo(-24,-43);ctx.lineTo(-1,-5);ctx.lineTo(10,-51);ctx.lineTo(3,-2);ctx.lineTo(69,0);ctx.lineTo(7,2);ctx.lineTo(29,40);ctx.lineTo(1,5);ctx.lineTo(-13,49);ctx.lineTo(-3,2);ctx.closePath();ctx.fill();
 ctx.globalAlpha=fade;ctx.fillStyle='#ffffff';
 ctx.beginPath();ctx.moveTo(-39,0);ctx.lineTo(-5,-2);ctx.lineTo(-14,-20);ctx.lineTo(-1,-4);ctx.lineTo(6,-26);ctx.lineTo(3,-2);ctx.lineTo(44,0);ctx.lineTo(5,2);ctx.lineTo(17,20);ctx.lineTo(1,4);ctx.lineTo(-7,25);ctx.lineTo(-3,2);ctx.closePath();ctx.fill();
 // Fine needle streaks detach briefly, reinforcing the arrow-tip direction.
 ctx.strokeStyle='#fff7fa';ctx.lineWidth=1;
 const streak=Math.max(0,age<.11?1:0);
 ctx.globalAlpha=streak;
 for(const [a,len] of [[-.8,24],[1.1,19],[2.6,27]]){
  const near=23+age*45;
  ctx.beginPath();ctx.moveTo(Math.cos(a)*near,Math.sin(a)*near);ctx.lineTo(Math.cos(a)*(near+len),Math.sin(a)*(near+len));ctx.stroke();
 }
 ctx.restore();
}
