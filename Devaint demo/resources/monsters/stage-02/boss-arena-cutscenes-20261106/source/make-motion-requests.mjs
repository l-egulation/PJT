// Builds the Aether hand-off files for the cutscene motion requests from preview/motion-requests/requests.json:
// <id>/start.png, end.png, reference.png (white-background enlargements of approved runtime frames, same framing as
// boss-phase2-cutscene-20261106/preview/handoff) and <id>/prompt.txt (motion prompt + character + frame rules).
// Source frames are read only; their bytes are never rewritten. Node 18+, no packages.
// Run from anywhere: node monsters/stage-02/boss-arena-cutscenes-20261106/source/make-motion-requests.mjs
import fs from 'node:fs';import path from 'node:path';import zlib from 'node:zlib';import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../..'),out=path.resolve(here,'../preview/motion-requests');
const R=JSON.parse(fs.readFileSync(path.join(out,'requests.json'),'utf8')),F=R.referenceImage;
// 8-bit RGBA, non-interlaced PNG (all approved runtime frames are this format).
function readPng(file){const b=fs.readFileSync(file);let o=8,w,h,idat=[];
 while(o<b.length){const len=b.readUInt32BE(o),type=b.toString('ascii',o+4,o+8),data=b.subarray(o+8,o+8+len);
  if(type==='IHDR'){w=data.readUInt32BE(0);h=data.readUInt32BE(4);if(data[8]!==8||data[9]!==6||data[12]!==0)throw Error(`${file}: expected 8-bit RGBA non-interlaced`);}
  if(type==='IDAT')idat.push(data);o+=12+len;}
 const raw=zlib.inflateSync(Buffer.concat(idat)),px=Buffer.alloc(w*h*4),stride=w*4;
 for(let y=0;y<h;y++){const f=raw[y*(stride+1)],line=raw.subarray(y*(stride+1)+1,(y+1)*(stride+1));
  for(let x=0;x<stride;x++){const a=x>=4?px[y*stride+x-4]:0,up=y?px[(y-1)*stride+x]:0,ul=y&&x>=4?px[(y-1)*stride+x-4]:0;let v=line[x];
   if(f===1)v+=a;else if(f===2)v+=up;else if(f===3)v+=(a+up)>>1;else if(f===4){const p=a+up-ul,pa=Math.abs(p-a),pb=Math.abs(p-up),pc=Math.abs(p-ul);v+=pa<=pb&&pa<=pc?a:pb<=pc?up:ul;}
   px[y*stride+x]=v&255;}}
 return {w,h,px};}
const CRC=new Int32Array(256).map((_,n)=>{let c=n;for(let k=0;k<8;k++)c=c&1?0xedb88320^(c>>>1):c>>>1;return c;});
const crc=buf=>{let c=-1;for(const v of buf)c=CRC[(c^v)&255]^(c>>>8);return (c^-1)>>>0;};
function chunk(type,data){const len=Buffer.alloc(4);len.writeUInt32BE(data.length);const td=Buffer.concat([Buffer.from(type,'ascii'),data]),c=Buffer.alloc(4);c.writeUInt32BE(crc(td));return Buffer.concat([len,td,c]);}
function writePng(file,w,h,rgb){const raw=Buffer.alloc((w*3+1)*h);for(let y=0;y<h;y++)rgb.copy(raw,y*(w*3+1)+1,y*w*3,(y+1)*w*3);
 const ihdr=Buffer.alloc(13);ihdr.writeUInt32BE(w,0);ihdr.writeUInt32BE(h,4);ihdr[8]=8;ihdr[9]=2;
 fs.writeFileSync(file,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',ihdr),chunk('IDAT',zlib.deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]));}
// Crop, composite over white, enlarge by nearest neighbour.
function reference(src,dst){const {w,h,px}=readPng(src);if(w!==F.sourceCanvas[0]||h!==F.sourceCanvas[1])throw Error(`${src}: ${w}x${h}`);
 const [x0,y0,x1,y1]=F.crop,k=F.enlargement,W=(x1-x0)*k,H=(y1-y0)*k;if(W!==F.canvas[0]||H!==F.canvas[1])throw Error('framing');
 const rgb=Buffer.alloc(W*H*3);
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){const s=((y0+Math.floor(y/k))*w+x0+Math.floor(x/k))*4,a=px[s+3]/255,d=(y*W+x)*3;
  for(let c=0;c<3;c++)rgb[d+c]=Math.round(px[s+c]*a+255*(1-a));}
 writePng(dst,W,H,rgb);}
for(const m of R.motions){const dir=path.join(out,m.id);fs.mkdirSync(dir,{recursive:true});
 for(const role of ['start','end','reference']){const file=path.join(dir,role+'.png');
  if(m[role])reference(path.join(repo,R.sourceRoot,m[role]),file);else if(fs.existsSync(file))fs.rmSync(file);}
 fs.writeFileSync(path.join(dir,'prompt.txt'),`${m.prompt}\n\n${R.common[m.character]}\n\n${R.common.frame}\n`);
 console.log(m.id,['start','end','reference'].filter(r=>m[r]).join(' '));}
