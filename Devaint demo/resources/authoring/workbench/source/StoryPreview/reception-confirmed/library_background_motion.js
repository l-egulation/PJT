// Native pixel poses: suspension points stay fixed; only ropes stretch and shelf bodies bob.
class LibraryBackgroundMotion {
 constructor(){this.items=new Map();this.padding=8;}
 build(o,image){
  const w=image.width,h=image.height,p=this.padding,canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
  const c=canvas.getContext('2d');c.drawImage(image,0,0);const source=c.getImageData(0,0,w,h).data,frames=[];
  for(let f=0;f<o.frameCount;f++){
   const phase=f/o.frameCount*Math.PI*2,bob=Math.round(Math.sin(phase)*o.amplitude);
   const outCanvas=document.createElement('canvas');outCanvas.width=w+p*2;outCanvas.height=h+p*2;
   const oc=outCanvas.getContext('2d'),out=oc.createImageData(outCanvas.width,outCanvas.height);
   for(let yy=-p;yy<h+p;yy++)for(let xx=0;xx<w;xx++){
    let sy;
    if(o.motion==='libraryShelf'){
     const fixed=8,rigid=o.rigidStart;
     // Inverse row sampling keeps the rope continuous when it lengthens or contracts.
     sy=yy<fixed?yy:yy<rigid+bob?fixed+Math.round((yy-fixed)*(rigid-fixed)/(rigid+bob-fixed)):yy-bob;
    }else{
     sy=yy-bob;
     for(let k=0;k<3;k++){
      const gutter=w*.47,edge=Math.min(1,Math.abs(xx-gutter)/(w*.53));
      const page=Math.max(0,1-sy/(h*.8));
      const flutter=Math.round(Math.sin(phase*(o.pageCycles||2)+(xx<gutter?0:.8))*edge*page*(o.pageAmplitude??1));
      sy=yy-bob-flutter;
     }
    }
    if(sy<0||sy>=h)continue;const si=(sy*w+xx)*4;if(!source[si+3])continue;
    const di=((yy+p)*outCanvas.width+xx+p)*4;out.data.set(source.subarray(si,si+4),di);
   }
   oc.putImageData(out,0,0);frames.push(outCanvas);
  }
  const renderFrames=frames.map(frame=>libraryDepthAppearance(frame,o));
  this.items.set(o.asset,{frames,renderFrames,padding:p,w,h,motion:o.motion});
 }
 frameIndex(o,time){return (Math.floor((o.frozen?0:time)*7.5*(o.motionSpeed??1))+Math.round(o.phase*7.5))%o.frameCount;}
 draw(ctx,o,time){const item=this.items.get(o.asset);ctx.drawImage(item.renderFrames[this.frameIndex(o,time)],-item.padding,-item.padding);}
}

// Cache display treatment separately, preserving the original native pixel poses.
function libraryDepthAppearance(source,o){
 const cv=document.createElement('canvas');cv.width=source.width;cv.height=source.height;
 const treated=o.backgroundNoOutline?backgroundWithoutInkContour(source,o.outlineWidth??2):source;
 const c=cv.getContext('2d');c.filter=`saturate(${o.displaySaturation??1})${o.displayBlur>0?' blur('+o.displayBlur+'px)':''}`;c.drawImage(treated,0,0);c.filter='none';
 if(o.displayTint){c.globalCompositeOperation='source-atop';c.globalAlpha=o.displayTint;c.fillStyle='#d7e6f5';c.fillRect(0,0,cv.width,cv.height)}
 return cv;
}

// A native-grid material pass: absorb a dark silhouette stroke into its inner surface.
// Alpha, position and the authored interior shading remain intact.
function backgroundWithoutInkContour(source,width){
 const cv=document.createElement('canvas');cv.width=source.width;cv.height=source.height;
 const c=cv.getContext('2d');c.drawImage(source,0,0);
 const img=c.getImageData(0,0,cv.width,cv.height),src=new Uint8ClampedArray(img.data),w=cv.width,h=cv.height;
 const solid=(x,y)=>x>=0&&y>=0&&x<w&&y<h&&src[(y*w+x)*4+3]>200;
 const luminance=i=>src[i]*.2126+src[i+1]*.7152+src[i+2]*.0722;
 const depth=new Uint8Array(w*h);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  if(!solid(x,y))continue;let d=width+1;
  for(let r=1;r<=width;r++){
   if(!solid(x-r,y)||!solid(x+r,y)||!solid(x,y-r)||!solid(x,y+r)){d=r;break;}
  }
  depth[y*w+x]=d;
 }
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const offset=y*w+x,d=depth[offset];if(!d)continue;
  const i=offset*4,light=luminance(i);let best=null,bestScore=Infinity;
  if(d>width){
   // Remove narrow dark separator strokes as well as the outside ink rim.
   // Broad spine shadows stay intact; warm gold material is excluded.
   if(light>150||src[i]>src[i+2]+8)continue;
   for(const [dx,dy] of [[1,0],[0,1],[1,1],[1,-1]])for(let step=1;step<=2;step++){
    const ax=x+dx*step,ay=y+dy*step,bx=x-dx*step,by=y-dy*step;
    if(!solid(ax,ay)||!solid(bx,by))continue;
    const a=(ay*w+ax)*4,b=(by*w+bx)*4,al=luminance(a),bl=luminance(b);
    if(Math.min(al,bl)<light+22)continue;
    const candidate=al<bl?a:b,score=step*8+Math.min(al,bl)-light;
    if(score<bestScore){bestScore=score;best=candidate;}
   }
   if(best!==null)for(let k=0;k<3;k++)img.data[i+k]=src[best+k];
   continue;
  }
  // Only a brighter inward surface can replace an outline; highlights stay untouched.
  for(let dy=-width-2;dy<=width+2;dy++)for(let dx=-width-2;dx<=width+2;dx++){
   const xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;
   const target=yy*w+xx;if(depth[target]<=d)continue;
   const ti=target*4,delta=luminance(ti)-light;if(delta<8)continue;
   // Prefer a nearby face of similar hue rather than crossing into ivory/gold trim.
   const hue=Math.abs((src[i+2]-src[i])-(src[ti+2]-src[ti]));
   const score=dx*dx+dy*dy+hue*.12+delta*.015;
   if(score<bestScore){bestScore=score;best=ti;}
  }
  if(best!==null)for(let k=0;k<3;k++)img.data[i+k]=src[best+k];
 }
 c.putImageData(img,0,0);return cv;
}
