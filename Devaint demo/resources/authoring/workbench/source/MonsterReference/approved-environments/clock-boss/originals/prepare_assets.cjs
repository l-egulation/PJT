// Mechanical atlas extraction only. All drawing comes from native image_gen;
// originals and their alpha are retained. No procedural redraw or recolouring.
const sharp=require('sharp'),fs=require('node:fs/promises'),path=require('node:path');
async function main(){
 const src=path.join(__dirname,'generated-atlas-original.png');
 const {data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const cells=[['wall',0,0,620,520],['alcove',620,0,550,520],['buttress',1170,0,366,520],['gear-large',0,520,620,504],['gear-small',620,520,460,504],['pulley',1080,520,456,504]];
 const cuts=[];
 for(const [name,left,top,width,height]of cells){
  let x1=left+width,y1=top+height,x2=left,y2=top;
  for(let y=top;y<top+height;y++)for(let x=left;x<left+width;x++)if(data[(y*info.width+x)*4+3]>120){x1=Math.min(x1,x);x2=Math.max(x2,x);y1=Math.min(y1,y);y2=Math.max(y2,y);}
  x1=Math.max(left,x1-2);y1=Math.max(top,y1-2);x2=Math.min(left+width-1,x2+2);y2=Math.min(top+height-1,y2+2);
  const crop={left:x1,top:y1,width:x2-x1+1,height:y2-y1+1};
  await sharp(src).extract(crop).png().toFile(path.join(__dirname,'assets','aged-'+name+'-source.png'));cuts.push({name,...crop});
 }
 // Exact presentation size; no world geometry or floor baked into this backplate.
 await sharp(path.join(__dirname,'generated-room-original.png')).resize(1280,615,{fit:'fill'}).png().toFile(path.join(__dirname,'assets','boss-rear.png'));
 await fs.writeFile(path.join(__dirname,'art-provenance.json'),JSON.stringify({tool:'built-in image_gen',styleReferences:['house_balcony.png','courtyard_wall.png','stone_arch.png'],originals:['generated-atlas-original.png','generated-room-original.png'],prompts:'prompts.txt',processing:'Atlas crop preserving original alpha; room resized to 1280 × 615. Lighting applied in renderer.',cuts},null,2));
 console.log(JSON.stringify(cuts));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
