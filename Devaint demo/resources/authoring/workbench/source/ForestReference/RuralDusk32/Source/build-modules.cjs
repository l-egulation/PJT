const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const sharp=require('sharp');
const base=__dirname,out=path.resolve(base,'../ReusableTiles'),root=path.resolve(base,'..'),unity=path.resolve(base,'../BuildOutput/Modular');
const size=32,paper=[243,237,215,255],catalog=[],pixels=new Map();
const empty=(w=32,h=32)=>({width:w,height:h,data:Buffer.alloc(w*h*4)});
const get=(im,x,y)=>x<0||y<0||x>=im.width||y>=im.height?[0,0,0,0]:[...im.data.subarray((y*im.width+x)*4,(y*im.width+x)*4+4)];
const put=(im,x,y,c)=>{for(let k=0;k<4;k++)im.data[(y*im.width+x)*4+k]=c[k];};
const over=(d,s)=>{const a=s[3]/255,b=d[3]/255*(1-a),z=a+b;return z?[...Array.from({length:3},(_,k)=>Math.round((s[k]*a+d[k]*b)/z)),Math.round(z*255)]:[0,0,0,0];};
const cut=(im,x,y,w=32,h=32)=>{const b=empty(w,h);for(let v=0;v<h;v++)for(let u=0;u<w;u++)put(b,u,v,get(im,x+u,y+v));return b;};
const rotate=(im,n)=>{let a=im;for(let k=0;k<n;k++){let b=empty(a.height,a.width);for(let y=0;y<a.height;y++)for(let x=0;x<a.width;x++)put(b,a.height-1-y,x,get(a,x,y));a=b;}return a;};
const read=async f=>{const {data,info}=await sharp(f).ensureAlpha().raw().toBuffer({resolveWithObject:true});return{data,width:info.width,height:info.height};};
async function save(id,im,group,label,extra={}){assert.equal(im.width,32);assert.equal(im.height,32);for(let i=0;i<im.data.length;i+=4)if(!im.data[i+3])im.data.fill(0,i,i+4);const file=id+'.png';for(const dest of [path.join(out,'PNG',file),path.join(unity,'Textures',file)]){fs.mkdirSync(path.dirname(dest),{recursive:true});await sharp(im.data,{raw:{width:32,height:32,channels:4}}).png().toFile(dest);}pixels.set(id,im);catalog.push({id,file,group,label,...extra});}
function normalize(mask){for(const [diagonal,a,b] of [[16,1,8],[32,1,2],[64,4,2],[128,4,8]])if(!(mask&a)||!(mask&b))mask&=~diagonal;return mask;}
const masks=[...new Set(Array.from({length:256},(_,i)=>normalize(i)))].sort((a,b)=>a-b);
function groundLabel(mask){if(mask===255)return'내부 채움';const edges=[[1,'윗면'],[2,'오른쪽 벽'],[4,'밑면'],[8,'왼쪽 벽']].filter(([b])=>!(mask&b)).map(([,s])=>s);if(!edges.length)return'안쪽 모서리';if(edges.length===1)return edges[0];if(edges.length===2)return edges.join(' + ');if(edges.length===3)return'기둥 끝 / 세 면';return'독립 한 칸';}
// All exchangeable columns use the same narrow seam samples. End silhouettes
// remain intact; only the inner six pixels of a connector are registered.
function column(source,x,left=true,right=true,seam=128){const im=cut(source,x,-15,32,96);for(let y=0;y<96;y++)for(let j=0;j<6;j++){
 for(const side of ['left','right']){if(side==='left'&&!left||side==='right'&&!right)continue;const xx=side==='left'?j:31-j,ref=get(source,seam+(side==='left'?j:-j),y-15),old=get(im,xx,y);const mix=j<2?1:(6-j)/5;const a=old[3]*(1-mix)+ref[3]*mix;put(im,xx,y,a?[...Array.from({length:3},(_,k)=>Math.round((old[k]*old[3]*(1-mix)+ref[k]*ref[3]*mix)/a)),Math.round(a)]:[0,0,0,0]);}
 }return im;}
