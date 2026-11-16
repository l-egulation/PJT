const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {groundSpec,groundContour,groundPixels,platformParts,TileArt,SOIL}=require('../editor/tile-art.js');
// Exercise occupied/empty centers and every diagonal/cardinal combination.
for(let mask=0;mask<512;mask++){
 const terrain={value(x,y){return x>=2&&x<=4&&y>=2&&y<=4?(mask>>((y-2)*3+x-2))&1:0;}};
 const spec=groundSpec(terrain,3,3);if(!spec)continue;
 if(spec.interior){assert.equal(mask,511);continue;}
 if(mask&2)assert(!spec.grass,'covered top must never contain grass');
 const contour=groundContour(spec.mask);
 if(mask&16)assert(contour[16*32+16].inside,'rounding must preserve the center of occupied cells');
 else assert(!contour[16*32+16].inside,'concave fill must stay near corners');
 for(const p of contour)assert(p.depth>=.5||!p.inside,'valid boundary distances, including an entirely solid field');
}
const profile=value=>{const s=groundSpec({value},0,0);return s&&groundContour(s.mask);};
const corner=profile((x,y)=>x>=0&&y<=0?1:0);
assert(!corner[31*32].inside,'outer ceiling corner is rounded');assert(corner[16*32+16].inside);
const concave=profile((x,y)=>x<0||y<0?1:0);
assert(concave[0].inside,'inner corners receive a connecting fillet');assert(!concave[16*32+16].inside);
const tip=profile((x,y)=>y<0||x===0&&y===0?1:0);
assert(tip[20*32+16].inside);assert(!tip[31*32+16].inside,'one-cell ceiling tip is shallower');assert(!tip[20*32].inside,'tip has curved sides');
// Solid-colour synthetic art makes boundary visibility / interior fading measurable.
const atlas=new Uint8ClampedArray(256*256*4);for(let i=0;i<atlas.length;i+=4)atlas.set([110,80,65,255],i);
const pixel=(data,x,y)=>Array.from(data.slice(((y+9)*32+x)*4,((y+9)*32+x)*4+4));
const full={value:()=>1},fullSpec=groundSpec(full,3,3),plain=groundPixels(fullSpec,null,atlas);
for(let y=0;y<32;y++)for(let x=0;x<32;x++)assert.deepEqual(pixel(plain,x,y),[...SOIL,255],'interior is exactly flat and opaque');
const floorSpec=groundSpec({value:(_x,y)=>y>=0?1:0},0,0),floor=groundPixels(floorSpec,groundContour(floorSpec.mask),atlas);
assert.deepEqual(pixel(floor,16,0),[110,80,65,255]);
const belowSpec=groundSpec({value:(_x,y)=>y>=0?1:0},0,1);
assert.deepEqual(pixel(groundPixels(belowSpec,groundContour(belowSpec.mask),atlas),16,31),[...SOIL,255],'roots terminate into flat interior after the painted top band');
// Coordinate-coded pixels prove that whole painted corners retain their native
// sampling across cell seams, rather than being bent from a straight grass strip.
const coloured=new Uint8ClampedArray(256*256*4);
for(let y=0;y<256;y++)for(let x=0;x<256;x++)coloured.set([x,y,50,255],(y*256+x)*4);
const render=(value,x,y)=>{const s=groundSpec({value},x,y);return groundPixels(s,s.interior?null:groundContour(s.mask),coloured);};
for(const direction of [-1,1]){
 const value=(x,y)=>x*direction>=0&&y>=0?1:0,edgeX=direction===1?2:29;
 assert.deepEqual(pixel(render(value,0,0),edgeX,20),[direction===1?2:253,29,50,255],'painted outer corner retains its source coordinates');
 const boundaryX=direction===1?0:31;
 assert.deepEqual(pixel(render(value,0,0),boundaryX,31),[direction===1?0:255,40,50,255],'corner patch reaches the cell boundary');
 assert.deepEqual(pixel(render(value,0,1),boundaryX,0),[direction===1?0:255,41,50,255],'next cell resumes the very next source row');
}
const underRoof=render((x,y)=>y<0||x<=0&&y<3?1:0,0,0);
for(let y=0;y<32;y++){const edge=pixel(underRoof,31,y);if(edge[3])assert(edge[1]>=96,'covered wall samples only the painted earth strip');}
// A rounded collision silhouette can extend beyond the narrower painted cap.
// Transparent source pixels must remain air instead of exposing brown fill.
for(const side of [-1,1]){
 const edge=side===1?1:254,source=(22*256+edge)*4,alpha=coloured[source+3];coloured[source+3]=0;
 const data=render((x,y)=>x*side>=0&&y>=0?1:0,0,0);
 assert.equal(pixel(data,side===1?1:30,13)[3],0,'no soil halo outside transparent left/right grass cap');
 coloured[source+3]=alpha;
}
const floorBend=render((x,y)=>y>=1||x>=1?1:0,0,0);
assert.deepEqual(pixel(floorBend,30,30),[126,158,50,255],'inside bend uses its separately painted junction patch');
// An open cap above a lower landing must never punch transparent holes into
// that landing's solid interior. Check ascending and descending one-cell steps.
for(const side of [-1,1]){
 const value=(x,y)=>y>=(x*side>=0?0:1)?1:0;
 const s=groundSpec({value},0,1),transparent=new Uint8ClampedArray(256*256*4);
 const data=groundPixels(s,groundContour(s.mask),transparent);
 for(let y=0;y<32;y++)for(let x=0;x<32;x++)
  assert.equal(pixel(data,x,y)[3],255,'cap alpha cannot erase occupied soil below a short step');
 // Saturated leaves everywhere make an overlong cap tail conspicuous: a point
 // over 40px from every actual exposed edge must return to the flat soil color.
 const leaves=new Uint8ClampedArray(256*256*4);
 for(let k=0;k<leaves.length;k+=4)leaves.set([80,150,55,255],k);
 const deep=groundSpec({value},0,2),soil=groundPixels(deep,groundContour(deep.mask),leaves);
 assert.deepEqual(pixel(soil,side===1?0:31,20),[...SOIL,255],'cap leaves stop within the real surface band, including descending steps');
}
// The old large inner patch painted earth over the upper canopy of short steps.
for(const rise of [1,2])for(const side of [-1,1]){
 const value=(x,y)=>y>=4+(x*side>=4?0:rise)?1:0;
 const data=render(value,4*side,4),x=side===1?8:23;
 assert.deepEqual(pixel(data,x,12),[side===1?8:247,21,50,255],'short-step upper canopy is never overwritten by inside-corner soil');
}
const sketch={value:(x,y)=>y>=3?2:0};assert(groundSpec(sketch,3,3).sketch);assert(!groundSpec(sketch,3,4).sketch);
const mutable={solid:true,value(x,y){return this.solid||x!==-1||y!==-1?1:0;}};
const beforeKey=groundSpec(mutable,0,0).key;mutable.solid=false;assert.notEqual(groundSpec(mutable,0,0).key,beforeKey,'diagonal edits invalidate art cache');
for(const kind of ['platform','dropfloor'])for(const width of [32,64,96,512,1024,18432]){
 const spec=platformParts(kind,width),coverage=Array(width).fill(0);
 for(const p of spec.parts){assert(p.sx>=0&&p.sx+p.w<=512);for(let x=p.dx;x<p.dx+p.w;x++)coverage[x]++;}
 assert(coverage.every(n=>n===1),'platform widths have neither gaps nor overlapping/stretched caps');
 assert.equal(spec.parts[0].sx,0);assert.equal(spec.parts.at(-1).sx+spec.parts.at(-1).w,512);
 assert.equal(spec.height,64);assert.equal(spec.image,kind==='platform'?'solidPlatform':'dropPlatform');
}
assert.throws(()=>platformParts('platform',31));assert.throws(()=>platformParts('enemy',32));
// Drawing must use the new sources at full opacity and preserve the marker's
// original landing line rather than shifting stored coordinates/height.
const draws=[],images={atlas:{id:'ground'},solidPlatform:{id:'solid'},dropPlatform:{id:'drop'}};
const art=new TileArt(images,()=>({getContext:()=>({drawImage(...args){draws.push(args);},save(){},restore(){}})}));
for(const kind of ['platform','dropfloor']){
 const m={kind,x:128,y:320,w:96,h:32},before=JSON.stringify(m),out=[];
 art.drawPlatform({drawImage(...args){out.push(args);}},m,1,32,64);
 assert.equal(JSON.stringify(m),before);assert.equal(out[0][2]+platformParts(kind,96).surface,256);
 assert(draws.some(d=>d[0]===images[kind==='platform'?'solidPlatform':'dropPlatform']));
}
const html=fs.readFileSync(path.join(__dirname,'../맵 편집기.html'),'utf8');
const bundle=JSON.parse(html.match(/window\.INLINE_ASSETS=(\{[^<]+\});<\/script>/)[1]);assert.equal(Object.keys(bundle).length,9);
for(const [n,w,h]of [['Terrain32',256,256],['SolidPlatform',512,64],['DropPlatform',512,64],['forest_depth',480,270],['forest_near',480,270]]){
 const b=Buffer.from(bundle['assets/'+n+'.png'].split(',')[1],'base64');assert.equal(b.readUInt32BE(16),w);assert.equal(b.readUInt32BE(20),h);
}
assert(html.includes('MapTileArt'));assert(!html.includes('<script src='));
console.log(JSON.stringify({passed:true,checks:['512 terrain neighbourhoods','opaque flat interior','earth settles into source soil colour','native corner continuity across cells','short-step canopy preserved on both sides','cap alpha never erases lower landing soil','cap foliage stops at the actual surface band','rounded outer and inner corners','shallow rounded ceiling tips','diagonal cache invalidation','sketch on exposed foliage only','32px through full-map platform widths','unchanged placement landing coordinates','9 embedded images and dimensions','offline script bundle']}));
