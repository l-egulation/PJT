// Blue's roar pillar, ported from the user-approved "블루 포효 원기둥" test (04_테스트, 2026-11-05): thin beam, one-frame
// inversion, stepped violet column with a dark ragged foot, broken spiral ribbons, flicked ink drops, a low floor film
// and rising specks, then the column collapses to a line. Geometry is scaled by S so the column keeps the approved
// proportion to Blue (102px tall there, 139px here). The cutscene version adds a violet dimming of the whole room and
// more specks so the moment carries the transformation.
export const PILLAR={glint:0,beam:.38,flash:.58,full:.64,swap:1.5,collapse:2.3,line:2.62,gone:2.78,end:3.8};
const T=PILLAR,S=139/102,W=960,H=540;
const C=h=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
// palette sampled from fused-blue-arrival-04, plus one pale core step
const V=["#19182d","#242646","#3c1949","#562362","#762d83","#9e39b0","#c553ce","#f097e7","#ffd6f6"].map(C);
const TAU=Math.PI*2,clamp=(v,a=0,b=1)=>v<a?a:v>b?b:v,lerp=(a,b,t)=>a+(b-a)*t,ease=t=>1-(1-t)*(1-t),easeIn=t=>t*t;
function hash(n){const s=Math.sin(n*127.1+311.7)*43758.5453;return s-Math.floor(s);}
function n1(x,s=0){const i=Math.floor(x),f=x-i,u=f*f*(3-2*f);return lerp(hash(i+s*57.3),hash(i+1+s*57.3),u)*2-1;}
function n2(x,y,s=0){const i=Math.floor(x),j=Math.floor(y),f=x-i,g=y-j,u=f*f*(3-2*f),v=g*g*(3-2*g);
 const h=(a,b)=>hash(a*12.9898+b*78.233+s*37.7);return lerp(lerp(h(i,j),h(i+1,j),u),lerp(h(i,j+1),h(i+1,j+1),u),v)*2-1;}
class Px{
 constructor(img){this.img=img;this.d=img.data;}
 px(x,y,c,a=1){x=Math.floor(x);y=Math.floor(y);if(x<0||y<0||x>=W||y>=H||a<=0)return;const i=(y*W+x)*4,d=this.d;
  if(a>=1){d[i]=c[0];d[i+1]=c[1];d[i+2]=c[2];return;}d[i]+=(c[0]-d[i])*a;d[i+1]+=(c[1]-d[i+1])*a;d[i+2]+=(c[2]-d[i+2])*a;}
 field(x0,y0,x1,y1,dens,paint){for(let y=Math.max(0,Math.floor(y0));y<=Math.min(H-1,y1);y++)for(let x=Math.max(0,Math.floor(x0));x<=Math.min(W-1,x1);x++){const v=dens(x+.5,y+.5);if(v>0)paint(x,y,v);}}
 invert(){const d=this.d;for(let i=0;i<d.length;i+=4){const l=255-(d[i]*.3+d[i+1]*.59+d[i+2]*.11);const v=l>150?Math.min(255,l+40):l*.25;d[i]=d[i+1]=d[i+2]=v;}}
 dim(a){const d=this.d,c=V[1];for(let i=0;i<d.length;i+=4){d[i]+=(c[0]-d[i])*a;d[i+1]+=(c[1]-d[i+1])*a;d[i+2]+=(c[2]-d[i+2])*a;}}
}
function radius(t){
 if(t<T.beam)return 0;if(t<T.flash)return 1.5;
 if(t<T.collapse)return (64+3*Math.sin(t*9))*S*ease(clamp((t-T.flash)/.1));
 if(t<T.line)return lerp(64*S,1.5,easeIn(clamp((t-T.collapse)/(T.line-T.collapse))));
 if(t<T.gone)return 1.5;return 0;}