function groundTile(mask,body){const tile=empty();for(let y=0;y<32;y++)for(let x=0;x<32;x++){
 let c=paper,nearest=100,dir=-1;for(const [bit,d,k] of [[1,y,0],[2,31-x,1],[4,31-y,2],[8,x,3]])if(!(mask&bit)&&d<nearest){nearest=d;dir=k;}
 if(dir>=0){let u=x,v=y;if(dir===1){u=y;v=31-x;}if(dir===2){u=31-x;v=31-y;}if(dir===3){u=31-y;v=x;}c=over(c,get(body,u,v));}
 // Concave corners reuse the same surface pixels without adding a square line.
 for(const [diag,a,b,sx,sy] of [[16,1,8,x,y],[32,1,2,31-x,y],[64,4,2,31-x,31-y],[128,4,8,x,31-y]])if((mask&a)&&(mask&b)&&!(mask&diag)&&sx<9&&sy<9&&sx+sy<12)c=over(c,get(body,sx,sy+17));
 // Round only the four exposed outer tips; retain the walkable baseline.
 for(const [a,b,sx,sy] of [[1,8,x,y],[1,2,31-x,y],[4,2,31-x,31-y],[4,8,x,31-y]])if(!(mask&a)&&!(mask&b)&&sx<3&&sy<3&&sx+sy<2)c=[0,0,0,0];
 put(tile,x,y,c);
 }return tile;}
