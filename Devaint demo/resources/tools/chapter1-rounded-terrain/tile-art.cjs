/* Artwork follows occupancy independently of the retained legacy tile IDs. */
(function(root){
'use strict';
const SIZE=32,GROUND_SURFACE=9,PLATFORM_SURFACE={platform:9,dropfloor:11};
// The atlas and interior share the same dark woodland earth colour.
const SOIL=[36,31,29],RADIUS=14,FIELD=96;
const mod=(n,m)=>(n%m+m)%m;
const occupied=(mask,x,y)=>x>=-2&&x<=2&&y>=-2&&y<=2&&!!(mask&(1<<((y+2)*5+x+2)));
function groundSpec(terrain,x,y){
 const material=terrain.value(x,y);let mask=0;
 let inner=true;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
  if(terrain.value(x+dx,y+dy))mask|=1<<((dy+2)*5+dx+2);else inner=false;
 }
 // Empty cells only draw a small fillet where three neighbours meet.
 if(!material&&![[1,1],[-1,1],[1,-1],[-1,-1]].some(([a,b])=>occupied(mask,a,0)&&occupied(mask,0,b)&&occupied(mask,a,b)))return null;
 for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++)if((Math.abs(dx)===2||Math.abs(dy)===2)&&terrain.value(x+dx,y+dy))mask|=1<<((dy+2)*5+dx+2);
 const value=(cx,cy)=>!!terrain.value(x+cx,y+cy),tops=[],corners=[];
 for(let cy=-1;cy<=0;cy++)if(value(0,cy)&&!value(0,cy-1)){
  let start=x;while(start>0&&terrain.value(start-1,y+cy)&&!terrain.value(start-1,y+cy-1))start--;
  tops.push({y:cy*32,phase:mod(x-start-2,4)});
 }
 for(let cy=-2;cy<=1;cy++)for(let cx=-2;cx<=1;cx++){
  if(value(cx,cy)&&!value(cx,cy-1))for(const side of [-1,1])if(!value(cx+side,cy)){
   const ox=(cx+(side===1?1:0))*32,oy=cy*32;
   if((side===-1?ox<32&&ox+64>0:ox>0&&ox-64<32)&&oy-9<32&&oy+87>0){
    // A cap's transparent silhouette ends where this exposed wall meets the
    // lower landing. Below that junction it is solid ground, not open air.
    let height=0;while(height<3&&value(cx,cy+height)&&!value(cx+side,cy+height))height++;
    corners.push({kind:'outer',side,x:ox,y:oy,height:height*32});
   }
  }
  if(!value(cx,cy)&&value(cx,cy+1))for(const side of [-1,1])if(value(cx+side,cy)&&value(cx+side,cy+1)){
   const ox=(cx+(side===1?1:0))*32,oy=(cy+1)*32;
   // A one-cell rise is entirely covered by the upper outer cap. An additional
   // 128px inner patch would paint its soil across that cap's green canopy.
   const tall=value(cx+side,cy-1)&&!value(cx,cy-1);
   if(tall&&ox-64<32&&ox+64>0&&oy-64<32&&oy+64>0)corners.push({kind:'inner',side,x:ox,y:oy});
  }
 }
 if(inner&&!tops.length&&!corners.length)return {key:'interior',interior:true};
 const wallPhase=side=>{let start=y;while(start>0&&terrain.value(x,start-1)&&!terrain.value(x+side,start-1))start--;return mod((y-start)*32+9-96,128);};
 const grass=!!material&&!occupied(mask,0,-1),sketch=material===2&&grass,sketchBand=material===2;
 const spec={mask,grass,sketch,sketchBand,vx:mod(x,4),vy:mod(y,4),tops,corners,wallLeft:wallPhase(-1),wallRight:wallPhase(1)};
 spec.key=JSON.stringify(spec);return spec;
}
function insideContour(mask,cx,cy,x,y){
 const solid=occupied(mask,cx,cy),top=occupied(mask,cx,cy-1),right=occupied(mask,cx+1,cy),bottom=occupied(mask,cx,cy+1),left=occupied(mask,cx-1,cy);
 if(!solid){
  for(const [a,b] of [[-1,-1],[1,-1],[-1,1],[1,1]]){
   const u=a<0?x:SIZE-x,v=b<0?y:SIZE-y;
   if(u<RADIUS&&v<RADIUS&&occupied(mask,cx+a,cy)&&occupied(mask,cx,cy+b)&&occupied(mask,cx+a,cy+b)&&Math.hypot(u-RADIUS,v-RADIUS)>=RADIUS)return true;
  }
  return false;
 }
 // A one-cell hanging tip has a shallow, round end instead of a square tooth.
 const tip=top&&!left&&!right&&!bottom;
 const height=tip?24:SIZE,r=tip?16:RADIUS;
 if(y>=height)return false;
 for(const [a,b,cut] of [[-1,-1,!left&&!top],[1,-1,!right&&!top],[-1,1,!left&&!bottom],[1,1,!right&&!bottom]]){
  const u=a<0?x:SIZE-x,v=b<0?y:height-y;
  if(cut&&u<r&&v<r&&Math.hypot(u-r,v-r)>r)return false;
 }
 return true;
}
function groundContour(mask){
 // Include neighbouring cells so edge fading and curved joins cross tile seams.
 const cover=new Uint8Array(FIELD*FIELD),nearest=new Int16Array(FIELD*FIELD);nearest.fill(-1);
 for(let y=0;y<FIELD;y++)for(let x=0;x<FIELD;x++){
  const k=y*FIELD+x;cover[k]=+insideContour(mask,Math.floor(x/SIZE)-1,Math.floor(y/SIZE)-1,x%SIZE+.5,y%SIZE+.5);
  if(!cover[k])nearest[k]=k;
 }
 const relax=(k,q,x,y)=>{
  const candidate=nearest[q];if(candidate<0)return;
  const prior=nearest[k],d=(candidate%FIELD-x)**2+(Math.floor(candidate/FIELD)-y)**2;
  if(prior<0||d<(prior%FIELD-x)**2+(Math.floor(prior/FIELD)-y)**2)nearest[k]=candidate;
 };
 for(let y=0;y<FIELD;y++)for(let x=0;x<FIELD;x++){
  const k=y*FIELD+x;if(!cover[k])continue;
  if(x)relax(k,k-1,x,y);if(y){relax(k,k-FIELD,x,y);if(x)relax(k,k-FIELD-1,x,y);if(x+1<FIELD)relax(k,k-FIELD+1,x,y);}
 }
 for(let y=FIELD-1;y>=0;y--)for(let x=FIELD-1;x>=0;x--){
  const k=y*FIELD+x;if(!cover[k])continue;
  if(x+1<FIELD)relax(k,k+1,x,y);if(y+1<FIELD){relax(k,k+FIELD,x,y);if(x)relax(k,k+FIELD-1,x,y);if(x+1<FIELD)relax(k,k+FIELD+1,x,y);}
 }
 // Trace the exposed top separately from the nearest soil boundary. A side
 // boundary must not cut the foliage off along a 45-degree diagonal.
 const topDepth=new Float32Array(SIZE*SIZE);topDepth.fill(Infinity);
 for(let x=0;x<SIZE;x++){
  let start=-1,canopy=false;
  for(let y=0;y<2*SIZE;y++){
   const k=y*FIELD+x+SIZE;
   if(!cover[k]){start=-1;canopy=false;}
   else if(y&&!cover[k-FIELD]){
    start=y;const cy=Math.floor(y/SIZE)-1;
    canopy=(occupied(mask,0,cy)&&!occupied(mask,0,cy-1))||(!occupied(mask,0,cy)&&occupied(mask,0,cy+1));
   }
   if(y>=SIZE&&start>=0&&canopy)topDepth[(y-SIZE)*SIZE+x]=y-start+.5;
  }
 }
 const pixels=[];
 for(let y=0;y<SIZE;y++)for(let x=0;x<SIZE;x++){
  const k=(y+SIZE)*FIELD+x+SIZE,n=nearest[k],dx=n<0?0:n%FIELD-x-SIZE,dy=n<0?0:Math.floor(n/FIELD)-y-SIZE;
  pixels.push({inside:!!cover[k],depth:n<0?Infinity:Math.hypot(dx,dy)-.5,dx,dy,topDepth:topDepth[y*SIZE+x]});
 }
 return pixels;
}
function groundPixels(spec,contour,atlas){
 const pixels=new Uint8ClampedArray(SIZE*(SIZE+GROUND_SURFACE)*4);
 for(let y=-GROUND_SURFACE;y<SIZE;y++)for(let x=0;x<SIZE;x++){
  const k=((y+GROUND_SURFACE)*SIZE+x)*4,p=y>=0?contour?.[y*SIZE+x]:null;
  const inside=y>=0&&(spec.interior||p.inside);
  if(inside)pixels.set([...SOIL,255],k);
  if(spec.interior)continue;
  let uv=null,foliage=false;
  if(inside&&p.depth<32){
   const d=Math.min(31,Math.floor(p.depth));
   if(Math.abs(p.dx)>Math.abs(p.dy))uv=[p.dx<0?d:255-d,96+mod((p.dx<0?spec.wallLeft:spec.wallRight)+y,128)];
   else if(p.dy>0)uv=[64+spec.vx*32+x,255-d];
  }
  for(const top of spec.tops){const sy=y-top.y+GROUND_SURFACE;if(sy>=0&&sy<64&&(inside||y<0&&top.y===0)){uv=[64+top.phase*32+x,sy];foliage=true;}}
  // Painted corner patches are sampled in world coordinates at native size.
  // Each patch spans neighbouring cells; no leaf or root is bent or clipped at a cell seam.
  let priority=Infinity,join=null,joinPriority=Infinity,outerOutline=false;
  for(const corner of spec.corners){
   const rx=x-corner.x,ry=y-corner.y;
   if(corner.kind==='outer'){
    const sx=corner.side===-1?rx:-rx-1,sy=ry+GROUND_SURFACE;
    if(sx>=0&&sx<64&&sy>=0&&sy<96){
     const score=Math.abs(rx)+Math.abs(ry);
     if(score<priority){priority=score;uv=[corner.side===-1?sx:255-sx,sy];foliage=true;outerOutline=ry<corner.height;}
    }
   }else{
    // Preserve the upper landing's canopy when a taller inside join overlaps it.
    if(spec.tops.some(top=>top.y<corner.y&&y<top.y+55))continue;
    const sx=corner.side===1?rx+64:63-rx,sy=ry+64;
    if(sx>=0&&sx<128&&sy>=0&&sy<128&&(inside||ry>=-9&&rx*corner.side<0)){
     const score=Math.abs(rx)+Math.abs(ry);
     if(score<joinPriority){
      joinPriority=score;
      // A short overlap joins the independently painted straight/corner borders.
      // The corner itself remains unscaled; root tips terminate inside the art.
      join={uv:[64+sx,96+sy],weight:Math.max(0,Math.min(1,sx/24,(128-sx)/16,(sy-24)/24,(128-sy)/12))};
     }
    }
   }
  }
  if(uv&&(inside||y<0||foliage)){
   const source=(uv[1]*256+uv[0])*4;
   if(atlas[source+3]>=128)for(let c=0;c<4;c++)pixels[k+c]=atlas[source+c];
  }
  if(join&&join.weight>0){
   const source=(join.uv[1]*256+join.uv[0])*4,opaque=atlas[source+3]>=128;
   if(opaque||inside){
    for(let c=0;c<3;c++)pixels[k+c]=pixels[k+c]*(1-join.weight)+(opaque?atlas[source+c]:SOIL[c])*join.weight;
    pixels[k+3]=255;foliage=true;
   }
  }
  // The painted cap owns its outer silhouette. The geometrical soil fill must
  // not show through the transparent pixels outside the leaf edge as a rim.
  if(outerOutline&&(!inside||p.depth<16)&&atlas[(uv[1]*256+uv[0])*4+3]<128){pixels.fill(0,k,k+4);continue;}
  // Earth detail settles into the colour sampled from the same source. Green
  // leaves stay crisp at the silhouette. All artwork settles into soil deeper
  // inside, so tall cap tails cannot dangle below a neighbouring short landing.
  const green=pixels[k+1]>pixels[k]*.95&&pixels[k+1]>pixels[k+2]*1.5;
  const fadeStart=green?28:20;
  if(inside&&p.depth>fadeStart){
   const blend=Math.min(1,(p.depth-fadeStart)/(40-fadeStart));
   for(let c=0;c<3;c++)pixels[k+c]=pixels[k+c]*(1-blend)+SOIL[c]*blend;
  }
  if(spec.sketchBand&&foliage&&pixels[k+1]>pixels[k]*.65&&pixels[k+1]>pixels[k+2]*1.3){
   const l=(pixels[k]*.213+pixels[k+1]*.715+pixels[k+2]*.072)*1.65;pixels[k]=l*1.03;pixels[k+1]=l;pixels[k+2]=l*.93;
  }
 }
 return pixels;
}
function platformParts(kind,width){
 if(!Object.hasOwn(PLATFORM_SURFACE,kind)||!Number.isInteger(width)||width<32||width%32)throw Error('Invalid platform artwork request');
 const parts=[],count=width/32;
 if(count===1){parts.push({sx:0,w:16,dx:0},{sx:496,w:16,dx:16});}
 else for(let i=0;i<count;i++)parts.push({sx:(i===0?0:i===count-1?15:1+(i-1)%14)*32,w:32,dx:i*32});
 return {image:kind==='dropfloor'?'dropPlatform':'solidPlatform',surface:PLATFORM_SURFACE[kind],height:64,parts};
}
class TileArt{
 constructor(images,createCanvas){this.images=images;this.createCanvas=createCanvas;this.groundCache=new Map();this.contourCache=new Map();this.platformCache=new Map();}
 ground(terrain,x,y){
  const spec=groundSpec(terrain,x,y);if(!spec)return null;
  if(this.groundCache.has(spec.key))return this.groundCache.get(spec.key);
  const canvas=this.createCanvas();canvas.width=32;canvas.height=32+GROUND_SURFACE;
  const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;
  if(!this.atlas){const source=this.createCanvas();source.width=source.height=256;const sourceCtx=source.getContext('2d');sourceCtx.drawImage(this.images.atlas,0,0);this.atlas=sourceCtx.getImageData(0,0,256,256).data;}
  let contour=this.contourCache.get(spec.mask);
  if(!spec.interior&&!contour){contour=groundContour(spec.mask);if(this.contourCache.size>=256)this.contourCache.delete(this.contourCache.keys().next().value);this.contourCache.set(spec.mask,contour);}
  const raster=ctx.createImageData(32,32+GROUND_SURFACE);raster.data.set(groundPixels(spec,contour,this.atlas));ctx.putImageData(raster,0,0);
  if(this.groundCache.size>=1024)this.groundCache.delete(this.groundCache.keys().next().value);
  this.groundCache.set(spec.key,canvas);return canvas;
 }
 drawGround(ctx,terrain,x,y,dx,dy,scale=1){const tile=this.ground(terrain,x,y);if(tile)ctx.drawImage(tile,dx,dy-GROUND_SURFACE*scale,SIZE*scale,(SIZE+GROUND_SURFACE)*scale);}
 platform(kind,width){
  const key=kind+':'+width;if(this.platformCache.has(key))return this.platformCache.get(key);
  const spec=platformParts(kind,width),canvas=this.createCanvas();canvas.width=width;canvas.height=spec.height;
  const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;
  for(const p of spec.parts)ctx.drawImage(this.images[spec.image],p.sx,0,p.w,64,p.dx,0,p.w,64);
  // Bound retained strips while users repeatedly resize placements.
  if(this.platformCache.size>=64)this.platformCache.delete(this.platformCache.keys().next().value);
  this.platformCache.set(key,canvas);return canvas;
 }
 drawPlatform(ctx,marker,scale,ox,oy){ctx.drawImage(this.platform(marker.kind,marker.w),(marker.x-ox)*scale,(marker.y-oy-PLATFORM_SURFACE[marker.kind])*scale,marker.w*scale,64*scale);}
}
const api={TileArt,groundSpec,groundContour,groundPixels,platformParts,GROUND_SURFACE,PLATFORM_SURFACE,SOIL};
root.MapTileArt=api;if(typeof module!=='undefined'&&module.exports)module.exports=api;
})(typeof globalThis!=='undefined'?globalThis:this);
