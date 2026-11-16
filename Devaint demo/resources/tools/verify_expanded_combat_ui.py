"""Check content coverage, independent image exports, bindings, and preview links."""
from pathlib import Path
import collections
import hashlib
import json
import re
import subprocess
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
G=ROOT/'ui/combat-hud-expanded-20261106'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

cat=read(G/'runtime/catalog.json');sprites=read(G/'runtime/sprites.json')['sprites'];by_id={s['id']:s for s in sprites}
assert len(by_id)==len(sprites)
expected={'base_sword','rapier','whip','dagger','hammer','greatsword','shield','launcher','shuriken','bomb'}
assert {w['id'] for w in cat['weapons']}==expected
assert collections.Counter(w['family'] for w in cat['weapons'])==dict(sword=4,blunt=3,ranged=3)
part_source=read(G/'source/reference-data/ink_color_parts_proposal.json')['parts']
assert {p['id'] for p in cat['inks']}=={p['id'] for p in part_source}
assert len(cat['inks'])==54 and len(cat['skills'])==40
assert len({by_id[p['partIcon']]['sha256'] for p in cat['inks']})==54,'Ink icons must remain individually distinguishable'
for w in cat['weapons']:
 assert w['icon'] in by_id and w['basicIcon'] in by_id,w['id']
 if w['id']=='launcher':
  assert w['specialPolicy']!='unavailable' and len(w['specials'])==4
for s in cat['skills']:assert s['icon'] in by_id
for p in cat['inks']:
 assert p['colorIcon'] in by_id and p['partIcon'] in by_id and p['color'] in ['red','yellow','green','blue','purple']
for r in sprites:
 p=G/r['file'];assert p.exists(),r['id'];assert sha(p)==r['sha256']
 im=Image.open(p);assert im.mode=='RGBA',r['id'];a=np.asarray(im)[:,:,3]
 assert a.min()==0 and a.max()>0,r['id']
 assert im.size==tuple(r['size'])
 if r.get('sourceSha256'):assert sha(G/r['source'])==r['sourceSha256'],r['id']
for e in read(G/'source/generation-files.json'):
 src=G/f"source/generated/{e['id']}.png";assert src.exists(),e['id']
 original=Path(e['path'])
 if original.exists():assert sha(src)==sha(original),e['id']
for recipe in read(G/'runtime/composition-recipes.json'):
 for l in recipe['layers']:assert l['sprite'] in by_id
red=np.asarray(Image.open(G/'runtime/sprites/health-fill-red.png'))
opaque=red[:,:,3]>240;rgb=red[:,:,:3][opaque].mean(axis=0)
assert rgb[0]>220 and rgb[1]<130 and rgb[2]<130,rgb
layout=read(G/'runtime/layout.json');_,_,mw,mh=layout['minimap']['rect'];assert abs(mw/mh-313/201)<1e-8
assert layout['minimap']['rect'][1]>=85
for html in (G/'preview').glob('*.html'):
 for link in re.findall(r'(?:src|href)="([^"]+)"',html.read_text(encoding='utf-8')):
  if link.startswith(('http','data:','#')):continue
  assert (html.parent/link.split('#')[0].split('?')[0]).exists(),link
files=set(read(G/'asset.json')['files'])
assert files=={p.relative_to(G).as_posix() for p in G.rglob('*') if p.is_file() and p.name!='asset.json'}
manifest={r['path']:r for r in read(ROOT/'generated-assets.json')['files']}
for name in files:
 p=G/name;key=p.relative_to(ROOT).as_posix();assert manifest[key]['sha256']==sha(p),key

