// Deterministic edits to the existing editor layers; never redraw approved poses.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=path.dirname(path.dirname(fileURLToPath(import.meta.url))),repo=path.dirname(root);
const asset=path.join(root,'characters/ink/weapons/hammer');
const outputAsset=process.argv[3]?path.resolve(process.argv[3]):asset;
const aligned=process.argv.includes('--align-ground');
// Measured visible boot bounds, not dark transparent canvas or hammer bounds.
const groundAnchors=[[533.5,622],[549,618],[551.5,618],[551.5,618],[553,618],
  [553.67,618.17],[554.33,618.33],[555,618.5],[555.67,618.67],[556.33,618.83],
  [557,619],[557,619],[557,619],[557,619]];
const oldBasic='user-hammer-basic-color-motion-20261101',oldSkill='user-hammer-skill-cleaned-20261101';
const scales=[1,1,1,1,1.01,1.025,1.045,1.055,1.055,1.04,1.02,1.01,1,1];
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const inputs=new Map();
function read(p){const b=fs.readFileSync(p);inputs.set(p,hash(b));return b;}
function branch(){for(const [dir,want] of [[repo,'feat/story-unity-essentials'],[root,'feat/basic-sword-normal-weapon']])if(execFileSync('git',['-C',dir,'branch','--show-current'],{encoding:'utf8'}).trim()!==want)throw Error('Branch changed; stopped');}
const json=p=>JSON.parse(read(p));
function put(p,b){fs.mkdirSync(path.dirname(p),{recursive:true});if(fs.existsSync(p))throw Error('Preserve existing file: '+p);fs.writeFileSync(p,b);}
const dump=(p,v)=>put(p,JSON.stringify(v,null,2)+'\n');