function beam(p,t,R,CX,FY){
 if(R<=0)return;
 if(R<3){const flick=t<T.flash?(Math.floor(t*50)%3?1:.6):1-clamp((t-T.line)/(T.gone-T.line));
  for(let y=0;y<FY;y++){const top=t>=T.line?lerp(0,FY,clamp((t-T.line)/(T.gone-T.line))):0;if(y<top)continue;
   p.px(CX,y,V[8],flick);p.px(CX-1,y,V[7],.85*flick);p.px(CX+1,y,V[6],.85*flick);}
  return;}
 const bottom=FY-4;
 p.field(CX-R-6*S,0,CX+R+6*S,bottom,(x,y)=>{const r=R*(1+.07*n2(y*.025/S-t*7,x*.01/S,3));const u=Math.abs(x-CX)/r;return u<1?1-u+.001:0;},(x,y,v)=>{
  const dx=(x-CX)/R,streak=n2(dx*5.5,(y/S+t*1100)*.011,7)*.5+n2(dx*11,(y/S+t*1600)*.02,8)*.25;
  const gap=Math.abs(dx)>.55&&n2(dx*9,(y/S+t*700)*.004,9)>.35?.45:0;const b=(1-dx*dx*1.05)+.5*streak-gap;
  const c=v<.05?V[3]:b>1.0?V[8]:b>.8?V[7]:b>.56?V[6]:b>.34?V[5]:b>.12?V[4]:V[3];p.px(x,y,c);});
 for(let i=0;i<22;i++){const x=Math.round(CX+(hash(i*1.7)*2-1)*R*.8),len=(16+hash(i*2.3)*36)*S,y=((hash(i*3.1)*FY-t*1500*S)%FY+FY)%FY;
  for(let k=0;k<len;k++)if(y+k<bottom)p.px(x,y+k,V[8],.9);}
 for(let i=0;i<4;i++){const x=CX+(hash(i*9.1)*2-1)*R*.5,y=((hash(i*4.4)*FY-t*600*S)%FY+FY)%FY,s=4;
  for(let k=-s;k<=s;k++){p.px(x+k,y,V[8]);p.px(x,y+k,V[8]);}p.px(x-1,y-1,V[7]);p.px(x+1,y+1,V[7]);p.px(x+1,y-1,V[7]);p.px(x-1,y+1,V[7]);}
}
function darkBase(p,t,R,CX,FY){
 if(t<T.flash||t>T.gone+.1)return;
 const k=t<T.collapse?ease(clamp((t-T.flash)/.12)):1-clamp((t-T.collapse)/(T.gone-T.collapse));if(k<=0)return;
 for(let x=Math.floor(CX-R-8*S);x<=CX+R+8*S;x++){const dx=(x-CX)/(R+8*S);if(Math.abs(dx)>1)continue;
  const h=(30+14*n1(x/S*.13+t*3,31)+10*Math.abs(n1(x/S*.41-t*5,32)))*S*k*(1-dx*dx*.35);
  for(let y=FY-1;y>FY-1-h;y--)p.px(x,y,y<FY-h+2?V[2]:V[0]);}
}
function ribbons(p,t,R,front,CX,FY){
 if(t<T.flash||t>T.line)return;
 const fling=clamp((t-T.collapse)/(T.line-T.collapse)),fade=1-fling;if(fade<=0)return;
 const grow=ease(clamp((t-T.flash)/.15));
 for(let i=0;i<3;i++){
  const pts=[];for(let h=14;h<FY;h++){const ph=h/S*.028-t*8.5+i*TAU/3,rad=(R+(9+5*Math.sin(h/S*.012+i))*S)*(1+fling*1.8)*grow;
   pts.push({x:CX+Math.sin(ph)*rad,y:FY-h,front:Math.cos(ph)>0,m:n1(h/S*.02+i*13-t*4,i+2)});}
  let k=0;while(k<pts.length){if(pts[k].m<0){k++;continue;}let e=k;while(e<pts.length&&pts[e].m>=0)e++;const n=e-k;
   for(let q=k;q<e;q++){const a=pts[q];if(a.front!==front)continue;const b=pts[Math.min(q+1,pts.length-1)];
    const u=(q-k)/Math.max(1,n-1),taper=Math.sin(Math.PI*u),w=front?Math.max(1,taper*6*S*clamp(pts[q].m*3+.4)):Math.max(1,taper*2*S);
    const L=Math.max(1,Math.ceil(Math.abs(b.x-a.x)));
    for(let s2=0;s2<=L;s2++){const xx=lerp(a.x,b.x,s2/L),yy=lerp(a.y,b.y,s2/L),Wd=Math.round(w);
     if(front){for(let r=-Wd;r<=Wd;r++){const d=Math.abs(r)/Wd;p.px(xx,yy+r,r===Wd?V[3]:d<.34?V[8]:d<.7?V[7]:V[6],fade);}}
     else for(let r=-Wd;r<=Wd;r++)p.px(xx,yy+r,V[3],.9*fade);}}
   k=e;}}
}
function drops(p,t,R,CX,FY){
 if(t<T.full||t>T.full+.7)return;const e=t-T.full;
 for(let i=0;i<22;i++){const side=i%2?1:-1,s=(90+hash(i*3.1)*170)*S,up=(60+hash(i*4.7)*120)*S,age=e-hash(i*6.3)*.08;if(age<0)continue;
  const x=CX+side*(R*.8+s*age),y=FY-4-up*age+380*S*age*age;if(y>FY-1)continue;
  const r=hash(i*8.1)>.6?3:2;for(let yy=0;yy<r;yy++)for(let xx=0;xx<r;xx++)p.px(x+xx,y+yy,yy===0&&age<.25?V[3]:V[0]);}
}
function sweep(p,t,R,CX,FY){
 if(t<T.full)return;const age=t-T.full,run=ease(clamp(age/.5))*150*S,fade=clamp((t-T.line)/(T.end-T.line));if(fade>=1)return;
 for(const side of[-1,1])for(let d=0;d<run+R;d++){const x=Math.round(CX+side*d),far=clamp((d-R)/(150*S));
  const brk=n1(d/S*.09+side*7-age*2,41);if(d>R&&brk<lerp(-.9,.2,Math.max(far,fade)))continue;
  const h=Math.round((d<R?5:lerp(6,1,far))*(1-fade)+(n1(d/S*.3+side*3,42)>.5?1:0));
  for(let k=0;k<h;k++)p.px(x,FY-1-k,k===h-1&&h>1?V[3]:V[0],d<R?1:.85);}
}
function sparkles(p,t,R,CX,FY){
 if(t<T.full||t>T.end)return;
 for(let i=0;i<90;i++){const life=.6+hash(i*1.9)*.8,born=T.full+hash(i*2.9)*(T.line-T.full+.3),age=t-born;if(age<0||age>life)continue;
  const u=age/life,x=CX+(hash(i*4.1)*2-1)*(R+(20+hash(i)*60)*S)+Math.sin(age*6+i)*4,y=FY-10-hash(i*5.7)*200*S-age*(60+hash(i*6.3)*160)*S;
  const c=u<.5?V[8]:V[7];p.px(x,y,c,1-u*.6);p.px(x+1,y,c,(1-u*.6)*.7);
  if(hash(i*8.8)>.75&&u<.6){p.px(x-1,y,V[6]);p.px(x+2,y,V[6]);p.px(x,y-1,V[6]);p.px(x,y+1,V[6]);}}
}
function glint(p,t,at){
 if(t>=T.beam+.06)return;const k=clamp(t/T.beam);
 for(let i=0;i<14;i++){const a=hash(i*2.7)*TAU,u=clamp(k*1.3-hash(i*5.9)*.3),r=lerp(48,3,ease(u));if(u<=0||u>=1)continue;
  p.px(at[0]+Math.cos(a)*r,at[1]+Math.sin(a)*r,u>.6?V[8]:V[6]);}
 const L=Math.round(lerp(2,20,ease(k))*(t>T.beam?.4:1));
 for(let s=-L;s<=L;s++){const c=Math.abs(s)<2?V[8]:Math.abs(s)<L*.6?V[7]:V[6];p.px(at[0]+s,at[1],c);p.px(at[0],at[1]+s,c);
  if(Math.abs(s)<L*.3){p.px(at[0]+s,at[1]-1,V[7]);p.px(at[0]+s,at[1]+1,V[7]);p.px(at[0]-1,at[1]+s,V[7]);p.px(at[0]+1,at[1]+s,V[7]);}}
 const d=Math.round(L*.4);for(let s=-d;s<=d;s++){p.px(at[0]+s,at[1]+s,V[6]);p.px(at[0]+s,at[1]-s,V[6]);}
}
// Room dimming: violet dusk that falls with the beam and lifts as the column becomes a line.
function dimAmount(t){if(t<T.beam)return .25*ease(clamp(t/T.beam));if(t<T.collapse)return .25+.2*ease(clamp((t-T.beam)/.3));return .45*(1-clamp((t-T.collapse)/(T.end-T.collapse)));}
export const pillarSilhouette=t=>t>=T.flash&&t<T.line;
export const pillarShake=t=>t>=T.flash&&t<T.collapse?Math.round(Math.sin(t*150)*(t<T.full+.25?4:2)):0;
// pose(ctx, silhouette) draws Blue onto ctx; inside the full column Blue reads as a dark silhouette.
export function roarPillar(ctx,t,CX,FY,glintAt,pose){
 t=Math.floor(t*25)/25;const R=radius(t);
 let p=new Px(ctx.getImageData(0,0,W,H));p.dim(dimAmount(t));ribbons(p,t,R,false,CX,FY);beam(p,t,R,CX,FY);ctx.putImageData(p.img,0,0);
 pose(ctx,pillarSilhouette(t));
 p=new Px(ctx.getImageData(0,0,W,H));sweep(p,t,R,CX,FY);darkBase(p,t,R,CX,FY);drops(p,t,R,CX,FY);ribbons(p,t,R,true,CX,FY);sparkles(p,t,R,CX,FY);glint(p,t,glintAt);
 if(t>=T.flash&&t<T.full)p.invert();ctx.putImageData(p.img,0,0);
}
