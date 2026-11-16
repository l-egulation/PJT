/* Export the approved editor recipes using exactly the audition DSP. */
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const DSP=require('./weapon_sfx_studio/dsp.js');
const root=path.resolve(__dirname,'../..'),args=process.argv.slice(2);
const arg=name=>{const i=args.indexOf(name);return i<0?null:args[i+1];};
const settings=JSON.parse(fs.readFileSync(path.join(root,'data/audio/approved-weapon-project.json'),'utf8'));
const out=path.join(root,'unity/Assets/Deviant/Audio/Resources/ApprovedCombatAudio');
const sourceFile=path.join(out,'Source/basic_sword_selected_07_v01.wav');
const sr=48000;
function readMonoWav(raw){let at=12;while(raw.toString('ascii',at,at+4)!=='data')at+=8+raw.readUInt32LE(at+4)+(raw.readUInt32LE(at+4)%2);const n=raw.readUInt32LE(at+4)/2;return Float32Array.from({length:n},(_,i)=>raw.readInt16LE(at+8+i*2)/32768);}
let source;
if(arg('--studio')){
 const html=fs.readFileSync(arg('--studio'),'utf8'),d=JSON.parse(html.match(/<script id="payload" type="application\/json">([\s\S]*?)<\/script>/)[1]);
 if(d.source.sha256!==settings.source_hash)throw Error('Approved source hash mismatch');
 const pcm=Buffer.from(d.source.pcm,'base64'),wav=Buffer.alloc(44+pcm.length);wav.write('RIFF',0);wav.writeUInt32LE(36+pcm.length,4);wav.write('WAVEfmt ',8);wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(sr,24);wav.writeUInt32LE(sr*2,28);wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(pcm.length,40);pcm.copy(wav,44);
 fs.mkdirSync(path.dirname(sourceFile),{recursive:true});fs.writeFileSync(sourceFile,wav);
}
const sourceBytes=fs.readFileSync(sourceFile);if(crypto.createHash('sha256').update(sourceBytes).digest('hex')!==settings.source_hash)throw Error('Stored source differs from selected source');source=readMonoWav(sourceBytes);
const melee=JSON.parse(fs.readFileSync(path.join(root,'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json'),'utf8'));
const entries=[];
function write(id,mix,metadata){
 const relative='Weapons/'+id+'.wav',target=path.join(out,relative);fs.mkdirSync(path.dirname(target),{recursive:true});const raw=Buffer.from(DSP.wav(mix));fs.writeFileSync(target,raw);
 entries.push({id,clip:'ApprovedCombatAudio/'+relative.slice(0,-4),samples:mix.left.length,frequency:sr,sha256:crypto.createHash('sha256').update(raw).digest('hex'),...metadata});
}
for(const form of settings.approved_forms){
 const f=melee.forms.find(x=>x.id===form),comboKey=form+'/combo',recipe=settings.project.actions[comboKey];if(!recipe)throw Error('Missing approved combo '+comboKey);
 const reference=settings.motion_reference[comboKey],mix=DSP.render(source,sr,recipe,reference.duration);let offset=0;
 const duration=f.basics.reduce((n,h)=>n+h.duration,0);let marker=0;
 const markers=f.basics.map(h=>{const at=marker+h.impact;marker+=h.duration;return at;});
 if(Math.abs(duration-reference.duration)>1e-5||markers.length!==reference.markers.length||markers.some((m,i)=>Math.abs(m-reference.markers[i])>1e-5))throw Error('Attack timing changed; re-approve the studio reference: '+comboKey);
 f.basics.forEach((h,i)=>{
  const a=Math.round(offset*sr),b=i===f.basics.length-1?mix.left.length:Math.round((offset+h.duration)*sr);
  write(form+'_basic_'+i,{...mix,left:mix.left.slice(a,b),right:mix.right.slice(a,b)},{sourceKey:comboKey,regionStart:offset,nominalDuration:h.duration,nominalImpact:h.impact,basic:true});offset+=h.duration;
 });
 for(const [key,events]of Object.entries(settings.project.actions)){
  if(key.includes('/')||!f.specials.some(s=>s.id===key))continue;
  const ref=settings.motion_reference[key],s=f.specials.find(s=>s.id===key);
  if(Math.abs(s.end-ref.duration)>1e-5)throw Error('Special timing changed; re-approve the studio reference: '+key);
  write(key,DSP.render(source,sr,events,ref.duration),{sourceKey:key,regionStart:0,nominalDuration:ref.duration,nominalImpact:s.activation,basic:false});
 }
 if(arg('--approved-output')){const target=path.join(arg('--approved-output'),'sfx_weapon_'+form+'_basic_cycle_01_v02.wav');fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,Buffer.from(DSP.wav(mix)));}
}
const approved=JSON.parse(fs.readFileSync(path.join(root,'data/audio/approved-sfx.json'),'utf8'));
const eventSources=[['sfx_combat_bomb_explosion','explosion_bomb'],['sfx_combat_large_bomb_explosion','explosion_large'],['sfx_combat_grenade_explosion','explosion_grenade'],['sfx_combat_fragment_explosion','explosion_fragment']];
for(const [species,prefix]of [['05_boar','boar'],['10_water_bucket','bucket']])for(const kind of ['attack','hurt','death'])eventSources.push(['sfx_monster_'+species+'_'+kind,prefix+'_'+kind]);
for(const [sourceId,id]of eventSources){
 const item=approved.items.find(i=>i.id===sourceId),raw=fs.readFileSync(path.join(root,item.path));
 if(crypto.createHash('sha256').update(raw).digest('hex')!==item.sha256)throw Error('Approved event source hash differs: '+sourceId);
 const target=path.join(out,'Events',path.basename(item.path));fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,raw);
 const pcm=readMonoWav(raw);let peak=0;for(let i=1;i<pcm.length;i++)if(Math.abs(pcm[i])>Math.abs(pcm[peak]))peak=i;
 entries.push({id,clip:'ApprovedCombatAudio/Events/'+path.basename(item.path,'.wav'),samples:pcm.length,frequency:sr,nominalImpact:peak/sr,sha256:item.sha256});
}
fs.writeFileSync(path.join(out,'Catalog.json'),JSON.stringify({schema:1,entries},null,2)+'\n');
console.log(JSON.stringify({exported:entries.length,forms:settings.approved_forms,sourceHash:settings.source_hash}));
