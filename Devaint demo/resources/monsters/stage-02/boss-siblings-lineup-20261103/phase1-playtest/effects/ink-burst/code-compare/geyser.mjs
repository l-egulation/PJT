// Deterministic pixel raster: a pressure-driven column plus ballistic heavy lobes.
// Comparison only; the selected Aether effect remains in the playtest.
const W=96,H=144;
const palette=[[12,10,22],[52,30,73],[106,68,135]];
export function makeGeyser(){return document.createElement('canvas');}
export function drawGeyser(ctx,raster,x,foot,age,scale=1){
 const t=age-.6;if(t<0||t>1.6)return;
 if(raster.width!==W){raster.width=W;raster.height=H;}
 const rc=raster.getContext('2d'),im=rc.createImageData(W,H),a=im.data;
 const rise=Math.max(0,Math.min(1,(t-.22)/.16));
 const decay=Math.max(0,Math.min(1,(t-.66)/.62));
 const height=t<.22?5+3*Math.sin(t/.22*Math.PI):8+112*(1-(1-rise)**3)*(1-decay)**.7;
 const mass=t<.22?1:Math.max(0,1-(t-1.1)/.5);
 const lobes=[];
 if(t>.32)for(let j=0;j<5;j++){
  const flight=t-.32-j*.016;
  const vx=(j-2)*18;
  const vy=-190-(j%2)*15;
  const cy=65+vy*flight+310*flight*flight;
  if(cy<138)lobes.push({x:10+j*19+vx*flight*.3,y:cy,rx:6+(j%2)*2,ry:10-(j%2)});
 }
 for(let py=0;py<H;py++)for(let px=0;px<W;px++){
  const above=138-py;
  const u=Math.max(0,Math.min(1,above/Math.max(height,1)));
  const center=48;
  // The entire platform emits a broad column, rather than a central nozzle.
  const edge=Math.min(px,95-px);
  const crest=(Math.sin(px*.18-t*9)*5+Math.sin(px*.075+t*11)*4)*rise;
  const top=height+crest-Math.max(0,5-edge)*1.2;
  let inside=px>=1&&px<=94&&above>=0&&above<Math.max(3,top);
  // Broad pooled foot anchors the effect to the whole platform.
  if(((px-48)/47)**2+((py-138)/(5+3*(1-decay)))**2<1&&py<=140)inside=true;
  for(const l of lobes)if(((px-l.x)/l.rx)**2+((py-l.y)/l.ry)**2<1)inside=true;
  if(!inside||mass<=0)continue;
  let color=0;
  // Broad vertical flow bands move through the whole sheet of ink.
  const flow=Math.sin(px*.17+Math.sin(py*.065-t*11)*.6);
  if(flow>.15)color=1;
  if(flow>.91&&Math.sin(py*.08+t*9)>.25)color=2;
  if(above>top-3&&px>4&&px<91)color=1;
  const k=(py*W+px)*4,c=palette[color];a[k]=c[0];a[k+1]=c[1];a[k+2]=c[2];a[k+3]=255;
 }
 rc.putImageData(im,0,0);ctx.save();ctx.imageSmoothingEnabled=false;
 ctx.globalAlpha*=Math.min(1,mass);ctx.drawImage(raster,Math.round(x),Math.round(foot-138*scale),W*scale,H*scale);ctx.restore();
}
