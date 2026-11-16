// Rebuild the existing editor's catalogue from approved source JSONs.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'approved/manifest.json'),'utf8'));
const projects=[];
fs.mkdirSync(path.join(__dirname,'projects'),{recursive:true});
for(const monster of manifest.monsters){
 for(const [motion,label] of [['base','기본형'],['idle','대기'],['death','죽음'],['walk','이동'],['attack','공격'],['hit','피격']]){
  if(!monster.motions[motion])continue;
  const source=monster.motions[motion].editorJSON,raw=fs.readFileSync(path.join(root,'approved',source));
  const data=JSON.parse(raw);require('./editor-core.js').normalize(data);
  const sha256=crypto.createHash('sha256').update(raw).digest('hex');
  const id=data.kind.replace(/[^A-Za-z0-9_-]/g,'-').slice(0,35)+'-'+sha256.slice(0,12);
  fs.writeFileSync(path.join(__dirname,'projects',id+'.json'),raw);
  projects.push({id,title:`채택 · ${monster.name} · ${label}`,file:id+'.json',sourceName:path.basename(source),sha256});
 }
}
const catalog={defaultId:projects[0].id,projects};
fs.writeFileSync(path.join(__dirname,'projects/catalog.json'),JSON.stringify(catalog,null,2)+'\n');
const bundle={defaultId:catalog.defaultId,revision:crypto.createHash('sha256').update(JSON.stringify(catalog)).digest('hex').slice(0,16),projects:projects.map(e=>({id:e.id,title:e.title,sourceName:e.sourceName,data:JSON.parse(fs.readFileSync(path.join(__dirname,'projects',e.file)))}))};
fs.writeFileSync(path.join(__dirname,'project-data.js'),'window.PIXEL_EDITOR_CONFIG = '+JSON.stringify(bundle)+';\n');
console.log(`Approved editor ready: ${projects.length} projects.`);
