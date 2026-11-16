// Export the approved V12 artwork, preserving the warm sRGB renderer colours.
// NODE_PATH must contain playwright. Pass a local resource-preview URL and an empty review output.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..');
if(!process.argv[2]||!process.argv[3])throw new Error('Pass an empty output directory and a local resource-preview URL.');
const out=path.resolve(process.argv[2]),url=new URL(process.argv[3]);
if(out===root||out.startsWith(root+path.sep)||(fs.existsSync(out)&&fs.readdirSync(out).length))throw new Error('Choose a new/empty review output outside resources.');
if(url.protocol!=='http:'||!['127.0.0.1','localhost','[::1]'].includes(url.hostname))throw new Error('Only a local resource-preview server is supported.');
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({headless:true});
 try{
  const page=await browser.newPage();
  await page.goto(url.href);
  await page.waitForFunction(()=>window.roomReady===true);
  const files=await page.evaluate(async()=>{
   const {createPassage}=await import('./passage.mjs');
   const names=['aged-wall-source','aged-alcove-source','aged-buttress-source','aged-gear-large-source','aged-gear-small-source','aged-pulley-source','boss-rear','terrain-body','terrain-wall-left','terrain-wall-right','terrain-underside',...Array.from({length:5},(_,i)=>'float-'+i)];
   const images={};await Promise.all(names.map(async n=>{const im=new Image();im.src='./assets/'+n+'.png';await im.decode();images[n]=im}));
   const result=[];
   function save(id,im,w,h,brightness=1){const c=document.createElement('canvas');c.width=w;c.height=h;const g=c.getContext('2d');g.imageSmoothingEnabled=true;g.imageSmoothingQuality='high';g.filter=`brightness(${brightness})`;g.drawImage(im,0,0,w,h);result.push({id,width:w,height:h,png:c.toDataURL().split(',')[1]});}
   save('room-rear',images['boss-rear'],960,461,.82);
   const c=document.createElement('canvas');c.width=1280;c.height=720;
   const passage=createPassage(c.getContext('2d'),images);
   const soil=document.createElement('canvas');soil.width=832;soil.height=224;
   // Only rear soil is shortened for the real 26u connector. Props retain their proportions.
   soil.getContext('2d').drawImage(passage.earth,0,615-224/0.75,4200,224/0.75,0,0,832,224);
   save('approach-soil',soil,832,224);
   for(const n of names.filter(n=>n.startsWith('aged-')||n.startsWith('float-'))){const im=images[n];const h=n.startsWith('float-')?im.height:Math.min(256,im.height);save(n,im,Math.round(h*im.width/im.height),h);}
   return result;
  });
  const manifest={version:1,approval:'V12 warm cream sketch, 2026-10-30',pixelsPerUnit:32,roomWidth:30,approachWidth:26,maximumPassageHeadroom:6,textures:[],room:[],approach:[]};
  for(const f of files){const data=Buffer.from(f.png,'base64'),p=path.join(out,f.id+'.bytes');fs.writeFileSync(p,data);manifest.textures.push({id:f.id,resource:'ClockBossEnvironment/'+f.id,width:f.width,height:f.height,sha256:crypto.createHash('sha256').update(data).digest('hex')});}
  function add(list,id,x,y,height,{light=1,alpha=1,flip=false,rotation=0,speed=0,bob=0,phase=0,order=-27}={}){list.push({id,x,y,height,light,alpha,flip,rotation,speed,bob,phase,order});}
  add(manifest.room,'room-rear',15,461/64,461/32,{order:-30});
  // Approved room canvas (1280x720, ground y615, 42.6667px/u).
  const roomProp=(id,x,y,h,opts)=>add(manifest.room,id,x/ (1280/30),(615-y)/(1280/30),h/(1280/30),opts);
  roomProp('aged-gear-large-source',139,368,228,{speed:-.13,light:.76,alpha:.78});
  roomProp('aged-gear-small-source',221,493,141,{speed:.21,rotation:-.4,light:.77,alpha:.78});
  roomProp('aged-gear-large-source',1140,287,202,{speed:.12,rotation:-.8,light:.73,alpha:.8});
  roomProp('aged-gear-small-source',1061,402,132,{speed:-.19,light:.75,alpha:.8});
  roomProp('aged-pulley-source',164,149,216,{light:.76,alpha:.72});
  roomProp('aged-pulley-source',1157,508,178,{light:.75,alpha:.75});
  [[239,337,0,0],[257,480,2,2],[290,231,1,1.1],[946,353,3,3],[1029,462,4,1.7],[902,165,2,4.1]].forEach(([x,y,k,phase])=>{const t=files.find(f=>f.id==='float-'+k);roomProp(t.id,x+t.width/2,y+t.height/2,t.height,{alpha:.36,bob:12/(1280/30),phase,order:-25});});
  add(manifest.approach,'approach-soil',13,3.5,7,{order:-30});
  function wall(id,left,h,light=1,flip=false){const t=files.find(f=>f.id===id);add(manifest.approach,id,left+h*t.width/t.height/2,h/2-.12,h,{light,alpha:.92,flip});}
  wall('aged-wall-source',3.8,5.5);
  wall('aged-wall-source',9.5,6.2,.98,true);
  wall('aged-buttress-source',15,6.1,.94);
  wall('aged-wall-source',17,6.9,.87);
  wall('aged-alcove-source',20.8,6.6,.82);
  wall('aged-buttress-source',25,6.4,.80);
  add(manifest.approach,'aged-pulley-source',25.1,2.6,4.7,{light:.80,alpha:.82});
  add(manifest.approach,'aged-gear-small-source',18.1,1.9,1.38,{light:.85,alpha:.88,speed:.15,order:-26});
  add(manifest.approach,'aged-gear-large-source',23.65,3.1,2.37,{light:.78,alpha:.88,speed:-.17,order:-26});
  add(manifest.approach,'aged-gear-small-source',25.1,1.8,1.48,{light:.78,alpha:.88,speed:.272,rotation:-.17,order:-26});
  const json=JSON.stringify(manifest,null,2)+'\n';fs.writeFileSync(path.join(out,'layout.json'),json);
  console.log(JSON.stringify({exported:files.length,roomLayers:manifest.room.length,approachLayers:manifest.approach.length,output:out}));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
