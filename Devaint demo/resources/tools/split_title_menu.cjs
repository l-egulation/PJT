/* Separate the title's shared selection artwork and tintable text sprites.
 * node tools/split_title_menu.cjs <ink-source.png> <text-source.png> <asset-directory>
 * Requires sharp on NODE_PATH. Existing original artwork is preserved.
 */
const fs=require('node:fs/promises');
const path=require('node:path');
const sharp=require('sharp');
const W=1672,H=941;
async function saveJson(p,v){await fs.writeFile(p,JSON.stringify(v,null,2)+'\n')}
async function preserve(source,target){
 try{await fs.copyFile(source,target,1)}catch(e){if(e.code!=='EEXIST')throw e;const a=await fs.readFile(source),b=await fs.readFile(target);if(!a.equals(b))throw Error('Preserving different existing source: '+target)}
}
async function trim(input,extract){
 let p=sharp(input);if(extract)p=p.extract(extract);
 const {data,info}=await p.ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let x0=info.width,y0=info.height,x1=-1,y1=-1,zero=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];if(a===0)zero++;if(a>8){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y)}
 }
 if(x1<0||zero===0)throw Error('Missing visible art or transparent alpha');
 const rect={left:Math.max(0,x0-2),top:Math.max(0,y0-2),width:Math.min(info.width,x1+3)-Math.max(0,x0-2),height:Math.min(info.height,y1+3)-Math.max(0,y0-2)};
 return{png:await sharp(data,{raw:info}).extract(rect).png().toBuffer(),rect,origin:{x:(extract?.left||0)+rect.left,y:(extract?.top||0)+rect.top},alphaZeroFraction:zero/(info.width*info.height)};
}
async function tint(input,rgb){
 const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 for(let i=0;i<data.length;i+=4){data[i]=rgb[0];data[i+1]=rgb[1];data[i+2]=rgb[2]}
 return sharp(data,{raw:info}).png().toBuffer();
}
async function main(){
 const [inkSource,textSource,assetDir]=process.argv.slice(2);if(!assetDir)throw Error('Supply both source PNGs and asset directory');
 const root=path.resolve(assetDir),out=path.join(root,'runtime/ui/menu');
 await fs.mkdir(out,{recursive:true});
 await preserve(inkSource,path.join(root,'source/generated/selection_ink_only.png'));
 await preserve(textSource,path.join(root,'source/generated/menu_labels_white.png'));
 const meta=await sharp(textSource).metadata();if(Math.abs(meta.width-W)>2||Math.abs(meta.height-H)>2||!meta.hasAlpha)throw Error('Unexpected text-atlas geometry');
 const textAtlas=await sharp(textSource).resize(W,H,{fit:'fill'}).png().toBuffer();
 const ink=await trim(inkSource);
 const inkPng=await sharp(ink.png).resize(322,78,{fit:'fill',kernel:'lanczos3'}).png().toBuffer();
 await fs.writeFile(path.join(out,'selection_ink.png'),inkPng);
 const areas={start:{left:130,top:470,width:230,height:95},settings:{left:130,top:565,width:155,height:70},quit:{left:130,top:640,width:155,height:70}};
 const labels={};
 for(const [id,area]of Object.entries(areas)){
  const result=await trim(textAtlas,area);
  await fs.writeFile(path.join(out,'label_'+id+'.png'),await tint(result.png,[255,255,255]));
  labels[id]={file:'ui/menu/label_'+id+'.png',rectTopLeftPx:{x:result.origin.x,y:result.origin.y,width:result.rect.width,height:result.rect.height},alphaZeroFraction:result.alphaZeroFraction,tintable:true};
 }
 const layoutPath=path.join(root,'runtime/layout.json'),layout=JSON.parse(await fs.readFile(layoutPath,'utf8'));
 layout.preferredTemporaryMode='layered-with-shared-selection';
 layout.layered.note='For interactive selection, compose no-UI background + logo + reusable selection ink + three independent tintable labels. The old combined overlay is reference only.';
 layout.menu={selection:{file:'ui/menu/selection_ink.png',sizePx:[322,78],defaultSelected:'start',positionsTopLeftPx:{start:[97,481],settings:[97,557],quit:[97,631]},raycastTarget:false},labels,normalTintRgba:[.13,.11,.09,1],selectedTintRgba:[.969,.933,.843,1],hierarchy:['Background','Logo','SelectionInk','Labels','ButtonHitTargets'],behavior:{pointerEnter:'Move selection to hovered button; tint that label selected and others normal.',select:'Use the same selection for mouse, keyboard and gamepad focus.',click:'Keep selection on the clicked button; invoke its existing action.',doNotBakeInkIntoText:true},legacyCompositeFiles:['ui/start_selected.png','ui/title_ui_overlay.png'],legacyNote:'Preserved reference only. These composites cannot move the ink selection to another row.'};
 await saveJson(layoutPath,layout);
 const pieces=[];
 for(let selected=0;selected<3;selected++){
  const overlay=[];
  overlay.push({input:inkPng,left:24,top:42+selected*84});
  for(const [i,id]of Object.keys(labels).entries()){
   const src=path.join(out,'label_'+id+'.png'),im=await sharp(src).metadata();
   const color=i===selected?[247,238,215]:[34,29,24];
   overlay.push({input:await tint(src,color),left:76,top:42+i*84+Math.round((78-im.height)/2)});
  }
  const tile=await sharp({create:{width:370,height:320,channels:4,background:'#ede5d6'}}).composite(overlay).png().toBuffer();
  await fs.writeFile(path.join(root,'preview','menu-selected-'+Object.keys(labels)[selected]+'.png'),tile);
  pieces.push({input:tile,left:selected*370,top:0});
 }
 await sharp({create:{width:1110,height:320,channels:4,background:'#ede5d6'}}).composite(pieces).png().toFile(path.join(root,'preview/menu-selection-states.png'));
 const full=[];
 full.push({input:path.join(root,'runtime/ui/logo.png'),left:layout.sprites.logo.rectTopLeftPx.x,top:layout.sprites.logo.rectTopLeftPx.y});
 full.push({input:inkPng,left:97,top:557});
 for(const id of Object.keys(labels)){
  const label=labels[id];full.push({input:await tint(path.join(root,'runtime',label.file),id==='settings'?[247,238,215]:[34,29,24]),left:label.rectTopLeftPx.x,top:label.rectTopLeftPx.y});
 }
 await sharp(path.join(root,'runtime/backgrounds/title_calm_no_ui.png')).composite(full).png().toFile(path.join(root,'preview/title-settings-selected.png'));
 console.log(JSON.stringify({files:['selection_ink.png',...Object.keys(labels).map(id=>'label_'+id+'.png')],labels,sourceInkAlphaZeroFraction:ink.alphaZeroFraction}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
