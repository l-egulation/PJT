const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const sharp=require('sharp');
(async()=>{
 const {data,info}=await sharp(path.join(__dirname,'01_원본/grass_only_sheet.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const records=[];
 for(const [i,name] of ['clean','unfinished','transition'].entries()){
  let left=info.width,right=0,top=info.height,bottom=0;
  const band=[[160,370],[440,660],[710,930]][i];
  for(let y=band[0];y<band[1];y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>64){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
  const sourceWidth=right-left+1,sourceHeight=bottom-top+1;
  // Only set width: forcing a height here vertically squashes the foliage.
  const {data:resized,info:scaled}=await sharp(data,{raw:info}).extract({left,top,width:sourceWidth,height:sourceHeight}).resize({width:512,kernel:'nearest'}).raw().toBuffer({resolveWithObject:true});
  assert(Math.abs(scaled.height-sourceHeight*512/sourceWidth)<=1,'Aspect ratio changed: '+name);
  // Register the continuous foliage surface to the existing 17px landing line.
  let baseline=-1;
  for(let y=0;y<30;y++){let hits=0;for(let x=9;x<503;x++)if(resized[(y*512+x)*4+3]>64)hits++;if(hits===494){baseline=y;break;}}
  assert(baseline>=0,'No continuous foliage surface: '+name);
  const output=Buffer.alloc(512*71*4),offset=17-baseline;
  for(let y=0;y<scaled.height;y++)for(let x=0;x<512;x++){const target=y+offset,a=(y*512+x)*4;if(!resized[a+3])continue;assert(target>=0&&target<71,'Foliage clipped: '+name);const b=(target*512+x)*4;resized.copy(output,b,a,a+4);}
  await sharp(output,{raw:{width:512,height:71,channels:4}}).png().toFile(path.join(__dirname,'02_게임용/grass_'+name+'.png'));
  records.push({name,sourceBounds:{left,top,right,bottom},sourceWidth,sourceHeight,nativeVisibleHeight:scaled.height,proportionalResize:true,noClippedPixels:true,landingY:17,offset});
 }
 fs.writeFileSync(path.join(__dirname,'grass-source-layout.json'),JSON.stringify(records,null,2));console.log(records);
})().catch(e=>{console.error(e);process.exitCode=1;});