const platformColumns={};
(async()=>{
 fs.mkdirSync(out,{recursive:true});assert.equal(masks.length,47);
 for(const [style,file] of [['clean','sketch_clean'],['unfinished','sketch_unfinished']]){
  const groundSource=await read(path.join(base,'02_게임용',file+'.png'));
  const source=await read(path.join(base,'02_게임용','grass_'+style+'.png'));
  const columns={left:column(source,0,false,true),middle_a:column(source,96),middle_b:column(source,192),middle_c:column(source,288),right:column(source,480,true,false)};
  const single=column(source,0,false,false);for(let y=0;y<96;y++)for(let x=16;x<32;x++)put(single,x,y,get(columns.right,x,y));columns.single=single;
  platformColumns[style]=columns;
  for(const [name,im] of Object.entries(columns))for(let row=0;row<3;row++)await save(`${style}_platform_${name}_${row}`,cut(im,0,row*32),style+'_platform',`${{left:'왼쪽 끝',middle_a:'중앙 A',middle_b:'중앙 B',middle_c:'중앙 C',right:'오른쪽 끝',single:'한 칸'}[name]} · ${['풀 위 장식','두꺼운 잎층','잎 끝'][row]}`,{style,kind:'platform',column:name,row});
  const mid=column(groundSource,96),body=cut(mid,0,32),fringe=cut(mid,0,0);
  for(const mask of masks)await save(`${style}_ground_${String(mask).padStart(3,'0')}`,groundTile(mask,body),style+'_ground',groundLabel(mask),{style,kind:'ground',mask});
  const fringes=Array.from({length:4},(_,turn)=>rotate(fringe,turn));
  for(let mask=1;mask<16;mask++){const im=empty();for(let turn=0;turn<4;turn++)if(mask&(1<<turn))for(let y=0;y<32;y++)for(let x=0;x<32;x++)put(im,x,y,over(get(im,x,y),get(fringes[turn],x,y)));await save(`${style}_detail_${String(mask).padStart(2,'0')}`,im,style+'_detail','외곽 풀 연결 '+mask,{style,kind:'fringe',mask});}
  for(let turn=0;turn<4;turn++){const im=rotate(cut(mid,0,64),turn);for(let y=0;y<32;y++)for(let x=0;x<32;x++)put(im,x,y,over(paper,get(im,x,y)));await save(`${style}_roots_${turn}`,im,style+'_detail',['윗면 아래 뿌리','오른쪽 벽 안 뿌리','밑면 안 뿌리','왼쪽 벽 안 뿌리'][turn],{style,kind:'roots',turn});}
 }
 // The colored cap and two transition columns retain native pixels. Each is
 // cut into individual32px PNGs; nothing is squeezed into a narrow end piece.
 const painted=await read(path.join(base,'02_게임용/grass_transition.png'));
 // Reuse the already approved colored foliage ends at native pixel scale.
 const colored={left:column(painted,0,false,true,32),middle_a:column(painted,24,true,true,32),middle_b:column(painted,40,true,true,32),middle_c:column(painted,56,true,true,32),right:column(painted,480,true,false,32)};
 const alone=column(painted,0,false,false,32);for(let y=0;y<96;y++)for(let x=16;x<32;x++)put(alone,x,y,get(colored.right,x,y));colored.single=alone;
 platformColumns.colored=colored;
 for(const [name,im] of Object.entries(colored))for(let row=0;row<3;row++)await save(`colored_platform_${name}_${row}`,cut(im,0,row*32),'colored_platform',`${name} · 채색 풀 ${row}`,{style:'colored',kind:'platform',column:name,row});
 for(const side of ['left','right']){
  const wide=empty(96,96);
  for(let y=0;y<96;y++)for(let x=0;x<96;x++)put(wide,x,y,get(painted,(side==='left'?0:416)+x,y-15));
  for(let y=0;y<96;y++)for(let j=0;j<6;j++){const x=side==='left'?95-j:j,ref=get(platformColumns.clean.middle_a,side==='left'?31-j:j,y),old=get(wide,x,y),m=j<2?1:(6-j)/5,a=old[3]*(1-m)+ref[3]*m;put(wide,x,y,a?[...Array.from({length:3},(_,k)=>Math.round((old[k]*old[3]*(1-m)+ref[k]*ref[3]*m)/a)),Math.round(a)]:[0,0,0,0]);}
  for(let col=0;col<3;col++)for(let row=0;row<3;row++)await save(`painted_${side}_${col}_${row}`,cut(wide,col*32,row*32),'painted_platform',`${side==='left'?'색→선화':'선화→색'} ${col+1} · ${['풀 위','두꺼운 잎층','잎 끝'][row]}`,{style:'painted',kind:'platform',side,column:col,row});
 }
 const manifest={tileSize:32,ppu:32,baselineRow:1,groundMaskBits:{N:1,E:2,S:4,W:8,NW:16,NE:32,SE:64,SW:128},palette:catalog,platforms:{rows:3,landingY:32,foliageOnly:true,columns:['left','middle_a','middle_b','middle_c','right','single']},notes:['Every PNG is 32x32. Repeat middle_a/b/c in any order.','Platforms are thick foliage only: upper tufts and a leaf mat, no soil or root row.','Ground uses 47 normalized neighbor shapes per sketch style. Details are separate cells outside the ground.','PNG names are roles; no length-specific output fragments.']};
 fs.writeFileSync(path.join(out,'tiles.json'),JSON.stringify(manifest,null,2));fs.writeFileSync(path.join(out,'tiles.js'),'window.MODULAR_TILES='+JSON.stringify(manifest)+';');fs.writeFileSync(path.join(unity,'tiles.json'),JSON.stringify(manifest,null,2));
 // A spaced contact sheet makes every individual PNG visibly inspectable.
 const cols=12,cell=44,rows=Math.ceil(catalog.length/cols),sheet=empty(cols*cell,rows*cell);
 for(let i=0;i<catalog.length;i++){const tile=pixels.get(catalog[i].id);for(let y=0;y<32;y++)for(let x=0;x<32;x++)put(sheet,i%cols*cell+6+x,Math.floor(i/cols)*cell+6+y,get(tile,x,y));}
 await sharp(sheet.data,{raw:{width:sheet.width,height:sheet.height,channels:4}}).png().toFile(path.join(out,'개별타일_모아보기.png'));
 const checks={pass:true,tiles:catalog.length,groundShapesPerStyle:masks.length,nativeTileSize:32,platformMiddleVariants:3,oneCellPlatform:true,allMiddlePairsMatch:true};
 for(const style of ['clean','unfinished','colored'])for(const a of ['left','middle_a','middle_b','middle_c'])for(const b of ['middle_a','middle_b','middle_c','right'])for(let y=0;y<96;y++)assert.deepEqual(get(platformColumns[style][a],31,y),get(platformColumns[style][b],0,y),'Connector pixel differs');
 fs.writeFileSync(path.join(out,'checks.json'),JSON.stringify(checks,null,2));console.log(JSON.stringify(checks));
})().catch(e=>{console.error(e);process.exitCode=1;});
