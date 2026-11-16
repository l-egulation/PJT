// Rose's eye glint for the entrance cutscene, drawn straight onto the 960x540 stage. The small painter and the glint
// port pixelfx (patch-gg/pixelfx, MIT): pickups/_lib glint(). Time is stepped per frame so it reads as pixel animation.
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
// cell: drawn size of one effect pixel; positions are in effect pixels around the origin.
function painter(ctx,ox,oy,cell=1){
 const px=(x,y,c,a=1)=>{if(a<=0)return;ctx.globalAlpha=clamp(a);ctx.fillStyle=c;ctx.fillRect(Math.round(ox+Math.floor(x)*cell),Math.round(oy+Math.floor(y)*cell),cell,cell);};
 return {px,dot:px,
  circle(cx,cy,r,c,a=1){const n=Math.ceil(r);for(let y=-n;y<=n;y++)for(let x=-n;x<=n;x++)if((x+.5)**2+(y+.5)**2<=r*r)px(cx+x,cy+y,c,a);},
  line(x0,y0,x1,y1,c,a=1){const n=Math.max(1,Math.ceil(Math.max(Math.abs(x1-x0),Math.abs(y1-y0))));for(let i=0;i<=n;i++)px(x0+(x1-x0)*i/n,y0+(y1-y0)*i/n,c,a);},
  glow(x,y,r,c,k){const g=ctx.createRadialGradient(ox+x*cell,oy+y*cell,0,ox+x*cell,oy+y*cell,r*cell);g.addColorStop(0,c);g.addColorStop(1,'rgba(0,0,0,0)');
   ctx.save();ctx.globalCompositeOperation='lighter';ctx.globalAlpha=clamp(k);ctx.fillStyle=g;ctx.fillRect(ox+(x-r)*cell,oy+(y-r)*cell,2*r*cell,2*r*cell);ctx.restore();},
  done(){ctx.globalAlpha=1;}};
}
// Rose's eye: a four-armed star that opens, holds a beat and closes (arm length per stepped frame), with a soft glow.
// Her mask is pale, so the star has a dark outline and long arms that reach past the face; a second small star
// pops beside it on the hold. Cells are 1px; the long horizontal arms make the classic anime "반짝".
const GLINT_ARMS=[2,4,7,9,8,8,8,6,4,2,0];
export const GLINT_SECONDS=GLINT_ARMS.length/20;
function star(p,cx,cy,n,core,arm,tip,line){
 const v=Math.max(1,Math.round(n*.7));
 for(let d=1;d<=n;d++){p.px(cx+d,cy-1,line);p.px(cx+d,cy+1,line);p.px(cx-d,cy-1,line);p.px(cx-d,cy+1,line);}
 for(let d=1;d<=v;d++){p.px(cx-1,cy+d,line);p.px(cx+1,cy+d,line);p.px(cx-1,cy-d,line);p.px(cx+1,cy-d,line);}
 p.px(cx+n+1,cy,line);p.px(cx-n-1,cy,line);p.px(cx,cy+v+1,line);p.px(cx,cy-v-1,line);
 for(let d=1;d<=n;d++){const c=d>=n-1&&n>2?tip:arm;p.px(cx+d,cy,c);p.px(cx-d,cy,c);}
 for(let d=1;d<=v;d++){const c=d>=v-1&&v>2?tip:arm;p.px(cx,cy+d,c);p.px(cx,cy-d,c);}
 p.px(cx,cy,core);
}
export function eyeGlint(ctx,x,y,age){
 if(age<0||age>=GLINT_SECONDS)return;const i=Math.floor(age*20),n=GLINT_ARMS[i],p=painter(ctx,x,y,1);if(!n)return;
 p.glow(.5,.5,5+n*1.6,'#fff0b8',.5);
 star(p,0,0,n,'#ffffff','#fff6cf','#ffc94a','#3b1222');
 if(i>=3&&i<=7)star(p,7,-6,i===5?3:2,'#ffffff','#fff6cf','#ffc94a','#3b1222');
 p.done();
}
