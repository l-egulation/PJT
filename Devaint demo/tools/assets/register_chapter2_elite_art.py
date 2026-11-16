"""Select the approved resource set and create main-owned Unity import/binding metadata."""
from pathlib import Path
import json,hashlib,uuid

ROOT=Path(__file__).resolve().parents[2]
GROUP='monsters/stage-02/elite/stone-fist';ASSET='monsters.stage-02.elite.stone-fist'
resource=ROOT/'resources'/GROUP
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def guid(path):return uuid.uuid5(uuid.NAMESPACE_URL,'deviant-stage2-a3/'+path).hex
binding=read(resource/'source/approved-art-binding.json');binding['sourceAsset']=ASSET
rock=read(resource/'source/approved-composition.json')['rock']
binding['rockCenterOffsetPx']=[(rock['bodyPivot'][0]-rock['rockCenter'][0])/2,(rock['bodyPivot'][1]-rock['rockCenter'][1])/2]
binding['rockDiameterPx']=rock['rockHeight']/2
write(ROOT/'data/combat/presentation/chapter2_elite_art.json',binding)
manifest_path=ROOT/'docs/assets/manifests/unity-deployment.json';manifest=read(manifest_path)
new=[]
for entry in binding['sprites']:
    target='unity/Assets/Chapter2/Resources/'+entry['path']+'.png';src=resource/entry['resource'];raw=src.read_bytes()
    new.append({'target':target,'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),'source':{'asset':ASSET,'path':GROUP+'/'+entry['resource']}})
    path=ROOT/target;path.parent.mkdir(parents=True,exist_ok=True);meta=Path(str(path)+'.meta')
    if not meta.exists():
        w,h=entry['canvas'];px,py=entry['pivot']
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid(target)}
TextureImporter:
  serializedVersion: 13
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: {1 if '/fragment/' in entry['path'] else 0}
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  textureFormat: 1
  maxTextureSize: 2048
  textureCompression: 0
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: {px/w}, y: {1-py/h}}}
  spritePixelsToUnits: 32
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''',encoding='utf-8')
manifest['files']=[f for f in manifest['files'] if not f['target'].startswith('unity/Assets/Chapter2/Resources/Chapter2EliteArt/')]+new
write(manifest_path,manifest)
for path in sorted((ROOT/'unity/Assets/Chapter2/Resources/Chapter2EliteArt').rglob('*')):
    if path.is_dir() and not Path(str(path)+'.meta').exists():Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(str(path.relative_to(ROOT)))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n',encoding='utf-8')
root_meta=ROOT/'unity/Assets/Chapter2/Resources/Chapter2EliteArt.meta'
if not root_meta.exists():root_meta.write_text(f'fileFormatVersion: 2\nguid: {guid("Chapter2EliteArt")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n',encoding='utf-8')
print(json.dumps({'sprites':len(new),'sourceAsset':ASSET}))
