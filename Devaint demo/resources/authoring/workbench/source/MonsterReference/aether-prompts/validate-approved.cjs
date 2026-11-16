'use strict';
// Validate the actual editable pixels, exports, palette, timing and manifest hashes.
const fs=require('fs'),p=require('path'),crypto=require('crypto'),assert=require('assert');
const sharp=require(process.env.SHARP_MODULE||'sharp'),core=require('../editor/editor-core.js');
const root=process.env.APPROVED_ROOT?p.resolve(process.env.APPROVED_ROOT):p.resolve(__dirname,'../approved'),manifest=JSON.parse(fs.readFileSync(p.join(root,'manifest.json')));
const palette=new Set(JSON.parse(fs.readFileSync(p.join(root,'master-palette.json'))).colors);
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
function list(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(x=>x.isDirectory()?list(p.join(dir,x.name)):[p.join(dir,x.name)])}
function strict(raw,label){for(let i=0;i<raw.length;i+=4){assert(raw[i+3]===0||raw[i+3]===255,label+': partial alpha');if(raw[i+3])assert(palette.has('#'+raw.subarray(i,i+3).toString('hex').toUpperCase()),label+': outside master palette')}}
async function motion(m){
 const raw=JSON.parse(fs.readFileSync(p.join(root,m.editorJSON))),d=core.normalize(raw),loaded={};
 assert.equal(d.ids.length,m.frameCount,m.editorJSON);assert.deepEqual(d.ms,m.ms,m.editorJSON);
 const width=m.width||128,height=m.height||128;assert.equal(d.width,width);assert.equal(d.height,height);
 const sheet=m.sheet?await sharp(p.join(root,m.sheet)).ensureAlpha().raw().toBuffer({resolveWithObject:true}):null;
 if(sheet){assert.equal(sheet.info.width,width*m.frameCount);assert.equal(sheet.info.height,height)}
 for(let i=0;i<d.ids.length;i++){
  const id=d.ids[i],a=await sharp(Buffer.from(d.sources[id].split(',')[1],'base64')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const b=await sharp(p.join(root,m.frames[i])).ensureAlpha().raw().toBuffer();assert(a.data.equals(b),m.frames[i]+': JSON differs');strict(b,m.frames[i]);
  loaded[id]={width:a.info.width,height:a.info.height,data:a.data};
  if(sheet)for(let y=0;y<height;y++)assert(b.subarray(y*width*4,(y+1)*width*4).equals(sheet.data.subarray((y*sheet.info.width+i*width)*4,(y*sheet.info.width+(i+1)*width)*4)),m.sheet+': frame mismatch');
 }
 new core.Model(d,loaded);
 for(const layer of Object.values(m.layers||{}))await motion(layer);
}
async function main(){
 const paths=list(root).filter(f=>p.basename(f)!=='manifest.json').sort();
 const files=paths.map(f=>{const b=fs.readFileSync(f);return{path:p.relative(root,f).replaceAll('\\','/'),bytes:b.length,sha256:sha(b)}});
 if(process.argv.includes('--refresh')){manifest.files=files;fs.writeFileSync(p.join(root,'manifest.json'),JSON.stringify(manifest,null,2)+'\n')}
 assert.deepEqual(manifest.files,files,'Manifest differs from actual approved files; run --refresh after intentional edits');
 let pngs=0,editors=0;
 for(const f of paths){if(f.endsWith('.png')){strict(await sharp(f).ensureAlpha().raw().toBuffer(),f);pngs++}if(f.endsWith('.json')){const a=JSON.parse(fs.readFileSync(f));if(a.images||a.image||a.bodyImages){const d=core.normalize(a);for(const id of d.ids)strict(await sharp(Buffer.from(d.sources[id].split(',')[1],'base64')).ensureAlpha().raw().toBuffer(),f);editors++}}}
 for(const monster of manifest.monsters)for(const m of Object.values(monster.motions))await motion(m);
 const elite=manifest.monsters.find(m=>m.id==='04_well-warden').motions.attack;
 assert.equal(elite.selectedCandidate,'C');assert.equal(elite.frameCount,34);assert.equal(elite.ms.reduce((a,b)=>a+b),2830);
 for(const id of ['03_gutter-spitter','04_well-warden']){
  const walk=manifest.monsters.find(m=>m.id===id).motions.walk;
  assert.equal(walk.frameCount,24);assert.deepEqual(walk.ms,Array(24).fill(100));
  assert.equal(walk.width,128);assert.equal(walk.height,128);assert.equal(walk.facing,'left');
  assert.equal(walk.loop,true);assert.equal(walk.paletteLock,true);
  assert.equal(manifest.monsters.find(m=>m.id===id).pendingMotions.walk,undefined);
 }
 console.log(JSON.stringify({files:files.length,pngs,editableJSONs:editors,colors:palette.size,alpha:'0/255',eliteAttack:'C / 34 frames / 2830 ms',gutterAndEliteWalks:'24 frames / 2400 ms each',result:'PASS'}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
