export const PALETTE=['#0a0910','#0a0910','#17131f','#17131f','#17131f','#30263e','#30263e'];
const BAYER=[0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5];
export const FLOOR=130,DT=1/60;
export function dither(v,x,y){v=Math.max(0,Math.min(6,v));const n=Math.floor(v);return Math.min(6,n+((v-n)>(BAYER[((y&3)*4)+(x&3)]+.5)/16?1:0));}
export function geometry(age,{height=65,width=8,hold=.8}){
 if(age<.28)return {phase:'예고',height:0,width};
 if(age<.52){const t=(age-.28)/.24;return {phase:'분출',height:height*(1.65*t-.65*t*t),width};}
 if(age<.52+hold)return {phase:'유지',height,width};
 const t=Math.min(1,(age-.52-hold)/.42);return {phase:t<1?'붕괴':'종료',height:height*(1-t*t),width:width*(1-.55*t)};
}
export function halfWidth(y,height,width){return width+width*.9*(1-y/Math.max(1,height))**6;}
const hash=(x,y)=>{const v=Math.sin(x*127.1+y*311.7)*43758.5453;return v-Math.floor(v);};
function noise(x,y){const a=Math.floor(x),b=Math.floor(y),u=x-a,v=y-b,s=u*u*(3-2*u),t=v*v*(3-2*v);return (hash(a,b)*(1-s)+hash(a+1,b)*s)*(1-t)+(hash(a,b+1)*(1-s)+hash(a+1,b+1)*s)*t;}
export class WaterColumn {
 constructor(seed=319){this.seed=seed;this.time=0;this.columns=[];this.drops=[];this.ripples=[];this.groundRings=[];this.shake=0;}
 random(){this.seed=(1664525*this.seed+1013904223)>>>0;return this.seed/4294967296;}
 emit(x,options={}){const opts={height:65,width:8,hold:.8,...options};x=Math.round(Math.max(opts.width*1.9+3,Math.min(237-opts.width*1.9,x)));this.columns.push({x,opts,age:0,burst:false});}
 drop(x,y,vx,vy,life=1,foam=false){this.drops.push({x,y,vx,vy,life,max:life,foam});}
 ripple(x,size=3){this.ripples.push({x,age:0,size});}
 step(dt=DT){this.time+=dt;this.shake=Math.max(0,this.shake-dt);
 for(const r of this.groundRings)r.age+=dt;this.groundRings=this.groundRings.filter(r=>r.age<.72);
 for(const r of this.ripples)r.age+=dt;this.ripples=this.ripples.filter(r=>r.age<.42);
 for(const c of this.columns){c.age+=dt;const g=geometry(c.age,c.opts);
 if(c.age<.28){if(this.random()<.55)this.drop(c.x+(this.random()-.5)*c.opts.width*2,FLOOR-1,0,-12-this.random()*16,.12+this.random()*.16,true);}
 if(c.age>=.28&&c.age<.52+c.opts.hold&&(!c.nextRing||c.age>=c.nextRing)){this.groundRings.push({x:c.x,width:c.opts.width,scale:c.opts.ringScale||1,age:0});c.nextRing=c.age+.28;}
 if(c.age>=.28&&!c.burst){c.burst=true;this.shake=.25;for(let i=0;i<24;i++){const side=i%2?1:-1;this.drop(c.x,FLOOR-2,side*(18+this.random()*65),-25-this.random()*65,.45+this.random()*.35,i%7===0);}}
 if(g.phase==='유지'&&this.random()<.18){const side=this.random()<.5?-1:1;this.drop(c.x+side*g.width*.8,FLOOR-2,side*(16+this.random()*22),-25-this.random()*30,.4,false);}
 const count=g.phase==='분출'?2:g.phase==='유지'?(this.random()<.6?1:0):g.phase==='붕괴'?(this.random()<.7?1:0):0;
 for(let i=0;i<count;i++){const speed=30+this.random()*60;this.drop(c.x+(this.random()-.5)*g.width*1.8,FLOOR-g.height,(this.random()-.5)*24,-speed,.35+this.random()*.35,i%2===0&&this.random()<.25);}
 }
 this.columns=this.columns.filter(c=>geometry(c.age,c.opts).phase!=='종료');
 for(const d of this.drops){d.life-=dt;d.vy+=260*dt;d.x+=d.vx*dt;d.y+=d.vy*dt;if(d.y>=FLOOR){d.life=0;}}
 this.drops=this.drops.filter(d=>d.life>0);
 }
 render(ctx,shake=true,{transparent=false,floor=true}={}){if(transparent)ctx.clearRect(0,0,240,150);else{ctx.fillStyle='#24343f';ctx.fillRect(0,0,240,150);}ctx.save();if(shake&&this.shake>0){const amp=2*this.shake/.25;ctx.translate(Math.round(Math.sin(this.time*173)*amp),Math.round(Math.cos(this.time*127)*amp));}
 const pixel=(x,y,k)=>{ctx.fillStyle=PALETTE[k];ctx.fillRect(Math.round(x),Math.round(y),1,1);};
 if(floor){ctx.fillStyle='#52616a';for(const c of this.columns)ctx.fillRect(c.x-12,FLOOR,24,3);if(!this.columns.length)ctx.fillRect(108,FLOOR,24,3);}
 // Low elliptical pressure waves leave a separate afterimage around the foot.
 for(const r of this.groundRings){const t=r.age/.72;const rx=Math.max(8,r.width*1.5)+t*10*r.scale,ry=(1.5+t*3.2)*r.scale;const visibility=1-t*.75;
 for(let dx=-Math.ceil(rx);dx<=rx;dx++){const z=1-dx*dx/(rx*rx);if(z<0)continue;const yy=Math.round(Math.sqrt(z)*ry);for(const side of [-1,1]){if(hash(dx+31,side*7+Math.floor(t*4))>visibility)continue;ctx.fillStyle=t<.5?'#51405f':'#30263e';ctx.fillRect(Math.round(r.x+dx),FLOOR+side*yy+1,1,1);if(t<.22&&Math.abs(dx)<rx*.7)ctx.fillRect(Math.round(r.x+dx),FLOOR+side*yy+2,1,1);}}
 }
 for(const c of this.columns){const g=geometry(c.age,c.opts);const warning=Math.min(1,c.age/.28);const h=Math.max(0,Math.round(g.height));
 // Flattened puddle, rasterized without antialiasing.
 const pw=c.opts.width*(.6+warning*1.7);for(let y=-2;y<=1;y++)for(let x=-Math.ceil(pw);x<=pw;x++)if(x*x/(pw*pw)+y*y/9<=1)pixel(c.x+x,FLOOR+y,dither(1.5+warning,x,y));
 if(c.age<.28&&c.age>.1){for(let x=-Math.ceil(pw);x<=pw;x++)if(Math.abs(x)>pw*.75)pixel(c.x+x,FLOOR-2,3);}
 for(let row=0;row<h;row++){const y=h-row;const hw=halfWidth(y,c.opts.height,g.width);for(let dx=-Math.ceil(hw);dx<=Math.ceil(hw);dx++){if(Math.abs(dx)>hw)continue;const edge=1-Math.abs(dx)/hw;let v;
 const n=dx/hw;const center=Math.max(0,1-Math.abs(n+.18)/1.18);v=2.2+center*.9;
 v+=(noise(dx*.24,(y-(c.age-.28)*180)*.13)-.5)*1.6;
 const stripe=Math.max(0,1-Math.abs(n+.34)/.20);const segment=((y-(c.age-.28)*240)%40+40)%40;v+=stripe*(segment<26?1.35:.1);if(g.phase==='분출'&&h-y<4&&Math.abs(n)<.72)v+=1.2;
 pixel(c.x+dx,FLOOR-y,dither(v,c.x+dx,FLOOR-y));}}
 }
 // Shallow splash basin: broad pooled ink with a broken raised lip, not expanding rings.
 for(const c of this.columns){if(c.age<.28)continue;const g=geometry(c.age,c.opts);const strength=g.phase==='붕괴'?Math.max(0,1-(c.age-.52-c.opts.hold)/.42):1;const w=Math.max(11,c.opts.width*1.65);
 for(let dx=-Math.ceil(w);dx<=w;dx++){const e=Math.sqrt(Math.max(0,1-dx*dx/(w*w)));for(let yy=-Math.round(e*2);yy<=1;yy++)pixel(c.x+dx,FLOOR+yy,2);
 const lip=-Math.round(e*3)-1;const flicker=hash(Math.floor(dx/2),Math.floor(c.age*10));
 if(Math.abs(dx)>w*.45&&flicker>.13){pixel(c.x+dx,FLOOR+lip,5);if(Math.abs(dx)>w*.78)pixel(c.x+dx,FLOOR+lip-1,5);}
 if(Math.abs(dx)<w*.78&&flicker>.3)pixel(c.x+dx,FLOOR+2+Math.round(e),5);}
 for(const side of [-1,1]){const x=c.x+side*w*.87;const lift=Math.round((2+2*Math.sin(c.age*19+side))*strength);for(let yy=1;yy<=lift;yy++)pixel(x+side*Math.floor(yy/3),FLOOR-2-yy,yy===lift?5:3);}
 }
 for(const d of this.drops){const k=d.foam?6:Math.max(1,Math.min(6,Math.ceil(d.life/d.max*5)));pixel(d.x,d.y,k);if(!d.foam&&d.max>.45){pixel(d.x+1,d.y,k);pixel(d.x,d.y+1,k);if(d.max>.65)pixel(d.x+1,d.y+1,k); }const speed=Math.hypot(d.vx,d.vy);if(!d.foam&&speed>65){for(let i=1;i<=(speed>110?2:1);i++)pixel(d.x-d.vx/speed*i,d.y-d.vy/speed*i,Math.max(1,k-1));}}
 ctx.restore();
 }
}
