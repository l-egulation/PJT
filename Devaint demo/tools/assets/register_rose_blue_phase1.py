"""Register the approved resource derivatives in the main-owned deployment contract."""
from pathlib import Path
import json,hashlib,uuid
ROOT=Path(__file__).resolve().parents[2]
GROUP='monsters/stage-02/boss-siblings-lineup-20261103'
ASSET='monsters.stage-02.boss-siblings-lineup-20261103'
source=ROOT/'resources'/GROUP/'unity-phase1'
manifest_path=ROOT/'docs/assets/manifests/unity-deployment.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
prefix='unity/Assets/Chapter2/Resources/RoseBluePhase1/'
manifest['files']=[x for x in manifest['files'] if not x['target'].startswith(prefix)]
for p in source.glob('*.png'):
 target=prefix+p.name;raw=p.read_bytes()
 manifest['files'].append({'target':target,'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),'source':{'asset':ASSET,'path':GROUP+'/unity-phase1/'+p.name}})
 meta=ROOT/(target+'.meta');meta.parent.mkdir(parents=True,exist_ok=True)
 meta.write_text(f'''fileFormatVersion: 2
guid: {uuid.uuid5(uuid.NAMESPACE_URL,'deviant-phase1/'+target).hex}
TextureImporter:
  serializedVersion: 13
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 4096
  textureCompression: 0
  nPOTScale: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 1
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 1
  spritePixelsToUnits: 32
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''',encoding='utf-8')
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
binding=json.loads((source/'binding.json').read_text(encoding='utf-8'))
binding['sourceAsset']=ASSET
for path in ['data/combat/presentation/chapter2_boss_phase1_art.json','unity/Assets/Chapter2/Resources/RoseBluePhase1Art.json']:
 p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(binding,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f"Registered {len(list(source.glob('*.png')))} approved textures and phase1 visual tuning")
