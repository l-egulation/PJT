/* Build an additive, local title-art set from explicitly supplied imagegen outputs.
 * Usage: node tools/build_title_well_assets.cjs <input.json> <new-output-directory>
 * sharp must be available on NODE_PATH. Source files are copied without reencoding.
 */
const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const sharp=require('sharp');
const W=1672,H=941;
const json=(p,v)=>fs.writeFile(p,JSON.stringify(v,null,2)+'\n');
let seed=20261105;
function rand(){seed^=seed<<13;seed^=seed>>>17;seed^=seed<<5;return(seed>>>0)/4294967296}
async function preserve(source,target){await fs.mkdir(path.dirname(target),{recursive:true});await fs.copyFile(source,target,1)}
async function wav(target){
 const rate=44100,duration=.68,n=Math.ceil(rate*duration),mix=new Float64Array(n);
 const bursts=[[.1,.085,.1],[.155,.055,.075],[.2962,.12,.11],[.3892,.07,.095],[.46,.13,.1],[.575,.045,.06]];
 const omega=2*Math.PI*650/rate,cos=Math.cos(omega),alpha=Math.sin(omega)/(2*.45),a0=1+alpha;
 const b0=(1+cos)/2/a0,b1=-(1+cos)/a0,b2=b0,a1=-2*cos/a0,a2=(1-alpha)/a0;
 for(const [at,length,gain]of bursts){
  let held=0,x1=0,x2=0,y1=0,y2=0;
  for(let i=0;i<Math.ceil(rate*length);i++){
   if(i%3===0)held=rand()*2-1;
   const t=i/rate,chatter=Math.sin(t*2*Math.PI*73)>0?.95:.2,x=held*chatter;
   const filtered=b0*x+b1*x1+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=filtered;
   const env=t<.003?gain*t/.003:t<length*.65?gain:gain*.7*Math.pow(.0001/(gain*.7),(t-length*.65)/(length*.35));
   const k=Math.round(at*rate)+i;if(k<n)mix[k]+=filtered*env;
  }
 }
 const out=Buffer.alloc(44+n*2);out.write('RIFF',0);out.writeUInt32LE(36+n*2,4);out.write('WAVEfmt ',8);out.writeUInt32LE(16,16);out.writeUInt16LE(1,20);out.writeUInt16LE(1,22);out.writeUInt32LE(rate,24);out.writeUInt32LE(rate*2,28);out.writeUInt16LE(2,32);out.writeUInt16LE(16,34);out.write('data',36);out.writeUInt32LE(n*2,40);
 let peak=0,energy=0;for(let i=0;i<n;i++){peak=Math.max(peak,Math.abs(mix[i]));energy+=mix[i]*mix[i];out.writeInt16LE(Math.round(Math.max(-1,Math.min(1,mix[i]))*32767),44+i*2)}
 if(peak===0||peak>=1)throw Error('Invalid audio peak: '+peak);
 await fs.writeFile(target,out);return{sampleRate:rate,channels:1,bitsPerSample:16,durationSeconds:duration,peak,rms:Math.sqrt(energy/n),bursts};
}
async function trimSprite(source,box,target){
 const crop=await sharp(source).extract(box).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let minX=crop.info.width,minY=crop.info.height,maxX=-1,maxY=-1,clear=0;
 for(let y=0;y<crop.info.height;y++)for(let x=0;x<crop.info.width;x++){
  const a=crop.data[(y*crop.info.width+x)*4+3];if(!a){clear++;continue}if(a>8){minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y)}
 }
 if(maxX<0||clear===0)throw Error('Missing transparent sprite: '+target);
 const rect={left:Math.max(0,minX-2),top:Math.max(0,minY-2),width:Math.min(crop.info.width,maxX+3)-Math.max(0,minX-2),height:Math.min(crop.info.height,maxY+3)-Math.max(0,minY-2)};
 await sharp(crop.data,{raw:crop.info}).extract(rect).png().toFile(target);
 return{x:box.left+rect.left,y:box.top+rect.top,width:rect.width,height:rect.height};
}
async function list(dir,prefix=''){const result=[];for(const e of await fs.readdir(dir,{withFileTypes:true})){const p=path.join(dir,e.name),rel=prefix+e.name;if(e.isDirectory())result.push(...await list(p,rel+'/'));else result.push(rel)}return result.sort()}
async function main(){
 const [inputPath,destination]=process.argv.slice(2);if(!inputPath||!destination)throw Error('input.json and a NEW destination required');
 const input=JSON.parse(await fs.readFile(inputPath,'utf8')),out=path.resolve(destination);
 try{await fs.access(out);throw Error('Destination already exists; preserve existing asset sets')}catch(e){if(e.code!=='ENOENT')throw e}
 for(const d of ['source/generated','runtime/flat','runtime/backgrounds','runtime/ui','runtime/audio','preview'])await fs.mkdir(path.join(out,d),{recursive:true});
 const sourceMap={};
 for(const f of input.files){if(!f.source)throw Error('Missing source '+f.name);const target=path.join(out,'source/generated',f.name);await preserve(f.source,target);sourceMap[f.id]=target}
 for(const [id,name]of [['calm','approved-title-calm.png'],['erased','approved-title-erased-eye.png'],['eye','approved-eye-detail.png']])await preserve(input.approved[id],path.join(out,'source',name));
 await preserve(input.approved.calm,path.join(out,'runtime/flat/title_calm.png'));
 await preserve(input.approved.erased,path.join(out,'runtime/flat/title_erased_eye.png'));
 await preserve(sourceMap.asset_calm_clean,path.join(out,'runtime/backgrounds/title_calm_no_ui.png'));
 await preserve(sourceMap.asset_ruin_clean,path.join(out,'runtime/backgrounds/title_erased_eye_no_ui.png'));
 const overlay=sourceMap.asset_ui_overlay,meta=await sharp(overlay).metadata();
 if(meta.width!==W||meta.height!==H||!meta.hasAlpha)throw Error('UI overlay must be transparent '+W+'x'+H);
 const raw=await sharp(overlay).ensureAlpha().raw().toBuffer();let transparent=0;for(let i=3;i<raw.length;i+=4)if(raw[i]===0)transparent++;
 if(transparent/(W*H)<.75)throw Error('UI extraction is not genuinely transparent');
 await preserve(overlay,path.join(out,'runtime/ui/title_ui_overlay.png'));
 const boxes={logo:{left:35,top:145,width:865,height:320},start_selected:{left:70,top:470,width:370,height:95},settings:{left:130,top:565,width:155,height:70},quit:{left:130,top:640,width:155,height:70}};
 const sprites={};for(const [id,box]of Object.entries(boxes))sprites[id]={file:'ui/'+id+'.png',rectTopLeftPx:await trimSprite(overlay,box,path.join(out,'runtime/ui',id+'.png'))};
 const audio=await wav(path.join(out,'runtime/audio/title_glitch.wav'));
 const layout={format:'deviant-title-well-assets-v1',referenceResolution:[W,H],imageAspect:W/H,preferredTemporaryMode:'flat-with-invisible-buttons',flat:{calm:'flat/title_calm.png',erased:'flat/title_erased_eye.png'},layered:{calm:'backgrounds/title_calm_no_ui.png',erased:'backgrounds/title_erased_eye_no_ui.png',overlay:'ui/title_ui_overlay.png',note:'AI-separated layers; use flat mode for exact approved pixels.'},sprites,buttons:{start:{rectTopLeftNormalized:[.062,.508,.19,.081],action:'start-after-sequence'},settings:{rectTopLeftNormalized:[.062,.595,.19,.081],action:'existing-settings-or-disabled-if-unimplemented'},quit:{rectTopLeftNormalized:[.062,.675,.19,.081],action:'quit'}},wellBoundsTopLeftNormalized:[.615,.135,.25,.405],audio:{file:'audio/title_glitch.wav',...audio},sequence:{clock:'unscaled',startAudioAtMs:0,rejectRepeatedStartClicks:true,phases:[{id:'acknowledge',fromMs:0,toMs:120,image:'calm'},{id:'first-glimpse',fromMs:120,toMs:249.2,image:'erased',glitchStrength:1.35},{id:'normal-interruption',fromMs:249.2,toMs:304.2,image:'calm',darken:.12},{id:'second-glimpse',fromMs:304.2,toMs:555,image:'erased',glitchStrength:1.1},{id:'fade-to-black',fromMs:555,toMs:855,image:'erased',fade:1}],onFinished:'Invoke existing start-game action once, after black; do not return to title in game.'},glitch:{style:'horizontal-slice-displacement-and-grain',sliceCount:13,sliceHeightPx:[4,28],horizontalDisplacementPx:[16,68],updateEveryMs:22,grainOpacity:.15,avoidFullWhiteFlash:true},unityImport:{textureType:'Sprite (2D and UI)',spriteMode:'Single',spriteMeshType:'Full Rect',filterMode:'Point',compression:'None',mipmaps:false,wrapMode:'Clamp',maxSize:2048,alphaIsTransparency:true,canvasScaleMode:'Scale With Screen Size',referenceResolution:[W,H],sfxLoadType:'Decompress On Load',sfxCompression:'PCM',sfxLoop:false},approval:{composition:'user-selected-for-temporary-title',eye:'B-round-lidless-burgundy-eye',separatedLayers:'extracted-from-approved-composition',gameIntegration:'assets-prepared-not-wired'}};
 await json(path.join(out,'runtime/layout.json'),layout);
 await json(path.join(out,'source/prompts.json'),{mode:'built-in image_gen',jobs:input.files.map(f=>({id:f.id,file:f.name,prompt:f.prompt}))});
 for(const [state,key]of [['calm','asset_calm_clean'],['erased','asset_ruin_clean']])await sharp(sourceMap[key]).composite([{input:overlay}]).png().toFile(path.join(out,'preview',state+'_layered.png'));
 const boards=[];for(const [i,file]of ['calm_layered.png','erased_layered.png'].entries())boards.push({input:await sharp(path.join(out,'preview',file)).resize(836,471).png().toBuffer(),left:0,top:i*471});
 await sharp({create:{width:836,height:942,channels:4,background:'#eee4d2'}}).composite(boards).png().toFile(path.join(out,'preview/contact-sheet.png'));
 const files=await list(out);
 const manifest=[];for(const file of files){const b=await fs.readFile(path.join(out,file));manifest.push({path:file,bytes:b.length,sha256:crypto.createHash('sha256').update(b).digest('hex')})}
 await json(path.join(out,'source/build-manifest.json'),{format:'deviant-title-build-v1',files:manifest,sourceOriginalsPreserved:true,audioGeneratorSeed:20261105});
 console.log(JSON.stringify({destination:out,images:meta,alphaTransparentFraction:transparent/(W*H),audio,sprites,files:files.length+1}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
