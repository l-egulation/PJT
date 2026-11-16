/* Bake the approved HTML artwork against the actual Unity map occupancy. */
'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require(process.env.SHARP_MODULE||'sharp');
const art=require('./tile-art.cjs');
const argument=name=>{const i=process.argv.indexOf(name);if(i<0||!process.argv[i+1])throw Error('Required '+name);return process.argv[i+1];};
const repo=path.resolve(argument('--main-repo')),ver2=process.argv.includes('--ver2'),root=path.resolve(argument('--destination'));
const resources=path.resolve(__dirname,'../..'),sourceRoot=path.join(repo,'unity/Assets/MeadowDuskV3'+(ver2?'/MapVer2':''));
if(root===resources||root.startsWith(resources+path.sep)||root.startsWith(path.join(repo,'unity/Assets')+path.sep)||(fs.existsSync(root)&&fs.readdirSync(root).length))throw Error('Choose a new/empty review output outside source assets.');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const guid=p=>hash('chapter1-rounded-runtime-v2:'+(ver2?'map-ver2:':'')+p).slice(0,32);
const write=(p,s)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,s);};
const data=JSON.parse(fs.readFileSync(path.join(sourceRoot,'Data/ForestWorld.json')));
const geometryKey=d=>`${d.cols},${d.rows}|${d.ground.join('')}|${d.platforms.map(p=>`${p.x},${p.y},${p.w}${p.earthArtwork?':earth':''}`).join(';')}`;
function blit(dst,W,H,raw,w,h,dx,dy){
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const xx=x+dx,yy=y+dy,k=(y*w+x)*4;if(xx<0||xx>=W||yy<0||yy>=H||!raw[k+3])continue;
  raw.copy(dst,(yy*W+xx)*4,k,k+4);
 }
}
async function main(){
 if(data.ground.length!==data.cols*data.rows)throw Error('Invalid ground dimensions');
 const inputs={};for(const name of ['Terrain32','DropPlatform','SolidPlatform'])inputs[name]=await sharp(path.join(resources,'environments/forest/rounded-terrain/source/tiles',name+'.png')).ensureAlpha().raw().toBuffer();
 const terrain={value:(x,y)=>x>=0&&x<data.cols&&y>=0&&y<data.rows?data.ground[y*data.cols+x]:0};
 const sprites=[],raws=[],ground=[],platforms=[],configs=new Map(),contours=new Map(),dedup=new Map();
 function add(raw,height,pivotY){
  const digest=hash(raw),key=digest+':'+pivotY;if(dedup.has(key))return dedup.get(key);
  const id=sprites.length;dedup.set(key,id);raws.push(raw);sprites.push({x:id%28*36+2,y:Math.floor(id/28)*68+2,width:32,height,pivotX:.5,pivotY,rgbaSha256:digest});return id;
 }
 for(let y=0;y<data.rows;y++)for(let x=0;x<data.cols;x++){
  const spec=art.groundSpec(terrain,x,y);if(!spec)continue;
  if(!configs.has(spec.key)){
   if(!spec.interior&&!contours.has(spec.mask))contours.set(spec.mask,art.groundContour(spec.mask));
   const raw=Buffer.from(art.groundPixels(spec,contours.get(spec.mask),inputs.Terrain32));
   configs.set(spec.key,add(raw,41,16/41));
  }
  ground.push({x,y,id:configs.get(spec.key)});
 }
 for(const p of data.platforms){
  if(p.x%32||p.y%32)throw Error('Platform is not aligned to the 32px grid');
  const parts=art.platformParts(p.earthArtwork?'platform':'dropfloor',p.w),image=p.earthArtwork?inputs.SolidPlatform:inputs.DropPlatform;
  for(let i=0;i<p.w/32;i++){
   const raw=Buffer.alloc(32*64*4);
   for(const part of parts.parts){
    if(part.dx>=i*32+32||part.dx+part.w<=i*32)continue;
    for(let y=0;y<64;y++)for(let x=0;x<part.w;x++){
     const dx=part.dx+x-i*32;if(dx<0||dx>=32)continue;
     const src=(y*512+part.sx+x)*4;image.copy(raw,(y*32+dx)*4,src,src+4);
    }
   }
   platforms.push({x:p.x/32+i,y:p.y/32,id:add(raw,64,(48-parts.surface)/64)});
  }
 }
 const width=28*36,height=Math.ceil(sprites.length/28)*68,atlas=Buffer.alloc(width*height*4);
 sprites.forEach((s,i)=>blit(atlas,width,height,raws[i],32,s.height,s.x,s.y));
 const png=await sharp(atlas,{raw:{width,height,channels:4}}).png().toBuffer();
 const layout={schema:'chapter1-rounded-runtime-v2',cols:data.cols,rows:data.rows,geometrySha256:hash(geometryKey(data)),atlasSha256:hash(png),atlasWidth:width,atlasHeight:height,sprites,ground,platforms};
 write(path.join(root,'Art/RoundedTerrain.png'),png);
 write(path.join(root,'Data/RoundedTerrain.json'),JSON.stringify(layout)+'\n');
 // Pixel/layout review outputs only. Final .meta, GUIDs and consumer selection belong to main.
 const output=path.join(root,'reference');fs.mkdirSync(output,{recursive:true});
 // Compare these views with terrain-only captures from the actual Unity camera.
 const views=data.views.map((v,i)=>({name:'area-'+(i+1),x:Math.max(0,Math.min(data.width-960,Math.round(v.x-432))),y:Math.max(-384,Math.min(data.height-540,Math.round(v.y-354)))}));
 views.push({name:'ceiling-joins',x:4500,y:900},{name:'corridor-joins',x:13200,y:720});
 for(const v of views){
  const pixels=Buffer.alloc(960*540*4);for(let i=3;i<pixels.length;i+=4)pixels[i]=255;
  for(const c of ground)blit(pixels,960,540,raws[c.id],32,41,c.x*32-v.x,c.y*32-9-v.y);
  for(const c of platforms)blit(pixels,960,540,raws[c.id],32,64,c.x*32-v.x,c.y*32+Math.round(sprites[c.id].pivotY*64-48)-v.y);
  await sharp(pixels,{raw:{width:960,height:540,channels:4}}).png().toFile(path.join(output,v.name+'.png'));
 }
 write(path.join(output,'views.json'),JSON.stringify({views},null,2));
 if(ver2){console.log(JSON.stringify({ver2:true,groundCells:ground.length,platformCells:platforms.length,uniqueSprites:sprites.length,atlas:[width,height]}));return;}
 console.log(JSON.stringify({groundCells:ground.length,platformCells:platforms.length,uniqueSprites:sprites.length,configurations:configs.size,atlas:[width,height],geometrySha256:layout.geometrySha256,atlasGuid:guid('atlas'),layoutGuid:guid('layout')}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
