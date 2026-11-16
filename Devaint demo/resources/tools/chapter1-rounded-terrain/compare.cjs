'use strict';
const fs=require('node:fs'),path=require('node:path');
const sharp=require(process.env.SHARP_MODULE||'sharp');
const {SOIL}=require('./tile-art.cjs');
const ver2=process.argv.includes('--ver2');
const i=process.argv.indexOf('--input');if(i<0||!process.argv[i+1])throw Error('Pass --input with reference/ and Unity captures.');
const output=path.resolve(process.argv[i+1]);
async function main(){
 const {views}=JSON.parse(fs.readFileSync(path.join(output,'reference/views.json'))),results=[];
 for(const v of views){
  const a=await sharp(path.join(output,'reference',v.name+'.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const b=await sharp(path.join(output,'unity',v.name+'-terrain.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(a.info.width!==b.info.width||a.info.height!==b.info.height)throw Error('Capture dimensions differ');
  let exact=0,overTolerance=0,maxChannelDifference=0,flatPixels=0,flatMatched=0;
  for(let k=0;k<a.data.length;k+=4){
   let diff=0;for(let c=0;c<3;c++)diff=Math.max(diff,Math.abs(a.data[k+c]-b.data[k+c]));
   if(diff===0)exact++;if(diff>1)overTolerance++;maxChannelDifference=Math.max(maxChannelDifference,diff);
   if(SOIL.every((value,c)=>a.data[k+c]===value)){flatPixels++;if(diff<=1)flatMatched++;}
  }
  results.push({view:v.name,pixels:a.info.width*a.info.height,exact,overTolerance,maxChannelDifference,flatPixels,flatMatched,pass:overTolerance===0});
 }
 const runtime=JSON.parse(fs.readFileSync(path.join(output,ver2?'unity/verification.json':'unity-verification.json')));
 const report={pass:runtime.complete&&runtime.pass&&results.every(r=>r.pass),unityVersion:runtime.unityVersion,allowedChannelDifference:1,results};
 console.log(JSON.stringify(report,null,2));if(!report.pass)process.exitCode=1;
}
main().catch(e=>{console.error(e);process.exitCode=1;});
