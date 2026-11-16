// Offline render using the same occupancy and pixel assembly as editor/tile-art.js.
'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const sharp=require(process.env.SHARP_MODULE||'sharp'),art=require('../editor/tile-art.js');
const root=path.resolve(__dirname,'..'),box={window:{}};vm.createContext(box);
for(const f of ['data/map.js','data/terrain-rules.js'])vm.runInContext(fs.readFileSync(path.join(root,f),'utf8'),box);
const d=box.window.EXPANDED_MAP,r=box.window.TERRAIN_RULES,out=path.resolve(process.argv[2]||path.join(root,'checks/relief'));
const assets=JSON.parse(fs.readFileSync(path.join(root,'맵 편집기.html'),'utf8').match(/window.INLINE_ASSETS=(\{[^<]+\});<\/script>/)[1]);
const png=n=>Buffer.from(assets['assets/'+n+'.png'].split(',')[1],'base64');
const terrain={value:(x,y)=>x>=0&&x<r.cols&&y>=0&&y<r.rows?Number(r.ground[y*r.cols+x]):0};
const cache=new Map(),contours=new Map();let atlas;
function ground(x,y){const spec=art.groundSpec(terrain,x,y);if(!spec)return null;if(cache.has(spec.key))return cache.get(spec.key);if(!spec.interior&&!contours.has(spec.mask))contours.set(spec.mask,art.groundContour(spec.mask));const raw=Buffer.from(art.groundPixels(spec,contours.get(spec.mask),atlas));cache.set(spec.key,raw);return raw;}
function blit(dest,W,H,src,w,h,dx,dy){dx=Math.round(dx);dy=Math.round(dy);for(let y=Math.max(0,-dy);y<Math.min(h,H-dy);y++)for(let x=Math.max(0,-dx);x<Math.min(w,W-dx);x++){
 const a=(y*w+x)*4,b=((y+dy)*W+x+dx)*4,alpha=src[a+3]/255;if(!alpha)continue;
 for(let c=0;c<3;c++)dest[b+c]=Math.round(src[a+c]*alpha+dest[b+c]*(1-alpha));dest[b+3]=255;
}}
const opaque=(w,h)=>{const b=Buffer.alloc(w*h*4);for(let k=0;k<b.length;k+=4)b.set([45,65,65,255],k);return b;};
(async()=>{
 fs.mkdirSync(out,{recursive:true});atlas=await sharp(png('Terrain32')).ensureAlpha().raw().toBuffer();
 const bg=await Promise.all(d.layers.map(l=>sharp(png(l.id)).resize(l.width,l.height,{kernel:'nearest'}).ensureAlpha().raw().toBuffer()));
 const strips={};for(const n of ['SolidPlatform','DropPlatform'])strips[n]=await sharp(png(n)).ensureAlpha().raw().toBuffer();
 const hero=await sharp(png('hero0')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const platformCache=new Map();function platform(m){const key=m.kind+':'+m.w;if(platformCache.has(key))return platformCache.get(key);const b=Buffer.alloc(m.w*64*4),spec=art.platformParts(m.kind,m.w),src=strips[m.kind==='platform'?'SolidPlatform':'DropPlatform'];for(const p of spec.parts)for(let y=0;y<64;y++)for(let x=0;x<p.w;x++){const k=(y*512+p.sx+x)*4;src.copy(b,(y*m.w+p.dx+x)*4,k,k+4);}platformCache.set(key,b);return b;}
 async function scene(name,cx,cy,foot,backgroundOnly=false){
  cx=Math.round(cx);cy=Math.round(cy);const b=opaque(960,540);
  for(let i=0;i<d.layers.length;i++){const l=d.layers[i],offset=cx*l.parallax+l.phase;let top=-Math.round(Math.max(l.minVertical,Math.min(l.maxVertical,l.baseCrop+cy*l.vertical)));if(l.coverBottom)top=Math.max(top,Math.ceil(540+l.bottomMargin-l.height*l.contentBottom));for(let n=Math.floor(offset/l.width)-1;n*l.width-offset<960;n++)blit(b,960,540,bg[i],l.width,l.height,n*l.width-offset,top);}
  if(!backgroundOnly)for(let y=Math.max(0,Math.floor(cy/32));y<Math.min(r.rows,Math.ceil((cy+549)/32));y++)for(let x=Math.max(0,Math.floor(cx/32));x<Math.min(r.cols,Math.ceil((cx+960)/32));x++){const tile=ground(x,y);if(tile)blit(b,960,540,tile,32,41,x*32-cx,y*32-cy-9);}
  if(!backgroundOnly)for(const m of d.markers)if(['platform','dropfloor'].includes(m.kind)&&m.x+m.w>cx&&m.x<cx+960&&m.y+64>cy&&m.y-11<cy+540)blit(b,960,540,platform(m),m.w,64,m.x-cx,m.y-cy-art.PLATFORM_SURFACE[m.kind]);
  if(foot)blit(b,960,540,hero.data,hero.info.width,hero.info.height,foot[0]-111-cx,foot[1]-141-cy);
  await sharp(b,{raw:{width:960,height:540,channels:4}}).png().toFile(path.join(out,name+'.png'));
 }
 for(const z of d.plan.zones)await scene('space-'+String(z.number).padStart(2,'0'),Math.max(0,z.foot[0]-432),Math.max(0,z.foot[1]-380),z.foot);
 await scene('cliff-wide',68*32-96,64*32-64,null);
 // The background seam at x=(n*width-phase)/parallax is placed in screen centre.
 for(const l of d.layers)await scene('seam-'+l.id,Math.round((l.width-l.phase-480)/l.parallax),500,null,true);
 const w=r.cols*4,h=r.rows*4,mini=opaque(w,h),small=new Map();
 for(let y=0;y<r.rows;y++)for(let x=0;x<r.cols;x++){
  const tile=ground(x,y);if(!tile)continue;if(!small.has(tile))small.set(tile,await sharp(tile,{raw:{width:32,height:41,channels:4}}).resize(4,5,{kernel:'nearest'}).raw().toBuffer());blit(mini,w,h,small.get(tile),4,5,x*4,y*4-1);
 }
 for(const m of d.markers)if(['platform','dropfloor'].includes(m.kind)){const strip=await sharp(platform(m),{raw:{width:m.w,height:64,channels:4}}).resize(m.w/8,8,{kernel:'nearest'}).raw().toBuffer();blit(mini,w,h,strip,m.w/8,8,m.x/8,m.y/8-1);}
 const labels=d.plan.zones.map(z=>{const x=(z.x+z.w/2)*4,y=(z.y+z.h/2)*4;return `<circle cx="${x}" cy="${y}" r="10" fill="#152426" stroke="#ccdab4"/><text x="${x}" y="${y+4}" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="12">${z.number}</text>`;}).join('');
 await sharp(mini,{raw:{width:w,height:h,channels:4}}).composite([{input:Buffer.from(`<svg width="${w}" height="${h}">${labels}</svg>`)}]).png().toFile(path.join(out,'overview.png'));
 const panels=[2,8,9,17];const board=await sharp({create:{width:1920,height:1160,channels:4,background:'#182325'}}).png().toBuffer();
 const ops=[];for(let i=0;i<panels.length;i++){const x=i%2*960,y=Math.floor(i/2)*580;ops.push({input:fs.readFileSync(path.join(out,'space-'+String(panels[i]).padStart(2,'0')+'.png')),left:x,top:y+40});ops.push({input:Buffer.from(`<svg width="960" height="40"><text x="18" y="28" fill="#dce5c9" font-family="Malgun Gothic" font-size="22">${panels[i]}. ${d.plan.zones[panels[i]-1].name}</text></svg>`),left:x,top:y});}
 await sharp(board).composite(ops).png().toFile(path.join(out,'preview-board.png'));console.log(JSON.stringify({output:out,scenes:26,terrainConfigurations:cache.size}));
})().catch(e=>{console.error(e);process.exitCode=1;});