js=r'''
const fs=require('fs'),assert=require('assert');const root=process.argv[1];
const cat=JSON.parse(fs.readFileSync(root+'/runtime/catalog.json','utf8'));
const {buildScene,paletteSlots}=require(root+'/runtime/ui-model.js');
const ids=new Set(JSON.parse(fs.readFileSync(root+'/runtime/sprites.json','utf8')).sprites.map(s=>s.id));
let cases=0;
for(const weapon of cat.weapons)for(const n of [3,4,5])for(const status of ['ready','cooldown','charging','charged']){
 const scene=buildScene(cat,{hp:25,reveal:true,weapons:[{weaponId:weapon.id,paletteSlots:paletteSlots(n,2,true),status,chargeCapacity:5,availableCharges:2}]});
 for(const layer of scene.layers){if(layer.type==='sprite'){assert(ids.has(layer.sprite),layer.sprite);assert(layer.rect.every(Number.isFinite));}}
 const main=scene.layers.find(l=>l.role==='weapon');assert.equal(main.sprite,weapon.icon);
 assert(scene.layers.some(l=>l.type==='text'&&l.text===weapon.name));
 assert.equal(scene.layers.filter(l=>l.role==='ink').length,n);
 assert.equal(scene.layers.filter(l=>l.role==='reveal-color').length,n);
 if(weapon.id==='launcher')assert.equal(scene.layers.find(l=>l.role==='skill').status,status);
 cases++;
}
for(const s of cat.skills){const scene=buildScene(cat,{weapons:[{weaponId:s.weaponId,skillId:s.id,paletteSlots:paletteSlots(3)}]});assert.equal(scene.layers.find(l=>l.role==='skill').sprite,s.icon);cases++;}
for(const s of cat.skills.filter(s=>s.weaponId==='launcher'))for(const status of ['ready','cooldown','charging','charged']){
 const scene=buildScene(cat,{reveal:true,weapons:[{weaponId:'launcher',skillId:s.id,status,paletteSlots:paletteSlots(5)}]});
 assert.equal(scene.layers.find(l=>l.role==='weapon').sprite,s.presentationIcon);
 assert(scene.layers.some(l=>l.group==='reveal-0'&&l.sprite===s.presentationIcon));
 assert(scene.layers.some(l=>l.type==='text'&&l.text===s.presentationName));
 assert(scene.layers.some(l=>l.id==='weapon-0.sprite:200,824'&&l.sprite==='action-ball'));
 assert.equal(scene.layers.find(l=>l.role==='skill').status,status);cases++;
}
for(const [old,mapping] of Object.entries(cat.legacyWeaponSelections)){
 const scene=buildScene(cat,{weapons:[{weaponId:old}]});assert.equal(scene.layers.find(l=>l.role==='skill').skillId,mapping.skillId);cases++;
}
for(const p of cat.inks){const scene=buildScene(cat,{showPartIcons:true,reveal:true,weapons:[{weaponId:'base_sword',paletteSlots:[{color:p.color,partId:p.id},{color:'colorless'},{color:null}]}]});assert.equal(scene.layers.find(l=>l.role==='ink').sprite,p.partIcon);assert.equal(scene.layers.find(l=>l.role==='reveal-color').sprite,p.colorIcon);cases++;}
const unknown=buildScene(cat,{weapons:[{weaponId:'future_unknown'}]});assert.equal(unknown.layers.find(l=>l.role==='weapon').sprite,'state-empty-weapon');
const boss=buildScene(cat,{boss:true});assert(!boss.layers.some(l=>l.role==='minimap'));assert(boss.layers.some(l=>l.sprite==='pause-glyph'));
console.log(cases+' catalog/state scenarios verified');
'''
res=subprocess.run(['node','-e',js,str(G)],check=True,capture_output=True,text=True)
for p in [G/'runtime/ui-model.js',G/'preview/preview.js',G/'preview/preview-data.js']:
 subprocess.run(['node','--check',str(p)],check=True,capture_output=True,text=True)
print(json.dumps(dict(sprites=len(sprites),weapons=len(cat['weapons']),skills=len(cat['skills']),inks=len(cat['inks']),
 uniqueInkIcons=54,minimapAspect='313:201',hpColor=list(rgb.round().astype(int).tolist()),
 originalBytesPreserved=True,manifestAndLinks='pass',modelChecks=res.stdout.strip(),
 browserInteraction='not-run',unityIntegration='not-run'),ensure_ascii=False))
