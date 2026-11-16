const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),repo=path.resolve(root,'../..'),T=require('../runtime/lookup.cjs');
const meta=JSON.parse(fs.readFileSync(path.join(root,'runtime/tileset.json'),'utf8'));
const ids=new Set(meta.atlas.rects.map(r=>r.id));
assert.equal(ids.size,199);
assert.equal(new Set(Array.from({length:256},(_,i)=>T.canonical(i))).size,47);
const resolved=JSON.parse(fs.readFileSync(path.join(root,'preview/assembly-selection.json'),'utf8'));
let cells=0;
for(const a of resolved){
 const at=(x,y)=>a.rows[y]?.[x]==='#';
 for(const c of a.cells){
  assert(ids.has(c.id),c.id);
  if(at(c.x,c.y))assert.equal(c.id,T.stone(at,c.x,c.y));
  cells++;
 }
}
for(let n=1;n<=64;n++)for(let sides=0;sides<4;sides++)for(let i=0;i<n;i++)assert(ids.has(T.platform(i,n,!!(sides&1),!!(sides&2))));
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const files=walk(root).filter(f=>path.basename(f)!=='asset.json').map(f=>path.relative(root,f).replaceAll('\\','/')).sort();
for(const m of fs.readFileSync(path.join(root,'README.md'),'utf8').matchAll(/\]\(([^)]+)\)/g)){assert(fs.existsSync(path.join(root,m[1])),m[1]);}
const id='environments.stage1-modular-tiles-20261111',rel=path.relative(repo,root).replaceAll('\\','/');
const asset={format:'deviant-resource-asset-v1',id,path:rel,status:'art-approved-reusable-tiles',approvedAt:'2026-11-11',approvedStyleGuide:'STYLE-GUIDE.md',gameIntegration:'not-applied',productionAtlasReady:true,selected:'preview/assembled-2x.png',imageGenerationMode:'built-in image_gen plus deterministic normalization and tile assembly',sourcePixelsUnmodified:true,spriteCount:199,pixelsPerUnit:32,files,fileHashes:Object.fromEntries(files.map(f=>[f,sha(path.join(root,f))])),scope:'Axis-aligned 32px solid cells and horizontal one-way platforms; Unity adapter not installed.'};
fs.writeFileSync(path.join(root,'asset.json'),JSON.stringify(asset,null,2)+'\n');
const catFile=path.join(repo,'catalog.json'),cat=JSON.parse(fs.readFileSync(catFile,'utf8'));
let entry=cat.assets.find(a=>a.id===id);if(!entry){entry={id};cat.assets.push(entry);}Object.assign(entry,{path:rel,fileCount:files.length});fs.writeFileSync(catFile,JSON.stringify(cat,null,2)+'\n');
const layerRoot=path.join(repo,'environments/stage1-actual-layered-bright-20261111');
const layerRel=path.relative(repo,layerRoot).replaceAll('\\','/');
const layerAssetFile=path.join(layerRoot,'asset.json');
const layerAsset=JSON.parse(fs.readFileSync(layerAssetFile,'utf8'));
const layerFiles=walk(layerRoot).filter(f=>path.basename(f)!=='asset.json').map(f=>path.relative(layerRoot,f).replaceAll('\\','/')).sort();
Object.assign(layerAsset,{status:'art-approved-layered-room-prototypes',approvedAt:'2026-11-11',approvedStyleGuide:rel+'/STYLE-GUIDE.md',files:layerFiles,fileHashes:Object.fromEntries(layerFiles.map(f=>[f,sha(path.join(layerRoot,f))])),dependencies:[{id,metadataSha256:sha(path.join(root,'asset.json'))}]});
fs.writeFileSync(layerAssetFile,JSON.stringify(layerAsset,null,2)+'\n');
const layerEntry=cat.assets.find(a=>a.id===layerAsset.id);assert(layerEntry);layerEntry.fileCount=layerFiles.length;
fs.writeFileSync(catFile,JSON.stringify(cat,null,2)+'\n');
const manifestFile=path.join(repo,'generated-assets.json'),manifest=JSON.parse(fs.readFileSync(manifestFile,'utf8'));
manifest.files=manifest.files.filter(f=>!f.path.startsWith(rel+'/')&&!f.path.startsWith(layerRel+'/'));
for(const [base,prefix,list] of [[root,rel,files],[layerRoot,layerRel,layerFiles]])for(const f of list){const p=path.join(base,f);manifest.files.push({path:prefix+'/'+f,bytes:fs.statSync(p).size,sha256:sha(p)});}
fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2)+'\n');
console.log(JSON.stringify({sprites:ids.size,assemblies:resolved.length,assembledCells:cells,registeredFiles:files.length+layerFiles.length,status:'art-approved',lookup:'OK',lengths:'1..64 x 4 endpoint states'},null,2));
