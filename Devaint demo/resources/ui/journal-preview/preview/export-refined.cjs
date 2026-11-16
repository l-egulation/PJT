const sharp=require('sharp');
const fs=require('fs'),path=require('path');
const root=path.join(__dirname,'assets/refined');
const records=[];
// Explicit artwork bounds preserve every edge. No largest-component segmentation.
async function output(source,name,rect,size,fit='fill') {
  const [left,top,width,height]=rect;
  let image=sharp(path.join(__dirname,'../source',source)).extract({left,top,width,height});
  const {data,info}=await image.ensureAlpha().raw().toBuffer({resolveWithObject:true});
  for(let i=3;i<data.length;i+=4)if(data[i]<32)data[i]=0;
  const raw=await sharp(data,{raw:info}).png().toBuffer();
  const crop=await sharp(raw).trim({threshold:10}).toBuffer();
  await sharp(crop).resize(size[0],size[1],{fit,kernel:'nearest',background:'#00000000'}).png().toFile(path.join(root,name+'.png'));
  records.push({file:'assets/refined/'+name+'.png',source:'../source/'+source,rect,size});
}
(async()=>{
  const controls=[['title',[82,150,450,118],[240,56]],['primary',[594,150,414,116],[240,56]],['secondary',[1048,152,402,112],[240,56]],['tab-active',[90,395,432,106],[240,56]],['tab',[596,394,412,106],[240,56]],['disabled',[1048,399,402,113],[240,56]],['card',[84,592,522,318],[320,200]],['slot',[732,618,286,280],[72,72]],['slot-active',[1140,618,288,280],[72,72]]];
  for(const [n,r,s]of controls)await output('controls-source.png',n,r,s);
  const colors=['red','yellow','green','blue','purple'],ids=[['01','04','07','10'],['01','04','07','09'],['03','04','07','09'],['02','04','08','09'],['01','04','07','09']];
  const centers=[172,438,702,967,1232],ys=[40,302,564,826];
  for(let c=0;c<5;c++)for(let r=0;r<4;r++)await output('ink-source.png','parts/ink2_'+colors[c]+'_'+ids[c][r],[centers[c]-94,ys[r],188,252],[40,56],'contain');
  await output('paper-source.png','paper',[0,0,1683,935],[960,540]);
  await sharp(path.join(__dirname,'../source/book.png')).extract({left:460,top:0,width:40,height:480}).toFile(path.join(root,'spine.png'));
  await output('menu-art-source.png','pause-vignette',[200,0,1140,545],[480,300],'contain');
  await output('menu-art-source.png','title-logo',[160,550,1220,474],[520,230],'contain');
  for(const[n,r]of [['coin',[145,145,320,325]],['heart',[605,155,330,310]],['bag',[1090,140,310,340]],['slots',[130,570,360,350]],['time',[650,565,225,345]],['kills',[1110,605,285,310]]])await output('utility-source.png',n,r,[64,64],'contain');
  for(const[n,r]of [['sword',[150,120,470,490]],['hammer',[850,120,480,490]],['launcher',[1570,140,490,470]]])await output('weapons-source.png',n,r,[80,80],'contain');
  await output('pigment-source.png','pigment',[0,0,1536,1024],[32,32],'contain');
  fs.writeFileSync(path.join(root,'exports.json'),JSON.stringify(records,null,2));
  console.log('Exported '+records.length+' generated components, plus preserved spine.');
})().catch(e=>{console.error(e);process.exit(1)});