// PNG RGBA/RGB codec uses only Node built-ins, retaining exact source pigments.
const crcTable=Array.from({length:256},(_,i)=>{for(let k=0;k<8;k++)i=i&1?0xedb88320^(i>>>1):i>>>1;return i>>>0;});
function crc(b){let c=0xffffffff;for(const v of b)c=crcTable[(c^v)&255]^(c>>>8);return (c^0xffffffff)>>>0;}
function chunk(type,data){const t=Buffer.from(type),b=Buffer.alloc(data.length+12);b.writeUInt32BE(data.length);t.copy(b,4);data.copy(b,8);b.writeUInt32BE(crc(Buffer.concat([t,data])),8+data.length);return b;}
function encode({w,h,data}){const head=Buffer.alloc(13);head.writeUInt32BE(w);head.writeUInt32BE(h,4);head[8]=8;head[9]=6;const rows=Buffer.alloc(h*(w*4+1));for(let y=0;y<h;y++)Buffer.from(data.buffer,data.byteOffset+y*w*4,w*4).copy(rows,y*(w*4+1)+1);return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]);}
function decode(b){let w,h,c,parts=[];for(let p=8;p<b.length;){const n=b.readUInt32BE(p),t=b.toString('ascii',p+4,p+8),v=b.subarray(p+8,p+8+n);if(t==='IHDR'){w=v.readUInt32BE(0);h=v.readUInt32BE(4);if(v[8]!==8||![2,6].includes(v[9])||v[12])throw Error('Unsupported PNG');c=v[9]===6?4:3;}if(t==='IDAT')parts.push(v);p+=n+12;}const raw=zlib.inflateSync(Buffer.concat(parts)),row=w*c,buf=new Uint8Array(row*h);for(let y=0;y<h;y++){const type=raw[y*(row+1)];for(let x=0;x<row;x++){const i=y*row+x,a=x>=c?buf[i-c]:0,d=y?buf[i-row]:0,e=y&&x>=c?buf[i-row-c]:0;let predict=0;if(type===1)predict=a;if(type===2)predict=d;if(type===3)predict=(a+d)>>1;if(type===4){const q=a+d-e,pa=Math.abs(q-a),pb=Math.abs(q-d),pc=Math.abs(q-e);predict=pa<=pb&&pa<=pc?a:pb<=pc?d:e;}if(type>4)throw Error('Bad PNG filter');buf[i]=(raw[y*(row+1)+1+x]+predict)&255;}}const data=new Uint8Array(w*h*4);for(let i=0;i<w*h;i++){data[i*4]=buf[i*c];data[i*4+1]=buf[i*c+1];data[i*4+2]=buf[i*c+2];data[i*4+3]=c===4?buf[i*c+3]:255;}return {w,h,data};}
function neighbors(i,w,n,fn){if(i%w)fn(i-1);if(i%w<w-1)fn(i+1);if(i>=w)fn(i-w);if(i+w<n)fn(i+w);}
function components(mask,w){const n=mask.length,labels=new Int32Array(n),sizes=[0],queue=new Int32Array(n);for(let i=0;i<n;i++)if(mask[i]&&!labels[i]){const id=sizes.length;let a=0,b=1;queue[0]=i;labels[i]=id;while(a<b){const j=queue[a++];neighbors(j,w,n,k=>{if(mask[k]&&!labels[k]){labels[k]=id;queue[b++]=k;}});}sizes.push(b);}return {labels,sizes};}
function nearest(mask,w){const n=mask.length,d=new Int32Array(n).fill(1e6),idx=new Int32Array(n).fill(-1),q=new Int32Array(n);let a=0,b=0;for(let i=0;i<n;i++)if(mask[i]){d[i]=0;idx[i]=i;q[b++]=i;}while(a<b){const i=q[a++];neighbors(i,w,n,j=>{if(d[j]>d[i]+1){d[j]=d[i]+1;idx[j]=idx[i];q[b++]=j;}});}return {d,idx};}
function cleanup(im){const {w,h,data}=im,n=w*h,active=new Uint8Array(n),blue=new Uint8Array(n),dark=new Uint8Array(n),light=new Uint8Array(n),empty=new Uint8Array(n);for(let i=0;i<n;i++){const p=i*4,r=data[p],g=data[p+1],b=data[p+2];active[i]=data[p+3]>0;empty[i]=!active[i];blue[i]=active[i]&&r===2&&((g===86&&b===115)||(g===100&&b===140));dark[i]=active[i]&&r===27&&g===22&&b===22;light[i]=active[i]&&!blue[i]&&!dark[i];}
  const lc=components(light,w),dc=components(dark,w),bc=components(blue,w),edge=nearest(empty,w),notDark=nearest(Uint8Array.from(dark,x=>!x),w);
  let biggest=0;for(let i=1;i<dc.sizes.length;i++)if(dc.sizes[i]>dc.sizes[biggest])biggest=i;
  const pigment=new Uint8Array(n),hammer=new Uint8Array(n),robe=new Uint8Array(n);for(let i=0;i<n;i++){hammer[i]=dark[i]&&dc.labels[i]===biggest&&notDark.d[i]>=9;pigment[i]=(blue[i]&&bc.sizes[bc.labels[i]]>=300)||hammer[i];robe[i]=light[i]&&lc.sizes[lc.labels[i]]>150;}
  const near=nearest(pigment,w),nearRobe=nearest(robe,w);let count=0;const corrected=data.slice();
  for(let i=0;i<n;i++)if(light[i]&&lc.sizes[lc.labels[i]]<=80&&(edge.d[i]<=3||blue[near.idx[i]])&&near.d[i]<=7&&nearRobe.d[i]>5){const p=i*4,r=data[p],g=data[p+1],b=data[p+2];const neutral=Math.max(r,g,b)-Math.min(r,g,b)<25,gold=r>150&&g>90&&b<100;if(neutral||gold)continue;const j=near.idx[i]*4;corrected[p]=data[j];corrected[p+1]=data[j+1];corrected[p+2]=data[j+2];count++;}
  // Tiny blue flecks on the black head are matte noise, not scarf pigment.
  const nearHammer=nearest(hammer,w);
  for(let i=0;i<n;i++)if(blue[i]&&bc.sizes[bc.labels[i]]<80&&nearHammer.d[i]<=12&&nearRobe.d[i]>5){const p=i*4;corrected[p]=27;corrected[p+1]=22;corrected[p+2]=22;count++;}
  return {image:{w,h,data:corrected},count};
}
// Premultiplied alpha interpolation prevents white/gray matte from reappearing.
function exportFrame(im,anchor,mult,factor){const w=224*factor,h=192*factor,out=new Uint8Array(w*h*4),s=.145*factor*mult;for(let y=0;y<h;y++)for(let x=0;x<w;x++){const sx=anchor[0]+(x+.5-96*factor)/s-.5,sy=anchor[1]+(y+.5-151*factor)/s-.5,ix=Math.floor(sx),iy=Math.floor(sy),fx=sx-ix,fy=sy-iy;let a=0,r=0,g=0,b=0;for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){const xx=ix+dx,yy=iy+dy;if(xx<0||xx>=im.w||yy<0||yy>=im.h)continue;const p=(yy*im.w+xx)*4,t=(dx?fx:1-fx)*(dy?fy:1-fy)*im.data[p+3];a+=t;r+=im.data[p]*t;g+=im.data[p+1]*t;b+=im.data[p+2]*t;}const p=(y*w+x)*4;if(a>=.5){out[p]=Math.round(r/a);out[p+1]=Math.round(g/a);out[p+2]=Math.round(b/a);out[p+3]=Math.round(a);}}return {w,h,data:out};}
function sheet(frames,cols){const w=frames[0].w,h=frames[0].h,out={w:w*cols,h:h*Math.ceil(frames.length/cols),data:new Uint8Array(w*cols*h*Math.ceil(frames.length/cols)*4)};frames.forEach((f,i)=>{for(let y=0;y<h;y++)out.data.set(f.data.subarray(y*w*4,(y+1)*w*4),((Math.floor(i/cols)*h+y)*out.w+i%cols*w)*4);});return out;}

