// Baked native-resolution poses. Rendering copies a complete pixel frame 1:1;
// it never rotates, shears, filters or interpolates the source sprite at runtime.
class PixelMotionBank {
 constructor(){this.items=new Map();this.frames=24;this.padding=4;this.fps=12;}
 build(name,image){
  const w=image.width,h=image.height,p=this.padding;
  const input=document.createElement('canvas');input.width=w;input.height=h;
  const ic=input.getContext('2d');ic.imageSmoothingEnabled=false;ic.drawImage(image,0,0);
  const source=ic.getImageData(0,0,w,h).data;
  const configs={
   '19_mobile_near':{fixed:12,bodyAmp:1.1,parts:[[0,69,15,28,-.7,1.8,1],[49,69,15,28,.7,1.8,1],[22,80,21,32,1.5,2.5,1]]},
   '12_mobile':{fixed:18,bodyAmp:1.8,parts:[[27,32,17,31,.6,1,2],[14,74,19,18,1.4,2,1]]},
   '20_mobile_far_a':{fixed:14,bodyAmp:.8,parts:[[0,49,9,27,.8,2,2],[29,49,9,22,.8,-2,2]]},
   '21_mobile_far_b':{fixed:13,bodyAmp:.6,parts:[[0,23,10,37,.3,1.4,2],[12,23,12,51,1.2,2.2,1],[25,23,11,42,2.4,1.5,2]]}
  };
  const basis={'19_mobile_near':[64,112],'12_mobile':[46,92],'20_mobile_far_a':[38,76],'21_mobile_far_b':[36,74]};
  let config=configs[name];
  if(config){const [bw,bh]=basis[name];config={...config,fixed:Math.round(config.fixed*h/bh),parts:config.parts.map(([x,y,pw,ph,lag,amp,frequency])=>[Math.round(x*w/bw),Math.round(y*h/bh),Math.round(pw*w/bw),Math.round(ph*h/bh),lag,amp,frequency])};}
  const frames=[];
  for(let index=0;index<this.frames;index++){
   const phase=index/this.frames*Math.PI*2;
   const cv=document.createElement('canvas');cv.width=w+p*2;cv.height=h+p*2;
   const cx=cv.getContext('2d'),out=cx.createImageData(cv.width,cv.height);
   const put=(x,y,si)=>{x+=p;y+=p;if(x<0||y<0||x>=cv.width||y>=cv.height)return;const di=(y*cv.width+x)*4;out.data[di]=source[si];out.data[di+1]=source[si+1];out.data[di+2]=source[si+2];out.data[di+3]=source[si+3];};
   for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    const si=(y*w+x)*4;if(!source[si+3])continue;
    let dx=0,dy=0;
    if(config){
     const body=Math.round(Math.sin(phase)*(config.bodyAmp||1.1));
     dx=y<config.fixed?0:Math.round(body*Math.min(1,(y-config.fixed)/8));
     for(const [rx,ry,rw,rh,lag,amp,frequency] of config.parts){if(x>=rx&&x<rx+rw&&y>=ry&&y<ry+rh){
      // Pendants lag the rigid frame; their attachment pixels stay connected.
      const weight=Math.min(1,(y-ry)/Math.max(3,rh*.5));
      dx=body+Math.round(Math.sin(phase*(frequency||1)-lag)*amp*weight);
      break;
     }}
    }else if(name==='11_banner'){
     // Cloth changes contour below its fixed hanging edge. Lower folds travel
     // later than upper folds, rather than shearing the entire rectangle.
     const weight=Math.max(0,(y-10)/(h-10));
     dx=Math.round(Math.sin(phase*2-y*.075)*2.2*weight);
     if(y>h*.82)dx=Math.round(Math.sin(phase*2-1.6)*2);
    }else if(name==='08_floating_book'){
     // Covers/spine hold their shape; page halves flex around the gutter.
     const edge=Math.abs(x-w/2)/(w/2),page=Math.max(0,1-y/(h*.78));
     dy=Math.round(Math.sin(phase*2+(x<w/2?0:.9))*edge*page*2);
     dy+=Math.round(Math.sin(phase)*.65);
    }else if(name==='09_quill'){
     // Bend the feather above a stable nib. Avoid whole-image rotation.
     const weight=(h-1-y)/(h-1);
     dx=Math.round(Math.sin(phase*2-weight*.7)*2*weight);
     dy=Math.round(Math.sin(phase)*.6);
    }else{
     // Paper strips: fixed attachment and a delayed lower edge.
     dx=Math.round(Math.sin(phase*2-y*.11)*Math.min(1.5,y/h*2));
    }
    put(x+dx,y+dy,si);
   }
   if(name==='08_floating_book'){
    // Inverse sampling of the bending pages avoids holes from forward-mapped
    // pixels separating when the page flexes upward. Source colors stay exact.
    out.data.fill(0);
    for(let yy=-p;yy<h+p;yy++)for(let xx=0;xx<w;xx++){
     let sy=yy;
     for(let k=0;k<3;k++){
      const edge=Math.abs(xx-w/2)/(w/2),page=Math.max(0,1-sy/(h*.78));
      sy=yy-Math.round(Math.sin(phase*2+(xx<w/2?0:.9))*edge*page*2)-Math.round(Math.sin(phase)*.65);
     }
     if(sy>=0&&sy<h){const si=(sy*w+xx)*4;if(source[si+3])put(xx,yy,si)}
    }
   }
   cx.putImageData(out,0,0);frames.push(cv);
  }
  this.items.set(name,{w,h,frames,padding:p});
 }
 frameIndex(time,offset=0){return (Math.floor(time*7.5)+Math.round(offset*4))%this.frames;}
 draw(ctx,name,time,offset=0){
  const item=this.items.get(name),index=this.frameIndex(time,offset);
  ctx.save();ctx.imageSmoothingEnabled=false;ctx.drawImage(item.frames[index],-item.padding,-item.padding);ctx.restore();
 }
}