branch();const summaries=[];
for(const [kind,prior,setid] of (aligned?[['skill',oldSkill,'user-hammer-skill-aligned-20261101']]:[['basic',oldBasic,'user-hammer-basic-final-20261101'],['skill',oldSkill,'user-hammer-skill-final-20261101']])){
  const from=path.join(asset,'source',prior),source=path.join(outputAsset,'source',setid),runtime=path.join(outputAsset,'runtime',setid),preview=path.join(outputAsset,'preview',setid);
  for(const p of [source,runtime,preview])if(fs.existsSync(p))throw Error('Preserve existing set: '+p);
  const an=json(path.join(from,'animation.json')),ed=json(path.join(from,'editor.json')),frames=[],native=[],stats=[],images=[];
  for(let i=0;i<an.frames.length;i++){
    const f=an.frames[i],key=f.sourceFrame??f.input,filename=`${String(i+1).padStart(2,'0')}-${key}.png`,input=path.join(from,kind==='skill'?'cleaned-highres':'color-corrected-highres',filename),raw=read(input),im=decode(raw),clean=cleanup(im);
    const anchor=aligned?groundAnchors[i]:kind==='skill'?an.fixedSourceAnchor:f.correctedAnchor,mult=aligned?1.055:kind==='skill'?scales[i]:1;
    const detail=exportFrame(clean.image,anchor,mult,4),small=exportFrame(clean.image,anchor,mult,1),png=encode(detail);
    put(path.join(source,'cleaned-highres',filename),encode(clean.image));put(path.join(runtime,'detail4x',f.frame+'.png'),png);put(path.join(runtime,'native224',f.frame+'.png'),encode(small));
    frames.push(detail);native.push(small);images.push('data:image/png;base64,'+png.toString('base64'));
    stats.push({frame:f.frame,sourceFrame:key,sourceSet:prior,sourcePath:path.relative(root,input).replaceAll('\\','/'),sourceSha256:hash(raw),scaleMultiplier:mult,sourceAnchor:anchor,edgePixelsRecolored:clean.count,sourceAlphaUnchanged:true});
  }
  put(path.join(runtime,'detail4x',`sheet-${frames.length}.png`),encode(sheet(frames,kind==='skill'?4:5)));put(path.join(runtime,'native224',`sheet-${frames.length}.png`),encode(sheet(native,kind==='skill'?4:5)));
  const recipe={sourceSet:prior,tool:'tools/polish_hammer_frames.mjs',operation:'Small bright isolated exterior pigments recolored to adjacent scarf/hammer pigment; neutral FX and gold accents preserved. Premultiplied alpha export.',scalePolicy:kind==='skill'?'Fixed source root and fixed exported pivot; 1–5.5% scale ramp only around slam, no per-frame foot locking.':'Same pose, timing and anchor; no visual scale changes.',scaleMultipliers:stats.map(x=>x.scaleMultiplier),motionTimingUnchanged:true,unityApplied:false,spatialPixelization:false,frames:stats};
  const updated={...an,frames:stats,sourceSet:prior,scaleMultipliers:recipe.scaleMultipliers,approval:'motion approved; final small appearance correction for review',appearanceApproval:'final edge and scale correction prepared',gameIntegration:'not-deployed',resampling:'premultiplied bilinear from high-resolution palette layers',polish:recipe.operation};
  if(kind==='skill'){delete updated.motionPolicy;updated.motionPolicy=recipe.scalePolicy;}
  if(aligned){recipe.scalePolicy='Constant 1.055 scale against approved basic idle; measured two-boot center/bottom on grounded frames. Airborne frames retain original lift using interpolated ground root, never their lifted feet.';recipe.groundAnchors=groundAnchors;updated.motionPolicy=recipe.scalePolicy;updated.groundAnchors=groundAnchors;delete updated.fixedSourceAnchor;}
  const editor={...ed,kind:`hammer-${kind}-final`,images,note:recipe.operation+' '+recipe.scalePolicy};
  dump(path.join(source,'animation.json'),updated);dump(path.join(source,'editor.json'),editor);dump(path.join(source,'recipe.json'),recipe);
  fs.mkdirSync(preview,{recursive:true});summaries.push({kind,setid,source,runtime,preview,frames:stats.length,edgePixels:stats.map(x=>x.edgePixelsRecolored),scales:recipe.scaleMultipliers});
}
branch();for(const [p,h]of inputs)if(hash(fs.readFileSync(p))!==h)throw Error('Input changed: '+p);
const report={sets:summaries,inputsPreserved:true,inputs:[...inputs].map(([path,sha256])=>({path,sha256})),unityApplied:false};
const out=process.argv[2];if(out)dump(path.resolve(out),report);console.log(JSON.stringify(summaries,null,2));
